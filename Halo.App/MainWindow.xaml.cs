using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Halo.App.Native;
using Halo.App.Views;
using Halo.Core.Models;
using Halo.Core.Services;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Halo.App;

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;
}

public partial class MainWindow : FluentWindow
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private RadialOverlayWindow? _radialWindow;
    private ShelfWindow? _shelfWindow;
    private HotkeyHelper? _hotkeyHelper;
    private bool _gesturesPaused = false;
    private bool _isRecordingHotkey = false;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeSubsystems();
        LoadRadialScreenData();
        RefreshTelemetry();
    }

    private void InitializeSubsystems()
    {
        _radialWindow = new RadialOverlayWindow();
        _radialWindow.OpenSettingsRequested += () => Dispatcher.Invoke(() =>
        {
            SwitchToPage("PageSettings");
        });
        _radialWindow.AddToShelfRequested += AddToShelf;

        _shelfWindow = new ShelfWindow();

        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        _hotkeyHelper = new HotkeyHelper(helper.Handle);
        _hotkeyHelper.HotkeyPressed += OnGlobalHotkeyPressed;

        RegisterConfiguredHotkey();
        FolderWatcherService.Instance.Start();

        SettingsService.Instance.SettingsChanged += s => Dispatcher.Invoke(() =>
        {
            RefreshTelemetry();
            LoadRadialScreenData();
            LoadFoldersData();
        });

        LoadFoldersData();
    }

    private void RegisterConfiguredHotkey()
    {
        if (_hotkeyHelper == null || _gesturesPaused) return;
        var hotkeyStr = SettingsService.Instance.Current.GlobalHotkey;
        _hotkeyHelper.Register(hotkeyStr);

        if (QuickHotkeyDisplay != null) QuickHotkeyDisplay.Text = hotkeyStr;
        if (HotkeyRecorderStatusText != null) HotkeyRecorderStatusText.Text = hotkeyStr;
    }

    private void RefreshTelemetry()
    {
        var settings = SettingsService.Instance.Current;
        if (QuickHotkeyDisplay != null) QuickHotkeyDisplay.Text = settings.GlobalHotkey;
        if (HotkeyRecorderStatusText != null) HotkeyRecorderStatusText.Text = settings.GlobalHotkey;
    }

    private void OnGlobalHotkeyPressed()
    {
        if (_gesturesPaused) return;

        GetCursorPos(out var pt);
        var screenPoint = new Point(pt.X, pt.Y);

        var selected = ExplorerHelper.GetSelectedFiles();
        string? targetFile = selected.Count > 0 ? selected[0] : null;

        _radialWindow?.ShowAt(screenPoint, targetFile);
    }

    // ==========================================
    // NAVIGATION
    // ==========================================
    private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (sender.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag is string tag)
        {
            SwitchToPage(tag);
        }
    }

    private void SwitchToPage(string pageTag)
    {
        if (PageMain == null || PageRadial == null || PageInput == null || PagePresets == null || PageFolders == null || PageSettings == null) return;

        PageMain.Visibility = Visibility.Collapsed;
        PageRadial.Visibility = Visibility.Collapsed;
        PageInput.Visibility = Visibility.Collapsed;
        PagePresets.Visibility = Visibility.Collapsed;
        PageFolders.Visibility = Visibility.Collapsed;
        PageSettings.Visibility = Visibility.Collapsed;

        switch (pageTag)
        {
            case "PageMain":
                PageMain.Visibility = Visibility.Visible;
                RefreshTelemetry();
                break;
            case "PageRadial":
                PageRadial.Visibility = Visibility.Visible;
                LoadRadialScreenData();
                break;
            case "PageInput":
                PageInput.Visibility = Visibility.Visible;
                break;
            case "PagePresets":
                PagePresets.Visibility = Visibility.Visible;
                break;
            case "PageFolders":
                PageFolders.Visibility = Visibility.Visible;
                LoadFoldersData();
                break;
            case "PageSettings":
                PageSettings.Visibility = Visibility.Visible;
                break;
        }
    }

    // ==========================================
    // SCREEN 1: ГОЛОВНА
    // ==========================================
    private void BtnOpenHaloHere_Click(object sender, RoutedEventArgs e)
    {
        GetCursorPos(out var pt);
        _radialWindow?.ShowAt(new Point(pt.X, pt.Y), null);
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

    private void BtnPickFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Виберіть файл для тесту Halo",
            Filter = "Всі файли (*.*)|*.*|Зображення (*.png;*.jpg;*.webp)|*.png;*.jpg;*.webp"
        };

        if (dialog.ShowDialog() == true)
        {
            GetCursorPos(out var pt);
            _radialWindow?.ShowAt(new Point(pt.X, pt.Y), dialog.FileName);
        }
    }

    private void AddToShelf(string filePath)
    {
        if (_shelfWindow == null || !_shelfWindow.IsLoaded)
        {
            _shelfWindow = new ShelfWindow();
        }
        _shelfWindow.AddFile(filePath);
    }

    // ==========================================
    // SCREEN 2: ХОТКЕЙ ТА ЖЕСТИ (KEY RECORDER)
    // ==========================================
    private void CheckPauseGestures_Click(object sender, RoutedEventArgs e)
    {
        _gesturesPaused = CheckPauseGestures.IsChecked ?? false;
        if (_gesturesPaused)
        {
            _hotkeyHelper?.Unregister();
            if (HotkeyRecorderStatusText != null) HotkeyRecorderStatusText.Text = "Призупинено";
        }
        else
        {
            RegisterConfiguredHotkey();
        }
    }

    private void BtnRecordHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecordingHotkey)
        {
            StopRecordingHotkey();
            return;
        }

        _isRecordingHotkey = true;
        HotkeyRecorderStatusText.Text = "Натисніть клавіші...";
        HotkeyRecorderHint.Text = "Натисніть комбінацію (наприклад Alt+Space або Ctrl+Shift+H). Esc для скасування.";
    }

    private void StopRecordingHotkey()
    {
        _isRecordingHotkey = false;
        HotkeyRecorderHint.Text = "Натисніть сюди, щоб призначити нову комбінацію на клавіатурі";
        RegisterConfiguredHotkey();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRecordingHotkey) return;

        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            StopRecordingHotkey();
            return;
        }

        // Ignore standalone modifier presses
        if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LWin || e.Key == Key.RWin ||
            e.Key == Key.System)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var parts = new List<string>();

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");

        if (parts.Count == 0)
        {
            parts.Add("Alt");
        }

        parts.Add(key.ToString());
        var newHotkey = string.Join("+", parts);

        var settings = SettingsService.Instance.Current;
        settings.GlobalHotkey = newHotkey;
        SettingsService.Instance.SaveSettings(settings);

        StopRecordingHotkey();
    }

    // ==========================================
    // SCREEN 3: КРУГОВІ МЕНЮ
    // ==========================================
    private void LoadRadialScreenData()
    {
        if (RadialCategoriesList == null) return;
        UpdateCategorySectors();
        UpdateCategoryBadges();
    }

    private void RadialCategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateCategorySectors();
    }

    private void UpdateCategorySectors()
    {
        if (RadialCategoriesList?.SelectedItem is not ListBoxItem selected) return;
        if (RadialActionsItemsControl == null || PreviewRadialDisc == null) return;

        var categoryTag = selected.Tag?.ToString() ?? "Image";

        var settings = SettingsService.Instance.Current;
        var catEnum = categoryTag switch
        {
            "Image" => FileTypeCategory.Image,
            "Document" => FileTypeCategory.Document,
            "Video" => FileTypeCategory.Video,
            "Audio" => FileTypeCategory.Audio,
            _ => FileTypeCategory.Generic
        };

        var filtered = settings.Actions
            .Where(a => a.SupportedFileTypes.Contains(catEnum) || a.SupportedFileTypes.Contains(FileTypeCategory.Generic))
            .OrderBy(a => a.Order)
            .ToList();

        RadialActionsItemsControl.ItemsSource = null;
        RadialActionsItemsControl.ItemsSource = filtered;

        string title = categoryTag switch
        {
            "Image" => "Зображення",
            "Document" => "Документи",
            "Video" => "Відео",
            "Audio" => "Аудіо",
            _ => "Файли"
        };

        PreviewRadialDisc.SetActions(filtered, title);
    }

    private void UpdateCategoryBadges()
    {
        var settings = SettingsService.Instance.Current;

        int imgActive = settings.Actions.Count(a => a.IsEnabled && a.SupportedFileTypes.Contains(FileTypeCategory.Image));
        int docActive = settings.Actions.Count(a => a.IsEnabled && a.SupportedFileTypes.Contains(FileTypeCategory.Document));
        int vidActive = settings.Actions.Count(a => a.IsEnabled && a.SupportedFileTypes.Contains(FileTypeCategory.Video));
        int audActive = settings.Actions.Count(a => a.IsEnabled && a.SupportedFileTypes.Contains(FileTypeCategory.Audio));
        int genActive = settings.Actions.Count(a => a.IsEnabled && a.SupportedFileTypes.Contains(FileTypeCategory.Generic));

        if (BadgeImageCount != null) BadgeImageCount.Text = $"{imgActive}/8";
        if (BadgeDocCount != null) BadgeDocCount.Text = $"{docActive}/8";
        if (BadgeVideoCount != null) BadgeVideoCount.Text = $"{vidActive}/8";
        if (BadgeAudioCount != null) BadgeAudioCount.Text = $"{audActive}/8";
        if (BadgeGenericCount != null) BadgeGenericCount.Text = $"{genActive}/8";
    }

    private void RadialActionToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch ts && ts.Tag is ActionItem item)
        {
            item.IsEnabled = ts.IsChecked ?? true;
            SettingsService.Instance.SaveSettings();
            UpdateCategorySectors();
            UpdateCategoryBadges();
            RefreshTelemetry();
        }
    }

    // ==========================================
    // SCREEN 4: ШАБЛОНИ
    // ==========================================
    private void BtnCreatePreset_Click(object sender, RoutedEventArgs e)
    {
        var name = CyberInputDialog.Prompt(this, "Введіть назву нового шаблону (макросу):", "Мій швидкий пресет");
        if (!string.IsNullOrWhiteSpace(name))
        {
            var settings = SettingsService.Instance.Current;
            var newAction = new ActionItem
            {
                Id = "preset_" + Guid.NewGuid().ToString("N")[..8],
                Title = name.Trim(),
                Description = "Користувацький шаблон швидкої обробки",
                IconKey = "⚡",
                AccentColor = "#00F0FF",
                Category = ActionCategory.CustomMacro,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Generic },
                IsEnabled = true,
                IsCustom = true,
                Order = settings.Actions.Count + 1
            };

            settings.Actions.Add(newAction);
            SettingsService.Instance.SaveSettings(settings);
            System.Windows.MessageBox.Show($"Шаблон «{name}» успішно додано!", "Halo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnTestPreset_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Пресет готовий до виконання. Виберіть файл або натисніть Alt+Space над файлом у Провіднику.", "Halo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ==========================================
    // SCREEN 5: ВІДСТЕЖЕННЯ ПАПОК
    // ==========================================
    private void LoadFoldersData()
    {
        if (FoldersRulesItemsControl == null) return;
        var settings = SettingsService.Instance.Current;
        FoldersRulesItemsControl.ItemsSource = null;
        FoldersRulesItemsControl.ItemsSource = settings.WatchedFolders;
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Виберіть папку для автоматичного фонового моніторингу"
        };

        if (dialog.ShowDialog() == true)
        {
            QuickFolderInput.Text = dialog.FolderName;
        }
    }

    private void BtnQuickAddRule_Click(object sender, RoutedEventArgs e)
    {
        var folder = QuickFolderInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            System.Windows.MessageBox.Show("Будь ласка, вкажіть дійсну існуючу папку.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var actionTag = (QuickActionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "image_to_webp";

        var rule = new WatchedFolderConfig
        {
            FolderPath = folder,
            AssignedActionId = actionTag,
            IsEnabled = true
        };

        var settings = SettingsService.Instance.Current;
        settings.WatchedFolders.Add(rule);
        SettingsService.Instance.SaveSettings(settings);

        LoadFoldersData();
        FolderWatcherService.Instance.Start();

        System.Windows.MessageBox.Show($"Папку «{Path.GetFileName(folder)}» додано до автоматичного моніторингу!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDeleteFolderRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is WatchedFolderConfig rule)
        {
            var settings = SettingsService.Instance.Current;
            settings.WatchedFolders.Remove(rule);
            SettingsService.Instance.SaveSettings(settings);

            LoadFoldersData();
            FolderWatcherService.Instance.Start();
        }
    }

    private void FolderRuleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch ts && ts.Tag is WatchedFolderConfig rule)
        {
            rule.IsEnabled = ts.IsChecked ?? true;
            SettingsService.Instance.SaveSettings();
            FolderWatcherService.Instance.Start();
        }
    }

    // ==========================================
    // SCREEN 6: НАЛАШТУВАННЯ
    // ==========================================
    private void GeneralThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GeneralThemeCombo == null) return;
        var settings = SettingsService.Instance.Current;
        settings.Theme = GeneralThemeCombo.SelectedIndex switch
        {
            1 => "Light",
            2 => "System",
            _ => "Dark"
        };
        SettingsService.Instance.SaveSettings(settings);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _hotkeyHelper?.Dispose();
        FolderWatcherService.Instance.Stop();
        _radialWindow?.Close();
        _shelfWindow?.Close();
    }
}
