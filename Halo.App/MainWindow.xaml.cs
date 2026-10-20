using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    private enum HotkeyRecordTarget
    {
        None,
        MainTrigger,
        GameModeTrigger
    }

    private RadialOverlayWindow? _radialWindow;
    private HotkeyHelper? _hotkeyHelper;
    private MouseGestureDetector? _gestureDetector;
    private HotkeyRecordTarget _recordTarget = HotkeyRecordTarget.None;
    private bool _isInitializingUi = false;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _isInitializingUi = true;
        InitializeSubsystems();
        SyncUiFromSettings();
        LoadRadialScreenData();
        RefreshDashboardStats();
        _isInitializingUi = false;

        // Check if launched from Windows Explorer context menu with a file path argument
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && (File.Exists(args[1]) || Directory.Exists(args[1])))
        {
            GetCursorPos(out var pt);
            _radialWindow?.ShowAt(new Point(pt.X, pt.Y), args[1]);
        }
    }

    private void InitializeSubsystems()
    {
        _radialWindow = new RadialOverlayWindow();
        _radialWindow.OpenSettingsRequested += () => Dispatcher.Invoke(() =>
        {
            Show();
            Activate();
            NavSettings.IsChecked = true;
        });

        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        _hotkeyHelper = new HotkeyHelper(helper.Handle);
        _hotkeyHelper.HotkeyPressed += OnGlobalHotkeyPressed;
        _hotkeyHelper.GameModeHotkeyPressed += OnGameModeHotkeyPressed;

        _gestureDetector = new MouseGestureDetector();
        _gestureDetector.ShakeDetected += OnMouseGestureTriggered;
        _gestureDetector.MiddleClickDetected += OnMouseGestureTriggered;
        _gestureDetector.Start();

        var settings = SettingsService.Instance.Current;
        if (string.IsNullOrWhiteSpace(QuickFolderInput.Text))
        {
            QuickFolderInput.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        RegisterConfiguredHotkeys();
        FolderWatcherService.Instance.Start();

        SettingsService.Instance.SettingsChanged += s => Dispatcher.Invoke(() =>
        {
            RefreshDashboardStats();
            LoadRadialScreenData();
            LoadFoldersData();
        });

        LoadFoldersData();
    }

    private void SyncUiFromSettings()
    {
        var s = SettingsService.Instance.Current;

        CheckPauseGestures.IsChecked = s.IsGameModeEnabled;
        ToggleShakeGesture.IsChecked = s.EnableDragShakeTrigger;
        ToggleMiddleMouse.IsChecked = s.EnableMiddleMouseTrigger;
        ToggleAutostart.IsChecked = s.EnableAutostart;
        ToggleExplorerMenu.IsChecked = s.EnableExplorerContextMenu || WindowsIntegrationHelper.IsExplorerContextMenuEnabled();
        ToggleAutoHide.IsChecked = s.AutoHideOnAction;
        ToggleSmoothAnim.IsChecked = s.SmoothAnimations;

        GeneralThemeCombo.SelectedIndex = s.Theme switch
        {
            "Indigo" => 1,
            "Light" => 2,
            _ => 0
        };

        LanguageCombo.SelectedIndex = s.Language switch
        {
            "en" => 1,
            "pl" => 2,
            _ => 0
        };

        ApplyTheme(s.Theme);
        ApplyLanguage(s.Language);
        UpdateGameModeVisualState();
    }

    private void RegisterConfiguredHotkeys()
    {
        if (_hotkeyHelper == null) return;
        var s = SettingsService.Instance.Current;

        // Game mode hotkey is ALWAYS registered so the user can toggle Game Mode on/off anytime!
        _hotkeyHelper.RegisterGameMode(s.GameModeHotkey);
        if (GameModeHotkeyStatusText != null) GameModeHotkeyStatusText.Text = s.GameModeHotkey;

        if (s.IsGameModeEnabled)
        {
            _hotkeyHelper.UnregisterMain();
        }
        else
        {
            _hotkeyHelper.Register(s.GlobalHotkey);
        }

        if (StatGlobalHotkey != null) StatGlobalHotkey.Text = s.GlobalHotkey;
        if (HotkeyRecorderStatusText != null) HotkeyRecorderStatusText.Text = s.GlobalHotkey;
        UpdateGameModeVisualState();
    }

    private void UpdateGameModeVisualState()
    {
        var s = SettingsService.Instance.Current;
        if (_gestureDetector != null)
        {
            _gestureDetector.IsPaused = s.IsGameModeEnabled;
        }

        if (s.IsGameModeEnabled)
        {
            if (SidebarHotkeyBadge != null) SidebarHotkeyBadge.Text = "Пауза (Ігри)";
            if (TxtSidebarServiceStatus != null) TxtSidebarServiceStatus.Text = s.Language == "en" ? "Game Mode (Paused)" : (s.Language == "pl" ? "Tryb gry (Pauza)" : "Ігровий режим");
            if (SidebarStatusDot != null) SidebarStatusDot.Fill = (Brush)FindResource("OrbitRoseBrush");
        }
        else
        {
            if (SidebarHotkeyBadge != null) SidebarHotkeyBadge.Text = s.GlobalHotkey;
            if (TxtSidebarServiceStatus != null) TxtSidebarServiceStatus.Text = s.Language == "en" ? "Service active" : (s.Language == "pl" ? "Usługa aktywna" : "Служба активна");
            if (SidebarStatusDot != null) SidebarStatusDot.Fill = (Brush)FindResource("OrbitGreenLightBrush");
        }
    }

    private void RefreshDashboardStats()
    {
        var settings = SettingsService.Instance.Current;
        int activeActions = settings.Actions.Count(a => a.IsEnabled && a.Id != "system_shelf_add");
        int activeFolders = settings.WatchedFolders.Count(w => w.IsEnabled);

        if (StatActiveActions != null) StatActiveActions.Text = activeActions.ToString();
        if (StatGlobalHotkey != null) StatGlobalHotkey.Text = settings.GlobalHotkey;
        if (StatWatchedFolders != null) StatWatchedFolders.Text = activeFolders.ToString();
        if (StatCurrentTheme != null)
        {
            StatCurrentTheme.Text = settings.Theme switch
            {
                "Indigo" => "Indigo",
                "Light" => "Light",
                _ => "Orbit"
            };
        }
        if (HotkeyRecorderStatusText != null) HotkeyRecorderStatusText.Text = settings.GlobalHotkey;
        if (GameModeHotkeyStatusText != null) GameModeHotkeyStatusText.Text = settings.GameModeHotkey;
        UpdateGameModeVisualState();
    }

    private void OnGlobalHotkeyPressed()
    {
        if (SettingsService.Instance.Current.IsGameModeEnabled) return;

        GetCursorPos(out var pt);
        var screenPoint = new Point(pt.X, pt.Y);

        var selected = ExplorerHelper.GetSelectedFiles();
        string? targetFile = selected.Count > 0 ? selected[0] : null;

        _radialWindow?.ShowAt(screenPoint, targetFile);
    }

    private void OnGameModeHotkeyPressed()
    {
        var s = SettingsService.Instance.Current;
        s.IsGameModeEnabled = !s.IsGameModeEnabled;
        CheckPauseGestures.IsChecked = s.IsGameModeEnabled;
        SettingsService.Instance.SaveSettings(s);
        RegisterConfiguredHotkeys();
    }

    private void OnMouseGestureTriggered(Point screenPoint)
    {
        if (SettingsService.Instance.Current.IsGameModeEnabled) return;

        // Avoid triggering middle-click when clicking inside our own settings window
        if (IsActive && IsMouseOver) return;

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
    // SIDEBAR NAVIGATION & PAGE TRANSITION ANIMATION
    // ==========================================
    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (ScreenMain == null || ScreenRadial == null || ScreenInput == null || ScreenFolders == null || ScreenSettings == null) return;

        ScreenMain.Visibility = Visibility.Collapsed;
        ScreenRadial.Visibility = Visibility.Collapsed;
        ScreenInput.Visibility = Visibility.Collapsed;
        ScreenFolders.Visibility = Visibility.Collapsed;
        ScreenSettings.Visibility = Visibility.Collapsed;

        var lang = SettingsService.Instance.Current.Language;
        Grid? activeScreen = null;

        if (sender == NavMain)
        {
            activeScreen = ScreenMain;
            PageHeaderTitle.Text = GetLoc(lang, "Головна", "Dashboard", "Główna");
            PageHeaderSubtitle.Text = GetLoc(lang,
                "Огляд активних інструментів, швидких дій та стану робочого простору",
                "Overview of active tools, quick actions, and workspace status",
                "Przegląd aktywnych narzędzi, szybkich akcji i stanu obszaru roboczego");
            RefreshDashboardStats();
        }
        else if (sender == NavRadial)
        {
            activeScreen = ScreenRadial;
            PageHeaderTitle.Text = GetLoc(lang, "Кругові меню", "Radial Menus", "Menu kołowe");
            PageHeaderSubtitle.Text = GetLoc(lang,
                "Налаштування секторів радіального диска для кожного типу файлів",
                "Customize radial disc sectors for each file category",
                "Konfiguracja sektorów menu kołowego dla każdego typu plików");
            LoadRadialScreenData();
        }
        else if (sender == NavInput)
        {
            activeScreen = ScreenInput;
            PageHeaderTitle.Text = GetLoc(lang, "Жести й клавіші", "Gestures & Keys", "Gesty i skróty");
            PageHeaderSubtitle.Text = GetLoc(lang,
                "Призначення комбінацій виклику, ігрового режиму та жестів миші",
                "Configure global hotkeys, Game Mode shortcut, and mouse gestures",
                "Przypisywanie skrótów klawiszowych, trybu gry oraz gestów myszy");
        }
        else if (sender == NavFolders)
        {
            activeScreen = ScreenFolders;
            PageHeaderTitle.Text = GetLoc(lang, "Відстеження папок", "Watched Folders", "Śledzenie folderów");
            PageHeaderSubtitle.Text = GetLoc(lang,
                "Автоматична фонова обробка та сортування нових файлів у вибраних каталогах",
                "Automatic background processing and sorting of new files in watched directories",
                "Automatyczne przetwarzanie i sortowanie nowych plików w wybranych folderach");
            LoadFoldersData();
        }
        else if (sender == NavSettings)
        {
            activeScreen = ScreenSettings;
            PageHeaderTitle.Text = GetLoc(lang, "Налаштування", "Settings", "Ustawienia");
            PageHeaderSubtitle.Text = GetLoc(lang,
                "Параметри мови, візуальної теми Orbit та інтеграції з Провідником Windows",
                "Language preferences, Orbit visual themes, and Windows Explorer integration",
                "Ustawienia języka, motywu wizualnego Orbit oraz integracji z Eksploratorem Windows");
        }

        if (activeScreen != null)
        {
            activeScreen.Visibility = Visibility.Visible;
            PlayPageTransition(activeScreen);
        }
    }

    private void PlayPageTransition(Grid targetScreen)
    {
        if (!SettingsService.Instance.Current.SmoothAnimations) return;

        var fade = new DoubleAnimation(0.15, 1.0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        targetScreen.BeginAnimation(OpacityProperty, fade);

        if (targetScreen.RenderTransform is TranslateTransform trans)
        {
            var slide = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            trans.BeginAnimation(TranslateTransform.YProperty, slide);
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

    private void BtnJumpToFolders_Click(object sender, MouseButtonEventArgs e)
    {
        NavFolders.IsChecked = true;
    }

    private void BtnJumpToSettings_Click(object sender, MouseButtonEventArgs e)
    {
        NavSettings.IsChecked = true;
    }

    // ==========================================
    // SCREEN 1: ГОЛОВНА (ДАШБОРД ORBIT)
    // ==========================================
    private void BtnOpenHaloHere_Click(object sender, RoutedEventArgs e)
    {
        GetCursorPos(out var pt);
        _radialWindow?.ShowAt(new Point(pt.X, pt.Y), null);
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
            .Where(a => a.Id != "system_shelf_add" && (a.SupportedFileTypes.Contains(catEnum) || a.SupportedFileTypes.Contains(FileTypeCategory.Generic)))
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
        var validActions = settings.Actions.Where(a => a.IsEnabled && a.Id != "system_shelf_add").ToList();

        int imgActive = validActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Image));
        int docActive = validActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Document));
        int vidActive = validActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Video));
        int audActive = validActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Audio));
        int genActive = validActions.Count(a => a.SupportedFileTypes.Contains(FileTypeCategory.Generic));

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
    // SCREEN 3: ЖЕСТИ Й КЛАВІШІ (MAIN + GAME MODE KEY RECORDER + RESET)
    // ==========================================
    private void CheckPauseGestures_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsService.Instance.Current;
        s.IsGameModeEnabled = CheckPauseGestures.IsChecked ?? false;
        SettingsService.Instance.SaveSettings(s);
        RegisterConfiguredHotkeys();
    }

    private void ToggleShakeGesture_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsService.Instance.Current;
        s.EnableDragShakeTrigger = ToggleShakeGesture.IsChecked ?? true;
        SettingsService.Instance.SaveSettings(s);
    }

    private void ToggleMiddleMouse_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsService.Instance.Current;
        s.EnableMiddleMouseTrigger = ToggleMiddleMouse.IsChecked ?? true;
        SettingsService.Instance.SaveSettings(s);
    }

    private void BtnRecordHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_recordTarget == HotkeyRecordTarget.MainTrigger)
        {
            StopRecordingHotkey();
            return;
        }

        StopRecordingHotkey();
        _recordTarget = HotkeyRecordTarget.MainTrigger;
        HotkeyRecorderStatusText.Text = "Натисніть клавіші...";
        HotkeyRecorderHint.Text = "Затисніть комбінацію на клавіатурі (напр. Alt+Space або Ctrl+Shift+H). Esc — скасувати.";
        BtnRecordHotkey.Content = "Скасувати запис";
        HotkeyRecorderCard.BorderBrush = (Brush)FindResource("OrbitPurpleBrush");
    }

    private void BtnRecordGameModeHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_recordTarget == HotkeyRecordTarget.GameModeTrigger)
        {
            StopRecordingHotkey();
            return;
        }

        StopRecordingHotkey();
        _recordTarget = HotkeyRecordTarget.GameModeTrigger;
        GameModeHotkeyStatusText.Text = "Натисніть клавіші...";
        GameModeRecorderHint.Text = "Затисніть комбінацію для швидкого ввімкнення Ігрового режиму (напр. Alt+G). Esc — скасувати.";
        BtnRecordGameModeHotkey.Content = "Скасувати запис";
        GameModeRecorderCard.BorderBrush = (Brush)FindResource("OrbitRoseBrush");
    }

    private void StopRecordingHotkey()
    {
        _recordTarget = HotkeyRecordTarget.None;
        HotkeyRecorderHint.Text = "Натисніть кнопку запису нижче, а потім затисніть бажані клавіші на клавіатурі.";
        BtnRecordHotkey.Content = "⏺️  Записати комбінацію з клавіатури";
        HotkeyRecorderCard.BorderBrush = (Brush)FindResource("OrbitCardBorderBrush");

        GameModeRecorderHint.Text = "Ви можете вмикати та вимикати Ігровий режим будь-коли гарячою комбінацією клавіш:";
        BtnRecordGameModeHotkey.Content = "⏺️  Записати хоткей ігрового режиму";
        GameModeRecorderCard.BorderBrush = (Brush)FindResource("OrbitCardBorderBrush");

        RegisterConfiguredHotkeys();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recordTarget == HotkeyRecordTarget.None) return;

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
            e.Key == Key.System && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt))
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
        if (_recordTarget == HotkeyRecordTarget.MainTrigger)
        {
            settings.GlobalHotkey = newHotkey;
        }
        else if (_recordTarget == HotkeyRecordTarget.GameModeTrigger)
        {
            settings.GameModeHotkey = newHotkey;
        }

        SettingsService.Instance.SaveSettings(settings);
        StopRecordingHotkey();
        RefreshDashboardStats();
    }

    private void BtnResetTriggers_Click(object sender, RoutedEventArgs e)
    {
        StopRecordingHotkey();
        var s = SettingsService.Instance.Current;
        s.GlobalHotkey = "Alt+Space";
        s.GameModeHotkey = "Alt+G";
        s.IsGameModeEnabled = false;
        s.EnableDragShakeTrigger = true;
        s.EnableMiddleMouseTrigger = true;

        CheckPauseGestures.IsChecked = false;
        ToggleShakeGesture.IsChecked = true;
        ToggleMiddleMouse.IsChecked = true;

        SettingsService.Instance.SaveSettings(s);
        RegisterConfiguredHotkeys();
        RefreshDashboardStats();
    }

    // ==========================================
    // SCREEN 4: ВІДСТЕЖЕННЯ ПАПОК
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
            MessageBox.Show("Будь ласка, вкажіть дійсну існуючу папку на диску (натисніть «Огляд папки...»).", "Halo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var actionTag = (QuickActionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "folder_auto_sort";

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

    private void BtnRunFolderRuleNow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchedFolderConfig rule)
        {
            int count = FolderWatcherService.Instance.ProcessExistingFilesInFolder(rule);
            MessageBox.Show($"Перевірку папки «{rule.FolderPath}» завершено.\nОброблено файлів: {count}.", "Відстеження папок Halo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
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
    // SCREEN 5: НАЛАШТУВАННЯ (МОВА, ТЕМИ, ПРОВІДНИК)
    // ==========================================
    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingUi || LanguageCombo == null) return;
        string lang = LanguageCombo.SelectedIndex switch
        {
            1 => "en",
            2 => "pl",
            _ => "uk"
        };

        var s = SettingsService.Instance.Current;
        s.Language = lang;
        SettingsService.Instance.SaveSettings(s);
        ApplyLanguage(lang);
    }

    private void GeneralThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingUi || GeneralThemeCombo == null) return;
        string theme = GeneralThemeCombo.SelectedIndex switch
        {
            1 => "Indigo",
            2 => "Light",
            _ => "Dark"
        };

        var s = SettingsService.Instance.Current;
        s.Theme = theme;
        SettingsService.Instance.SaveSettings(s);
        ApplyTheme(theme);
        RefreshDashboardStats();
    }

    private void ToggleAutostart_Click(object sender, RoutedEventArgs e)
    {
        bool enable = ToggleAutostart.IsChecked ?? false;
        WindowsIntegrationHelper.SetAutostart(enable);

        var s = SettingsService.Instance.Current;
        s.EnableAutostart = enable;
        SettingsService.Instance.SaveSettings(s);
    }

    private void ToggleExplorerMenu_Click(object sender, RoutedEventArgs e)
    {
        bool enable = ToggleExplorerMenu.IsChecked ?? false;
        var lang = SettingsService.Instance.Current.Language;
        string menuTitle = GetLoc(lang, "Відкрити у Halo", "Open with Halo", "Otwórz w Halo");

        bool ok = WindowsIntegrationHelper.SetExplorerContextMenu(enable, menuTitle);
        var s = SettingsService.Instance.Current;
        s.EnableExplorerContextMenu = enable && ok;
        SettingsService.Instance.SaveSettings(s);
    }

    private void ToggleAutoHide_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsService.Instance.Current;
        s.AutoHideOnAction = ToggleAutoHide.IsChecked ?? true;
        SettingsService.Instance.SaveSettings(s);
    }

    private void ToggleSmoothAnim_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsService.Instance.Current;
        s.SmoothAnimations = ToggleSmoothAnim.IsChecked ?? true;
        SettingsService.Instance.SaveSettings(s);
    }

    private void ApplyTheme(string theme)
    {
        var res = Application.Current.Resources;
        if (theme == "Light")
        {
            res["OrbitWindowBgBrush"] = new SolidColorBrush(Color.FromRgb(243, 244, 248));
            res["OrbitSidebarBgBrush"] = new SolidColorBrush(Color.FromRgb(233, 235, 242));
            res["OrbitCardBgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            res["OrbitCardHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(237, 235, 252));
            res["OrbitRecessedBgBrush"] = new SolidColorBrush(Color.FromRgb(238, 240, 246));
            res["OrbitSidebarDividerBrush"] = new SolidColorBrush(Color.FromRgb(214, 217, 228));
            res["OrbitCardBorderBrush"] = new SolidColorBrush(Color.FromRgb(216, 219, 230));
            res["OrbitSubtleBorderBrush"] = new SolidColorBrush(Color.FromRgb(220, 223, 234));
            res["OrbitTextPrimary"] = new SolidColorBrush(Color.FromRgb(24, 25, 36));
            res["OrbitTextSecondary"] = new SolidColorBrush(Color.FromRgb(82, 86, 106));
            res["OrbitTextMuted"] = new SolidColorBrush(Color.FromRgb(122, 126, 146));
        }
        else if (theme == "Indigo")
        {
            res["OrbitWindowBgBrush"] = new SolidColorBrush(Color.FromRgb(15, 20, 34));
            res["OrbitSidebarBgBrush"] = new SolidColorBrush(Color.FromRgb(11, 15, 26));
            res["OrbitCardBgBrush"] = new SolidColorBrush(Color.FromRgb(24, 32, 52));
            res["OrbitCardHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(32, 43, 68));
            res["OrbitRecessedBgBrush"] = new SolidColorBrush(Color.FromRgb(13, 18, 30));
            res["OrbitSidebarDividerBrush"] = new SolidColorBrush(Color.FromRgb(28, 38, 60));
            res["OrbitCardBorderBrush"] = new SolidColorBrush(Color.FromRgb(42, 56, 88));
            res["OrbitSubtleBorderBrush"] = new SolidColorBrush(Color.FromRgb(32, 44, 70));
            res["OrbitTextPrimary"] = new SolidColorBrush(Color.FromRgb(242, 245, 252));
            res["OrbitTextSecondary"] = new SolidColorBrush(Color.FromRgb(154, 168, 198));
            res["OrbitTextMuted"] = new SolidColorBrush(Color.FromRgb(104, 118, 150));
        }
        else // Default "Dark" Orbit
        {
            res["OrbitWindowBgBrush"] = new SolidColorBrush(Color.FromRgb(30, 30, 36));
            res["OrbitSidebarBgBrush"] = new SolidColorBrush(Color.FromRgb(23, 23, 28));
            res["OrbitCardBgBrush"] = new SolidColorBrush(Color.FromRgb(40, 40, 50));
            res["OrbitCardHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(47, 47, 61));
            res["OrbitRecessedBgBrush"] = new SolidColorBrush(Color.FromRgb(27, 27, 33));
            res["OrbitSidebarDividerBrush"] = new SolidColorBrush(Color.FromRgb(38, 38, 48));
            res["OrbitCardBorderBrush"] = new SolidColorBrush(Color.FromRgb(53, 53, 69));
            res["OrbitSubtleBorderBrush"] = new SolidColorBrush(Color.FromRgb(44, 44, 56));
            res["OrbitTextPrimary"] = new SolidColorBrush(Color.FromRgb(242, 243, 247));
            res["OrbitTextSecondary"] = new SolidColorBrush(Color.FromRgb(160, 163, 181));
            res["OrbitTextMuted"] = new SolidColorBrush(Color.FromRgb(108, 112, 133));
        }
    }

    private void ApplyLanguage(string lang)
    {
        if (TxtSidebarSubtitle == null) return;

        TxtSidebarSubtitle.Text = GetLoc(lang, "Робочий простір", "Workspace", "Przestrzeń robocza");
        TxtSidebarMenuHeader.Text = GetLoc(lang, "МЕНЮ", "MENU", "MENU");
        TxtSidebarTriggerLabel.Text = GetLoc(lang, "Тригер:", "Trigger:", "Skrót:");

        TxtNavMain.Text = GetLoc(lang, "Головна", "Dashboard", "Główna");
        TxtNavRadial.Text = GetLoc(lang, "Кругові меню", "Radial Menus", "Menu kołowe");
        TxtNavInput.Text = GetLoc(lang, "Жести й клавіші", "Gestures & Keys", "Gesty i skróty");
        TxtNavFolders.Text = GetLoc(lang, "Відстеження папок", "Watched Folders", "Śledzenie folderów");
        TxtNavSettings.Text = GetLoc(lang, "Налаштування", "Settings", "Ustawienia");

        BtnHeaderOpenHalo.Content = GetLoc(lang, "✦  Відкрити Halo", "✦  Open Halo", "✦  Otwórz Halo");

        TxtMetric1Title.Text = GetLoc(lang, "Активні дії", "Active Actions", "Aktywne akcje");
        TxtMetric1Sub.Text = GetLoc(lang, "Налаштувати сектори →", "Configure sectors →", "Konfiguruj sektory →");
        TxtMetric2Title.Text = GetLoc(lang, "Швидкий виклик", "Quick Trigger", "Szybkie wywołanie");
        TxtMetric2Sub.Text = GetLoc(lang, "Жести та клавіші →", "Gestures & keys →", "Gesty i skróty →");
        TxtMetric3Title.Text = GetLoc(lang, "Автоматизація", "Automation", "Automatyzacja");
        TxtMetric3Sub.Text = GetLoc(lang, "Відстеження папок →", "Watched folders →", "Śledzenie folderów →");
        TxtMetric4Title.Text = GetLoc(lang, "Тема та система", "Theme & System", "Motyw i system");
        TxtMetric4Sub.Text = GetLoc(lang, "Відкрити налаштування →", "Open settings →", "Otwórz ustawienia →");

        TxtHubBadge.Text = GetLoc(lang, "✦  ІНТЕРАКТИВНИЙ ХАБ", "✦  INTERACTIVE HUB", "✦  CENTRUM INTERAKTYWNE");
        TxtHubTitle.Text = GetLoc(lang, "Миттєва робота з будь-яким файлом", "Instant File Action Hub", "Błyskawiczna praca z plikami");
        TxtHubDesc.Text = GetLoc(lang,
            "Перетягніть сюди зображення, відео чи документ або оберіть файл з диска, щоб відкрити радіальне меню дій.",
            "Drag and drop an image, video, or document here, or pick a file from disk to open the radial action menu.",
            "Przeciągnij tutaj obraz, wideo lub dokument albo wybierz plik z dysku, aby otworzyć kołowe menu akcji.");
        BtnHubPickFile.Content = GetLoc(lang, "📂  Вибрати файл...", "📂  Select File...", "📂  Wybierz plik...");
        BtnHubTestMenu.Content = GetLoc(lang, "◎  Тест кругового меню", "◎  Test Radial Menu", "◎  Testuj menu kołowe");

        TxtProfilesTitle.Text = GetLoc(lang, "Профілі кругового меню", "Radial Menu Profiles", "Profile menu kołowego");
        TxtProfilesSub.Text = GetLoc(lang, "Наповнення секторів за типами файлів", "Sector configuration by file type", "Zawartość sektorów według typu pliku");
        BtnConfigureSectors.Content = GetLoc(lang, "Налаштувати сектори меню →", "Customize Menu Sectors →", "Konfiguruj sektory menu →");

        TxtSettingsLangTitle.Text = GetLoc(lang, "Мова інтерфейсу", "Interface Language", "Język interfejsu");
        TxtSettingsLangSub.Text = GetLoc(lang, "Миттєва зміна мови всіх меню та підписів", "Instantly switch the language of all menus and labels", "Natychmiastowa zmiana języka wszystkich menu");
        TxtSettingsThemeTitle.Text = GetLoc(lang, "Кольорова тема Halo", "Halo Color Theme", "Motyw kolorystyczny Halo");
        TxtSettingsThemeSub.Text = GetLoc(lang, "Налаштування палітри вікна та карток у реальному часі", "Real-time window and card palette customization", "Dostosowanie palety okna i kart w czasie rzeczywistym");
        TxtSettingsWinTitle.Text = GetLoc(lang, "Інтеграція з Windows", "Windows Integration", "Integracja z Windows");
        TxtSettingsBehaviorTitle.Text = GetLoc(lang, "Поведінка та швидкодія", "Behavior & Performance", "Zachowanie i wydajność");

        UpdateGameModeVisualState();

        // Update current header text
        if (NavMain.IsChecked == true) Nav_Checked(NavMain, new RoutedEventArgs());
        else if (NavRadial.IsChecked == true) Nav_Checked(NavRadial, new RoutedEventArgs());
        else if (NavInput.IsChecked == true) Nav_Checked(NavInput, new RoutedEventArgs());
        else if (NavFolders.IsChecked == true) Nav_Checked(NavFolders, new RoutedEventArgs());
        else if (NavSettings.IsChecked == true) Nav_Checked(NavSettings, new RoutedEventArgs());
    }

    private static string GetLoc(string lang, string uk, string en, string pl)
    {
        return lang switch
        {
            "en" => en,
            "pl" => pl,
            _ => uk
        };
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _gestureDetector?.Dispose();
        _hotkeyHelper?.Dispose();
        FolderWatcherService.Instance.Stop();
        _radialWindow?.Close();
    }
}
