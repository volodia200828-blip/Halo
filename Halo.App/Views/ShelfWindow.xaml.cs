using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Halo.Core.Models;
using Halo.Core.Services;
using Microsoft.Win32;

namespace Halo.App.Views;

public class ShelfFileItem
{
    private static bool IsEn => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);

    public string FullPath { get; set; } = string.Empty;
    public string Name => Path.GetFileName(FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    public string SizeText { get; set; } = string.Empty;
    public string Icon { get; set; } = "📄";

    public string DragTooltip => IsEn
        ? "Drag with mouse to drop this file into any app or folder"
        : "Потягніть мишкою, щоб перетягнути файл у будь-яку програму або папку";

    public string CopyClipboardTooltip => IsEn
        ? "Copy file to Windows clipboard (press Ctrl+V anywhere to paste)"
        : "Скопіювати файл у буфер обміну Windows (натисніть Ctrl+V будь-де для вставки)";

    public string OpenInHaloTooltip => IsEn ? "Open in Halo radial menu" : "Відкрити у круговому меню Halo";
    public string RevealTooltip => IsEn ? "Reveal in Windows Explorer" : "Показати у Провіднику Windows";
    public string RemoveTooltip => IsEn ? "Remove from Shelf" : "Прибрати з полиці";
}

public partial class ShelfWindow : Window
{
    private readonly ObservableCollection<ShelfFileItem> _items = new();
    private Point _dragStartPoint;

    public event Action<IReadOnlyList<string>>? OpenInRadialRequested;

    private static bool IsEn => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
    private static string T(string uk, string en) => IsEn ? en : uk;

    public ShelfWindow()
    {
        InitializeComponent();
        ShelfItemsControl.ItemsSource = _items;
        LoadFromSettings();
        ApplyLocalization();

        SettingsService.Instance.SettingsChanged += _ => Dispatcher.Invoke(ApplyLocalization);
    }

    public void OpenShelf()
    {
        ApplyLocalization();
        UpdateEmptyState();
        if (!IsVisible)
        {
            var workArea = SystemParameters.WorkArea;
            if (double.IsNaN(Left) || double.IsNaN(Top) || Left <= 0 || Top <= 0 ||
                Left + Width > workArea.Right || Top + Height > workArea.Bottom)
            {
                Left = Math.Max(workArea.Left + 24, workArea.Right - Width - 28);
                Top = Math.Max(workArea.Top + 24, workArea.Top + (workArea.Height - Height) / 2);
            }
            Show();
        }
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    public void ApplyLocalization()
    {
        Title = T("Halo — Полиця файлів", "Halo — File Shelf");
        ShelfTitleText.Text = T("Полиця файлів", "File Shelf");
        ShelfSubtitleText.Text = T("Тимчасове сховище та групові дії", "Temporary file staging & batch actions");
        BtnAddFiles.Content = T("＋ Додати файли", "＋ Add Files");
        BtnClearShelf.Content = T("Очистити", "Clear");
        EmptyStateTitleText.Text = T("Полиця порожня", "Shelf is empty");
        EmptyStateDescText.Text = T(
            "Перетягніть сюди файли з Провідника або додайте їх через кругове меню Halo (Дії → Покласти на полицю).",
            "Drag & drop files here from Explorer or add them via the Halo radial menu (Actions → Add to Shelf).");

        BtnBatchClipboard.Content = T("📋 У буфер (Ctrl+V)", "📋 Copy (Ctrl+V)");
        BtnBatchClipboard.ToolTip = T(
            "Скопіювати всі файли полиці у буфер обміну Windows — вставте їх клавішами Ctrl+V у Провідник, Telegram чи іншу програму",
            "Copy all Shelf files to the Windows Clipboard — press Ctrl+V in Explorer, Telegram, or any app to paste");

        BtnBatchCopy.Content = T("📁 Копіювати в папку…", "📁 Copy to Folder…");
        BtnBatchCopy.ToolTip = T(
            "Вибрати цільову папку на диску та зберегти туди копії всіх файлів із полиці",
            "Choose a destination folder on disk and save copies of all Shelf files there");

        BtnBatchHalo.Content = T("✦ Відкрити у Halo", "✦ Open in Halo");
        BtnBatchHalo.ToolTip = T(
            "Відкрити всі файли з полиці у круговому меню Halo для групової обробки",
            "Open all Shelf files in the Halo radial menu for batch processing");

        BtnBatchZip.Content = T("🗜️ Запакувати в ZIP", "🗜️ Pack to ZIP");
        BtnBatchZip.ToolTip = T(
            "Створити єдиний ZIP-архів з усіх файлів на полиці",
            "Create a single ZIP archive containing all files on the Shelf");

        ShelfFooterHintText.Text = T(
            "«У буфер (Ctrl+V)» копіює файли для швидкої вставки будь-куди, а «Копіювати в папку…» зберігає копії в обраний каталог.",
            "'Copy (Ctrl+V)' copies files to the clipboard to paste anywhere; 'Copy to Folder…' saves copies into a chosen directory.");

        // Refresh items for localized tooltips/folder label
        var paths = _items.Select(i => i.FullPath).ToList();
        _items.Clear();
        foreach (var p in paths.AsEnumerable().Reverse())
        {
            if (File.Exists(p) || Directory.Exists(p))
            {
                AddItemInternal(p);
            }
        }
        UpdateEmptyState();
    }

    private void LoadFromSettings()
    {
        var settings = SettingsService.Instance.Current;
        foreach (var path in settings.ShelfFilePaths)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                AddItemInternal(path);
            }
        }
        UpdateEmptyState();
    }

    public void AddFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            OpenShelf();
            return;
        }

        if (!_items.Any(i => string.Equals(i.FullPath, path, StringComparison.OrdinalIgnoreCase)))
        {
            AddItemInternal(path);
            SaveToSettings();
            UpdateEmptyState();
        }

        OpenShelf();
    }

    private void AddItemInternal(string path)
    {
        string size = "";
        if (File.Exists(path))
        {
            long bytes = new FileInfo(path).Length;
            size = $"{FormatFileSize(bytes)} • {Path.GetExtension(path).TrimStart('.').ToUpperInvariant()}";
        }
        else if (Directory.Exists(path))
        {
            size = T("Папка", "Folder");
        }

        var cat = FileTypeDetector.Detect(path);
        string icon = cat switch
        {
            FileTypeCategory.Image => "🖼️",
            FileTypeCategory.Video => "🎬",
            FileTypeCategory.Pdf => "📕",
            FileTypeCategory.Document => "📄",
            FileTypeCategory.Presentation => "📽️",
            FileTypeCategory.Spreadsheet => "📊",
            FileTypeCategory.Archive => "🗜️",
            FileTypeCategory.Folder => "📁",
            _ => "📦"
        };

        _items.Insert(0, new ShelfFileItem
        {
            FullPath = path,
            SizeText = size,
            Icon = icon
        });
    }

    private void UpdateEmptyState()
    {
        bool isEmpty = _items.Count == 0;
        EmptyStatePanel.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        ShelfCountBadgeText.Text = _items.Count.ToString();
        BtnBatchClipboard.IsEnabled = !isEmpty;
        BtnBatchHalo.IsEnabled = !isEmpty;
        BtnBatchZip.IsEnabled = !isEmpty;
        BtnBatchCopy.IsEnabled = !isEmpty;
    }

    private void SaveToSettings()
    {
        var settings = SettingsService.Instance.Current;
        settings.ShelfFilePaths = _items.Select(i => i.FullPath).ToList();
        SettingsService.Instance.SaveSettings(settings);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = T("Оберіть файли для додавання на полицю", "Select files to add to Shelf"),
            Multiselect = true
        };
        if (dlg.ShowDialog(this) == true)
        {
            foreach (var f in dlg.FileNames)
            {
                AddFile(f);
            }
        }
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        _items.Clear();
        SaveToSettings();
        UpdateEmptyState();
    }

    private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            var item = _items.FirstOrDefault(i => i.FullPath == path);
            if (item != null)
            {
                _items.Remove(item);
                SaveToSettings();
                UpdateEmptyState();
            }
        }
    }

    private void BtnCopyItemToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && (File.Exists(path) || Directory.Exists(path)))
        {
            var dropList = new System.Collections.Specialized.StringCollection { path };
            Clipboard.SetFileDropList(dropList);
            ShelfFooterHintText.Text = T(
                $"✅ «{Path.GetFileName(path)}» скопійовано у буфер — натисніть Ctrl+V у будь-якій програмі",
                $"✅ '{Path.GetFileName(path)}' copied to clipboard — press Ctrl+V anywhere to paste");
        }
    }

    private void BtnOpenItemInHalo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && (File.Exists(path) || Directory.Exists(path)))
        {
            OpenInRadialRequested?.Invoke([path]);
        }
    }

    private void BtnRevealItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path && (File.Exists(path) || Directory.Exists(path)))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
        }
    }

    private void BtnBatchClipboard_Click(object sender, RoutedEventArgs e)
    {
        var paths = _items.Select(i => i.FullPath).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (paths.Count == 0) return;

        var dropList = new System.Collections.Specialized.StringCollection();
        dropList.AddRange(paths.ToArray());
        Clipboard.SetFileDropList(dropList);

        ShelfFooterHintText.Text = T(
            $"✅ Скопійовано у буфер обміну ({paths.Count} шт.) — натисніть Ctrl+V у Провіднику чи месенджері!",
            $"✅ Copied to clipboard ({paths.Count} item(s)) — press Ctrl+V in Explorer or any app to paste!");
    }

    private void BtnBatchHalo_Click(object sender, RoutedEventArgs e)
    {
        var paths = _items.Select(i => i.FullPath).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (paths.Count > 0)
        {
            OpenInRadialRequested?.Invoke(paths);
        }
    }

    private void BtnBatchZip_Click(object sender, RoutedEventArgs e)
    {
        var paths = _items.Select(i => i.FullPath).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (paths.Count == 0) return;

        try
        {
            var zipPath = ImageProcessor.CompressToZip(paths);
            ShelfFooterHintText.Text = T($"✅ Створено ZIP: {Path.GetFileName(zipPath)}", $"✅ Created ZIP: {Path.GetFileName(zipPath)}");
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{zipPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShelfFooterHintText.Text = T($"❌ Помилка: {ex.Message}", $"❌ Error: {ex.Message}");
        }
    }

    private void BtnBatchCopy_Click(object sender, RoutedEventArgs e)
    {
        var paths = _items.Select(i => i.FullPath).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (paths.Count == 0) return;

        var folderDialog = new OpenFolderDialog
        {
            Title = T("Оберіть цільову папку, куди скопіювати всі файли з полиці", "Select destination folder to save copies of all Shelf files")
        };

        if (folderDialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(folderDialog.FolderName))
        {
            int copied = 0;
            string? lastDest = null;
            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    var dest = ImageProcessor.GetUniqueFilePath(Path.Combine(folderDialog.FolderName, Path.GetFileName(p)));
                    File.Copy(p, dest, overwrite: false);
                    lastDest = dest;
                    copied++;
                }
                else if (Directory.Exists(p))
                {
                    var trimmed = p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var destDir = ImageProcessor.GetUniqueDirectoryPath(Path.Combine(folderDialog.FolderName, Path.GetFileName(trimmed)));
                    CopyDirectoryRecursive(trimmed, destDir);
                    lastDest = destDir;
                    copied++;
                }
            }
            ShelfFooterHintText.Text = T(
                $"✅ Скопійовано у папку «{Path.GetFileName(folderDialog.FolderName)}»: {copied} шт.",
                $"✅ Copied to folder '{Path.GetFileName(folderDialog.FolderName)}': {copied} item(s).");

            if (!string.IsNullOrEmpty(lastDest))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{lastDest}\"",
                    UseShellExecute = true
                });
            }
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSub = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSub);
        }
    }

    private void ShelfItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement elem && elem.DataContext is ShelfFileItem item)
        {
            var pos = e.GetPosition(this);
            if ((pos - _dragStartPoint).Length > 6)
            {
                _dragStartPoint = pos;
                var data = new DataObject(DataFormats.FileDrop, new string[] { item.FullPath });
                DragDrop.DoDragDrop(elem, data, DragDropEffects.Copy | DragDropEffects.Move);
            }
        }
        else
        {
            _dragStartPoint = e.GetPosition(this);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null)
            {
                foreach (var f in files)
                {
                    AddFile(f);
                }
            }
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.#} {sizes[order]}";
    }
}
