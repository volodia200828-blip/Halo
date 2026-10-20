using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    public event Action<string>? AddToShelfRequested;

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
            if (IsVisible && _currentFilePath != null)
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

        UpdateCenterHubInfo();
        RebuildRadialMenu();

        // Position MenuCanvas centered at screenPoint
        var settings = SettingsService.Instance.Current;
        double radius = settings.MenuRadius;
        double diameter = (radius + 20) * 2;

        // Ensure canvas stays within screen boundaries
        double left = Math.Max(20, Math.Min(SystemParameters.PrimaryScreenWidth - diameter - 20, screenPoint.X - radius - 20));
        double top = Math.Max(20, Math.Min(SystemParameters.PrimaryScreenHeight - diameter - 20, screenPoint.Y - radius - 20));

        Canvas.SetLeft(MenuCanvas, left);
        Canvas.SetTop(MenuCanvas, top);

        Show();
        Activate();

        var anim = (Storyboard)FindResource("EntranceAnimation");
        anim.Begin();
    }

    private void UpdateCenterHubInfo()
    {
        var settings = SettingsService.Instance.Current;
        double innerRadius = settings.InnerRadius;
        double hubSize = innerRadius * 2 - 10;

        CenterHub.Width = hubSize;
        CenterHub.Height = hubSize;

        double centerOffset = settings.MenuRadius + 20;
        Canvas.SetLeft(CenterHub, centerOffset - hubSize / 2.0);
        Canvas.SetTop(CenterHub, centerOffset - hubSize / 2.0);

        if (!string.IsNullOrEmpty(_currentFilePath) && (File.Exists(_currentFilePath) || Directory.Exists(_currentFilePath)))
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

            CenterFileIcon.Text = _currentCategory switch
            {
                FileTypeCategory.Image => "🖼️",
                FileTypeCategory.Video => "🎬",
                FileTypeCategory.Audio => "🎵",
                FileTypeCategory.Document => "📄",
                FileTypeCategory.Folder => "📁",
                _ => "📦"
            };
        }
        else
        {
            CenterFileName.Text = "Скиньте файл";
            CenterFileInfo.Text = "або виділіть";
            CenterFileIcon.Text = "🎯";
        }
    }

    private void RebuildRadialMenu()
    {
        SectorsHost.Children.Clear();

        var settings = SettingsService.Instance.Current;
        var activeActions = settings.Actions
            .Where(a => a.IsEnabled && (a.SupportedFileTypes.Contains(_currentCategory) || a.SupportedFileTypes.Contains(FileTypeCategory.Generic)))
            .OrderBy(a => a.Order)
            .ToList();

        if (activeActions.Count == 0)
        {
            // Fallback: at least show system settings
            activeActions = settings.Actions.Where(a => a.Id == "system_open_settings").ToList();
        }

        int count = activeActions.Count;
        double gapDeg = count > 1 ? 2.5 : 0;
        double totalGap = gapDeg * count;
        double sectorSpan = (360.0 - totalGap) / count;

        double centerOffset = settings.MenuRadius + 20;
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
                settings.ShowLabelsOnSectors);

            sector.ActionTriggered += OnActionSelected;
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

        if (action.Id == "system_shelf_add")
        {
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                AddToShelfRequested?.Invoke(_currentFilePath);
                ShowNotification("📌 Додано на поличку");
            }
            return;
        }

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            ShowNotification("⚠️ Не вибрано файл для обробки");
            return;
        }

        if (action.Id == "system_copy_path")
        {
            Clipboard.SetText(_currentFilePath);
            ShowNotification("📋 Шлях скопійовано у буфер");
            ScheduleHide();
            return;
        }

        ShowNotification($"⏳ Виконується: {action.Title}...");

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
                Interval = TimeSpan.FromMilliseconds(700)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Hide();
            };
            timer.Start();
        }
    }

    private void ShowNotification(string message)
    {
        NotificationText.Text = message;

        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180));
        NotificationPill.BeginAnimation(OpacityProperty, fadeIn);

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(300));
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
        // Click outside menu dismisses it
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
                UpdateCenterHubInfo();
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
