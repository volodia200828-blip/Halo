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
    private ShelfWindow? _shelfWindow;
    private HotkeyHelper? _hotkeyHelper;
    private bool _gesturesPaused = false;

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
        RefreshDashboardKPIs();
    }

    private void InitializeSubsystems()
    {
        _radialWindow = new RadialOverlayWindow();
        _radialWindow.OpenSettingsRequested += () => Dispatcher.Invoke(() => NavSettings.IsChecked = true);
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
        SettingsService.Instance.SettingsChanged += s => Dispatcher.Invoke(() =>
        {
            RefreshDashboardKPIs();
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
        MainHotkeyText.Text = hotkeyStr;
    }

    private void RefreshDashboardKPIs()
    {
        var settings = SettingsService.Instance.Current;
        var activeCount = settings.Actions.Count(a => a.IsEnabled);
        MainActiveActionsText.Text = $"{activeCount} функцій";
        MainWatchedFoldersText.Text = $"{settings.WatchedFolders.Count(w => w.IsEnabled)} правил";
        MainHotkeyText.Text = settings.GlobalHotkey;
    }

    private void OnGlobalHotkeyPressed()
    {
        if (_gesturesPaused) return;

        GetCursorPos(out var pt);
        var screenPoint = new Point(pt.X, pt.Y);

        // Try to get selected files in Explorer
        var selected = ExplorerHelper.GetSelectedFiles();
        string? targetFile = selected.Count > 0 ? selected[0] : null;

        _radialWindow?.ShowAt(screenPoint, targetFile);
    }

    // ==========================================
    // SIDEBAR NAVIGATION
    // ==========================================
    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (ScreenMain == null || ScreenInput == null || ScreenRadial == null || ScreenPresets == null || ScreenFolders == null || ScreenSettings == null) return;

        ScreenMain.Visibility = Visibility.Collapsed;
        ScreenInput.Visibility = Visibility.Collapsed;
        ScreenRadial.Visibility = Visibility.Collapsed;
        ScreenPresets.Visibility = Visibility.Collapsed;
        ScreenFolders.Visibility = Visibility.Collapsed;
        ScreenSettings.Visibility = Visibility.Collapsed;

        if (sender == NavMain)
        {
            ScreenMain.Visibility = Visibility.Visible;
            RefreshDashboardKPIs();
        }
        else if (sender == NavInput)
        {
            ScreenInput.Visibility = Visibility.Visible;
        }
        else if (sender == NavRadial)
        {
            ScreenRadial.Visibility = Visibility.Visible;
            LoadRadialScreenData();
        }
        else if (sender == NavPresets)
        {
            ScreenPresets.Visibility = Visibility.Visible;
        }
        else if (sender == NavFolders)
        {
            ScreenFolders.Visibility = Visibility.Visible;
            LoadFoldersData();
        }
        else if (sender == NavSettings)
        {
            ScreenSettings.Visibility = Visibility.Visible;
        }
    }

    private void BtnOpenSettingsScreen_Click(object sender, RoutedEventArgs e)
    {
        NavSettings.IsChecked = true;
    }

    private void BtnSupport_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Дякуємо за інтерес до підтримки Halo!\n\nПроєкт розвивається як сучасна незалежна українська утиліта для продуктивності.",
            "Підтримати Halo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ==========================================
    // SCREEN 1: ГОЛОВНА
    // ==========================================
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
                GetCursorPos(out var pt);
                _radialWindow?.ShowAt(new Point(pt.X, pt.Y), files[0]);
            }
        }
    }

    // ==========================================
    // SCREEN 2: ЖЕСТИ Й КЛАВІШІ
    // ==========================================
    private void CheckPauseGestures_Click(object sender, RoutedEventArgs e)
    {
        _gesturesPaused = CheckPauseGestures.IsChecked ?? false;
        if (_gesturesPaused)
        {
            _hotkeyHelper?.Unregister();
            MainHotkeyText.Text = "Призупинено";
            MainHotkeyText.Foreground = System.Windows.Media.Brushes.Gray;
        }
        else
        {
            RegisterConfiguredHotkey();
            MainHotkeyText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
        }
    }

    private void BtnChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance.Current;
        var current = settings.GlobalHotkey;
        var input = CyberInputDialog.Prompt(this, "Введіть нову комбінацію клавіш:", current);

        if (!string.IsNullOrWhiteSpace(input))
        {
            settings.GlobalHotkey = input.Trim();
            SettingsService.Instance.SaveSettings(settings);
            RegisterConfiguredHotkey();
        }
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
        if (sender is CheckBox cb && cb.Tag is ActionItem item)
        {
            item.IsEnabled = cb.IsChecked ?? true;
            SettingsService.Instance.SaveSettings();
            UpdateCategorySectors();
            UpdateCategoryBadges();
            RefreshDashboardKPIs();
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
                AccentColor = "#38BDF8",
                Category = ActionCategory.CustomMacro,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Generic },
                IsEnabled = true,
                IsCustom = true,
                Order = settings.Actions.Count + 1
            };

            settings.Actions.Add(newAction);
            SettingsService.Instance.SaveSettings(settings);
            MessageBox.Show($"Шаблон «{name}» успішно збережено!", "Halo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
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
            Title = "Виберіть папку для автоматичного моніторингу"
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
            MessageBox.Show("Будь ласка, оберіть дійсну папку на диску.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        RefreshDashboardKPIs();
        FolderWatcherService.Instance.Start();

        MessageBox.Show($"Папку «{Path.GetFileName(folder)}» додано до автоматичного моніторингу!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDeleteFolderRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchedFolderConfig rule)
        {
            var settings = SettingsService.Instance.Current;
            settings.WatchedFolders.Remove(rule);
            SettingsService.Instance.SaveSettings(settings);

            LoadFoldersData();
            RefreshDashboardKPIs();
            FolderWatcherService.Instance.Start();
        }
    }

    private void FolderRuleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is WatchedFolderConfig rule)
        {
            rule.IsEnabled = cb.IsChecked ?? true;
            SettingsService.Instance.SaveSettings();
            RefreshDashboardKPIs();
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
