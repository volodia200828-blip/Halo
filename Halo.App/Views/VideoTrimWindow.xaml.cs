using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Halo.Core.Models;
using Halo.Core.Services;

namespace Halo.App.Views;

public enum VideoProcessingState
{
    Ready,       // Ready for user editing
    Processing,  // Backend operation running; interactive controls locked, completion dialog blocked
    Completed,   // Backend 100% callback confirmed and output file verified; completion UI displayed
    Failed       // Error reported; interactive controls restored to retry
}

public partial class VideoTrimWindow : Window
{
    private readonly string _videoPath;
    private readonly DispatcherTimer _timer;
    private bool _isDraggingSlider = false;
    private bool _isMuted = false;
    private bool _isSyncingTime = false;
    private bool _isInitializing = true;
    private TimeSpan _totalDuration = TimeSpan.Zero;

    public VideoProcessingState ProcessingState { get; private set; } = VideoProcessingState.Ready;

    private static bool IsEn => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
    private static string T(string uk, string en) => IsEn ? en : uk;

    public VideoTrimWindow(string videoPath)
    {
        _isInitializing = true;
        InitializeComponent();
        _videoPath = videoPath;

        TxtVideoFileName.Text = Path.GetFileName(_videoPath);
        TxtWindowTitle.Text = T("Обрізка та редагування відео", "Video Trimmer & Editor");
        LblTrackStart.Text = T("▶ Початок кліпу:", "▶ Clip Start:");
        LblTrackEnd.Text = T("⏹ Кінець кліпу:", "⏹ Clip End:");
        LblStartTime.Text = T("Початок кліпу (гг:хх:сс):", "Clip Start (hh:mm:ss):");
        LblEndTime.Text = T("Кінець кліпу (гг:хх:сс):", "Clip End (hh:mm:ss):");
        BtnSetCurrentAsStart.Content = T("⏱ Поточний", "⏱ Current");
        BtnSetCurrentAsEnd.Content = T("⏱ Поточний", "⏱ Current");
        BtnCancel.Content = T("Скасувати", "Cancel");
        BtnTrimExecute.Content = T("✂️  Обрізати відео", "✂️  Trim Video");
        BtnPlayPause.Content = T("▶ Відтворити", "▶ Play");
        BtnMuteToggle.Content = T("🔊 Звук", "🔊 Sound");
        BtnJumpStart.Content = T("⏮ На початок", "⏮ To Start");

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += Timer_Tick;

        Loaded += VideoTrimWindow_Loaded;
        Closing += VideoTrimWindow_Closing;
        _isInitializing = false;
    }

    /// <summary>
    /// Single Source of Truth for video processing status management.
    /// Locks UI and prevents premature completion messages until 100% confirmation callback is received.
    /// </summary>
    private void SetProcessingState(VideoProcessingState newState, string? outputPath = null, string? errorMessage = null)
    {
        ProcessingState = newState;
        bool isProcessing = newState == VideoProcessingState.Processing;
        bool isReady = newState == VideoProcessingState.Ready || newState == VideoProcessingState.Failed;
        bool isCompleted = newState == VideoProcessingState.Completed;

        // Lock / Unlock interactive controls from the single source of truth
        if (SeekSlider != null) SeekSlider.IsEnabled = isReady;
        if (TrimStartSlider != null) TrimStartSlider.IsEnabled = isReady;
        if (TrimEndSlider != null) TrimEndSlider.IsEnabled = isReady;
        if (TxtStartTime != null) TxtStartTime.IsEnabled = isReady;
        if (TxtEndTime != null) TxtEndTime.IsEnabled = isReady;
        if (BtnSetCurrentAsStart != null) BtnSetCurrentAsStart.IsEnabled = isReady;
        if (BtnSetCurrentAsEnd != null) BtnSetCurrentAsEnd.IsEnabled = isReady;
        if (BtnPreset15s != null) BtnPreset15s.IsEnabled = isReady;
        if (BtnPreset30s != null) BtnPreset30s.IsEnabled = isReady;
        if (BtnPreset60s != null) BtnPreset60s.IsEnabled = isReady;
        if (BtnPresetFull != null) BtnPresetFull.IsEnabled = isReady;
        if (BtnPlayPause != null) BtnPlayPause.IsEnabled = isReady;
        if (BtnMuteToggle != null) BtnMuteToggle.IsEnabled = isReady;
        if (BtnJumpStart != null) BtnJumpStart.IsEnabled = isReady;
        if (BtnTopClose != null) BtnTopClose.IsEnabled = !isProcessing;
        if (BtnCancel != null) BtnCancel.IsEnabled = isReady;
        if (BtnTrimExecute != null)
        {
            BtnTrimExecute.IsEnabled = isReady;
            BtnTrimExecute.Content = isProcessing ? T("⏳ Обрізка...", "⏳ Trimming...") : T("✂️  Обрізати відео", "✂️  Trim Video");
        }

        if (isProcessing)
        {
            try { VideoPlayer.Pause(); } catch { }
            if (TrimProgressCard != null)
            {
                TrimProgressCard.Visibility = Visibility.Visible;
                TrimProgressCard.SetResourceReference(Border.BorderBrushProperty, "OrbitCyanBrush");
            }
            if (TxtTrimProgressStatus != null)
            {
                TxtTrimProgressStatus.Text = T("Триває безвтратна обрізка відео...", "Trimming video in progress...");
                TxtTrimProgressStatus.SetResourceReference(TextBlock.ForegroundProperty, "OrbitCyanBrush");
            }
            if (TxtTrimProgressRemaining != null)
            {
                TxtTrimProgressRemaining.Text = T("Обробка...", "Processing...");
                TxtTrimProgressRemaining.SetResourceReference(TextBlock.ForegroundProperty, "OrbitAmberLightBrush");
            }
            if (TrimProgressBar != null)
            {
                TrimProgressBar.Value = 10;
            }
        }
        else if (isCompleted)
        {
            if (TrimProgressCard != null)
            {
                TrimProgressCard.Visibility = Visibility.Visible;
                TrimProgressCard.SetResourceReference(Border.BorderBrushProperty, "OrbitGreenLightBrush");
            }
            if (TxtTrimProgressStatus != null)
            {
                TxtTrimProgressStatus.Text = T("Обрізку відео успішно завершено! (100%)", "Video trimming successfully completed! (100%)");
                TxtTrimProgressStatus.SetResourceReference(TextBlock.ForegroundProperty, "OrbitGreenLightBrush");
            }
            if (TxtTrimProgressRemaining != null)
            {
                TxtTrimProgressRemaining.Text = T("Готово ✓", "Done ✓");
                TxtTrimProgressRemaining.SetResourceReference(TextBlock.ForegroundProperty, "OrbitGreenLightBrush");
            }
            if (TrimProgressBar != null)
            {
                TrimProgressBar.Value = 100;
            }
        }
        else if (newState == VideoProcessingState.Failed)
        {
            if (TrimProgressCard != null)
            {
                TrimProgressCard.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void VideoTrimWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (File.Exists(_videoPath))
            {
                VideoPlayer.Source = new Uri(_videoPath, UriKind.Absolute);
                VideoPlayer.Play();
                _timer.Start();
                BtnPlayPause.Content = T("⏸ Пауза", "⏸ Pause");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load video in VideoTrimWindow: {ex.Message}");
        }
    }

    private void VideoTrimWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (ProcessingState == VideoProcessingState.Processing)
        {
            e.Cancel = true;
            return;
        }

        _timer.Stop();
        try
        {
            VideoPlayer.Stop();
            VideoPlayer.Close();
        }
        catch { }
    }

    private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (VideoPlayer.NaturalDuration.HasTimeSpan)
        {
            _totalDuration = VideoPlayer.NaturalDuration.TimeSpan;
            double totalSec = _totalDuration.TotalSeconds;

            SeekSlider.Maximum = totalSec;
            TrimStartSlider.Maximum = totalSec;
            TrimEndSlider.Maximum = totalSec;

            double defaultEndSec = Math.Min(15.0, totalSec);
            _isSyncingTime = true;
            try
            {
                TrimStartSlider.Value = 0;
                TrimEndSlider.Value = defaultEndSec;
                TxtTrimStartSeconds.Text = "0.0 с";
                TxtTrimEndSeconds.Text = $"{defaultEndSec:F1} с";
                TxtStartTime.Text = FormatTime(TimeSpan.Zero);
                TxtEndTime.Text = FormatTime(TimeSpan.FromSeconds(defaultEndSec));
            }
            finally
            {
                _isSyncingTime = false;
            }

            UpdateDurationBadge();
        }
    }

    private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Stop();
        BtnPlayPause.Content = T("▶ Відтворити", "▶ Play");
        PlayOverlay.Visibility = Visibility.Visible;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_isDraggingSlider && VideoPlayer.NaturalDuration.HasTimeSpan)
        {
            var pos = VideoPlayer.Position;
            SeekSlider.Value = pos.TotalSeconds;
            TxtCurrentPlaybackTime.Text = $"{FormatTime(pos)} / {FormatTime(_totalDuration)}";
        }
    }

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayPause();
    }

    private void VideoPlayer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        TogglePlayPause();
    }

    private void TogglePlayPause()
    {
        // Toggle play/pause
        if (BtnPlayPause.Content?.ToString()?.Contains("Пауза") == true || BtnPlayPause.Content?.ToString()?.Contains("Pause") == true)
        {
            VideoPlayer.Pause();
            BtnPlayPause.Content = T("▶ Відтворити", "▶ Play");
            PlayOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            VideoPlayer.Play();
            BtnPlayPause.Content = T("⏸ Пауза", "⏸ Pause");
            PlayOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnMuteToggle_Click(object sender, RoutedEventArgs e)
    {
        _isMuted = !_isMuted;
        VideoPlayer.IsMuted = _isMuted;
        BtnMuteToggle.Content = _isMuted ? T("🔇 Без звуку", "🔇 Muted") : T("🔊 Звук", "🔊 Sound");
    }

    private void BtnJumpStart_Click(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Position = TimeSpan.Zero;
        SeekSlider.Value = 0;
    }

    private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSlider = false;
        VideoPlayer.Position = TimeSpan.FromSeconds(SeekSlider.Value);
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing || !IsLoaded || SeekSlider == null) return;

        try
        {
            if (_isDraggingSlider && VideoPlayer != null && VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var pos = TimeSpan.FromSeconds(SeekSlider.Value);
                if (TxtCurrentPlaybackTime != null) TxtCurrentPlaybackTime.Text = $"{FormatTime(pos)} / {FormatTime(_totalDuration)}";
                VideoPlayer.Position = pos;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in SeekSlider_ValueChanged: {ex.Message}");
        }
    }

    private void TrimStartSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing || !IsLoaded || _isSyncingTime || TrimStartSlider == null || TrimEndSlider == null) return;

        try
        {
            if (TrimStartSlider.Value > TrimEndSlider.Value)
            {
                TrimEndSlider.Value = Math.Min(TrimStartSlider.Maximum, TrimStartSlider.Value);
            }

            _isSyncingTime = true;
            try
            {
                var startTs = TimeSpan.FromSeconds(TrimStartSlider.Value);
                if (TxtStartTime != null) TxtStartTime.Text = FormatTime(startTs);
                if (TxtTrimStartSeconds != null) TxtTrimStartSeconds.Text = $"{TrimStartSlider.Value:F1} с";
                if (VideoPlayer != null && VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
                {
                    VideoPlayer.Position = startTs;
                }
            }
            finally
            {
                _isSyncingTime = false;
            }

            UpdateDurationBadge();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in TrimStartSlider_ValueChanged: {ex.Message}");
        }
    }

    private void TrimEndSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing || !IsLoaded || _isSyncingTime || TrimStartSlider == null || TrimEndSlider == null) return;

        try
        {
            if (TrimEndSlider.Value < TrimStartSlider.Value)
            {
                TrimStartSlider.Value = Math.Max(0, TrimEndSlider.Value);
            }

            _isSyncingTime = true;
            try
            {
                var endTs = TimeSpan.FromSeconds(TrimEndSlider.Value);
                if (TxtEndTime != null) TxtEndTime.Text = FormatTime(endTs);
                if (TxtTrimEndSeconds != null) TxtTrimEndSeconds.Text = $"{TrimEndSlider.Value:F1} с";
                if (VideoPlayer != null && VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
                {
                    VideoPlayer.Position = endTs;
                }
            }
            finally
            {
                _isSyncingTime = false;
            }

            UpdateDurationBadge();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in TrimEndSlider_ValueChanged: {ex.Message}");
        }
    }

    private void BtnSetCurrentAsStart_Click(object sender, RoutedEventArgs e)
    {
        var pos = VideoPlayer.Position;
        _isSyncingTime = true;
        try
        {
            TxtStartTime.Text = FormatTime(pos);
            TrimStartSlider.Value = pos.TotalSeconds;
            TxtTrimStartSeconds.Text = $"{pos.TotalSeconds:F1} с";
        }
        finally
        {
            _isSyncingTime = false;
        }
        UpdateDurationBadge();
    }

    private void BtnSetCurrentAsEnd_Click(object sender, RoutedEventArgs e)
    {
        var pos = VideoPlayer.Position;
        _isSyncingTime = true;
        try
        {
            TxtEndTime.Text = FormatTime(pos);
            TrimEndSlider.Value = pos.TotalSeconds;
            TxtTrimEndSeconds.Text = $"{pos.TotalSeconds:F1} с";
        }
        finally
        {
            _isSyncingTime = false;
        }
        UpdateDurationBadge();
    }

    private void BtnPreset15s_Click(object sender, RoutedEventArgs e)
    {
        ApplyPresetDuration(15);
    }

    private void BtnPreset30s_Click(object sender, RoutedEventArgs e)
    {
        ApplyPresetDuration(30);
    }

    private void BtnPreset60s_Click(object sender, RoutedEventArgs e)
    {
        ApplyPresetDuration(60);
    }

    private void BtnPresetFull_Click(object sender, RoutedEventArgs e)
    {
        _isSyncingTime = true;
        try
        {
            TrimStartSlider.Value = 0;
            TrimEndSlider.Value = _totalDuration.TotalSeconds;
            TxtStartTime.Text = FormatTime(TimeSpan.Zero);
            TxtEndTime.Text = FormatTime(_totalDuration);
            TxtTrimStartSeconds.Text = "0.0 с";
            TxtTrimEndSeconds.Text = $"{_totalDuration.TotalSeconds:F1} с";
        }
        finally
        {
            _isSyncingTime = false;
        }
        UpdateDurationBadge();
    }

    private void ApplyPresetDuration(double seconds)
    {
        var start = ParseTime(TxtStartTime.Text);
        var targetEnd = start + TimeSpan.FromSeconds(seconds);
        if (_totalDuration > TimeSpan.Zero && targetEnd > _totalDuration)
        {
            targetEnd = _totalDuration;
        }

        _isSyncingTime = true;
        try
        {
            TxtEndTime.Text = FormatTime(targetEnd);
            TrimEndSlider.Value = targetEnd.TotalSeconds;
            TxtTrimEndSeconds.Text = $"{targetEnd.TotalSeconds:F1} с";
        }
        finally
        {
            _isSyncingTime = false;
        }
        UpdateDurationBadge();
    }

    private void TimeInputs_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _isSyncingTime) return;

        var start = ParseTime(TxtStartTime?.Text);
        var end = ParseTime(TxtEndTime?.Text);

        _isSyncingTime = true;
        try
        {
            if (TrimStartSlider != null && start.TotalSeconds <= TrimStartSlider.Maximum)
            {
                TrimStartSlider.Value = start.TotalSeconds;
                if (TxtTrimStartSeconds != null) TxtTrimStartSeconds.Text = $"{start.TotalSeconds:F1} с";
            }
            if (TrimEndSlider != null && end.TotalSeconds <= TrimEndSlider.Maximum)
            {
                TrimEndSlider.Value = end.TotalSeconds;
                if (TxtTrimEndSeconds != null) TxtTrimEndSeconds.Text = $"{end.TotalSeconds:F1} с";
            }
        }
        finally
        {
            _isSyncingTime = false;
        }

        UpdateDurationBadge();
    }

    private void UpdateDurationBadge()
    {
        if (!IsLoaded || TxtTrimDuration == null || TxtStartTime == null || TxtEndTime == null) return;

        var start = ParseTime(TxtStartTime?.Text);
        var end = ParseTime(TxtEndTime?.Text);

        if (end > start)
        {
            var diff = end - start;
            TxtTrimDuration.Text = T(
                $"Тривалість фрагмента: {diff.TotalSeconds:F1} сек ({FormatTime(diff)})",
                $"Clip duration: {diff.TotalSeconds:F1}s ({FormatTime(diff)})");
            TxtTrimDuration.Foreground = (System.Windows.Media.Brush)FindResource("OrbitAmberLightBrush");
            if (BtnTrimExecute != null) BtnTrimExecute.IsEnabled = true;
        }
        else
        {
            TxtTrimDuration.Text = T("Помилка: кінець має бути пізніше початку!", "Error: End time must be after start time!");
            TxtTrimDuration.Foreground = (System.Windows.Media.Brush)FindResource("OrbitRoseLightBrush");
            if (BtnTrimExecute != null) BtnTrimExecute.IsEnabled = false;
        }
    }

    private async void BtnTrimExecute_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessingState == VideoProcessingState.Processing) return;

        var start = ParseTime(TxtStartTime.Text);
        var end = ParseTime(TxtEndTime.Text);

        if (end <= start)
        {
            OrbitMessageDialog.Show(
                this,
                T("Час закінчення кліпу повинен бути більшим за час початку.",
                  "End time must be greater than start time."),
                T("Помилка часу", "Time Error"),
                "⚠️");
            return;
        }

        var duration = end - start;

        // Transition to Processing state via Single Source of Truth: UI locked, completion blocked
        SetProcessingState(VideoProcessingState.Processing);

        var startTs = DateTime.UtcNow;
        double estSec = 3.5;
        var progressTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        progressTimer.Tick += (s, ev) =>
        {
            if (ProcessingState != VideoProcessingState.Processing)
            {
                progressTimer.Stop();
                return;
            }

            var el = (DateTime.UtcNow - startTs).TotalSeconds;
            var rem = Math.Max(0.2, estSec - el);
            if (TrimProgressBar.Value < 92)
            {
                TrimProgressBar.Value = Math.Min(92.0, (el / estSec) * 92.0);
            }
            bool isEn = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
            TxtTrimProgressRemaining.Text = rem <= 0.8 ?
                (isEn ? "Finishing..." : "Завершення...") :
                (isEn ? $"~{Math.Ceiling(rem)}s left" : $"Залишилось: ~{Math.Ceiling(rem)} сек");
        };
        progressTimer.Start();

        bool backend100PercentConfirmed = false;
        string confirmedOutputPath = string.Empty;

        try
        {
            string outPath = await Task.Run(() => ImageProcessor.TrimVideoRange(
                _videoPath,
                start,
                duration,
                onCompleted: (confirmed, resPath) =>
                {
                    if (confirmed && !string.IsNullOrWhiteSpace(resPath) && File.Exists(resPath))
                    {
                        backend100PercentConfirmed = true;
                        confirmedOutputPath = resPath;
                    }
                }));

            progressTimer.Stop();

            // Strict verification: Single Source of Truth requires 100% confirmation callback from backend
            if (!backend100PercentConfirmed || string.IsNullOrWhiteSpace(confirmedOutputPath) || !File.Exists(confirmedOutputPath))
            {
                throw new InvalidOperationException(T("Бекенд не підтвердив успішне завершення операції обрізки відео.", "Backend did not confirm 100% completion of video trimming."));
            }

            // Synchronize status: transition editor state to Completed BEFORE any result popup
            SetProcessingState(VideoProcessingState.Completed, confirmedOutputPath);

            OperationHistoryService.Instance.RecordOperation(new OperationRecord
            {
                ActionId = "video_trim",
                Title = string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase) ? "Trim Video" : "Обрізка відео",
                Description = $"{Path.GetFileName(_videoPath)} → {Path.GetFileName(confirmedOutputPath)}",
                CanUndo = false,
                CreatedFiles = [confirmedOutputPath]
            });

            // Brief delay so progress at 100% is visibly synchronized
            await Task.Delay(250);

            // Now display the result dialog (both editor and popup are in 100% harmony)
            OrbitMessageDialog.ShowVideoResult(this, confirmedOutputPath);

            Close();
        }
        catch (Exception ex)
        {
            progressTimer.Stop();
            SetProcessingState(VideoProcessingState.Failed, errorMessage: ex.Message);

            OrbitMessageDialog.Show(
                this,
                ex.Message,
                T("Помилка обрізки", "Trimming Error"),
                "⚠️");
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessingState == VideoProcessingState.Processing) return;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (ProcessingState == VideoProcessingState.Processing) return;
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Space && !(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
        {
            e.Handled = true;
            TogglePlayPause();
        }
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private static TimeSpan ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return TimeSpan.Zero;

        text = text.Trim();
        var parts = text.Split(':');
        try
        {
            if (parts.Length == 3)
            {
                int h = int.Parse(parts[0], CultureInfo.InvariantCulture);
                int m = int.Parse(parts[1], CultureInfo.InvariantCulture);
                double s = double.Parse(parts[2], CultureInfo.InvariantCulture);
                return TimeSpan.FromHours(h) + TimeSpan.FromMinutes(m) + TimeSpan.FromSeconds(s);
            }
            if (parts.Length == 2)
            {
                int m = int.Parse(parts[0], CultureInfo.InvariantCulture);
                double s = double.Parse(parts[1], CultureInfo.InvariantCulture);
                return TimeSpan.FromMinutes(m) + TimeSpan.FromSeconds(s);
            }
            if (parts.Length == 1 && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double sec))
            {
                return TimeSpan.FromSeconds(sec);
            }
        }
        catch { }

        return TimeSpan.Zero;
    }
}
