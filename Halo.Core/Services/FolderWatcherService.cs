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

    private void OnFileCreated(string filePath, WatchedFolderConfig rule)
    {
        Task.Run(async () =>
        {
            try
            {
                // Debounce rapid events
                if (_recentlyProcessed.TryGetValue(filePath, out var lastTime) && (DateTime.UtcNow - lastTime).TotalSeconds < 3)
                    return;

                _recentlyProcessed[filePath] = DateTime.UtcNow;

                // Wait briefly for the writer to release file lock
                await Task.Delay(1000);

                if (!WaitForFileUnlock(filePath, 10))
                    return;

                var category = FileTypeDetector.Detect(filePath);
                if (category == FileTypeCategory.Image)
                {
                    string? resultPath = null;
                    if (rule.AssignedActionId == "image_to_webp")
                    {
                        resultPath = ImageProcessor.ConvertFormat(filePath, SKEncodedImageFormat.Webp);
                    }
                    else if (rule.AssignedActionId == "image_to_png")
                    {
                        resultPath = ImageProcessor.ConvertFormat(filePath, SKEncodedImageFormat.Png);
                    }
                    else if (rule.AssignedActionId == "image_to_jpg")
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
                        if (!string.IsNullOrWhiteSpace(rule.TargetOutputFolder) && Directory.Exists(rule.TargetOutputFolder))
                        {
                            var dest = Path.Combine(rule.TargetOutputFolder, Path.GetFileName(resultPath));
                            File.Move(resultPath, dest, overwrite: true);
                            resultPath = dest;
                        }

                        if (rule.DeleteSourceAfterProcess && File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }

                        FileProcessed?.Invoke(resultPath, rule.AssignedActionId);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing watched file {filePath}: {ex.Message}");
            }
        });
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
