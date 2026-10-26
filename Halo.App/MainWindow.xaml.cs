using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Halo.App.Native;
using Halo.App.Services;
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
    private ShelfWindow? _shelfWindow;
    private HotkeyHelper? _hotkeyHelper;
    private MouseGestureDetector? _gestureDetector;
    private NativeTrayIcon? _trayIcon;
    private bool _isExplicitExit = false;
    private HotkeyRecordTarget _recordTarget = HotkeyRecordTarget.None;
    private ActionCategory _selectedRadialMode = ActionCategory.Convert;
    private bool _isInitializingUi = false;
    private string? _lastDashboardOutputPath;

    private static bool IsEn => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
    private static string T(string uk, string en) => IsEn ? en : uk;

    private Point GetLogicalCursorPosition()
    {
        GetCursorPos(out var pt);
        var raw = new Point(pt.X, pt.Y);
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            return source.CompositionTarget.TransformFromDevice.Transform(raw);
        }
        return raw;
    }

    public MainWindow()
    {
        _isInitializingUi = true;
        InitializeComponent();

        // Pre-apply theme, language, and data BEFORE the window is shown on screen to prevent any startup blink/flash
        if (string.IsNullOrWhiteSpace(QuickFolderInput.Text))
        {
            QuickFolderInput.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        SyncUiFromSettings();
        LoadRadialScreenData();
        LoadFoldersData();
        RefreshDashboardStats();
        RefreshDependenciesStatus();
        _isInitializingUi = false;

        OperationHistoryService.Instance.HistoryChanged += () => Dispatcher.Invoke(RefreshHistoryUI);
        ThemeManager.Instance.ThemeChanged += OnThemeChanged;

        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SizeChanged += (s, e) =>
        {
            RadioButton? cur = NavMain?.IsChecked == true ? NavMain :
                               NavRadial?.IsChecked == true ? NavRadial :
                               NavInput?.IsChecked == true ? NavInput :
                               NavFolders?.IsChecked == true ? NavFolders :
                               NavSettings?.IsChecked == true ? NavSettings : null;
            if (cur != null) UpdateNavIndicator(cur, animate: false);
        };
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        InitializeSubsystems();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshHistoryUI();

        RadioButton? selectedNav = NavMain?.IsChecked == true ? NavMain :
                                   NavRadial?.IsChecked == true ? NavRadial :
                                   NavInput?.IsChecked == true ? NavInput :
                                   NavFolders?.IsChecked == true ? NavFolders :
                                   NavSettings?.IsChecked == true ? NavSettings : NavMain;
        UpdateNavIndicator(selectedNav, animate: false);

        // Check if launched from Windows Explorer context menu with a file path argument
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && (File.Exists(args[1]) || Directory.Exists(args[1])))
        {
            Hide();
            var pt = GetLogicalCursorPosition();
            EnsureRadialWindow();
            _radialWindow?.ShowAt(pt, args[1]);
        }
        else if (args.Length > 1 && (args[1] == "--autostart" || args[1] == "/autostart"))
        {
            Hide();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                HaloToastWindow.ShowToast(
                    T("Halo активна", "Halo is active"),
                    T("Програма працює у фоні. Натисніть Alt+Space для виклику.", "Halo is running in background. Press Alt+Space to summon."),
                    -1,
                    4.5);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void EnsureRadialWindow() => InitializeSubsystems();

    private void InitializeSubsystems()
    {
        if (_radialWindow != null) return;

        _radialWindow = new RadialOverlayWindow();
        _radialWindow.PrewarmWindow();
        _radialWindow.OpenSettingsRequested += () => Dispatcher.Invoke(() =>
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            NavSettings.IsChecked = true;
        });
        _radialWindow.AddToShelfRequested += filePath => Dispatcher.Invoke(() =>
        {
            EnsureShelfWindow().AddFile(filePath);
        });
        _radialWindow.OpenShelfRequested += () => Dispatcher.Invoke(() =>
        {
            EnsureShelfWindow().OpenShelf();
        });

        SingleInstanceIpc.FilePathReceived += path => Dispatcher.Invoke(() =>
        {
            var pt = GetLogicalCursorPosition();
            _radialWindow?.ShowAt(pt, path);
        });

        SingleInstanceIpc.ShowMainWindowRequested += () => Dispatcher.Invoke(() =>
        {
            BringToForeground(selectMainTab: true);
        });

        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(HwndMessageHook);

        _trayIcon = new NativeTrayIcon();
        _trayIcon.Initialize(helper.Handle);
        _trayIcon.OpenMainWindowRequested += () => Dispatcher.Invoke(() =>
        {
            BringToForeground(selectMainTab: true);
        });
        _trayIcon.ToggleGameModeRequested += () => Dispatcher.Invoke(ToggleGameMode);
        _trayIcon.OpenShelfRequested += () => Dispatcher.Invoke(() =>
        {
            EnsureShelfWindow().OpenShelf();
        });
        _trayIcon.OpenSettingsRequested += () => Dispatcher.Invoke(() =>
        {
            if (NavSettings != null) NavSettings.IsChecked = true;
            BringToForeground(selectMainTab: false);
        });
        _trayIcon.ExitAppRequested += () => Dispatcher.Invoke(() =>
        {
            _isExplicitExit = true;
            Close();
        });

        _hotkeyHelper = new HotkeyHelper(helper.Handle);
        _hotkeyHelper.HotkeyPressed += OnGlobalHotkeyPressed;
        _hotkeyHelper.GameModeHotkeyPressed += OnGameModeHotkeyPressed;

        _gestureDetector = new MouseGestureDetector();
        _gestureDetector.ShakeDetected += OnShakeGestureTriggered;
        _gestureDetector.MiddleClickDetected += OnMiddleClickGestureTriggered;
        _gestureDetector.Start();

        RegisterConfiguredHotkeys();
        FolderWatcherService.Instance.Start();

        SettingsService.Instance.SettingsChanged += s => Dispatcher.Invoke(() =>
        {
            RefreshDashboardStats();
            LoadRadialScreenData();
            LoadFoldersData();
        });
    }

    private ShelfWindow EnsureShelfWindow()
    {
        if (_shelfWindow == null || !_shelfWindow.IsLoaded)
        {
            _shelfWindow = new ShelfWindow();
            _shelfWindow.OpenInRadialRequested += paths => Dispatcher.Invoke(() =>
            {
                var pt = GetLogicalCursorPosition();
                _radialWindow?.ShowAt(pt, paths);
            });
        }
        return _shelfWindow;
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

        LanguageCombo.SelectedIndex = string.Equals(s.Language, "en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        ApplyTheme(s.Theme);
        ApplyLanguage(s.Language);
        UpdateGameModeVisualState();
    }

    private void RegisterConfiguredHotkeys()
    {
        if (_hotkeyHelper == null) return;
        var s = SettingsService.Instance.Current;

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
            if (SidebarHotkeyBadge != null)
            {
                SidebarHotkeyBadge.Text = T("Пауза (Ігри)", "Paused (Game)");
                SidebarHotkeyBadge.Foreground = (Brush)FindResource("OrbitRoseLightBrush");
            }
            if (SidebarTriggerBadgeBorder != null)
            {
                SidebarTriggerBadgeBorder.Background = (Brush)FindResource("OrbitRoseSoftBg");
            }
            if (TxtSidebarServiceStatus != null) TxtSidebarServiceStatus.Text = T("Ігровий режим", "Game Mode (Paused)");
            if (SidebarStatusDot != null) SidebarStatusDot.Fill = (Brush)FindResource("OrbitRoseBrush");

            // Game Mode active state
            if (CheckPauseGestures != null) CheckPauseGestures.IsChecked = true;
            if (MainWindowGameModeIconBox != null)
            {
                MainWindowGameModeIconBox.Background = (Brush)FindResource("OrbitRoseSoftBg");
                MainWindowGameModeIconBox.BorderThickness = new Thickness(0);
            }
            if (TxtMainWindowGameEmoji != null)
            {
                TxtMainWindowGameEmoji.Foreground = (Brush)FindResource("OrbitRoseLightBrush");
                TxtMainWindowGameEmoji.Opacity = 1.0;
            }
            if (TxtInputGameModeTitle != null) TxtInputGameModeTitle.Foreground = (Brush)FindResource("OrbitRoseLightBrush");
        }
        else
        {
            if (SidebarHotkeyBadge != null)
            {
                SidebarHotkeyBadge.Text = s.GlobalHotkey;
                SidebarHotkeyBadge.Foreground = (Brush)FindResource("OrbitPurpleLightBrush");
            }
            if (SidebarTriggerBadgeBorder != null)
            {
                SidebarTriggerBadgeBorder.Background = (Brush)FindResource("OrbitPurpleSoftBg");
            }
            if (TxtSidebarServiceStatus != null) TxtSidebarServiceStatus.Text = T("Служба активна", "Service active");
            if (SidebarStatusDot != null) SidebarStatusDot.Fill = (Brush)FindResource("OrbitGreenLightBrush");

            // Game Mode inactive state (rose-accented subtle and readable, not plain white)
            if (CheckPauseGestures != null) CheckPauseGestures.IsChecked = false;
            if (MainWindowGameModeIconBox != null)
            {
                MainWindowGameModeIconBox.Background = (Brush)FindResource("OrbitRoseSoftBg");
                MainWindowGameModeIconBox.BorderThickness = new Thickness(0);
            }
            if (TxtMainWindowGameEmoji != null)
            {
                TxtMainWindowGameEmoji.Foreground = (Brush)FindResource("OrbitRoseLightBrush");
                TxtMainWindowGameEmoji.Opacity = 0.75;
            }
            if (TxtInputGameModeTitle != null) TxtInputGameModeTitle.Foreground = (Brush)FindResource("OrbitTextPrimary");
        }

        _trayIcon?.UpdateStatus(s.IsGameModeEnabled);
    }

    private IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeTrayIcon.WM_TRAY_CALLBACK)
        {
            _trayIcon?.HandleTrayMessage(lParam.ToInt32());
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void RefreshDashboardStats()
    {
        var settings = SettingsService.Instance.Current;
        int activeActions = settings.Actions.Count(a => a.IsEnabled);
        int activeFolders = settings.WatchedFolders.Count(w => w.IsEnabled);

        if (StatActiveActions != null) StatActiveActions.Text = activeActions.ToString();
        if (StatGlobalHotkey != null) StatGlobalHotkey.Text = settings.GlobalHotkey;
        if (StatWatchedFolders != null) StatWatchedFolders.Text = activeFolders.ToString();
        if (StatCurrentTheme != null)
        {
            StatCurrentTheme.Text = settings.Theme switch
            {
                "Indigo" => T("Глибоко-синя", "Deep Indigo"),
                "Light" => T("Світла", "Light"),
                _ => T("Темна", "Dark")
            };
        }
        if (HotkeyRecorderStatusText != null) HotkeyRecorderStatusText.Text = settings.GlobalHotkey;
        if (GameModeHotkeyStatusText != null) GameModeHotkeyStatusText.Text = settings.GameModeHotkey;
        UpdateGameModeVisualState();
    }

    private void OnGlobalHotkeyPressed()
    {
        if (SettingsService.Instance.Current.IsGameModeEnabled) return;

        var screenPoint = GetLogicalCursorPosition();
        var selected = ExplorerHelper.GetSelectedFiles();
        _radialWindow?.ShowAt(screenPoint, selected);
    }

    private void OnGameModeHotkeyPressed()
    {
        ToggleGameMode();
    }

    private void ToggleGameMode()
    {
        var s = SettingsService.Instance.Current;
        s.IsGameModeEnabled = !s.IsGameModeEnabled;
        if (CheckPauseGestures != null) CheckPauseGestures.IsChecked = s.IsGameModeEnabled;
        SettingsService.Instance.SaveSettings(s);
        RegisterConfiguredHotkeys();
    }

    private void OnShakeGestureTriggered(Point screenPoint)
    {
        if (SettingsService.Instance.Current.IsGameModeEnabled) return;
        if (IsActive && IsMouseOver) return;
        if (_radialWindow != null && _radialWindow.IsVisible) return;

        var selected = ExplorerHelper.GetSelectedFiles();
        if (selected.Count == 0) return;

        _radialWindow?.ShowAt(screenPoint, selected);
    }

    private void OnMiddleClickGestureTriggered(Point screenPoint)
    {
        if (SettingsService.Instance.Current.IsGameModeEnabled) return;
        if (IsActive && IsMouseOver) return;

        // When summoned via middle mouse click, do not lock/bind to background open folder or directory.
        // Only bind if an actual concrete regular file is selected; otherwise open in clean universal mode!
        var selected = ExplorerHelper.GetSelectedFiles();
        var filesOnly = selected.Where(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f) && !Directory.Exists(f)).ToList();
        _radialWindow?.ShowAt(screenPoint, filesOnly);
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

    private void BtnOpenShelf_Click(object sender, RoutedEventArgs e)
    {
        EnsureShelfWindow().OpenShelf();
    }

    // ==========================================
    // SIDEBAR NAVIGATION & PAGE TRANSITION ANIMATION
    // ==========================================
    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (ScreenMain == null || ScreenRadial == null || ScreenInput == null || ScreenFolders == null || ScreenSettings == null) return;

        var lang = SettingsService.Instance.Current.Language;
        Grid? activeScreen = null;

        if (sender == NavMain)
        {
            activeScreen = ScreenMain;
            PageHeaderTitle.Text = GetLoc(lang, "Головна", "Dashboard");
            PageHeaderSubtitle.Text = GetLoc(lang,
                "Огляд активних інструментів, швидких дій та стану робочого простору",
                "Overview of active tools, quick actions, and workspace status");
            RefreshDashboardStats();
        }
        else if (sender == NavRadial)
        {
            activeScreen = ScreenRadial;
            PageHeaderTitle.Text = GetLoc(lang, "Кругові меню", "Radial Menus");
            PageHeaderSubtitle.Text = GetLoc(lang,
                "Налаштування секторів радіального диска для кожного типу файлів",
                "Customize radial disc sectors for each file category");
            LoadRadialScreenData();
        }
        else if (sender == NavInput)
        {
            activeScreen = ScreenInput;
            PageHeaderTitle.Text = GetLoc(lang, "Жести й клавіші", "Gestures & Keys");
            PageHeaderSubtitle.Text = GetLoc(lang,
                "Призначення комбінацій виклику, ігрового режиму та жестів миші",
                "Configure global hotkeys, Game Mode shortcut, and mouse gestures");
        }
        else if (sender == NavFolders)
        {
            activeScreen = ScreenFolders;
            PageHeaderTitle.Text = GetLoc(lang, "Відстеження папок", "Watched Folders");
            PageHeaderSubtitle.Text = GetLoc(lang,
                "Автоматична фонова обробка та сортування нових файлів у вибраних каталогах",
                "Automatic background processing and sorting of new files in watched directories");
            LoadFoldersData();
        }
        else if (sender == NavSettings)
        {
            activeScreen = ScreenSettings;
            PageHeaderTitle.Text = GetLoc(lang, "Налаштування", "Settings");
            PageHeaderSubtitle.Text = GetLoc(lang,
                "Параметри мови, візуальної теми, додаткових програм та інтеграції з Провідником Windows",
                "Language preferences, visual themes, external dependencies, and Windows Explorer integration");
            RefreshDependenciesStatus();
        }

        if (activeScreen != null)
        {
            bool wasAlreadyVisible = activeScreen.Visibility == Visibility.Visible;

            if (activeScreen != ScreenMain) ScreenMain.Visibility = Visibility.Collapsed;
            if (activeScreen != ScreenRadial) ScreenRadial.Visibility = Visibility.Collapsed;
            if (activeScreen != ScreenInput) ScreenInput.Visibility = Visibility.Collapsed;
            if (activeScreen != ScreenFolders) ScreenFolders.Visibility = Visibility.Collapsed;
            if (activeScreen != ScreenSettings) ScreenSettings.Visibility = Visibility.Collapsed;

            activeScreen.Visibility = Visibility.Visible;

            if (!wasAlreadyVisible && !_isInitializingUi && IsLoaded)
            {
                PlayLightPageTransition(activeScreen);
            }
        }

        UpdateNavIndicator(sender as RadioButton, animate: !_isInitializingUi && IsLoaded);
    }

    public void BringToForeground(bool selectMainTab = false)
    {
        if (selectMainTab && NavMain != null)
        {
            NavMain.IsChecked = true;
        }
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Topmost = true;
        Activate();
        Focus();
        Topmost = false;

        RadioButton? selectedNav = NavMain?.IsChecked == true ? NavMain :
                                   NavRadial?.IsChecked == true ? NavRadial :
                                   NavInput?.IsChecked == true ? NavInput :
                                   NavFolders?.IsChecked == true ? NavFolders :
                                   NavSettings?.IsChecked == true ? NavSettings : null;
        if (selectedNav != null) UpdateNavIndicator(selectedNav, animate: false);
    }

    private void PlayLightPageTransition(Grid targetScreen)
    {
        targetScreen.BeginAnimation(OpacityProperty, null);

        if (!SettingsService.Instance.Current.SmoothAnimations || SettingsService.Instance.Current.IsGameModeEnabled)
        {
            targetScreen.Opacity = 1.0;
            return;
        }

        var fadeIn = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(110))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Timeline.SetDesiredFrameRate(fadeIn, 30);
        targetScreen.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void UpdateNavIndicator(RadioButton? button, bool animate)
    {
        if (button == null || NavMenuContainer == null || NavIndicatorPill == null || NavIndicatorTransform == null)
            return;

        if (!button.IsLoaded || button.ActualHeight <= 0)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                UpdateNavIndicator(button, animate: false);
            }));
            return;
        }

        try
        {
            Point pt = button.TranslatePoint(new Point(0, 0), NavMenuContainer);
            double targetY = pt.Y;
            double targetHeight = button.ActualHeight;

            bool smooth = SettingsService.Instance.Current.SmoothAnimations
                          && !SettingsService.Instance.Current.IsGameModeEnabled
                          && animate
                          && NavIndicatorPill.Opacity > 0.5;

            NavIndicatorTransform.BeginAnimation(TranslateTransform.YProperty, null);
            NavIndicatorPill.BeginAnimation(HeightProperty, null);
            NavIndicatorPill.BeginAnimation(OpacityProperty, null);

            if (!smooth)
            {
                NavIndicatorTransform.Y = targetY;
                NavIndicatorPill.Height = targetHeight;
                NavIndicatorPill.Opacity = 1.0;
            }
            else
            {
                var animY = new DoubleAnimation(targetY, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Timeline.SetDesiredFrameRate(animY, 60);

                var animH = new DoubleAnimation(targetHeight, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Timeline.SetDesiredFrameRate(animH, 60);

                if (NavIndicatorPill.Opacity < 1.0)
                {
                    var animO = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120));
                    Timeline.SetDesiredFrameRate(animO, 60);
                    NavIndicatorPill.BeginAnimation(OpacityProperty, animO);
                }

                NavIndicatorTransform.BeginAnimation(TranslateTransform.YProperty, animY);
                NavIndicatorPill.BeginAnimation(HeightProperty, animH);
            }
        }
        catch
        {
            // Visual tree disconnect fallback
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

    private void SettingsSubTab_Checked(object sender, RoutedEventArgs e)
    {
        if (SettingsParamsPanel == null || SettingsGuidePanel == null) return;
        bool isGuide = SettingsTabGuide?.IsChecked == true;
        SettingsParamsPanel.Visibility = isGuide ? Visibility.Collapsed : Visibility.Visible;
        SettingsGuidePanel.Visibility = isGuide ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshHistoryUI()
    {
        if (HistoryItemsPanel == null) return;
        HistoryItemsPanel.Children.Clear();

        var history = OperationHistoryService.Instance.GetRecentOperations(15);
        var lang = SettingsService.Instance.Current.Language;

        if (history.Count == 0)
        {
            var emptyBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 24, 16, 24),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 4, 0, 0)
            };
            emptyBorder.SetResourceReference(Border.BackgroundProperty, "OrbitLogItemBgBrush");
            emptyBorder.SetResourceReference(Border.BorderBrushProperty, "OrbitLogItemBorderBrush");

            var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            emptyStack.Children.Add(new TextBlock
            {
                Text = "📜",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var emptyTitle = new TextBlock
            {
                Text = GetLoc(lang, "Журнал операцій порожній", "Operations log is empty"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            emptyTitle.SetResourceReference(TextBlock.ForegroundProperty, "OrbitLogTextSecondaryBrush");
            emptyStack.Children.Add(emptyTitle);

            var emptyDesc = new TextBlock
            {
                Text = GetLoc(lang, "Автосортування папок та файлові дії фіксуватимуться тут у реальному часі", "Folder auto-sorting and file actions will be recorded here in real time"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            emptyDesc.SetResourceReference(TextBlock.ForegroundProperty, "OrbitLogTextMutedBrush");
            emptyStack.Children.Add(emptyDesc);

            emptyBorder.Child = emptyStack;
            HistoryItemsPanel.Children.Add(emptyBorder);
            return;
        }

        foreach (var op in history)
        {
            var itemBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 7)
            };
            itemBorder.SetResourceReference(Border.BackgroundProperty, "OrbitLogItemBgBrush");
            itemBorder.SetResourceReference(Border.BorderBrushProperty, "OrbitLogItemBorderBrush");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var timeBlock = new TextBlock
            {
                Text = op.Timestamp.ToString("HH:mm:ss"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            timeBlock.SetResourceReference(TextBlock.ForegroundProperty, "OrbitLogTimestampBrush");
            headerPanel.Children.Add(timeBlock);

            var titleBlock = new TextBlock
            {
                Text = op.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "OrbitLogTextPrimaryBrush");
            headerPanel.Children.Add(titleBlock);

            textStack.Children.Add(headerPanel);

            if (!string.IsNullOrWhiteSpace(op.Description))
            {
                var descBlock = new TextBlock
                {
                    Text = op.Description,
                    FontSize = 10.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                descBlock.SetResourceReference(TextBlock.ForegroundProperty, "OrbitLogTextMutedBrush");
                textStack.Children.Add(descBlock);
            }
            Grid.SetColumn(textStack, 0);
            grid.Children.Add(textStack);

            var actionContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            var statusIcon = new TextBlock
            {
                Text = op.IsUndone ? "✕" : "✓",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(8, 0, 6, 0),
                ToolTip = op.IsUndone ? GetLoc(lang, "Скасовано", "Undone") : GetLoc(lang, "Успішно виконано", "Completed successfully")
            };
            statusIcon.SetResourceReference(TextBlock.ForegroundProperty, "OrbitLogTextMutedBrush");
            actionContainer.Children.Add(statusIcon);

            Grid.SetColumn(actionContainer, 1);
            grid.Children.Add(actionContainer);

            itemBorder.Child = grid;
            HistoryItemsPanel.Children.Add(itemBorder);
        }
    }

    private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        OperationHistoryService.Instance.ClearHistory();
        RefreshHistoryUI();
    }

    // ==========================================
    // SCREEN 1: ГОЛОВНА (ДАШБОРД ORBIT)
    // ==========================================
    private void SetHubStatus(string message, string? outputPath = null, bool isError = false)
    {
        _lastDashboardOutputPath = outputPath;
        if (TxtHubStatus != null)
        {
            TxtHubStatus.Text = message;
            TxtHubStatus.Foreground = isError
                ? (Brush)FindResource("OrbitRoseLightBrush")
                : (outputPath != null ? (Brush)FindResource("OrbitGreenLightBrush") : (Brush)FindResource("OrbitHeroDescBrush"));
        }
        if (BtnHubRevealResult != null)
        {
            BtnHubRevealResult.Visibility = !string.IsNullOrWhiteSpace(outputPath) && (File.Exists(outputPath) || Directory.Exists(outputPath))
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private static void RevealInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Ignore explorer launch errors
        }
    }

    private void BtnHubRevealResult_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_lastDashboardOutputPath))
        {
            RevealInExplorer(_lastDashboardOutputPath);
        }
    }

    private void BtnOpenHaloHere_Click(object sender, RoutedEventArgs e)
    {
        var pt = GetLogicalCursorPosition();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            _radialWindow?.ShowAt(pt, (string?)null);
        }));
    }

    private void BtnPickFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = T("Виберіть файл(и) для відкриття у круговому меню Halo", "Select file(s) to open in the Halo radial menu"),
            Filter = T(
                "Усі файли (*.*)|*.*|Зображення (*.png;*.jpg;*.jpeg;*.webp;*.avif;*.tiff;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.webp;*.avif;*.tiff;*.bmp;*.ico|Документи та таблиці (*.pdf;*.docx;*.doc;*.txt;*.rtf;*.md;*.xlsx;*.csv;*.pptx)|*.pdf;*.docx;*.doc;*.txt;*.rtf;*.md;*.xlsx;*.csv;*.pptx|Відео та архіви (*.mp4;*.mkv;*.mov;*.webm;*.avi;*.zip;*.7z;*.rar;*.tar)|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.zip;*.7z;*.rar;*.tar",
                "All Files (*.*)|*.*|Images (*.png;*.jpg;*.jpeg;*.webp;*.avif;*.tiff;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.webp;*.avif;*.tiff;*.bmp;*.ico|Documents & Sheets (*.pdf;*.docx;*.doc;*.txt;*.rtf;*.md;*.xlsx;*.csv;*.pptx)|*.pdf;*.docx;*.doc;*.txt;*.rtf;*.md;*.xlsx;*.csv;*.pptx|Video & Archives (*.mp4;*.mkv;*.mov;*.webm;*.avi;*.zip;*.7z;*.rar;*.tar)|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.zip;*.7z;*.rar;*.tar")
        };

        if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
        {
            var files = dialog.FileNames;
            string label = files.Length == 1
                ? Path.GetFileName(files[0])
                : T($"{files.Length} файлів обрано", $"{files.Length} files selected");
            SetHubStatus(T($"✓ Обрано для меню: {label}", $"✓ Loaded into menu: {label}"), files[0]);

            var pt = GetLogicalCursorPosition();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                _radialWindow?.ShowAt(pt, files);
            }));
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
                string label = files.Length == 1
                    ? Path.GetFileName(files[0])
                    : T($"{files.Length} об'єктів", $"{files.Length} items");
                SetHubStatus(T($"✓ Завантажено у кругове меню: {label}", $"✓ Loaded into radial menu: {label}"), files[0]);

                var pt = GetLogicalCursorPosition();
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                {
                    _radialWindow?.ShowAt(pt, files);
                }));
            }
        }
    }

    // --- Прямі швидкі дії на Головному екрані (без відкриття радіального меню) ---
    private void BtnQuickZip_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = T("Оберіть один або декілька файлів для стиснення у ZIP-архів", "Select one or more files to compress into a ZIP archive"),
            Filter = T("Усі файли (*.*)|*.*", "All Files (*.*)|*.*")
        };

        if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
        {
            ExecuteQuickZip(dialog.FileNames);
        }
    }

    private void QuickZip_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                ExecuteQuickZip(files);
            }
        }
    }

    private void ExecuteQuickZip(IReadOnlyList<string> paths)
    {
        try
        {
            string zipPath = ImageProcessor.CompressToZip(paths);
            string zipName = Path.GetFileName(zipPath);
            OperationHistoryService.Instance.RecordOperation(new OperationRecord
            {
                ActionId = "quick_zip",
                Title = T("Стиснення у ZIP", "Compress to ZIP"),
                Description = $"{zipName} ({paths.Count} items)",
                CanUndo = false,
                CreatedFiles = [zipPath]
            });
            SetHubStatus(
                T($"✓ Створено ZIP-архів: {zipName} (об'єктів: {paths.Count})",
                  $"✓ Created ZIP archive: {zipName} ({paths.Count} items)"),
                zipPath);
            RevealInExplorer(zipPath);
        }
        catch (Exception ex)
        {
            SetHubStatus(T($"Помилка створення ZIP: {ex.Message}", $"ZIP creation failed: {ex.Message}"), null, isError: true);
        }
    }

    private void BtnQuickPdf_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = T("Оберіть документ або зображення для збереження у PDF", "Select document(s) or image(s) to save as PDF"),
            Filter = T(
                "Підтримувані файли (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.docx;*.doc;*.txt;*.rtf;*.md;*.html;*.csv;*.xlsx;*.pptx)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.docx;*.doc;*.txt;*.rtf;*.md;*.html;*.csv;*.xlsx;*.pptx|Усі файли (*.*)|*.*",
                "Supported files (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.docx;*.doc;*.txt;*.rtf;*.md;*.html;*.csv;*.xlsx;*.pptx)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.docx;*.doc;*.txt;*.rtf;*.md;*.html;*.csv;*.xlsx;*.pptx|All Files (*.*)|*.*")
        };

        if (dialog.ShowDialog() == true && dialog.FileNames.Length > 0)
        {
            ExecuteQuickPdf(dialog.FileNames);
        }
    }

    private void QuickPdf_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                ExecuteQuickPdf(files);
            }
        }
    }

    private void ExecuteQuickPdf(IReadOnlyList<string> paths)
    {
        try
        {
            var validFiles = paths.Where(File.Exists).ToList();
            if (validFiles.Count == 0)
            {
                SetHubStatus(T("Для збереження у PDF оберіть файл документа або зображення.", "Select a document or image file to save as PDF."), null, isError: true);
                return;
            }

            string lastPdf;
            if (validFiles.Count > 1 && validFiles.All(f => FileTypeDetector.Detect(f) == FileTypeCategory.Image))
            {
                lastPdf = ImageProcessor.MergeImagesToPdf(validFiles);
            }
            else
            {
                lastPdf = string.Empty;
                foreach (var file in validFiles)
                {
                    lastPdf = ImageProcessor.ConvertToPdf(file);
                }
            }

            string pdfName = Path.GetFileName(lastPdf);
            if (File.Exists(lastPdf))
            {
                OperationHistoryService.Instance.RecordOperation(new OperationRecord
                {
                    ActionId = "quick_pdf",
                    Title = T("Зберегти в PDF", "Save to PDF"),
                    Description = pdfName,
                    CanUndo = false,
                    CreatedFiles = [lastPdf]
                });
            }
            SetHubStatus(
                T($"✓ Збережено у PDF: {pdfName}", $"✓ Saved to PDF: {pdfName}"),
                lastPdf);
            RevealInExplorer(lastPdf);
        }
        catch (Exception ex)
        {
            SetHubStatus(T($"Помилка збереження у PDF: {ex.Message}", $"PDF conversion failed: {ex.Message}"), null, isError: true);
        }
    }

    private void BtnQuickOcr_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = false,
            Title = T("Оберіть зображення, PDF або документ для розпізнавання тексту (OCR)", "Select an image, PDF, or document for text extraction (OCR)"),
            Filter = T(
                "Зображення та документи (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.pdf;*.docx;*.txt;*.md)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.pdf;*.docx;*.txt;*.md|Усі файли (*.*)|*.*",
                "Images & Documents (*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.pdf;*.docx;*.txt;*.md)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tiff;*.pdf;*.docx;*.txt;*.md|All Files (*.*)|*.*")
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            ExecuteQuickOcr(dialog.FileName);
        }
    }

    private void QuickOcr_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && File.Exists(files[0]))
            {
                ExecuteQuickOcr(files[0]);
            }
        }
    }

    private void ExecuteQuickOcr(string filePath)
    {
        try
        {
            var cat = FileTypeDetector.Detect(filePath);
            string txtPath = (cat == FileTypeCategory.Image || cat == FileTypeCategory.Pdf)
                ? ImageProcessor.ExtractTextOcr(filePath)
                : ImageProcessor.ExtractPlainText(filePath);

            string extracted = File.Exists(txtPath) ? File.ReadAllText(txtPath).Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                Clipboard.SetText(extracted);
            }

            if (File.Exists(txtPath))
            {
                OperationHistoryService.Instance.RecordOperation(new OperationRecord
                {
                    ActionId = "quick_ocr",
                    Title = T("Розпізнати текст (OCR)", "Extract Text (OCR)"),
                    Description = $"{Path.GetFileName(filePath)} → {Path.GetFileName(txtPath)}",
                    CanUndo = false,
                    CreatedFiles = [txtPath]
                });
            }

            SetHubStatus(
                T($"✓ Текст розпізнано й скопійовано в буфер ({Path.GetFileName(txtPath)})",
                  $"✓ Text extracted & copied to Clipboard ({Path.GetFileName(txtPath)})"),
                txtPath);
            RevealInExplorer(txtPath);
        }
        catch (Exception ex)
        {
            SetHubStatus(T($"Помилка розпізнавання тексту: {ex.Message}", $"OCR failed: {ex.Message}"), null, isError: true);
        }
    }

    private void BtnQuickClip_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = false,
            Title = T("Оберіть відео для обрізки та створення кліпу", "Select a video to trim and create clip"),
            Filter = T(
                "Відео файли (*.mp4;*.mkv;*.mov;*.avi;*.webm)|*.mp4;*.mkv;*.mov;*.avi;*.webm|Усі файли (*.*)|*.*",
                "Video files (*.mp4;*.mkv;*.mov;*.avi;*.webm)|*.mp4;*.mkv;*.mov;*.avi;*.webm|All Files (*.*)|*.*")
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            OpenVideoTrimmer(dialog.FileName);
        }
    }

    private void QuickClip_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var videoFile = files?.FirstOrDefault(f => File.Exists(f) && FileTypeDetector.Detect(f) == FileTypeCategory.Video);
            if (videoFile != null)
            {
                OpenVideoTrimmer(videoFile);
            }
            else if (files != null && files.Length > 0 && File.Exists(files[0]))
            {
                OpenVideoTrimmer(files[0]);
            }
        }
    }

    private void OpenVideoTrimmer(string videoPath)
    {
        try
        {
            var trimmer = new Views.VideoTrimWindow(videoPath)
            {
                Owner = this
            };
            trimmer.Show();
        }
        catch (Exception ex)
        {
            SetHubStatus(T($"Помилка відкриття відеоредактора: {ex.Message}", $"Failed to open video editor: {ex.Message}"), null, isError: true);
        }
    }

    // ==========================================
    // SCREEN 2: КРУГОВІ МЕНЮ (3 РЕЖИМИ + 7 КАТЕГОРІЙ)
    // ==========================================
    private void LoadRadialScreenData()
    {
        if (RadialCategoriesList == null) return;
        UpdateCategorySectors();
        UpdateCategoryBadges();
    }

    private void RadialModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
        {
            _selectedRadialMode = (rb.Tag?.ToString()) switch
            {
                "Edit" => ActionCategory.Edit,
                "System" => ActionCategory.System,
                _ => ActionCategory.Convert
            };
            if (RadialCategoriesList != null)
            {
                UpdateCategorySectors();
                UpdateCategoryBadges();
            }
        }
    }

    private void RadialCategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingUi) return;
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
            "Video" => FileTypeCategory.Video,
            "Pdf" => FileTypeCategory.Pdf,
            "Document" => FileTypeCategory.Document,
            "Presentation" => FileTypeCategory.Presentation,
            "Spreadsheet" => FileTypeCategory.Spreadsheet,
            "Archive" => FileTypeCategory.Archive,
            _ => FileTypeCategory.Generic
        };

        List<ActionItem> filtered;
        if (_selectedRadialMode == ActionCategory.System)
        {
            filtered = settings.Actions
                .Where(a => a.Category == ActionCategory.System)
                .OrderBy(a => a.Order)
                .ToList();
        }
        else if (_selectedRadialMode == ActionCategory.Edit)
        {
            // Dynamic tools strictly tailored to the selected file category
            filtered = settings.Actions
                .Where(a => a.Category == ActionCategory.Edit && a.SupportedFileTypes.Contains(catEnum))
                .OrderBy(a => a.Order)
                .ToList();
        }
        else
        {
            filtered = settings.Actions
                .Where(a => a.Category == ActionCategory.Convert && a.SupportedFileTypes.Contains(catEnum))
                .OrderBy(a => a.Order)
                .ToList();
        }

        RadialActionsItemsControl.ItemsSource = null;
        RadialActionsItemsControl.ItemsSource = filtered;

        string catName = categoryTag switch
        {
            "Image" => T("Зображення", "Images"),
            "Video" => T("Відео", "Video"),
            "Pdf" => "PDF",
            "Document" => T("Документи", "Documents"),
            "Presentation" => T("Презентації", "Presentations"),
            "Spreadsheet" => T("Таблиці", "Spreadsheets"),
            "Archive" => T("Архіви", "Archives"),
            _ => T("Усі файли", "All Files")
        };

        string discLabel = _selectedRadialMode switch
        {
            ActionCategory.System => T("Дії", "Actions"),
            _ => catName
        };

        if (TxtRadialListHeader != null)
        {
            string modeLabel = _selectedRadialMode switch
            {
                ActionCategory.Edit => T($"ІНСТРУМЕНТИ: {catName.ToUpperInvariant()} (CTRL)", $"TOOLS: {catName.ToUpperInvariant()} (CTRL)"),
                ActionCategory.System => T("ДІЇ ДЛЯ БУДЬ-ЯКИХ ФАЙЛІВ (ALT)", "ACTIONS FOR ANY FILE (ALT)"),
                _ => T($"КОНВЕРТАЦІЯ: {catName.ToUpperInvariant()} (SHIFT)", $"CONVERT: {catName.ToUpperInvariant()} (SHIFT)")
            };
            TxtRadialListHeader.Text = modeLabel;
        }

        PreviewRadialDisc.SetActions(filtered, discLabel);
    }

    private void UpdateCategoryBadges()
    {
        var settings = SettingsService.Instance.Current;
        var modeToCount = _selectedRadialMode;

        int CountFor(FileTypeCategory cat)
        {
            if (modeToCount == ActionCategory.System)
            {
                return settings.Actions.Count(a => a.IsEnabled && a.Category == ActionCategory.System);
            }
            return settings.Actions.Count(a => a.IsEnabled && a.Category == modeToCount && a.SupportedFileTypes.Contains(cat));
        }

        if (BadgeImageCount != null) BadgeImageCount.Text = CountFor(FileTypeCategory.Image).ToString();
        if (BadgeVideoCount != null) BadgeVideoCount.Text = CountFor(FileTypeCategory.Video).ToString();
        if (BadgePdfCount != null) BadgePdfCount.Text = CountFor(FileTypeCategory.Pdf).ToString();
        if (BadgeDocCount != null) BadgeDocCount.Text = CountFor(FileTypeCategory.Document).ToString();
        if (BadgePresCount != null) BadgePresCount.Text = CountFor(FileTypeCategory.Presentation).ToString();
        if (BadgeSheetCount != null) BadgeSheetCount.Text = CountFor(FileTypeCategory.Spreadsheet).ToString();
        if (BadgeGenericCount != null) BadgeGenericCount.Text = CountFor(FileTypeCategory.Archive).ToString();
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

    private void BtnRadialTestScreen_Click(object sender, RoutedEventArgs e)
    {
        InitializeSubsystems();
        var selected = RadialCategoriesList?.SelectedItem as ListBoxItem;
        var categoryTag = selected?.Tag?.ToString() ?? "Image";
        var catEnum = categoryTag switch
        {
            "Image" => FileTypeCategory.Image,
            "Video" => FileTypeCategory.Video,
            "Pdf" => FileTypeCategory.Pdf,
            "Document" => FileTypeCategory.Document,
            "Presentation" => FileTypeCategory.Presentation,
            "Spreadsheet" => FileTypeCategory.Spreadsheet,
            "Archive" => FileTypeCategory.Archive,
            _ => FileTypeCategory.Generic
        };

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            _radialWindow?.ShowPreviewForCategory(catEnum, _selectedRadialMode);
        }));
    }

    // ==========================================
    // SCREEN 3: ЖЕСТИ Й КЛАВІШІ
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
        HotkeyRecorderStatusText.Text = T("Натисніть клавіші...", "Press keys...");
        HotkeyRecorderHint.Text = T(
            "Затисніть комбінацію на клавіатурі (напр. Alt+Space або Ctrl+Shift+H). Esc — скасувати.",
            "Hold down your desired shortcut on the keyboard (e.g. Alt+Space or Ctrl+Shift+H). Esc — cancel.");
        BtnRecordHotkey.Content = T("Скасувати запис", "Cancel recording");
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
        GameModeHotkeyStatusText.Text = T("Натисніть клавіші...", "Press keys...");
        GameModeRecorderHint.Text = T(
            "Затисніть комбінацію для швидкого ввімкнення Ігрового режиму (напр. Alt+G). Esc — скасувати.",
            "Hold down a shortcut to quickly toggle Game Mode (e.g. Alt+G). Esc — cancel.");
        BtnRecordGameModeHotkey.Content = T("Скасувати запис", "Cancel recording");
        GameModeRecorderCard.BorderBrush = (Brush)FindResource("OrbitRoseBrush");
    }

    private void StopRecordingHotkey()
    {
        _recordTarget = HotkeyRecordTarget.None;
        HotkeyRecorderHint.Text = T(
            "Натисніть кнопку запису нижче, а потім затисніть бажані клавіші на клавіатурі.",
            "Click the record button below, then press your desired key combination on the keyboard.");
        BtnRecordHotkey.Content = T("Записати комбінацію з клавіатури", "Record keyboard shortcut");
        HotkeyRecorderCard.BorderBrush = (Brush)FindResource("OrbitCardBorderBrush");

        GameModeRecorderHint.Text = T(
            "Ви можете вмикати та вимикати Ігровий режим будь-коли гарячою комбінацією клавіш:",
            "You can toggle Game Mode on or off anytime using this global shortcut:");
        BtnRecordGameModeHotkey.Content = T("Записати комбінацію з клавіатури", "Record keyboard shortcut");
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
    // SCREEN 4: ВІДСТЕЖЕННЯ ПАПОК (РОЗШИРЕНИЙ ФУНКЦІОНАЛ)
    // ==========================================
    private void LoadFoldersData()
    {
        if (FoldersRulesItemsControl == null) return;
        var settings = SettingsService.Instance.Current;
        FoldersRulesItemsControl.ItemsSource = null;
        FoldersRulesItemsControl.ItemsSource = settings.WatchedFolders;
        if (EmptyFoldersPanel != null)
        {
            EmptyFoldersPanel.Visibility = (settings.WatchedFolders == null || settings.WatchedFolders.Count == 0)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void BtnFolderPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string preset)
        {
            string path = preset switch
            {
                "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };
            QuickFolderInput.Text = path;
        }
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = T("Виберіть папку для автоматичного моніторингу", "Select folder for automatic monitoring")
        };

        if (dialog.ShowDialog() == true)
        {
            QuickFolderInput.Text = dialog.FolderName;
        }
    }

    private void BtnBrowseTargetFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = T("Виберіть цільову папку для збереження оброблених файлів", "Select target output folder for processed files")
        };

        if (dialog.ShowDialog() == true)
        {
            QuickTargetFolderInput.Text = dialog.FolderName;
        }
    }

    private void BtnQuickAddRule_Click(object sender, RoutedEventArgs e)
    {
        var folder = QuickFolderInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            OrbitMessageDialog.Show(
                this,
                T("Будь ласка, вкажіть дійсну існуючу папку на диску (натисніть «Огляд...»).",
                  "Please specify a valid existing folder on disk (click 'Browse...')."),
                T("Відстеження папок Halo", "Halo Watched Folders"),
                "⚠️");
            return;
        }

        var actionTag = (QuickActionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "folder_auto_sort";
        var filterTag = (QuickFilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "*.*";
        var targetOut = QuickTargetFolderInput.Text.Trim();

        var rule = new WatchedFolderConfig
        {
            FolderPath = folder,
            AssignedActionId = actionTag,
            FileFilter = filterTag,
            TargetOutputFolder = string.IsNullOrWhiteSpace(targetOut) ? string.Empty : targetOut,
            IncludeSubdirectories = CheckFolderSubdirs.IsChecked ?? false,
            DeleteSourceAfterProcess = CheckFolderDeleteSource.IsChecked ?? false,
            IsEnabled = true
        };

        var settings = SettingsService.Instance.Current;
        settings.WatchedFolders.Add(rule);
        SettingsService.Instance.SaveSettings(settings);

        LoadFoldersData();
        RefreshDashboardStats();
        FolderWatcherService.Instance.Start();
    }

    private void BtnProcessAllFolders_Click(object sender, RoutedEventArgs e)
    {
        int total = FolderWatcherService.Instance.ProcessAllActiveRules();
        LoadFoldersData();
        OrbitMessageDialog.Show(
            this,
            T($"Перевірку всіх активних правил завершено.\nОброблено файлів: {total}.",
              $"All active folder rules processed.\nFiles processed: {total}."),
            T("Відстеження папок Halo", "Halo Watched Folders"),
            "⚡");
    }

    private void BtnRunFolderRuleNow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchedFolderConfig rule)
        {
            int count = FolderWatcherService.Instance.ProcessExistingFilesInFolder(rule);
            LoadFoldersData();
            OrbitMessageDialog.Show(
                this,
                T($"Перевірку папки «{rule.FolderPath}» завершено.\nОброблено файлів: {count}.",
                  $"Folder check for '{rule.FolderPath}' completed.\nFiles processed: {count}."),
                T("Відстеження папок Halo", "Halo Watched Folders"),
                "📁");
        }
    }

    private void BtnOpenWatchedFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchedFolderConfig rule && Directory.Exists(rule.FolderPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{rule.FolderPath}\"",
                UseShellExecute = true
            });
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
    // SCREEN 5: НАЛАШТУВАННЯ (МОВА, ТЕМИ, ПРОВІДНИК, ДОДАТКОВІ ПРОГРАМИ ЧЕРЕЗ POWERSHELL)
    // ==========================================
    private void RefreshDependenciesStatus()
    {
        if (TxtFfmpegStatus == null || TxtLibreOfficeStatus == null || TxtPandocStatus == null)
            return;

        var greenBg = (Brush)FindResource("OrbitGreenSoftBg");
        var greenFg = (Brush)FindResource("OrbitGreenLightBrush");
        var roseBg = (Brush)FindResource("OrbitRoseSoftBg");
        var roseFg = (Brush)FindResource("OrbitRoseLightBrush");

        // 1. Built-in + Pandoc CLI
        bool pandocCliInstalled = DependencyManagerService.IsPandocInstalled;
        if (pandocCliInstalled)
        {
            // Remove redundant green badge, leave only 1 indicator (the button)
            BadgePandocStatus.Visibility = Visibility.Collapsed;
            BtnInstallPandoc.Content = T("Встановлено ✓", "Installed ✓");
            BtnInstallPandoc.IsEnabled = false;
            BtnInstallPandoc.Style = (Style)FindResource("OrbitSecondaryButton");
        }
        else
        {
            BadgePandocStatus.Visibility = Visibility.Visible;
            BadgePandocStatus.Background = roseBg;
            TxtPandocStatus.Foreground = roseFg;
            TxtPandocStatus.Text = T("Відсутнє", "Missing");
            BtnInstallPandoc.Content = T("⚡ Встановити (PowerShell)", "⚡ Install (PowerShell)");
            BtnInstallPandoc.IsEnabled = true;
            BtnInstallPandoc.Style = (Style)FindResource("OrbitPrimaryButton");
        }

        // 2. FFmpeg
        bool ffmpegInstalled = DependencyManagerService.IsFfmpegInstalled;
        if (ffmpegInstalled)
        {
            // Remove redundant green badge, leave only 1 indicator (the button)
            BadgeFfmpegStatus.Visibility = Visibility.Collapsed;
            BtnInstallFfmpeg.Content = T("Встановлено ✓", "Installed ✓");
            BtnInstallFfmpeg.IsEnabled = false;
            BtnInstallFfmpeg.Style = (Style)FindResource("OrbitSecondaryButton");
        }
        else
        {
            BadgeFfmpegStatus.Visibility = Visibility.Visible;
            BadgeFfmpegStatus.Background = roseBg;
            TxtFfmpegStatus.Foreground = roseFg;
            TxtFfmpegStatus.Text = T("Відсутнє", "Missing");
            BtnInstallFfmpeg.Content = T("⚡ Встановити (PowerShell)", "⚡ Install (PowerShell)");
            BtnInstallFfmpeg.IsEnabled = true;
            BtnInstallFfmpeg.Style = (Style)FindResource("OrbitPrimaryButton");
        }

        // 3. LibreOffice
        bool loInstalled = DependencyManagerService.IsLibreOfficeInstalled;
        if (loInstalled)
        {
            // Remove redundant green badge, leave only 1 indicator (the button)
            BadgeLibreOfficeStatus.Visibility = Visibility.Collapsed;
            BtnInstallLibreOffice.Content = T("Встановлено ✓", "Installed ✓");
            BtnInstallLibreOffice.IsEnabled = false;
            BtnInstallLibreOffice.Style = (Style)FindResource("OrbitSecondaryButton");
        }
        else
        {
            BadgeLibreOfficeStatus.Visibility = Visibility.Visible;
            BadgeLibreOfficeStatus.Background = roseBg;
            TxtLibreOfficeStatus.Foreground = roseFg;
            TxtLibreOfficeStatus.Text = T("Відсутнє", "Missing");
            BtnInstallLibreOffice.Content = T("⚡ Встановити (PowerShell)", "⚡ Install (PowerShell)");
            BtnInstallLibreOffice.IsEnabled = true;
            BtnInstallLibreOffice.Style = (Style)FindResource("OrbitPrimaryButton");
        }
    }

    private async void BtnRefreshDependencies_Click(object sender, RoutedEventArgs e)
    {
        if (BtnRefreshDeps != null)
        {
            BtnRefreshDeps.IsEnabled = false;
            BtnRefreshDeps.Content = T("⏳  Перевірка...", "⏳  Checking...");
        }

        await System.Threading.Tasks.Task.Delay(260);
        RefreshDependenciesStatus();

        if (BtnRefreshDeps != null)
        {
            BtnRefreshDeps.IsEnabled = true;
            BtnRefreshDeps.Content = T("↺  Перевірити статус", "↺  Check Status");
        }

        if (BadgeRefreshFeedback != null && TxtRefreshFeedback != null)
        {
            var lang = SettingsService.Instance.Current.Language;
            TxtRefreshFeedback.Text = GetLoc(lang, "Перевірено ✓", "Verified ✓");
            BadgeRefreshFeedback.Visibility = Visibility.Visible;
            BadgeRefreshFeedback.BeginAnimation(UIElement.OpacityProperty, null);
            BadgeRefreshFeedback.Opacity = 1.0;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(400));
                fade.Completed += (_, _) =>
                {
                    BadgeRefreshFeedback.Visibility = Visibility.Hidden;
                };
                BadgeRefreshFeedback.BeginAnimation(UIElement.OpacityProperty, fade);
            };
            timer.Start();
        }
    }

    private async void BtnInstallPandoc_Click(object sender, RoutedEventArgs e)
    {
        BtnInstallPandoc.IsEnabled = false;
        var (success, message) = await DependencyManagerService.InstallPandocAsync(status =>
        {
            Dispatcher.Invoke(() => BtnInstallPandoc.Content = status);
        });
        RefreshDependenciesStatus();
        if (!success)
        {
            BtnInstallPandoc.IsEnabled = true;
        }
        OrbitMessageDialog.Show(this, message, "Pandoc (PowerShell)", success ? "✓" : "⚠️");
    }

    private async void BtnInstallFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        BtnInstallFfmpeg.IsEnabled = false;
        var (success, message) = await DependencyManagerService.InstallFfmpegAsync(status =>
        {
            Dispatcher.Invoke(() => BtnInstallFfmpeg.Content = status);
        });
        RefreshDependenciesStatus();
        if (!success)
        {
            BtnInstallFfmpeg.IsEnabled = true;
        }
        OrbitMessageDialog.Show(this, message, "FFmpeg (PowerShell)", success ? "✓" : "⚠️");
    }

    private async void BtnInstallLibreOffice_Click(object sender, RoutedEventArgs e)
    {
        BtnInstallLibreOffice.IsEnabled = false;
        var (success, message) = await DependencyManagerService.InstallLibreOfficeAsync(status =>
        {
            Dispatcher.Invoke(() => BtnInstallLibreOffice.Content = status);
        });
        RefreshDependenciesStatus();
        if (!success)
        {
            BtnInstallLibreOffice.IsEnabled = true;
        }
        OrbitMessageDialog.Show(this, message, "LibreOffice (PowerShell)", success ? "✓" : "⚠️");
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializingUi || LanguageCombo == null) return;
        string lang = LanguageCombo.SelectedIndex == 1 ? "en" : "uk";

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

    private void GeneralThemeCombo_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (GeneralThemeCombo != null && !GeneralThemeCombo.IsDropDownOpen)
        {
            e.Handled = true;
            var parent = VisualTreeHelper.GetParent((DependencyObject)sender);
            while (parent != null && parent is not ScrollViewer)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            if (parent is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta / 3.0));
            }
        }
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
        string menuTitle = GetLoc(lang, "Відкрити в Halo", "Open in Halo");

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
        ThemeManager.Instance.ApplyTheme(theme);
    }

    private void OnThemeChanged(string _)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshHistoryUI();
            UpdateGameModeVisualState();
            RefreshDashboardStats();
        });
    }

    private void ApplyLanguage(string lang)
    {
        if (TxtSidebarSubtitle == null || TxtSettingsBehaviorTitle == null) return;

        DefaultActionsProvider.LocalizeActions(SettingsService.Instance.Current.Actions, lang);

        // Sidebar & Header
        TxtSidebarSubtitle.Text = GetLoc(lang, "Робочий простір", "Workspace");
        TxtSidebarMenuHeader.Text = GetLoc(lang, "МЕНЮ", "MENU");
        TxtSidebarTriggerLabel.Text = GetLoc(lang, "Тригер:", "Trigger:");

        TxtNavMain.Text = GetLoc(lang, "Головна", "Dashboard");
        TxtNavRadial.Text = GetLoc(lang, "Кругові меню", "Radial Menus");
        TxtNavInput.Text = GetLoc(lang, "Жести й клавіші", "Gestures & Keys");
        TxtNavFolders.Text = GetLoc(lang, "Відстеження папок", "Watched Folders");
        TxtNavSettings.Text = GetLoc(lang, "Налаштування", "Settings");

        BtnHeaderOpenShelf.Content = GetLoc(lang, "📌  Полиця", "📌  Shelf");
        BtnHeaderOpenHalo.Content = GetLoc(lang, "✦  Відкрити Halo", "✦  Open Halo");
        BtnWinMinimize.ToolTip = GetLoc(lang, "Згорнути", "Minimize");
        BtnWinClose.ToolTip = GetLoc(lang, "Закрити", "Close");

        // Screen 1: Main / Dashboard
        TxtMetric1Title.Text = GetLoc(lang, "Активні дії", "Active Actions");
        TxtMetric1Sub.Text = GetLoc(lang, "Налаштувати сектори →", "Configure sectors →");
        TxtMetric2Title.Text = GetLoc(lang, "Швидкий виклик", "Quick Trigger");
        TxtMetric2Sub.Text = GetLoc(lang, "Жести та клавіші →", "Gestures & keys →");
        TxtMetric3Title.Text = GetLoc(lang, "Автоматизація", "Automation");
        TxtMetric3Sub.Text = GetLoc(lang, "Відстеження папок →", "Watched folders →");
        TxtMetric4Title.Text = GetLoc(lang, "Тема та система", "Theme & System");
        TxtMetric4Sub.Text = GetLoc(lang, "Відкрити налаштування →", "Open settings →");

        TxtHubBadge.Text = GetLoc(lang, "✦  ІНТЕРАКТИВНИЙ ХАБ", "✦  INTERACTIVE HUB");
        TxtHubTitle.Text = GetLoc(lang, "Миттєва робота з будь-яким файлом", "Instant File Action Hub");
        TxtHubDesc.Text = GetLoc(lang,
            "Перетягніть сюди зображення, відео, архів чи документ або оберіть файл(и) з диска, щоб відкрити радіальне меню дій.",
            "Drag and drop an image, video, archive, or document here, or pick file(s) from disk to open the radial action menu.");
        BtnHubPickFile.Content = GetLoc(lang, "📂  Вибрати файл(и)", "📂  Select File(s)");
        BtnHubTestMenu.Content = GetLoc(lang, "◎  Відкрити кругове меню", "◎  Open Radial Menu");
        if (string.IsNullOrWhiteSpace(_lastDashboardOutputPath))
        {
            if (HubStatusPill != null) HubStatusPill.Visibility = Visibility.Collapsed;
            if (TxtHubStatus != null) TxtHubStatus.Text = string.Empty;
        }
        if (BtnHubRevealResult != null)
        {
            BtnHubRevealResult.Content = GetLoc(lang, "Показати в папці →", "Show in folder →");
        }

        TxtQuickZipTitle.Text = GetLoc(lang, "Стиснення у ZIP", "Compress to ZIP");
        TxtQuickZipSub.Text = GetLoc(lang, "Миттєве пакування файлів в архів", "Instantly pack files into a ZIP archive");
        TxtQuickPdfTitle.Text = GetLoc(lang, "Зберегти в PDF", "Save to PDF");
        TxtQuickPdfSub.Text = GetLoc(lang, "Пряма конвертація фото й документів", "Direct conversion of images & docs");
        if (TxtQuickOcrTitle != null) TxtQuickOcrTitle.Text = GetLoc(lang, "Розпізнати текст (OCR)", "Extract Text (OCR)");
        if (TxtQuickOcrSub != null) TxtQuickOcrSub.Text = GetLoc(lang, "Текст із фото чи PDF одразу в буфер", "Text from image or PDF to Clipboard");
        if (TxtQuickClipTitle != null) TxtQuickClipTitle.Text = GetLoc(lang, "Створити кліп", "Create Clip");
        if (TxtQuickClipSub != null) TxtQuickClipSub.Text = GetLoc(lang, "Обрізка та вибір моменту з відео", "Trim and extract video highlights");

        if (TxtHistoryTitle != null) TxtHistoryTitle.Text = GetLoc(lang, "Журнал операцій", "Operations Log");
        if (TxtHistorySub != null) TxtHistorySub.Text = GetLoc(lang, "Історія файлових дій та фонової автоматизації", "File activity and automation log");
        if (TxtHistoryBadge != null) TxtHistoryBadge.Text = GetLoc(lang, "Живий лог", "Live Log");
        if (BtnClearHistory != null) BtnClearHistory.Content = GetLoc(lang, "🗑️  Очистити журнал", "🗑️  Clear History");
        RefreshHistoryUI();

        // Screen 2: Radial Menus
        TxtRadialModeLabel.Text = GetLoc(lang, "ГЛОБАЛЬНИЙ РЕЖИМ:", "GLOBAL MODE:");
        RadialModeConvertRadio.Content = GetLoc(lang, "⚡ Конвертація (Shift)", "⚡ Convert (Shift)");
        RadialModeToolsRadio.Content = GetLoc(lang, "🛠️ Інструменти (Ctrl)", "🛠️ Tools (Ctrl)");
        RadialModeActionsRadio.Content = GetLoc(lang, "🚀 Дії (Alt)", "🚀 Actions (Alt)");
        TxtRadialModifierHint.Text = GetLoc(lang,
            "Клавіші в меню: Shift — Конвертація • Ctrl — Інструменти • Alt — Дії",
            "Menu keys: Shift — Convert • Ctrl — Tools • Alt — Actions");

        TxtRadialCategoriesHeader.Text = GetLoc(lang, "КАТЕГОРІЇ ФАЙЛІВ", "FILE CATEGORIES");
        TxtCatImage.Text = GetLoc(lang, "Зображення", "Images");
        TxtCatVideo.Text = GetLoc(lang, "Відео", "Video");
        TxtCatPdf.Text = "PDF";
        TxtCatDocument.Text = GetLoc(lang, "Документи", "Documents");
        TxtCatPresentation.Text = GetLoc(lang, "Презентації", "Presentations");
        TxtCatSpreadsheet.Text = GetLoc(lang, "Таблиці", "Spreadsheets");
        TxtCatArchive.Text = GetLoc(lang, "Архіви", "Archives");

        TxtRadialPreviewHeader.Text = GetLoc(lang, "МАКЕТ РАДІАЛЬНОГО КОЛА", "RADIAL DISC PREVIEW");
        BtnRadialTestScreen.Content = GetLoc(lang, "✦  Протестувати меню на екрані", "✦  Test Menu on Screen");

        // Screen 3: Gestures & Keys
        TxtInputMainHotkeyTitle.Text = GetLoc(lang, "Комбінація клавіш Halo", "Halo Global Shortcut");
        TxtInputMainHotkeySub.Text = GetLoc(lang, "Глобальне перехоплення виклику", "Global summon hotkey");
        TxtInputPrimaryBadge.Text = GetLoc(lang, "Основний", "Primary");
        TxtCurrentMainHotkeyLabel.Text = GetLoc(lang, "Поточний хоткей:", "Current shortcut:");

        TxtInputGameModeTitle.Text = GetLoc(lang, "Ігровий режим (Тиша)", "Game Mode (Silent)");
        TxtInputGameModeSub.Text = GetLoc(lang, "Пауза всіх тригерів у іграх", "Pause all triggers while gaming");
        TxtCurrentGameHotkeyLabel.Text = GetLoc(lang, "Поточний хоткей:", "Current shortcut:");

        if (_recordTarget == HotkeyRecordTarget.None)
        {
            HotkeyRecorderHint.Text = GetLoc(lang,
                "Натисніть кнопку запису нижче, а потім затисніть бажані клавіші на клавіатурі.",
                "Click the record button below, then press your desired key combination on the keyboard.");
            BtnRecordHotkey.Content = GetLoc(lang, "Записати комбінацію з клавіатури", "Record keyboard shortcut");

            GameModeRecorderHint.Text = GetLoc(lang,
                "Ви можете вмикати та вимикати Ігровий режим будь-коли гарячою комбінацією клавіш:",
                "You can toggle Game Mode on or off anytime using this global shortcut:");
            BtnRecordGameModeHotkey.Content = GetLoc(lang, "Записати комбінацію з клавіатури", "Record keyboard shortcut");
        }

        TxtShakeTitle.Text = GetLoc(lang, "Струшування мишею (Shake)", "Mouse Shake Gesture");
        TxtShakeDesc.Text = GetLoc(lang,
            "Швидко потрусіть курсор вліво-вправо, щоб викликати радіальне меню для виділеного файлу",
            "Quickly shake the cursor left and right to summon the radial menu for a selected file");
        TxtMiddleMouseTitle.Text = GetLoc(lang, "Клік коліщатком миші", "Middle Mouse Click");
        TxtMiddleMouseDesc.Text = GetLoc(lang,
            "Виклик радіального меню натисканням середньої кнопки миші",
            "Summon the radial menu by pressing the middle mouse wheel button");
        TxtResetTriggersTitle.Text = GetLoc(lang, "Скидання налаштувань керування", "Reset Input Controls");
        TxtResetTriggersDesc.Text = GetLoc(lang,
            "Повернути стандартні комбінації (Alt+Space та Alt+G) і увімкнути всі жести миші",
            "Restore default shortcuts (Alt+Space and Alt+G) and enable all mouse gestures");
        BtnResetTriggers.Content = GetLoc(lang, "↺  Скинути до стандартних налаштувань", "↺  Restore Default Controls");

        // Screen 4: Watched Folders
        TxtFoldersHeroTitle.Text = GetLoc(lang,
            "Автоматичний моніторинг та розумна обробка каталогів",
            "Automatic Folder Monitoring & Smart Processing");
        TxtFoldersHeroDesc.Text = GetLoc(lang,
            "Halo стежить за папками у фоні: автоматично сортує завантаження, пакує та розпаковує архіви, конвертує фото й документи або очищує тимчасові файли.",
            "Halo monitors folders in the background: auto-sorts downloads, packs/extracts archives, converts photos & documents, or cleans temporary files.");
        BtnProcessAllFolders.Content = GetLoc(lang, "▶ Обробити всі активні", "▶ Process All Active");

        TxtAddFolderTitle.Text = GetLoc(lang, "Нове правило автоматизації", "New Automation Rule");
        TxtAddFolderSub.Text = GetLoc(lang,
            "Оберіть папку, фільтр файлів, дію та додаткові параметри",
            "Choose a folder, file filter, action, and additional options");
        BtnPresetDownloads.Content = GetLoc(lang, "⬇ Завантаження", "⬇ Downloads");
        BtnPresetDesktop.Content = GetLoc(lang, "🖥 Робочий стіл", "🖥 Desktop");
        BtnPresetDocuments.Content = GetLoc(lang, "📄 Документи", "📄 Documents");
        BtnBrowseFolder.Content = GetLoc(lang, "📁 Огляд...", "📁 Browse...");

        FilterItemAll.Content = GetLoc(lang, "Усі файли (*.*)", "All files (*.*)");
        FilterItemImages.Content = GetLoc(lang, "Тільки зображення", "Images only");
        FilterItemDocs.Content = GetLoc(lang, "Тільки документи", "Documents only");
        FilterItemArchives.Content = GetLoc(lang, "Тільки архіви", "Archives only");
        FilterItemVideos.Content = GetLoc(lang, "Тільки відео", "Videos only");

        RuleActAutoSort.Content = GetLoc(lang, "Авто-сортування по підпапках", "Auto-sort into subfolders");
        RuleActZip.Content = GetLoc(lang, "Стискати нові файли у ZIP", "Compress new files to ZIP");
        RuleActExtract.Content = GetLoc(lang, "Авто-розпакування архівів", "Auto-extract archives");
        RuleActPng.Content = GetLoc(lang, "Конвертувати фото у PNG", "Convert images to PNG");
        RuleActJpg.Content = GetLoc(lang, "Конвертувати фото у JPG", "Convert images to JPG");
        RuleActWebp.Content = GetLoc(lang, "Конвертувати фото у WEBP", "Convert images to WEBP");
        RuleActPdf.Content = GetLoc(lang, "Конвертувати у PDF", "Convert to PDF");
        RuleActCompressImg.Content = GetLoc(lang, "Стискати нові зображення", "Compress new images");
        RuleActCleanTemp.Content = GetLoc(lang, "Очищення тимчасових файлів", "Clean temporary files");

        BtnQuickAddRule.Content = GetLoc(lang, "➕  Додати правило", "➕  Add Rule");
        TxtTargetFolderLabel.Text = GetLoc(lang, "Папка збереження (опційно):", "Output folder (optional):");
        TxtCheckSubdirs.Text = GetLoc(lang, "Включаючи підпапки", "Include subfolders");
        TxtCheckDeleteSource.Text = GetLoc(lang, "Видаляти оригінал", "Delete source file");

        if (EmptyFoldersTitleText != null)
            EmptyFoldersTitleText.Text = GetLoc(lang, "Немає активних правил відстеження", "No active watched folder rules");
        if (EmptyFoldersDescText != null)
            EmptyFoldersDescText.Text = GetLoc(lang,
                "Додайте каталог вище через «Додати правило», щоб Halo автоматично сортувала або конвертувала нові файли.",
                "Add a directory above via 'Add Rule' so Halo can automatically sort or convert new files.");

        // Screen 5: Settings & Dependencies
        if (SettingsTabParams != null) SettingsTabParams.Content = GetLoc(lang, "⚙️  Параметри системи", "⚙️  System Settings");
        if (SettingsTabGuide != null) SettingsTabGuide.Content = GetLoc(lang, "📖  Інструкція та Гід Halo", "📖  Halo User Guide");

        if (TxtGuideBadge != null) TxtGuideBadge.Text = GetLoc(lang, "✦  ІНСТРУКЦІЯ ТА ГІД КОРИСТУВАЧА", "✦  USER GUIDE & MANUAL");
        if (TxtGuideTitle != null) TxtGuideTitle.Text = GetLoc(lang, "Як ефективно працювати з Halo", "Getting the Most Out of Halo");
        if (TxtGuideDesc != null) TxtGuideDesc.Text = GetLoc(lang,
            "Повний огляд можливостей: швидкий виклик меню, клавіші режимів, автоматичне сортування папок та полиця файлів.",
            "Full feature walkthrough: radial menu invocation, modifier keys, folder auto-sorting, and file shelf.");
        if (TxtGuideS1Title != null) TxtGuideS1Title.Text = GetLoc(lang, "1. Способи виклику меню Halo", "1. Ways to Summon Halo Menu");
        if (TxtGuideS1Sub != null) TxtGuideS1Sub.Text = GetLoc(lang, "Миттєвий доступ до операцій з будь-якого вікна Windows", "Instant access to file actions from any Windows window");
        if (TxtGuideBadgeNav != null) TxtGuideBadgeNav.Text = GetLoc(lang, "Навігація", "Navigation");
        if (TxtGuideHotkeysTitle != null) TxtGuideHotkeysTitle.Text = GetLoc(lang, "⌨️ Гаряча комбінація клавіш", "⌨️ Global Hotkey");
        if (TxtGuideHotkeysDesc != null) TxtGuideHotkeysDesc.Text = GetLoc(lang, "Натисніть Alt + Space (або власну комбінацію) над будь-яким виділеним файлом чи у вікні Провідника.", "Press Alt + Space (or your custom shortcut) over any selected file or inside Windows File Explorer.");
        if (TxtGuideShakeTitle != null) TxtGuideShakeTitle.Text = GetLoc(lang, "🌀 Жест струшування (Shake)", "🌀 Shake Gesture");
        if (TxtGuideShakeDesc != null) TxtGuideShakeDesc.Text = GetLoc(lang, "Затисніть файл лівою кнопкою миші та швидко струсніть курсором — меню одразу відкриється біля файлу.", "Grab a file with the left mouse button and shake the cursor quickly — the menu opens right by the file.");
        if (TxtGuideMiddleMouseTitle != null) TxtGuideMiddleMouseTitle.Text = GetLoc(lang, "🖱️ Середня кнопка миші", "🖱️ Middle Mouse Click");
        if (TxtGuideMiddleMouseDesc != null) TxtGuideMiddleMouseDesc.Text = GetLoc(lang, "Одне клацання коліщатком миші над обраним елементом одразу відкриває коло дій.", "Click the mouse wheel once over a selected file or folder to instantly open the radial actions ring.");
        if (TxtGuideExplorerTitle != null) TxtGuideExplorerTitle.Text = GetLoc(lang, "📁 Меню Провідника Windows", "📁 Windows Explorer Context Menu");
        if (TxtGuideExplorerDesc != null) TxtGuideExplorerDesc.Text = GetLoc(lang, "Клацніть правою кнопкою миші на файлі чи папці та виберіть «Відкрити в Halo».", "Right-click any file or folder and select 'Open in Halo' from the context menu.");

        if (TxtGuideS2Title != null) TxtGuideS2Title.Text = GetLoc(lang, "2. Три режими роботи меню (Модифікатори Shift / Ctrl / Alt)", "2. Three Menu Modes (Shift / Ctrl / Alt Modifiers)");
        if (TxtGuideS2Sub != null) TxtGuideS2Sub.Text = GetLoc(lang, "Миттєве перемикання функціоналу прямо під час відкритого меню", "Switch sector capabilities in real time while menu is open");
        if (TxtGuideBadgeKeys != null) TxtGuideBadgeKeys.Text = GetLoc(lang, "Клавіші", "Modifiers");
        if (TxtGuideModeShiftTitle != null) TxtGuideModeShiftTitle.Text = GetLoc(lang, "⚡ Конвертація форматів", "⚡ Format Conversions");
        if (TxtGuideModeShiftDesc != null) TxtGuideModeShiftDesc.Text = GetLoc(lang, "Миттєве перетворення: зображення (JPG, PNG, WebP, AVIF, TIFF, ICO), документи у PDF або DOCX, аудіо та відео формати.", "Instant conversion: images (JPG, PNG, WebP, AVIF, TIFF, ICO), documents to PDF or DOCX, audio and video formats.");
        if (TxtGuideModeCtrlTitle != null) TxtGuideModeCtrlTitle.Text = GetLoc(lang, "🛠️ Професійні інструменти", "🛠️ Category Tools");
        if (TxtGuideModeCtrlDesc != null) TxtGuideModeCtrlDesc.Text = GetLoc(lang, "Стиснення без втрати якості, розпізнавання тексту OCR, обрізка відео, видалення дублікатів з Excel/CSV, транспонування та сортування таблиць.", "Lossless compression, OCR text extraction, video trimming, Excel/CSV deduplication, table transpose and sorting.");
        if (TxtGuideModeAltTitle != null) TxtGuideModeAltTitle.Text = GetLoc(lang, "🚀 Швидкі системні дії", "🚀 Quick System Actions");
        if (TxtGuideModeAltDesc != null) TxtGuideModeAltDesc.Text = GetLoc(lang, "Копіювання абсолютного шляху, запуск PowerShell у каталозі файлу, переміщення/копіювання у призначені папки, закріплення на Полицю.", "Copy full path, launch PowerShell in directory, move/copy to presets, pin to File Shelf.");

        if (TxtGuideS3Title != null) TxtGuideS3Title.Text = GetLoc(lang, "3. Автоматизація папок та Журнал операцій", "3. Folder Automation & Operations Log");
        if (TxtGuideS3Sub != null) TxtGuideS3Sub.Text = GetLoc(lang, "Фонова обробка завантажень із детальним журналом дій", "Safe background processing of downloads with detailed activity log");
        if (TxtGuideBadgeAuto != null) TxtGuideBadgeAuto.Text = GetLoc(lang, "Автоматизація", "Automation");
        if (TxtGuideAutoSortTitle != null) TxtGuideAutoSortTitle.Text = GetLoc(lang, "🤖 Розумне сортування файлів", "🤖 Smart Folder Auto-Sorting");
        if (TxtGuideAutoSortDesc != null) TxtGuideAutoSortDesc.Text = GetLoc(lang, "Halo автоматично слідкує за вибраними папками (наприклад, Завантаження) і самостійно розкладає файли у підпапки «Документи», «Зображення», «Відео», «Архіви».", "Halo monitors designated folders (e.g. Downloads) and auto-sorts files into Documents, Images, Videos, Archives subfolders.");
        if (TxtGuideLogTitle != null) TxtGuideLogTitle.Text = GetLoc(lang, "📜 Журнал дій у реальному часі", "📜 Real-Time Operations Log");
        if (TxtGuideLogDesc != null) TxtGuideLogDesc.Text = GetLoc(lang, "Усі переміщення, автоматичні сортування та перетворення файлів миттєво фіксуються в журналі операцій на Головній, щоб ви завжди знали точний час та статус обробки кожного файлу.", "All file moves, automatic sorting, and conversions are instantly recorded in the Dashboard history log so you always know processing status and timestamp.");

        if (TxtGuideS4Title != null) TxtGuideS4Title.Text = GetLoc(lang, "4. Полиця файлів (File Shelf — швидкий буфер)", "4. File Shelf (Fast Staging Buffer)");
        if (TxtGuideS4Sub != null) TxtGuideS4Sub.Text = GetLoc(lang, "Компактна плаваюча панель для тимчасового закріплення документів та фото", "Floating dock for pinning files and drag-and-drop workflows");
        if (TxtGuideBadgeBuffer != null) TxtGuideBadgeBuffer.Text = GetLoc(lang, "Буфер", "Buffer");
        if (TxtGuideShelfWhatTitle != null) TxtGuideShelfWhatTitle.Text = GetLoc(lang, "🎯 Що це таке?", "🎯 What is the Shelf?");
        if (TxtGuideShelfWhatDesc != null) TxtGuideShelfWhatDesc.Text = GetLoc(lang, "Полиця — це міні-панель Halo поверх усіх вікон. Вона виступає швидким тимчасовим буфером, куди зручно відкласти файли під час активної роботи, щоб не шукати їх знову.", "The Shelf is a floating Halo mini-dock on top of all windows. It serves as a fast staging buffer to set aside working files without losing track of them.");
        if (TxtGuideShelfHowTitle != null) TxtGuideShelfHowTitle.Text = GetLoc(lang, "📌 Як закріпити файли?", "📌 How to Pin Files?");
        if (TxtGuideShelfHowDesc != null) TxtGuideShelfHowDesc.Text = GetLoc(lang, "• Меню дій: відкрийте меню Halo над файлом, затисніть Alt і виберіть «Покласти на полицю».\n• Drag-and-Drop: перетягніть файли мишкою на панель Полиці.\n• Трей: клацніть «Полиця файлів» у треї.", "• Actions menu: open Halo over a file, hold Alt and choose 'Add to Shelf'.\n• Drag-and-Drop: drag files directly into the Shelf dock.\n• Tray: click 'File Shelf' in system tray.");
        if (TxtGuideShelfWhyTitle != null) TxtGuideShelfWhyTitle.Text = GetLoc(lang, "⚡ Зручні сценарії", "⚡ Convenient Workflows");
        if (TxtGuideShelfWhyDesc != null) TxtGuideShelfWhyDesc.Text = GetLoc(lang, "• Перетягуйте файли з Полиці у браузер, пошту (Outlook/Gmail) чи Telegram/Slack.\n• Швидке відкриття файлу або папки в один клік.\n• Очищайте поличку після завершення задачі.", "• Drag files from Shelf into browser, email (Outlook/Gmail), or Telegram/Slack.\n• Fast 1-click open of files or containing folders.\n• Clear shelf items when your task is done.");

        if (TxtGuideS5Title != null) TxtGuideS5Title.Text = GetLoc(lang, "5. Ігровий режим (Game Mode — режим повної тиші)", "5. Game Mode (Silent Mode)");
        if (TxtGuideS5Sub != null) TxtGuideS5Sub.Text = GetLoc(lang, "Миттєве призупинення глобальних перехоплень для ігор і 3D-програм", "Instant pause of all shortcuts and gestures for games and 3D apps");
        if (TxtGuideBadgePause != null) TxtGuideBadgePause.Text = GetLoc(lang, "Пауза", "Pause");
        if (TxtGuideGameModeDesc != null) TxtGuideGameModeDesc.Text = GetLoc(lang, "Тимчасово вимикає всі тригери миші, жести струшування та перехоплення комбінацій клавіш, щоб уникнути будь-яких випадкових спрацьовувань під час відеоігор або роботи в Unreal Engine, Blender та 3ds Max. Вмикається миттєво через вікно у системному треї або гарячою комбінацією (за замовчуванням Alt+G).", "Temporarily disables all mouse triggers, shake gestures, and hotkey hooks to prevent accidental activations during gaming or work in Unreal Engine, Blender, and 3ds Max. Toggle instantly via system tray or the Alt+G shortcut.");

        TxtSettingsDepsTitle.Text = GetLoc(lang,
            "Додаткові програми (Встановлення через PowerShell)",
            "External Dependencies (PowerShell Installer)");
        TxtSettingsDepsSub.Text = GetLoc(lang,
            "Автоматичне встановлення інструментів через PowerShell за вашою ініціативою (без відкриття браузера)",
            "User-initiated installation directly via PowerShell (without opening browser web pages)");
        BtnRefreshDeps.Content = GetLoc(lang, "↺  Перевірити статус", "↺  Check Status");

        TxtDepPandocTitle.Text = GetLoc(lang, "Pandoc (Документи та Markdown)", "Pandoc (Documents & Markdown)");
        TxtDepPandocDesc.Text = GetLoc(lang,
            "Рушій для розширеної конвертації документів, PDF, ePub, HTML та Markdown. Встановлюється напряму через PowerShell.",
            "Engine for advanced document conversion, PDF, ePub, HTML, and Markdown. Installed directly via PowerShell.");
        TxtDepFfmpegTitle.Text = GetLoc(lang, "FFmpeg (Відео та Аудіо)", "FFmpeg (Video & Audio)");
        TxtDepFfmpegDesc.Text = GetLoc(lang,
            "Необхідний для конвертації та обробки відео (MP4, MOV, MKV, WEBM, GIF, MP3). Встановлюється напряму через PowerShell.",
            "Required for video conversion and tools (MP4, MOV, MKV, WEBM, GIF, MP3). Installed directly via PowerShell.");
        TxtDepLibreTitle.Text = GetLoc(lang, "LibreOffice (Офісні формати)", "LibreOffice (Office Formats)");
        TxtDepLibreDesc.Text = GetLoc(lang,
            "Рушій для офісних форматів (DOC, PPTX, ODS, ODP) та точного експорту PDF. Встановлюється напряму через PowerShell.",
            "Engine for office formats (DOC, PPTX, ODS, ODP) and high-fidelity PDF export. Installed directly via PowerShell.");

        TxtSettingsLangBadge.Text = GetLoc(lang, "Локалізація", "Localization");
        TxtSettingsLangTitle.Text = GetLoc(lang, "Мова інтерфейсу", "Interface Language");
        TxtSettingsLangSub.Text = GetLoc(lang, "Миттєва зміна мови всіх меню та підписів", "Instantly switch the language of all menus and labels");
        LangItemUk.Content = GetLoc(lang, "Українська (Основна)", "Ukrainian (Українська)");
        LangItemEn.Content = "English (US)";

        TxtSettingsThemeBadge.Text = GetLoc(lang, "Оформлення", "Appearance");
        TxtSettingsThemeTitle.Text = GetLoc(lang, "Кольорова тема Halo", "Halo Color Theme");
        TxtSettingsThemeSub.Text = GetLoc(lang, "Налаштування палітри вікна та карток у реальному часі", "Real-time window and card palette customization");
        ThemeItemDark.Content = GetLoc(lang, "Темна", "Dark");
        ThemeItemIndigo.Content = GetLoc(lang, "Глибоко-синя", "Deep Indigo");
        ThemeItemLight.Content = GetLoc(lang, "Світла", "Light");

        TxtSettingsWinTitle.Text = GetLoc(lang, "Інтеграція з Windows", "Windows Integration");
        TxtAutostartTitle.Text = GetLoc(lang, "Запускати разом із Windows", "Launch with Windows");
        TxtAutostartDesc.Text = GetLoc(lang, "Автоматичний старт програми при вході в систему", "Automatically start Halo when signing in to Windows");
        TxtExplorerMenuTitle.Text = GetLoc(lang, "Контекстне меню провідника Windows", "Windows Explorer Context Menu");
        TxtExplorerMenuDesc.Text = GetLoc(lang, "Додати пункт «Відкрити в Halo» при натисканні правою кнопкою миші на файлі чи папці", "Add 'Open in Halo' item when right-clicking a file or folder");

        TxtSettingsBehaviorTitle.Text = GetLoc(lang, "Поведінка та швидкодія", "Behavior & Performance");
        TxtAutoHideTitle.Text = GetLoc(lang, "Автоматично ховати меню після дії", "Auto-hide menu after action");
        TxtAutoHideDesc.Text = GetLoc(lang, "Закривати радіальне коло одразу після обробки файлу", "Close the radial wheel immediately after processing a file");
        TxtSmoothAnimTitle.Text = GetLoc(lang, "Плавні анімації переходів та секторів", "Smooth transitions & sector animations");
        TxtSmoothAnimDesc.Text = GetLoc(lang, "Анімація появи меню та перемикання вкладок", "Animate menu appearance and tab transitions");

        UpdateGameModeVisualState();
        RefreshDashboardStats();
        RefreshDependenciesStatus();
        UpdateCategorySectors();
        LoadFoldersData();
        _shelfWindow?.ApplyLocalization();

        if (ToggleExplorerMenu != null && ToggleExplorerMenu.IsChecked == true)
        {
            WindowsIntegrationHelper.SetExplorerContextMenu(true, GetLoc(lang, "Відкрити в Halo", "Open in Halo"));
        }

        // Update current header text without re-triggering page fade
        if (NavMain.IsChecked == true) Nav_Checked(NavMain, new RoutedEventArgs());
        else if (NavRadial.IsChecked == true) Nav_Checked(NavRadial, new RoutedEventArgs());
        else if (NavInput.IsChecked == true) Nav_Checked(NavInput, new RoutedEventArgs());
        else if (NavFolders.IsChecked == true) Nav_Checked(NavFolders, new RoutedEventArgs());
        else if (NavSettings.IsChecked == true) Nav_Checked(NavSettings, new RoutedEventArgs());
    }

    private static string GetLoc(string lang, string uk, string en)
    {
        return string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? en : uk;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
        _trayIcon?.Dispose();
        _gestureDetector?.Dispose();
        _hotkeyHelper?.Dispose();
        FolderWatcherService.Instance.Stop();
        _radialWindow?.Close();
        _shelfWindow?.Close();
    }
}
