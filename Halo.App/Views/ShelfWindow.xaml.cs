using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Halo.Core.Services;

namespace Halo.App.Views;

public class ShelfFileItem
{
    public string FullPath { get; set; } = string.Empty;
    public string Name => Path.GetFileName(FullPath);
    public string SizeText { get; set; } = string.Empty;
}

public partial class ShelfWindow : Window
{
    private readonly ObservableCollection<ShelfFileItem> _items = new();

    public ShelfWindow()
    {
        InitializeComponent();
        ShelfItemsControl.ItemsSource = _items;
        LoadFromSettings();
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
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!_items.Any(i => string.Equals(i.FullPath, path, StringComparison.OrdinalIgnoreCase)))
        {
            AddItemInternal(path);
            SaveToSettings();
            UpdateEmptyState();
        }

        Show();
        Activate();
    }

    private void AddItemInternal(string path)
    {
        string size = "";
        if (File.Exists(path))
        {
            long bytes = new FileInfo(path).Length;
            size = FormatFileSize(bytes);
        }
        else if (Directory.Exists(path))
        {
            size = "Папка";
        }

        _items.Insert(0, new ShelfFileItem { FullPath = path, SizeText = size });
    }

    private void UpdateEmptyState()
    {
        EmptyStateText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveToSettings()
    {
        var settings = SettingsService.Instance.Current;
        settings.ShelfFilePaths = _items.Select(i => i.FullPath).ToList();
        SettingsService.Instance.SaveSettings(settings);
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

    private void ShelfItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement elem && elem.DataContext is ShelfFileItem item)
        {
            var data = new DataObject(DataFormats.FileDrop, new string[] { item.FullPath });
            DragDrop.DoDragDrop(elem, data, DragDropEffects.Copy | DragDropEffects.Move);
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
        string[] sizes = { "B", "KB", "MB", "GB" };
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
