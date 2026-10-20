using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Halo.App.Native;
using Halo.App.Views;
using Halo.Core.Services;
using Microsoft.Win32;

namespace Halo.App;

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;
}

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private RadialOverlayWindow? _radialWindow;
    private SettingsWindow? _settingsWindow;
    private ShelfWindow? _shelfWindow;
    private HotkeyHelper? _hotkeyHelper;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeSubsystems();
    }

    private void InitializeSubsystems()
    {
        _radialWindow = new RadialOverlayWindow();
        _radialWindow.OpenSettingsRequested += OpenSettings;
        _radialWindow.AddToShelfRequested += AddToShelf;

        _shelfWindow = new ShelfWindow();

        // Hotkey registration
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        _hotkeyHelper = new HotkeyHelper(helper.Handle);
        _hotkeyHelper.HotkeyPressed += OnGlobalHotkeyPressed;

        RegisterConfiguredHotkey();

        // Start watched folders
        FolderWatcherService.Instance.Start();

        // Listen for settings change
        SettingsService.Instance.SettingsChanged += s => Dispatcher.Invoke(UpdateDashboardStats);

        UpdateDashboardStats();
    }

    private void RegisterConfiguredHotkey()
    {
        if (_hotkeyHelper == null) return;
        var hotkeyStr = SettingsService.Instance.Current.GlobalHotkey;
        _hotkeyHelper.Register(hotkeyStr);
        HotkeyStatusText.Text = hotkeyStr;
    }

    private void UpdateDashboardStats()
    {
        var settings = SettingsService.Instance.Current;
        var activeCount = settings.Actions.Count(a => a.IsEnabled);
        ActiveActionsCountText.Text = $"{activeCount} функцій";
        WatchedFoldersStatusText.Text = $"{settings.WatchedFolders.Count(w => w.IsEnabled)} правил";
        HotkeyStatusText.Text = settings.GlobalHotkey;
        RegisterConfiguredHotkey();
    }

    private void OnGlobalHotkeyPressed()
    {
        GetCursorPos(out var pt);
        var screenPoint = new Point(pt.X, pt.Y);

        // Try to get selected files in explorer
        var selected = ExplorerHelper.GetSelectedFiles();
        string? targetFile = selected.Count > 0 ? selected[0] : null;

        _radialWindow?.ShowAt(screenPoint, targetFile);
    }

    private void BtnOpenHaloHere_Click(object sender, RoutedEventArgs e)
    {
        GetCursorPos(out var pt);
        _radialWindow?.ShowAt(new Point(pt.X, pt.Y), null);
    }

    private void BtnPickFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Виберіть файл для тестування Halo",
            Filter = "Всі файли (*.*)|*.*|Зображення (*.png;*.jpg;*.webp)|*.png;*.jpg;*.webp"
        };

        if (dialog.ShowDialog() == true)
        {
            GetCursorPos(out var pt);
            _radialWindow?.ShowAt(new Point(pt.X, pt.Y), dialog.FileName);
        }
    }

    private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void OpenSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow();
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void BtnOpenShelf_Click(object sender, RoutedEventArgs e)
    {
        if (_shelfWindow == null || !_shelfWindow.IsLoaded)
        {
            _shelfWindow = new ShelfWindow();
        }
        _shelfWindow.Show();
        _shelfWindow.Activate();
    }

    private void AddToShelf(string filePath)
    {
        if (_shelfWindow == null || !_shelfWindow.IsLoaded)
        {
            _shelfWindow = new ShelfWindow();
        }
        _shelfWindow.AddFile(filePath);
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var target = files[0];
                GetCursorPos(out var pt);
                _radialWindow?.ShowAt(new Point(pt.X, pt.Y), target);
            }
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _hotkeyHelper?.Dispose();
        FolderWatcherService.Instance.Stop();
        _radialWindow?.Close();
        _settingsWindow?.Close();
        _shelfWindow?.Close();
    }
}
