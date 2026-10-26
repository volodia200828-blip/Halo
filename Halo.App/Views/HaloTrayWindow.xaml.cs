using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Halo.App.Services;
using Halo.Core.Services;

namespace Halo.App.Views;

public partial class HaloTrayWindow : Window
{
    public event Action? OpenMainWindowRequested;
    public event Action? OpenShelfRequested;
    public event Action? OpenSettingsRequested;
    public event Action? ToggleGameModeRequested;
    public event Action? ExitAppRequested;

    public HaloTrayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Deactivated += OnDeactivated;
        UpdateLocalization();
        ThemeManager.Instance.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(string _)
    {
        Dispatcher.Invoke(RefreshStatusDisplay);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Position window cleanly above the taskbar in the bottom-right corner
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(10, workArea.Right - Width - 14);
        Top = Math.Max(10, workArea.Bottom - ActualHeight - 14);

        // Slide up + Fade in animation
        Opacity = 0;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);

        RefreshStatusDisplay();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        CloseWindow();
    }

    public void UpdateLocalization()
    {
        bool isEn = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);

        TxtTrayTagline.Text = isEn ? "Desktop file productivity assistant" : "Швидкий асистент роботи з файлами";
        TxtGameModeTitle.Text = isEn ? "Game Mode (Silent)" : "Ігровий режим";
        TxtGameModeExplanation.Text = isEn
            ? "Temporarily pauses mouse gestures and shortcuts so Halo stays completely silent during games or 3D work."
            : "Призупиняє всі тригери миші та гарячі клавіші, щоб програма не заважала під час ігор або роботи у 3D-редакторах.";

        TxtNavDashboardTitle.Text = isEn ? "Halo Dashboard" : "Головне вікно Halo";
        TxtNavDashboardSub.Text = isEn ? "Interactive hub, converter, and quick actions" : "Інтерактивний хаб, конвертер та швидкі дії";

        TxtNavShelfTitle.Text = isEn ? "File Shelf" : "Полиця файлів (File Shelf)";
        TxtNavShelfSub.Text = isEn ? "Floating temporary dock for pinned files" : "Тимчасовий буфер закріплених документів і фото";

        TxtNavSettingsTitle.Text = isEn ? "Settings & User Guide" : "Налаштування та Інструкція";
        TxtNavSettingsSub.Text = isEn ? "Gesture options, watched folders, and manual" : "Параметри жестів, автосортування та гід";

        BtnExitHalo.Content = isEn ? "✕  Exit Halo" : "✕  Вийти з Halo";

        RefreshStatusDisplay();
    }

    public void RefreshStatusDisplay()
    {
        var settings = SettingsService.Instance.Current;
        bool isGame = settings.IsGameModeEnabled;
        bool isEn = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);

        CheckGameMode.IsChecked = isGame;

        if (isGame)
        {
            StatusDot.Fill = (Brush)FindResource("OrbitRoseLightBrush");
            TxtStatusLabel.Text = isEn ? "Game Mode (Paused)" : "Ігровий режим (Пауза)";
            TxtHotkeyBadge.Text = isEn ? "Paused" : "Пауза";
            TxtHotkeyBadge.Foreground = (Brush)FindResource("OrbitRoseLightBrush");
            if (TrayHotkeyBadgeBorder != null)
            {
                TrayHotkeyBadgeBorder.Background = (Brush)FindResource("OrbitRoseSoftBg");
            }

            // Active Game Mode pattern (vibrant rose soft background & border)
            GameModeIconBox.Background = (Brush)FindResource("OrbitRoseSoftBg");
            GameModeIconBox.BorderThickness = new Thickness(0);
            TxtGameModeEmoji.Opacity = 1.0;
            TxtGameModeTitle.Foreground = (Brush)FindResource("OrbitTextPrimary");
        }
        else
        {
            StatusDot.Fill = (Brush)FindResource("OrbitGreenLightBrush");
            TxtStatusLabel.Text = isEn ? "Background service active" : "Служба активна";
            string hk = !string.IsNullOrWhiteSpace(settings.GlobalHotkey) ? settings.GlobalHotkey : "Alt+Space";
            TxtHotkeyBadge.Text = hk;
            TxtHotkeyBadge.Foreground = (Brush)FindResource("OrbitPurpleLightBrush");
            if (TrayHotkeyBadgeBorder != null)
            {
                TrayHotkeyBadgeBorder.Background = (Brush)FindResource("OrbitPurpleSoftBg");
            }

            // Inactive Game Mode pattern (predictable, muted, subtle and readable)
            GameModeIconBox.Background = (Brush)FindResource("OrbitToggleTrackOffBrush");
            GameModeIconBox.BorderThickness = new Thickness(0);
            TxtGameModeEmoji.Opacity = 0.55;
            TxtGameModeTitle.Foreground = (Brush)FindResource("OrbitTextSecondary");
        }
    }

    private void CheckGameMode_Click(object sender, RoutedEventArgs e)
    {
        ToggleGameModeRequested?.Invoke();
        RefreshStatusDisplay();
    }

    private void BtnDashboard_Click(object sender, MouseButtonEventArgs e)
    {
        OpenMainWindowRequested?.Invoke();
        CloseWindow();
    }

    private void BtnShelf_Click(object sender, MouseButtonEventArgs e)
    {
        OpenShelfRequested?.Invoke();
        CloseWindow();
    }

    private void BtnSettings_Click(object sender, MouseButtonEventArgs e)
    {
        OpenSettingsRequested?.Invoke();
        CloseWindow();
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        CloseWindow();
        ExitAppRequested?.Invoke();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseWindow();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseWindow();
        }
    }

    private void CloseWindow()
    {
        try
        {
            ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
            Deactivated -= OnDeactivated;
            Close();
        }
        catch { }
    }
}