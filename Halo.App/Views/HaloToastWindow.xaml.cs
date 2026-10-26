using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Halo.Core.Services;

namespace Halo.App.Views;

public partial class HaloToastWindow : Window
{
    private DispatcherTimer? _autoCloseTimer;
    private DispatcherTimer? _tickerTimer;
    private Action? _onClickAction;
    private DateTime _startTime = DateTime.UtcNow;
    private double _estimatedTotalSeconds = 0;

    public HaloToastWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Position at bottom-right corner of current work area (above taskbar)
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 16;

        // Slide up + Fade in animation
        Opacity = 0;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }

    public static HaloToastWindow ShowToast(string title, string message, double progress = -1, double durationSec = 4.0, Action? onClick = null)
    {
        var toast = new HaloToastWindow();
        toast.TxtToastTitle.Text = title;
        toast.TxtToastMessage.Text = message;
        toast._onClickAction = onClick;

        if (progress >= 0)
        {
            toast.ToastProgressBar.Visibility = Visibility.Visible;
            if (progress == 0)
            {
                toast.ToastProgressBar.IsIndeterminate = true;
            }
            else
            {
                toast.ToastProgressBar.IsIndeterminate = false;
                toast.ToastProgressBar.Value = Math.Min(100, progress);
            }
        }
        else
        {
            toast.ToastProgressBar.Visibility = Visibility.Collapsed;
        }

        toast.Show();

        if (durationSec > 0)
        {
            toast._autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(durationSec)
            };
            toast._autoCloseTimer.Tick += (s, e) =>
            {
                toast._autoCloseTimer?.Stop();
                toast.Dismiss();
            };
            toast._autoCloseTimer.Start();
        }

        return toast;
    }

    public static HaloToastWindow ShowProgressToast(string title, string message, double estimatedSeconds = 5.0, Action? onClick = null)
    {
        var toast = new HaloToastWindow();
        toast.TxtToastTitle.Text = title;
        toast.TxtToastMessage.Text = message;
        toast._onClickAction = onClick;
        toast._estimatedTotalSeconds = Math.Max(1.0, estimatedSeconds);
        toast._startTime = DateTime.UtcNow;

        toast.ToastProgressBar.Visibility = Visibility.Visible;
        toast.ToastProgressBar.IsIndeterminate = false;
        toast.ToastProgressBar.Value = 5;

        toast.TxtRemainingTime.Visibility = Visibility.Visible;
        toast.UpdateRemainingTimeDisplay(toast._estimatedTotalSeconds);

        toast._tickerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        toast._tickerTimer.Tick += (s, e) =>
        {
            var elapsed = (DateTime.UtcNow - toast._startTime).TotalSeconds;
            var remaining = Math.Max(0.2, toast._estimatedTotalSeconds - elapsed);
            if (toast.ToastProgressBar.Value < 92)
            {
                double calculatedProgress = Math.Min(92.0, (elapsed / toast._estimatedTotalSeconds) * 92.0);
                toast.ToastProgressBar.Value = Math.Max(toast.ToastProgressBar.Value, calculatedProgress);
            }
            toast.UpdateRemainingTimeDisplay(remaining);
        };
        toast._tickerTimer.Start();

        toast.Show();
        return toast;
    }

    public void Complete(string message, double autoCloseSec = 2.5)
    {
        Dispatcher.Invoke(() =>
        {
            _tickerTimer?.Stop();
            TxtToastMessage.Text = message;
            ToastProgressBar.Visibility = Visibility.Visible;
            ToastProgressBar.IsIndeterminate = false;
            ToastProgressBar.Value = 100;
            TxtRemainingTime.Visibility = Visibility.Visible;
            TxtRemainingTime.Text = FormatDoneText();
            TxtRemainingTime.Foreground = (System.Windows.Media.Brush)FindResource("OrbitGreenLightBrush");

            _autoCloseTimer?.Stop();
            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoCloseSec) };
            _autoCloseTimer.Tick += (_, _) =>
            {
                _autoCloseTimer?.Stop();
                Dismiss();
            };
            _autoCloseTimer.Start();
        });
    }

    public void Fail(string message, double autoCloseSec = 3.5)
    {
        Dispatcher.Invoke(() =>
        {
            _tickerTimer?.Stop();
            TxtToastMessage.Text = message;
            TxtRemainingTime.Visibility = Visibility.Visible;
            TxtRemainingTime.Text = "✕";
            TxtRemainingTime.Foreground = (System.Windows.Media.Brush)FindResource("OrbitRoseLightBrush");

            _autoCloseTimer?.Stop();
            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoCloseSec) };
            _autoCloseTimer.Tick += (_, _) =>
            {
                _autoCloseTimer?.Stop();
                Dismiss();
            };
            _autoCloseTimer.Start();
        });
    }

    public void UpdateProgress(double progress, string? statusMessage = null, double? remainingSec = null)
    {
        Dispatcher.Invoke(() =>
        {
            if (statusMessage != null)
            {
                TxtToastMessage.Text = statusMessage;
            }
            ToastProgressBar.Visibility = Visibility.Visible;
            if (progress <= 0)
            {
                ToastProgressBar.IsIndeterminate = true;
            }
            else
            {
                ToastProgressBar.IsIndeterminate = false;
                ToastProgressBar.Value = Math.Min(100, progress);
            }

            if (remainingSec.HasValue)
            {
                TxtRemainingTime.Visibility = Visibility.Visible;
                UpdateRemainingTimeDisplay(remainingSec.Value);
            }
            else if (progress > 5 && progress < 100)
            {
                var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
                var rem = (elapsed / progress) * (100.0 - progress);
                TxtRemainingTime.Visibility = Visibility.Visible;
                UpdateRemainingTimeDisplay(rem);
            }
        });
    }

    private void UpdateRemainingTimeDisplay(double remainingSec)
    {
        bool isEn = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        if (remainingSec <= 0.8)
        {
            TxtRemainingTime.Text = isEn ? "Finishing..." : "Завершення...";
        }
        else if (remainingSec < 60)
        {
            int s = (int)Math.Ceiling(remainingSec);
            TxtRemainingTime.Text = isEn ? $"~{s}s left" : $"Залишилось: ~{s} сек";
        }
        else
        {
            int m = (int)Math.Ceiling(remainingSec / 60.0);
            TxtRemainingTime.Text = isEn ? $"~{m}m left" : $"Залишилось: ~{m} хв";
        }
    }

    private string FormatDoneText()
    {
        bool isEn = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        return isEn ? "✓ Done" : "✓ Готово";
    }

    public void Dismiss()
    {
        _autoCloseTimer?.Stop();
        _tickerTimer?.Stop();
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
        fadeOut.Completed += (_, _) =>
        {
            try { Close(); } catch { }
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ToastRoot_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _onClickAction?.Invoke();
        Dismiss();
    }

    private void BtnCloseToast_Click(object sender, RoutedEventArgs e)
    {
        Dismiss();
    }
}
