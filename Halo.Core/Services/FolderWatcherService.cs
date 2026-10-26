using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Halo.Core.Models;
using SkiaSharp;

namespace Halo.Core.Services;

public class FolderWatcherService : IDisposable
{
    private static readonly Lazy<FolderWatcherService> _instance = new(() => new FolderWatcherService());
    public static FolderWatcherService Instance => _instance.Value;

    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _recentlyProcessed = new();

    public event Action<string, string>? FileProcessed; // (filePath, actionTitle)

    public void Start()
    {
        Stop();
        var settings = SettingsService.Instance.Current;
        foreach (var rule in settings.WatchedFolders)
        {
            if (rule.IsEnabled && Directory.Exists(rule.FolderPath))
            {
                try
                {
                    var watcher = new FileSystemWatcher(rule.FolderPath)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        EnableRaisingEvents = true,
                        IncludeSubdirectories = rule.IncludeSubdirectories
                    };

                    watcher.Created += (s, e) => OnFileCreated(e.FullPath, rule);
                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start watcher for {rule.FolderPath}: {ex.Message}");
                }
            }
        }
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    public int ProcessExistingFilesInFolder(WatchedFolderConfig rule)
    {
        if (string.IsNullOrWhiteSpace(rule.FolderPath) || !Directory.Exists(rule.FolderPath))
            return 0;

        int processedCount = 0;
        var searchOption = rule.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(rule.FolderPath, "*", searchOption);
        var batchMovements = new List<FileMovementRecord>();

        foreach (var file in files)
        {
            try
            {
                if (ExecuteRuleOnFile(file, rule, batchMovements))
                {
                    processedCount++;
                }
            }
            catch
            {
                // Ignore individual locked files during batch run
            }
        }

        if (batchMovements.Count > 0)
        {
            bool isEn = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
            string folderName = Path.GetFileName(rule.FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            OperationHistoryService.Instance.RecordOperation(new OperationRecord
            {
                ActionId = rule.AssignedActionId,
                Title = isEn ? $"Sorted '{folderName}' ({batchMovements.Count} files)" : $"Сортування «{folderName}» ({batchMovements.Count} файлів)",
                Description = isEn ? $"Processed by rule '{rule.ActionDisplayName}'" : $"Опрацьовано правилом «{rule.ActionDisplayName}»",
                CanUndo = false,
                Movements = batchMovements
            });
        }

        if (processedCount > 0)
        {
            SettingsService.Instance.SaveSettings();
        }

        return processedCount;
    }

    public int ProcessAllActiveRules()
    {
        int total = 0;
        var settings = SettingsService.Instance.Current;
        foreach (var rule in settings.WatchedFolders.Where(r => r.IsEnabled))
        {
            total += ProcessExistingFilesInFolder(rule);
        }
        return total;
    }

    private void OnFileCreated(string filePath, WatchedFolderConfig rule)
    {
        Task.Run(async () =>
        {
            try
            {
                if (_recentlyProcessed.TryGetValue(filePath, out var lastTime) && (DateTime.UtcNow - lastTime).TotalSeconds < 3)
                    return;

                _recentlyProcessed[filePath] = DateTime.UtcNow;

                await Task.Delay(900);

                if (!WaitForFileUnlock(filePath, 10))
                    return;

                if (ExecuteRuleOnFile(filePath, rule))
                {
                    SettingsService.Instance.SaveSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing watched file {filePath}: {ex.Message}");
            }
        });
    }

    private static bool MatchesFileFilter(string filePath, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter == "*.*")
            return true;

        var cat = FileTypeDetector.Detect(filePath);
        var norm = filter.Trim().ToLowerInvariant();
        return norm switch
        {
            "images" => cat == FileTypeCategory.Image,
            "docs" => cat is FileTypeCategory.Document or FileTypeCategory.Pdf or FileTypeCategory.Presentation or FileTypeCategory.Spreadsheet,
            "archives" => cat == FileTypeCategory.Archive,
            "videos" => cat == FileTypeCategory.Video,
            _ => norm.Split(';', ',', ' ')
                     .Select(p => p.Trim().TrimStart('*'))
                     .Where(p => !string.IsNullOrEmpty(p))
                     .Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        };
    }

    private bool ExecuteRuleOnFile(string filePath, WatchedFolderConfig rule, List<FileMovementRecord>? batchCollector = null)
    {
        if (!File.Exists(filePath)) return false;

        if (!MatchesFileFilter(filePath, rule.FileFilter))
            return false;

        // Avoid re-processing already converted files
        var nameNoExt = Path.GetFileNameWithoutExtension(filePath);
        if (nameNoExt.EndsWith("_compressed", StringComparison.OrdinalIgnoreCase) ||
            nameNoExt.EndsWith("_rotated", StringComparison.OrdinalIgnoreCase) ||
            nameNoExt.EndsWith("_cleaned", StringComparison.OrdinalIgnoreCase) ||
            nameNoExt.EndsWith("_optimized", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        string fileName = Path.GetFileName(filePath);
        bool isEn = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);

        if (rule.AssignedActionId == "clean_temp_files")
        {
            bool isTemp = ext is ".tmp" or ".temp" or ".bak" or ".old" or ".crdownload" or ".part" or ".log" ||
                          fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase) ||
                          fileName.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase) ||
                          fileName.Equals(".ds_store", StringComparison.OrdinalIgnoreCase);
            if (!isTemp) return false;

            File.Delete(filePath);
            RecordRuleHit(rule, filePath);
            return true;
        }

        if (rule.AssignedActionId == "folder_auto_sort")
        {
            var cat = FileTypeDetector.Detect(filePath);
            string subFolder = isEn
                ? cat switch
                {
                    FileTypeCategory.Image => "Images",
                    FileTypeCategory.Document or FileTypeCategory.Pdf or FileTypeCategory.Presentation or FileTypeCategory.Spreadsheet => "Documents",
                    FileTypeCategory.Video => "Videos",
                    FileTypeCategory.Archive => "Archives",
                    _ => "Other"
                }
                : cat switch
                {
                    FileTypeCategory.Image => "Зображення",
                    FileTypeCategory.Document or FileTypeCategory.Pdf or FileTypeCategory.Presentation or FileTypeCategory.Spreadsheet => "Документи",
                    FileTypeCategory.Video => "Відео",
                    FileTypeCategory.Archive => "Архіви",
                    _ => "Інше"
                };

            string baseRoot = !string.IsNullOrWhiteSpace(rule.TargetOutputFolder) && Directory.Exists(rule.TargetOutputFolder)
                ? rule.TargetOutputFolder
                : rule.FolderPath;

            string targetDir = Path.Combine(baseRoot, subFolder);
            Directory.CreateDirectory(targetDir);
            string destPath = Path.Combine(targetDir, fileName);
            if (!string.Equals(filePath, destPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(filePath, destPath, overwrite: true);
                _recentlyProcessed[destPath] = DateTime.UtcNow;
                RecordRuleHit(rule, destPath);

                var movement = new FileMovementRecord { SourcePath = filePath, DestinationPath = destPath, IsDirectory = false };
                if (batchCollector != null)
                {
                    batchCollector.Add(movement);
                }
                else
                {
                    OperationHistoryService.Instance.RecordOperation(new OperationRecord
                    {
                        ActionId = "folder_auto_sort",
                        Title = isEn ? $"Sorted: {fileName}" : $"Сортування: {fileName}",
                        Description = isEn ? $"Moved to {subFolder} by rule '{rule.ActionDisplayName}'" : $"Переміщено до {subFolder} правилом «{rule.ActionDisplayName}»",
                        CanUndo = false,
                        Movements = [movement]
                    });
                }

                return true;
            }
            return false;
        }

        if (rule.AssignedActionId == "compress_to_zip")
        {
            if (ext == ".zip") return false;

            var zipResult = ImageProcessor.CompressToZip(filePath);
            zipResult = FinalizeRuleOutput(filePath, zipResult, rule);
            RecordRuleHit(rule, zipResult);
            return true;
        }

        if (rule.AssignedActionId == "extract_archive")
        {
            var cat = FileTypeDetector.Detect(filePath);
            if (cat != FileTypeCategory.Archive) return false;

            var extractedDir = ImageProcessor.ExtractArchive(filePath);
            if (rule.DeleteSourceAfterProcess && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            RecordRuleHit(rule, extractedDir);
            return true;
        }

        var category = FileTypeDetector.Detect(filePath);
        string? resultPath = null;

        if (category == FileTypeCategory.Image)
        {
            if (rule.AssignedActionId == "image_to_png" && ext != ".png")
            {
                resultPath = ImageProcessor.ConvertFormat(filePath, SKEncodedImageFormat.Png);
            }
            else if (rule.AssignedActionId == "image_to_jpg" && ext != ".jpg" && ext != ".jpeg")
            {
                resultPath = ImageProcessor.ConvertFormat(filePath, SKEncodedImageFormat.Jpeg);
            }
            else if (rule.AssignedActionId == "image_to_webp" && ext != ".webp")
            {
                resultPath = ImageProcessor.ConvertFormat(filePath, SKEncodedImageFormat.Webp);
            }
            else if (rule.AssignedActionId == "doc_to_pdf" && ext != ".pdf")
            {
                resultPath = ImageProcessor.ConvertToPdf(filePath);
            }
            else if (rule.AssignedActionId == "image_compress")
            {
                resultPath = ImageProcessor.CompressImage(filePath);
            }
        }
        else if (rule.AssignedActionId == "doc_to_pdf" && ext != ".pdf" &&
                 category is FileTypeCategory.Document or FileTypeCategory.Presentation or FileTypeCategory.Spreadsheet)
        {
            resultPath = ImageProcessor.ConvertToPdf(filePath);
        }

        if (resultPath != null)
        {
            resultPath = FinalizeRuleOutput(filePath, resultPath, rule);
            RecordRuleHit(rule, resultPath);
            return true;
        }

        return false;
    }

    private string FinalizeRuleOutput(string sourcePath, string resultPath, WatchedFolderConfig rule)
    {
        _recentlyProcessed[resultPath] = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(rule.TargetOutputFolder) && Directory.Exists(rule.TargetOutputFolder))
        {
            var dest = ImageProcessor.GetUniqueFilePath(Path.Combine(rule.TargetOutputFolder, Path.GetFileName(resultPath)));
            File.Move(resultPath, dest, overwrite: true);
            resultPath = dest;
            _recentlyProcessed[resultPath] = DateTime.UtcNow;
        }

        if (rule.DeleteSourceAfterProcess && File.Exists(sourcePath) && !string.Equals(sourcePath, resultPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(sourcePath);
        }

        return resultPath;
    }

    private void RecordRuleHit(WatchedFolderConfig rule, string outputPath)
    {
        rule.ProcessedCount++;
        rule.LastProcessedAt = DateTime.Now.ToString("HH:mm");
        FileProcessed?.Invoke(outputPath, rule.ActionDisplayName);
    }

    private static bool WaitForFileUnlock(string filePath, int maxRetries)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                if (stream.Length > 0)
                    return true;
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(500);
            }
        }
        return false;
    }

    public void Dispose()
    {
        Stop();
    }
}
