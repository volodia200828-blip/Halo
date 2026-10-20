using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
                        IncludeSubdirectories = false
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
        var files = Directory.GetFiles(rule.FolderPath);
        foreach (var file in files)
        {
            try
            {
                if (ExecuteRuleOnFile(file, rule))
                {
                    processedCount++;
                }
            }
            catch
            {
                // Ignore individual locked files during batch run
            }
        }
        return processedCount;
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

                ExecuteRuleOnFile(filePath, rule);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing watched file {filePath}: {ex.Message}");
            }
        });
    }

    private bool ExecuteRuleOnFile(string filePath, WatchedFolderConfig rule)
    {
        if (!File.Exists(filePath)) return false;

        // Avoid re-processing already converted files with "_compressed" or "_bw" suffixes
        var nameNoExt = Path.GetFileNameWithoutExtension(filePath);
        if (nameNoExt.EndsWith("_compressed", StringComparison.OrdinalIgnoreCase) ||
            nameNoExt.EndsWith("_bw", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.AssignedActionId == "folder_auto_sort")
        {
            var cat = FileTypeDetector.Detect(filePath);
            string subFolder = cat switch
            {
                FileTypeCategory.Image => "Зображення",
                FileTypeCategory.Document => "Документи",
                FileTypeCategory.Video => "Відео",
                FileTypeCategory.Audio => "Музика",
                _ => "Інше"
            };

            string targetDir = Path.Combine(rule.FolderPath, subFolder);
            Directory.CreateDirectory(targetDir);
            string destPath = Path.Combine(targetDir, Path.GetFileName(filePath));
            if (!string.Equals(filePath, destPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(filePath, destPath, overwrite: true);
                FileProcessed?.Invoke(destPath, rule.ActionDisplayName);
                return true;
            }
            return false;
        }

        var category = FileTypeDetector.Detect(filePath);
        if (category == FileTypeCategory.Image)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string? resultPath = null;

            if (rule.AssignedActionId == "image_to_webp" && ext != ".webp")
            {
                resultPath = ImageProcessor.ConvertFormat(filePath, SKEncodedImageFormat.Webp);
            }
            else if (rule.AssignedActionId == "image_to_png" && ext != ".png")
            {
                resultPath = ImageProcessor.ConvertFormat(filePath, SKEncodedImageFormat.Png);
            }
            else if (rule.AssignedActionId == "image_to_jpg" && ext != ".jpg" && ext != ".jpeg")
            {
                resultPath = ImageProcessor.ConvertFormat(filePath, SKEncodedImageFormat.Jpeg);
            }
            else if (rule.AssignedActionId == "image_compress")
            {
                resultPath = ImageProcessor.CompressImage(filePath);
            }
            else if (rule.AssignedActionId == "image_grayscale")
            {
                resultPath = ImageProcessor.ApplyGrayscale(filePath);
            }

            if (resultPath != null)
            {
                _recentlyProcessed[resultPath] = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(rule.TargetOutputFolder) && Directory.Exists(rule.TargetOutputFolder))
                {
                    var dest = Path.Combine(rule.TargetOutputFolder, Path.GetFileName(resultPath));
                    File.Move(resultPath, dest, overwrite: true);
                    resultPath = dest;
                }

                if (rule.DeleteSourceAfterProcess && File.Exists(filePath) && !string.Equals(filePath, resultPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(filePath);
                }

                FileProcessed?.Invoke(resultPath, rule.ActionDisplayName);
                return true;
            }
        }

        return false;
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
