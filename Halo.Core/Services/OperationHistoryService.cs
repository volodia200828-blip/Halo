using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Halo.Core.Models;

namespace Halo.Core.Services;

public class OperationHistoryService
{
    private static readonly Lazy<OperationHistoryService> _instance = new(() => new OperationHistoryService());
    public static OperationHistoryService Instance => _instance.Value;

    private readonly string _historyFilePath;
    private readonly List<OperationRecord> _history = [];
    private readonly object _lock = new();

    public event Action? HistoryChanged;

    public OperationHistoryService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var haloFolder = Path.Combine(appData, "Halo");
        if (!Directory.Exists(haloFolder))
        {
            Directory.CreateDirectory(haloFolder);
        }
        _historyFilePath = Path.Combine(haloFolder, "operations_history.json");
        LoadHistory();
    }

    public IReadOnlyList<OperationRecord> GetRecentOperations(int maxCount = 20)
    {
        lock (_lock)
        {
            return _history.OrderByDescending(o => o.Timestamp).Take(maxCount).ToList();
        }
    }

    public void RecordOperation(OperationRecord record)
    {
        lock (_lock)
        {
            _history.Insert(0, record);
            if (_history.Count > 100)
            {
                _history.RemoveRange(100, _history.Count - 100);
            }
            SaveHistory();
        }
        HistoryChanged?.Invoke();
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            _history.Clear();
            SaveHistory();
        }
        HistoryChanged?.Invoke();
    }

    public (bool Success, string Message, int RestoredCount) UndoOperation(string operationId)
    {
        OperationRecord? record;
        lock (_lock)
        {
            record = _history.FirstOrDefault(r => r.Id == operationId);
        }

        if (record == null)
            return (false, "Операцію не знайдено в журналі.", 0);

        if (record.IsUndone)
            return (false, "Цю операцію вже було скасовано.", 0);

        if (!record.CanUndo || (record.Movements.Count == 0 && record.CreatedFiles.Count == 0))
            return (false, "Цю операцію неможливо скасувати автоматично.", 0);

        int restored = 0;
        var createdDirsToCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Undo file movements (restore files to original paths)
        for (int i = record.Movements.Count - 1; i >= 0; i--)
        {
            var mov = record.Movements[i];
            try
            {
                if (!mov.IsDirectory && File.Exists(mov.DestinationPath))
                {
                    var origDir = Path.GetDirectoryName(mov.SourcePath);
                    if (!string.IsNullOrWhiteSpace(origDir) && !Directory.Exists(origDir))
                    {
                        Directory.CreateDirectory(origDir);
                    }

                    var targetPath = mov.SourcePath;
                    if (File.Exists(targetPath))
                    {
                        targetPath = ImageProcessor.GetUniqueFilePath(targetPath);
                    }

                    File.Move(mov.DestinationPath, targetPath);
                    restored++;

                    var destDir = Path.GetDirectoryName(mov.DestinationPath);
                    if (!string.IsNullOrWhiteSpace(destDir))
                    {
                        createdDirsToCheck.Add(destDir);
                    }
                }
                else if (mov.IsDirectory && Directory.Exists(mov.DestinationPath))
                {
                    var parentDir = Path.GetDirectoryName(mov.SourcePath);
                    if (!string.IsNullOrWhiteSpace(parentDir) && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    var targetPath = mov.SourcePath;
                    if (Directory.Exists(targetPath))
                    {
                        targetPath = ImageProcessor.GetUniqueDirectoryPath(targetPath);
                    }

                    Directory.Move(mov.DestinationPath, targetPath);
                    restored++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error undoing item {mov.DestinationPath}: {ex.Message}");
            }
        }

        // 2. Undo created output files (e.g. converted videos, documents, images, archives)
        foreach (var createdFile in record.CreatedFiles)
        {
            try
            {
                if (File.Exists(createdFile))
                {
                    File.Delete(createdFile);
                    restored++;

                    var parentDir = Path.GetDirectoryName(createdFile);
                    if (!string.IsNullOrWhiteSpace(parentDir))
                    {
                        createdDirsToCheck.Add(parentDir);
                    }
                }
                else if (Directory.Exists(createdFile))
                {
                    Directory.Delete(createdFile, recursive: true);
                    restored++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting created output file {createdFile}: {ex.Message}");
            }
        }

        // Clean up empty directories that were created during sorting or output generation
        foreach (var dir in createdDirsToCheck)
        {
            try
            {
                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir, recursive: false);
                }
            }
            catch { }
        }

        lock (_lock)
        {
            record.IsUndone = true;
            SaveHistory();
        }

        HistoryChanged?.Invoke();
        bool isEn = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        return (true, isEn ? $"Undone! Restored/removed items: {restored}" : $"Скасовано! Опрацьовано елементів: {restored}", restored);
    }

    private void LoadHistory()
    {
        try
        {
            if (File.Exists(_historyFilePath))
            {
                var json = File.ReadAllText(_historyFilePath);
                var items = JsonSerializer.Deserialize<List<OperationRecord>>(json);
                if (items != null)
                {
                    _history.Clear();
                    _history.AddRange(items);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load operation history: {ex.Message}");
        }
    }

    public void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save operation history: {ex.Message}");
        }
    }
}
