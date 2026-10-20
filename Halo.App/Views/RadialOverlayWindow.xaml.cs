using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Halo.App.Controls;
using Halo.Core.Models;
using Halo.Core.Services;

namespace Halo.App.Views;

public partial class RadialOverlayWindow : Window
{
    private string? _currentFilePath;
    private FileTypeCategory _currentCategory = FileTypeCategory.Generic;

    public event Action? OpenSettingsRequested;

    public RadialOverlayWindow()
    {
        InitializeComponent();
        Loaded += (s, e) => SettingsService.Instance.SettingsChanged += OnSettingsChanged;
        Unloaded += (s, e) => SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        Dispatcher.Invoke(() =>
        {
            if (IsVisible)
            {
                RebuildRadialMenu();
            }
        });
    }

    public void ShowAt(Point screenPoint, string? filePath = null)
    {
        _currentFilePath = filePath;
        _currentCategory = !string.IsNullOrEmpty(filePath)
            ? FileTypeDetector.Detect(filePath)
            : FileTypeCategory.Generic;

        UpdateCenterHubInfo(null);
        RebuildRadialMenu();

        var settings = SettingsService.Instance.Current;
        double radius = settings.MenuRadius;
        double diameter = (radius + 24) * 2;

        double left = Math.Max(24, Math.Min(SystemParameters.PrimaryScreenWidth - diameter - 24, screenPoint.X - radius - 24));
        double top = Math.Max(24, Math.Min(SystemParameters.PrimaryScreenHeight - diameter - 60, screenPoint.Y - radius - 24));

        MenuCanvas.Width = diameter;
        MenuCanvas.Height = diameter + 48;

        Canvas.SetLeft(MenuCanvas, Math.Round(left));
        Canvas.SetTop(MenuCanvas, Math.Round(top));

        Show();
        Activate();

        if (settings.SmoothAnimations)
        {
            var anim = (Storyboard)FindResource("EntranceAnimation");
            anim.Begin();
        }
    }

    private void UpdateCenterHubInfo(ActionItem? hoveredAction)
    {
        var settings = SettingsService.Instance.Current;
        double innerRadius = settings.InnerRadius;
        double hubSize = innerRadius * 2 - 12;

        CenterHub.Width = hubSize;
        CenterHub.Height = hubSize;

        double centerOffset = settings.MenuRadius + 24;
        Canvas.SetLeft(CenterHub, Math.Round(centerOffset - hubSize / 2.0));
        Canvas.SetTop(CenterHub, Math.Round(centerOffset - hubSize / 2.0));

        // Position AmbientBackdropRing
        double ringSize = (settings.MenuRadius + 14) * 2;
        AmbientBackdropRing.Width = ringSize;
        AmbientBackdropRing.Height = ringSize;
        Canvas.SetLeft(AmbientBackdropRing, Math.Round(centerOffset - ringSize / 2.0));
        Canvas.SetTop(AmbientBackdropRing, Math.Round(centerOffset - ringSize / 2.0));

        if (hoveredAction != null)
        {
            CenterFileIcon.Text = hoveredAction.IconKey;
            CenterFileName.Text = hoveredAction.Title;
            CenterFileInfo.Text = "Клік для запуску";
            CenterFileInfo.Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250));

            BottomHintText.Text = $"{hoveredAction.Title} — {hoveredAction.Description}";
            BottomHintText.Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247));
            BottomHintPill.BorderBrush = new SolidColorBrush(Color.FromRgb(109, 90, 230));
        }
        else if (!string.IsNullOrEmpty(_currentFilePath) && (File.Exists(_currentFilePath) || Directory.Exists(_currentFilePath)))
        {
            var fileName = Path.GetFileName(_currentFilePath);
            CenterFileName.Text = string.IsNullOrEmpty(fileName) ? _currentFilePath : fileName;

            if (File.Exists(_currentFilePath))
            {
                var fi = new FileInfo(_currentFilePath);
                CenterFileInfo.Text = FormatFileSize(fi.Length);
            }
            else
            {
                CenterFileInfo.Text = "Папка";
            }
            CenterFileInfo.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));

            CenterFileIcon.Text = _currentCategory switch
            {
                FileTypeCategory.Image => "🖼️",
                FileTypeCategory.Video => "🎬",
                FileTypeCategory.Audio => "🎵",
                FileTypeCategory.Document => "📄",
                FileTypeCategory.Folder => "📁",
                _ => "📦"
            };

            BottomHintText.Text = $"Файл: {CenterFileName.Text} ({CenterFileInfo.Text})";
            BottomHintText.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));
            BottomHintPill.BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 82));
        }
        else
        {
            CenterFileIcon.Text = "◎";
            CenterFileName.Text = "Halo";
            CenterFileInfo.Text = "Виберіть дію";
            CenterFileInfo.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));

            BottomHintText.Text = "Перетягніть сюди файл або оберіть сектор";
            BottomHintText.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));
            BottomHintPill.BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 82));
        }

        PositionBottomPill(centerOffset, settings.MenuRadius);
    }

    private void PositionBottomPill(double centerOffset, double radius)
    {
        BottomHintPill.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double pillW = BottomHintPill.DesiredSize.Width > 0 ? BottomHintPill.DesiredSize.Width : 240;
        Canvas.SetLeft(BottomHintPill, Math.Round(centerOffset - pillW / 2.0));
        Canvas.SetTop(BottomHintPill, Math.Round(centerOffset + radius + 22));
    }

    private void RebuildRadialMenu()
    {
        SectorsHost.Children.Clear();

        var settings = SettingsService.Instance.Current;
        var activeActions = settings.Actions
            .Where(a => a.IsEnabled && a.Id != "system_shelf_add" && (a.SupportedFileTypes.Contains(_currentCategory) || a.SupportedFileTypes.Contains(FileTypeCategory.Generic)))
            .OrderBy(a => a.Order)
            .ToList();

        if (activeActions.Count == 0)
        {
            activeActions = settings.Actions.Where(a => a.Id == "system_open_settings").ToList();
        }

        int count = activeActions.Count;
        double gapDeg = count > 1 ? 3.2 : 0;
        double totalGap = gapDeg * count;
        double sectorSpan = (360.0 - totalGap) / count;

        double centerOffset = settings.MenuRadius + 24;
        Point center = new Point(centerOffset, centerOffset);

        for (int i = 0; i < count; i++)
        {
            double startAngle = i * (sectorSpan + gapDeg);
            double endAngle = startAngle + sectorSpan;

            var sector = new RadialSectorButton(
                center,
                settings.InnerRadius,
                settings.MenuRadius,
                startAngle,
                endAngle,
                activeActions[i],
                settings.ShowLabelsOnSectors,
                count);

            sector.ActionTriggered += OnActionSelected;
            sector.SectorHovered += UpdateCenterHubInfo;
            SectorsHost.Children.Add(sector);
        }
    }

    private async void OnActionSelected(ActionItem action)
    {
        if (action.Id == "system_open_settings")
        {
            Hide();
            OpenSettingsRequested?.Invoke();
            return;
        }

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            ShowNotification("Спочатку перетягніть або виділіть файл у Провіднику");
            return;
        }

        if (action.Id == "system_copy_path")
        {
            Clipboard.SetText(_currentFilePath);
            ShowNotification("📋 Шлях скопійовано у буфер обміну");
            ScheduleHide();
            return;
        }

        ShowNotification($"Виконується: {action.Title}...");

        var result = await ActionExecutor.ExecuteAsync(action, _currentFilePath);

        if (result.Success)
        {
            ShowNotification($"✅ {result.Message}");
            ScheduleHide();
        }
        else
        {
            ShowNotification($"❌ {result.Message}");
        }
    }

    private void ScheduleHide()
    {
        var settings = SettingsService.Instance.Current;
        if (settings.AutoHideOnAction)
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(750)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Hide();
            };
            timer.Start();
        }
    }

    public void ShowNotification(string message)
    {
        NotificationText.Text = message;

        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160));
        NotificationPill.BeginAnimation(OpacityProperty, fadeIn);

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.4)
        };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(260));
            NotificationPill.BeginAnimation(OpacityProperty, fadeOut);
        };
        timer.Start();
    }

    private void CenterHub_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Hide();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Hide();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
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
            if (files != null && files.Length > 0)
            {
                _currentFilePath = files[0];
                _currentCategory = FileTypeDetector.Detect(_currentFilePath);
                UpdateCenterHubInfo(null);
                RebuildRadialMenu();
            }
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
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
