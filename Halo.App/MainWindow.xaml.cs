using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
        RefreshDashboardStats();
    }

    private void InitializeSubsystems()
    {
        _radialWindow = new RadialOverlayWindow();
        _radialWindow.OpenSettingsRequested += () => Dispatcher.Invoke(() => NavSettings.IsChecked = true);
        _radialWindow.AddToShelfRequested += AddToShelf;

        _shelfWindow = new ShelfWindow();

        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        _hotkeyHelper = new HotkeyHelper(helper.Handle);
        _hotkeyHelper.HotkeyPressed += OnGlobalHotkeyPressed;

        RegisterConfiguredHotkey();
        FolderWatcherService.Instance.Start();

        SettingsService.Instance.SettingsChanged += s => Dispatcher.Invoke(() =>
        {
            RefreshDashboardStats();
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

        if (StatGlobalHotkey != null) StatGlobalHotkey.Text = hotkeyStr;
        if (SidebarHotkeyBadge != null) SidebarHotkeyBadge.Text = hotkeyStr;
        if (HotkeyRecorderStatusText != null) HotkeyRecorderStatusText.Text = hotkeyStr;
    }

    private void RefreshDashboardStats()
    {
        var settings = SettingsService.Instance.Current;
        int activeActions = settings.Actions.Count(a => a.IsEnabled);
        int customPresets = Math.Max(3, settings.Actions.Count(a => a.IsCustom) + 3);
        int activeFolders = settings.WatchedFolders.Count(w => w.IsEnabled);

        if (StatActiveActions != null) StatActiveActions.Text = activeActions.ToString();
        if (StatGlobalHotkey != null) StatGlobalHotkey.Text = settings.GlobalHotkey;
        if (SidebarHotkeyBadge != null) SidebarHotkeyBadge.Text = settings.GlobalHotkey;
        if (StatPresetsCount != null) StatPresetsCount.Text = customPresets.ToString();
        if (StatWatchedFolders != null) StatWatchedFolders.Text = activeFolders.ToString();
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
    // TITLEBAR & WINDOW CONTROLS
    // ==========================================
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // ==========================================
    // SIDEBAR NAVIGATION (ORBIT STYLE)
    // ==========================================
    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (ScreenMain == null || ScreenRadial == null || ScreenInput == null || ScreenPresets == null || ScreenFolders == null || ScreenSettings == null) return;

        ScreenMain.Visibility = Visibility.Collapsed;
        ScreenRadial.Visibility = Visibility.Collapsed;
        ScreenInput.Visibility = Visibility.Collapsed;
        ScreenPresets.Visibility = Visibility.Collapsed;
        ScreenFolders.Visibility = Visibility.Collapsed;
        ScreenSettings.Visibility = Visibility.Collapsed;

        if (sender == NavMain)
        {
            ScreenMain.Visibility = Visibility.Visible;
            PageHeaderTitle.Text = "Головна";
            PageHeaderSubtitle.Text = "Огляд активних інструментів, швидких дій та стану робочого простору";
            RefreshDashboardStats();
        }
        else if (sender == NavRadial)
        {
            ScreenRadial.Visibility = Visibility.Visible;
            PageHeaderTitle.Text = "Кругові меню";
            PageHeaderSubtitle.Text = "Налаштування секторів радіального диска для кожного типу файлів";
            LoadRadialScreenData();
        }
        else if (sender == NavInput)
        {
            ScreenInput.Visibility = Visibility.Visible;
            PageHeaderTitle.Text = "Жести й клавіші";
            PageHeaderSubtitle.Text = "Призначення глобальної комбінації виклику та керування жестами миші";
        }
        else if (sender == NavPresets)
        {
            ScreenPresets.Visibility = Visibility.Visible;
            PageHeaderTitle.Text = "Шаблони";
            PageHeaderSubtitle.Text = "Готові комбіновані макроси для миттєвої пакетної обробки файлів";
        }
        else if (sender == NavFolders)
        {
            ScreenFolders.Visibility = Visibility.Visible;
            PageHeaderTitle.Text = "Відстеження папок";
            PageHeaderSubtitle.Text = "Автоматична фонова обробка нових файлів у вибраних каталогах";
            LoadFoldersData();
        }
        else if (sender == NavSettings)
        {
            ScreenSettings.Visibility = Visibility.Visible;
            PageHeaderTitle.Text = "Налаштування";
            PageHeaderSubtitle.Text = "Параметри локалізації, візуальної теми та системної інтеграції";
        }
    }

    private void BtnJumpToRadial_Click(object sender, RoutedEventArgs e)
    {
        NavRadial.IsChecked = true;
    }

    private void BtnJumpToInput_Click(object sender, MouseButtonEventArgs e)
    {
        NavInput.IsChecked = true;
    }

    private void BtnJumpToPresets_Click(object sender, MouseButtonEventArgs e)
    {
        NavPresets.IsChecked = true;
    }

    private void BtnJumpToFolders_Click(object sender, MouseButtonEventArgs e)
    {
        NavFolders.IsChecked = true;
    }

    // ==========================================
    // SCREEN 1: ГОЛОВНА (ДАШБОРД ORBIT)
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
            Title = "Виберіть файл для відкриття у круговому меню Halo",
            Filter = "Всі файли (*.*)|*.*|Зображення (*.png;*.jpg;*.webp)|*.png;*.jpg;*.webp"
        };

        if (dialog.ShowDialog() == true)
        {
            GetCursorPos(out var pt);
            _radialWindow?.ShowAt(new Point(pt.X, pt.Y), dialog.FileName);
        }
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

    private void AddToShelf(string filePath)
    {
        if (_shelfWindow == null || !_shelfWindow.IsLoaded)
        {
            _shelfWindow = new ShelfWindow();
        }
        _shelfWindow.AddFile(filePath);
    }

    // ==========================================
    // SCREEN 2: КРУГОВІ МЕНЮ
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
            _ => "Усі файли"
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

        if (BadgeImageCount != null) BadgeImageCount.Text = imgActive.ToString();
        if (BadgeDocCount != null) BadgeDocCount.Text = docActive.ToString();
        if (BadgeVideoCount != null) BadgeVideoCount.Text = vidActive.ToString();
        if (BadgeAudioCount != null) BadgeAudioCount.Text = audActive.ToString();
        if (BadgeGenericCount != null) BadgeGenericCount.Text = genActive.ToString();
    }

    private void RadialActionToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is ActionItem item)
        {
            item.IsEnabled = cb.IsChecked ?? true;
            SettingsService.Instance.SaveSettings();
            UpdateCategorySectors();
            UpdateCategoryBadges();
            RefreshDashboardStats();
        }
    }

    // ==========================================
    // SCREEN 3: ЖЕСТИ Й КЛАВІШІ (KEY RECORDER)
    // ==========================================
    private void CheckPauseGestures_Click(object sender, RoutedEventArgs e)
    {
        _gesturesPaused = CheckPauseGestures.IsChecked ?? false;
        if (_gesturesPaused)
        {
            _hotkeyHelper?.Unregister();
            if (HotkeyRecorderStatusText != null) HotkeyRecorderStatusText.Text = "Призупинено";
            if (SidebarHotkeyBadge != null) SidebarHotkeyBadge.Text = "Пауза";
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
        HotkeyRecorderStatusText.Text = "Очікування...";
        HotkeyRecorderHint.Text = "Затисніть нову комбінацію на клавіатурі (наприклад Alt+Space або Ctrl+Shift+H). Натисніть Esc для скасування.";
        BtnRecordHotkey.Content = "Скасувати запис";

        HotkeyRecorderCard.BorderBrush = (Brush)FindResource("OrbitPurpleBrush");
        HotkeyRecorderCard.Effect = (Effect)FindResource("OrbitPurpleGlow");
    }

    private void StopRecordingHotkey()
    {
        _isRecordingHotkey = false;
        HotkeyRecorderHint.Text = "Натисніть кнопку запису нижче, а потім затисніть бажані клавіші на клавіатурі.";
        BtnRecordHotkey.Content = "⏺️  Записати нову комбінацію з клавіатури";

        HotkeyRecorderCard.BorderBrush = (Brush)FindResource("OrbitCardBorderBrush");
        HotkeyRecorderCard.Effect = (Effect)FindResource("OrbitCardShadow");

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
        RefreshDashboardStats();
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
                AccentColor = "#8B5CF6",
                Category = ActionCategory.CustomMacro,
                SupportedFileTypes = new List<FileTypeCategory> { FileTypeCategory.Generic },
                IsEnabled = true,
                IsCustom = true,
                Order = settings.Actions.Count + 1
            };

            settings.Actions.Add(newAction);
            SettingsService.Instance.SaveSettings(settings);
            RefreshDashboardStats();
            MessageBox.Show($"Шаблон «{name}» додано до списку секторів!", "Halo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnTestPreset_Click(object sender, RoutedEventArgs e)
    {
        BtnPickFile_Click(sender, e);
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
            MessageBox.Show("Будь ласка, виберіть дійсну існуючу папку на диску.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        RefreshDashboardStats();
        FolderWatcherService.Instance.Start();
    }

    private void BtnDeleteFolderRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchedFolderConfig rule)
        {
            var settings = SettingsService.Instance.Current;
            settings.WatchedFolders.Remove(rule);
            SettingsService.Instance.SaveSettings(settings);

            LoadFoldersData();
            RefreshDashboardStats();
            FolderWatcherService.Instance.Start();
        }
    }

    private void FolderRuleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is WatchedFolderConfig rule)
        {
            rule.IsEnabled = cb.IsChecked ?? true;
            SettingsService.Instance.SaveSettings();
            RefreshDashboardStats();
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
