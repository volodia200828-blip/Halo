using System;
using System.Collections.Generic;
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
using Microsoft.Win32;

namespace Halo.App.Views;

public partial class RadialOverlayWindow : Window
{
    private static readonly FontFamily FormatBadgeFont = new("Bahnschrift, Segoe UI Variable Display, Segoe UI");
    private static readonly FontFamily DefaultUiFont = new("Segoe UI Variable Display, Segoe UI");

    private List<string> _currentFiles = [];
    private string? _currentFilePath => _currentFiles.FirstOrDefault();
    private FileTypeCategory _currentCategory = FileTypeCategory.Generic;
    private ActionCategory _currentMode = ActionCategory.Convert;
    private bool _isPreviewMode = false;
    private DateTime _lastShownUtc = DateTime.MinValue;
    private System.Windows.Threading.DispatcherTimer? _autoHideTimer;
    private System.Windows.Threading.DispatcherTimer? _notificationTimer;

    public event Action? OpenSettingsRequested;
    public event Action<string>? AddToShelfRequested;
    public event Action? OpenShelfRequested;

    private static bool IsEn => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
    private static string T(string uk, string en) => IsEn ? en : uk;

    public RadialOverlayWindow()
    {
        InitializeComponent();
        Loaded += (s, e) => SettingsService.Instance.SettingsChanged += OnSettingsChanged;
        Unloaded += (s, e) => SettingsService.Instance.SettingsChanged -= OnSettingsChanged;
    }

    public void PrewarmWindow()
    {
        try
        {
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            OverlayRoot.Opacity = 0.0;
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            helper.EnsureHandle();
        }
        catch
        {
            // Ignore prewarm errors in headless/test environments
        }
    }

    public void HideMenu()
    {
        _autoHideTimer?.Stop();
        _notificationTimer?.Stop();

        var anim = (Storyboard)FindResource("EntranceAnimation");
        anim.Stop(this);

        NotificationPill.BeginAnimation(OpacityProperty, null);
        NotificationPill.Opacity = 0.0;

        if (IsVisible)
        {
            // Flush a 100% transparent frame to the Win32 layered window surface BEFORE hiding
            // so DWM never caches a stale visible menu frame that would blink on the next Show()
            OverlayRoot.Opacity = 0.0;
            SectorsHost.Children.Clear();
            try
            {
                Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.Render);
            }
            catch { }
            Hide();
        }
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateModeTabsVisuals();
            if (IsVisible)
            {
                UpdateCenterHubInfo(null);
                RebuildRadialMenu();
            }
        });
    }

    public void ShowAt(Point screenPoint, string? filePath = null)
    {
        var list = !string.IsNullOrWhiteSpace(filePath) ? new List<string> { filePath } : new List<string>();
        ShowAt(screenPoint, list);
    }

    public void ShowAt(Point screenPoint, IReadOnlyList<string>? filePaths)
    {
        // Prevent double-summon flicker if triggered repeatedly within 350ms while already open
        if (IsVisible && (DateTime.UtcNow - _lastShownUtc).TotalMilliseconds < 350)
        {
            return;
        }
        _lastShownUtc = DateTime.UtcNow;

        _autoHideTimer?.Stop();
        _notificationTimer?.Stop();
        NotificationPill.BeginAnimation(OpacityProperty, null);
        NotificationPill.Opacity = 0.0;

        _isPreviewMode = false;
        _currentFiles = filePaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? [];
        _currentCategory = _currentFiles.Count > 0
            ? FileTypeDetector.Detect(_currentFiles[0])
            : FileTypeCategory.Generic;

        _currentMode = ActionCategory.Convert;
        UpdateModeTabsVisuals();

        // Size window explicitly to primary screen without WindowState=Maximized to eliminate DWM surface reallocation flicker
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        var settings = SettingsService.Instance.Current;
        double radius = GetEffectiveOuterRadius(settings);
        double topBarHeight = 52;
        double diameter = (radius + 24) * 2;

        double left = Math.Max(24, Math.Min(SystemParameters.PrimaryScreenWidth - diameter - 24, screenPoint.X - radius - 24));
        double top = Math.Max(24, Math.Min(SystemParameters.PrimaryScreenHeight - (diameter + topBarHeight + 52) - 24, screenPoint.Y - radius - 24 - topBarHeight));

        MenuCanvas.Width = diameter;
        MenuCanvas.Height = diameter + topBarHeight + 52;

        Canvas.SetLeft(MenuCanvas, Math.Round(left));
        Canvas.SetTop(MenuCanvas, Math.Round(top));

        UpdateCenterHubInfo(null);
        RebuildRadialMenu();

        var anim = (Storyboard)FindResource("EntranceAnimation");
        anim.Stop(this);

        // Keep Opacity at 1.0 before Show() so there is never a 1.0 -> 0.0 -> 1.0 blink
        OverlayRoot.Opacity = 1.0;
        MenuCanvas.Opacity = 1.0;
        MenuRootTransform.ScaleX = 1.0;
        MenuRootTransform.ScaleY = 1.0;
        UpdateLayout();

        if (!IsVisible)
        {
            Show();
        }
        Activate();
        Focus();

        if (settings.SmoothAnimations)
        {
            anim.Begin(this, true);
        }
    }

    public void ShowPreviewForCategory(FileTypeCategory category, ActionCategory mode)
    {
        _autoHideTimer?.Stop();
        _notificationTimer?.Stop();
        NotificationPill.BeginAnimation(OpacityProperty, null);
        NotificationPill.Opacity = 0.0;

        _isPreviewMode = true;
        _currentFiles.Clear();
        _currentCategory = category;
        _currentMode = mode;
        UpdateModeTabsVisuals();

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        var settings = SettingsService.Instance.Current;
        double radius = GetEffectiveOuterRadius(settings);
        double topBarHeight = 52;
        double diameter = (radius + 24) * 2;

        Point screenPoint = new Point(SystemParameters.PrimaryScreenWidth / 2.0, SystemParameters.PrimaryScreenHeight / 2.0);

        double left = Math.Max(24, Math.Min(SystemParameters.PrimaryScreenWidth - diameter - 24, screenPoint.X - radius - 24));
        double top = Math.Max(24, Math.Min(SystemParameters.PrimaryScreenHeight - (diameter + topBarHeight + 52) - 24, screenPoint.Y - radius - 24 - topBarHeight));

        MenuCanvas.Width = diameter;
        MenuCanvas.Height = diameter + topBarHeight + 52;

        Canvas.SetLeft(MenuCanvas, Math.Round(left));
        Canvas.SetTop(MenuCanvas, Math.Round(top));

        UpdateCenterHubInfo(null);
        RebuildRadialMenu();

        var anim = (Storyboard)FindResource("EntranceAnimation");
        anim.Stop(this);

        OverlayRoot.Opacity = 1.0;
        MenuCanvas.Opacity = 1.0;
        MenuRootTransform.ScaleX = 1.0;
        MenuRootTransform.ScaleY = 1.0;
        UpdateLayout();

        if (!IsVisible)
        {
            Show();
        }
        Activate();
        Focus();

        if (settings.SmoothAnimations)
        {
            anim.Begin(this, true);
        }
    }

    private double GetEffectiveOuterRadius(AppSettings settings)
    {
        var actions = GetActionsForCurrentMode(settings);
        return actions.Count >= 8 ? Math.Max(settings.MenuRadius, 172) : settings.MenuRadius;
    }

    public void SetMode(ActionCategory mode)
    {
        if (_currentMode == mode)
            return;

        _currentMode = mode;
        UpdateModeTabsVisuals();
        UpdateCenterHubInfo(null);
        RebuildRadialMenu();
    }

    private void UpdateModeTabsVisuals()
    {
        TabConvertText.Text = T("Конвертація", "Convert");
        TabToolsText.Text = T("Інструменти", "Tools");
        TabActionsText.Text = T("Дії", "Actions");

        var activeBg = new SolidColorBrush(Color.FromRgb(124, 92, 255));
        var inactiveBg = Brushes.Transparent;
        var activeFg = Brushes.White;
        var inactiveFg = new SolidColorBrush(Color.FromRgb(160, 163, 181));

        var activeBadgeBg = new SolidColorBrush(Color.FromArgb(90, 16, 16, 24));
        var activeBadgeBorder = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255));
        var inactiveBadgeBg = new SolidColorBrush(Color.FromRgb(38, 38, 54));
        var inactiveBadgeBorder = new SolidColorBrush(Color.FromRgb(62, 62, 82));
        var inactiveBadgeFg = new SolidColorBrush(Color.FromRgb(148, 163, 184));

        bool isConvert = _currentMode == ActionCategory.Convert;
        TabConvertBorder.Background = isConvert ? activeBg : inactiveBg;
        TabConvertText.Foreground = isConvert ? activeFg : inactiveFg;
        TabConvertText.FontWeight = isConvert ? FontWeights.Bold : FontWeights.SemiBold;
        TabConvertKeyBadge.Background = isConvert ? activeBadgeBg : inactiveBadgeBg;
        TabConvertKeyBadge.BorderBrush = isConvert ? activeBadgeBorder : inactiveBadgeBorder;
        TabConvertKeyText.Foreground = isConvert ? activeFg : inactiveBadgeFg;

        bool isTools = _currentMode == ActionCategory.Edit;
        TabToolsBorder.Background = isTools ? activeBg : inactiveBg;
        TabToolsText.Foreground = isTools ? activeFg : inactiveFg;
        TabToolsText.FontWeight = isTools ? FontWeights.Bold : FontWeights.SemiBold;
        TabToolsKeyBadge.Background = isTools ? activeBadgeBg : inactiveBadgeBg;
        TabToolsKeyBadge.BorderBrush = isTools ? activeBadgeBorder : inactiveBadgeBorder;
        TabToolsKeyText.Foreground = isTools ? activeFg : inactiveBadgeFg;

        bool isActions = _currentMode == ActionCategory.System;
        TabActionsBorder.Background = isActions ? activeBg : inactiveBg;
        TabActionsText.Foreground = isActions ? activeFg : inactiveFg;
        TabActionsText.FontWeight = isActions ? FontWeights.Bold : FontWeights.SemiBold;
        TabActionsKeyBadge.Background = isActions ? activeBadgeBg : inactiveBadgeBg;
        TabActionsKeyBadge.BorderBrush = isActions ? activeBadgeBorder : inactiveBadgeBorder;
        TabActionsKeyText.Foreground = isActions ? activeFg : inactiveBadgeFg;
    }

    private void UpdateCenterHubInfo(ActionItem? hoveredAction)
    {
        var settings = SettingsService.Instance.Current;
        double outerRadius = GetEffectiveOuterRadius(settings);
        double innerRadius = settings.InnerRadius;
        double hubSize = innerRadius * 2 - 12;
        double topBarOffset = 52;

        CenterHub.Width = hubSize;
        CenterHub.Height = hubSize;
        CenterFileName.MaxWidth = Math.Max(68, hubSize - 24);
        CenterFileInfo.MaxWidth = Math.Max(64, hubSize - 28);

        double centerX = outerRadius + 24;
        double centerY = outerRadius + 24 + topBarOffset;

        Canvas.SetLeft(CenterHub, Math.Round(centerX - hubSize / 2.0));
        Canvas.SetTop(CenterHub, Math.Round(centerY - hubSize / 2.0));

        // Position TopModeContainer strictly centered around centerX
        Canvas.SetLeft(TopModeContainer, Math.Round(centerX - TopModeContainer.Width / 2.0));
        Canvas.SetTop(TopModeContainer, 0);

        // Position AmbientBackdropRing
        double ringSize = (outerRadius + 14) * 2;
        AmbientBackdropRing.Width = ringSize;
        AmbientBackdropRing.Height = ringSize;
        Canvas.SetLeft(AmbientBackdropRing, Math.Round(centerX - ringSize / 2.0));
        Canvas.SetTop(AmbientBackdropRing, Math.Round(centerY - ringSize / 2.0));

        if (hoveredAction != null)
        {
            bool isTextBadge = hoveredAction.Category == ActionCategory.Convert || RadialSectorButton.IsAsciiFormatBadge(hoveredAction.IconKey);
            CenterFileIcon.FontFamily = isTextBadge ? FormatBadgeFont : DefaultUiFont;
            CenterFileIcon.FontWeight = isTextBadge ? FontWeights.ExtraBold : FontWeights.Normal;
            CenterFileIcon.FontSize = isTextBadge ? 19 : 22;

            SolidColorBrush accentBrush;
            try
            {
                var accent = (Color)ColorConverter.ConvertFromString(hoveredAction.AccentColor);
                accentBrush = new SolidColorBrush(accent);
            }
            catch
            {
                accentBrush = new SolidColorBrush(Color.FromRgb(167, 139, 250));
            }

            CenterFileIcon.Foreground = accentBrush;
            CenterFileIcon.Text = hoveredAction.IconKey;
            if (isTextBadge)
            {
                // Avoid repeating "Формат JPG" right underneath "JPG"
                CenterFileName.Visibility = Visibility.Collapsed;
            }
            else
            {
                CenterFileName.Visibility = Visibility.Visible;
                CenterFileName.Text = hoveredAction.Title;
                CenterFileName.Foreground = accentBrush;
            }
            CenterFileInfo.Text = T("Клік для запуску", "Click to run");
            CenterFileInfo.Foreground = accentBrush;

            BottomHintText.Text = !string.IsNullOrWhiteSpace(hoveredAction.Description)
                ? hoveredAction.Description
                : hoveredAction.Title;
            BottomHintText.Foreground = accentBrush;
            BottomHintPill.BorderBrush = accentBrush;
        }
        else if (!string.IsNullOrEmpty(_currentFilePath) && (File.Exists(_currentFilePath) || Directory.Exists(_currentFilePath)))
        {
            CenterFileName.Visibility = Visibility.Visible;
            CenterFileIcon.FontFamily = DefaultUiFont;
            CenterFileIcon.FontWeight = FontWeights.Normal;
            CenterFileIcon.FontSize = 22;
            var catColor = _currentCategory switch
            {
                FileTypeCategory.Image => Color.FromRgb(56, 189, 248),
                FileTypeCategory.Video => Color.FromRgb(167, 139, 250),
                FileTypeCategory.Pdf => Color.FromRgb(251, 113, 133),
                FileTypeCategory.Document => Color.FromRgb(129, 140, 248),
                FileTypeCategory.Presentation => Color.FromRgb(251, 191, 36),
                FileTypeCategory.Spreadsheet => Color.FromRgb(52, 211, 153),
                FileTypeCategory.Archive => Color.FromRgb(251, 146, 60),
                _ => Color.FromRgb(167, 139, 250)
            };
            CenterFileIcon.Foreground = new SolidColorBrush(catColor);
            CenterFileName.Foreground = new SolidColorBrush(catColor);

            var fileName = Path.GetFileName(_currentFilePath);
            CenterFileName.Text = _currentFiles.Count > 1
                ? T($"{_currentFiles.Count} файл(ів)", $"{_currentFiles.Count} file(s)")
                : (string.IsNullOrEmpty(fileName) ? _currentFilePath : fileName);

            if (File.Exists(_currentFilePath))
            {
                var fi = new FileInfo(_currentFilePath);
                CenterFileInfo.Text = FormatFileSize(fi.Length);
            }
            else
            {
                CenterFileInfo.Text = T("Папка", "Folder");
            }
            CenterFileInfo.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));

            CenterFileIcon.Text = _currentCategory switch
            {
                FileTypeCategory.Image => "🖼️",
                FileTypeCategory.Video => "🎬",
                FileTypeCategory.Pdf => "📕",
                FileTypeCategory.Document => "📄",
                FileTypeCategory.Presentation => "📽️",
                FileTypeCategory.Spreadsheet => "📊",
                FileTypeCategory.Archive => "🗜️",
                FileTypeCategory.Folder => "📁",
                _ => "📦"
            };

            string modeTitle = _currentMode switch
            {
                ActionCategory.Convert => T("Конвертація (Shift)", "Convert (Shift)"),
                ActionCategory.Edit => T("Інструменти (Ctrl)", "Tools (Ctrl)"),
                _ => T("Дії (Alt)", "Actions (Alt)")
            };
            BottomHintText.Text = T(
                $"Режим: {modeTitle} • Shift / Ctrl / Alt для перемикання",
                $"Mode: {modeTitle} • Press Shift / Ctrl / Alt to switch");
            BottomHintText.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));
            BottomHintPill.BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 82));
        }
        else if (_isPreviewMode)
        {
            CenterFileName.Visibility = Visibility.Visible;
            CenterFileIcon.FontFamily = DefaultUiFont;
            CenterFileIcon.FontWeight = FontWeights.Normal;
            CenterFileIcon.FontSize = 22;
            var catColor = _currentCategory switch
            {
                FileTypeCategory.Image => Color.FromRgb(56, 189, 248),
                FileTypeCategory.Video => Color.FromRgb(167, 139, 250),
                FileTypeCategory.Pdf => Color.FromRgb(251, 113, 133),
                FileTypeCategory.Document => Color.FromRgb(129, 140, 248),
                FileTypeCategory.Presentation => Color.FromRgb(251, 191, 36),
                FileTypeCategory.Spreadsheet => Color.FromRgb(52, 211, 153),
                FileTypeCategory.Archive => Color.FromRgb(251, 146, 60),
                _ => Color.FromRgb(167, 139, 250)
            };
            CenterFileIcon.Foreground = new SolidColorBrush(catColor);
            CenterFileName.Foreground = new SolidColorBrush(catColor);

            CenterFileIcon.Text = _currentCategory switch
            {
                FileTypeCategory.Image => "🖼️",
                FileTypeCategory.Video => "🎬",
                FileTypeCategory.Pdf => "📕",
                FileTypeCategory.Document => "📄",
                FileTypeCategory.Presentation => "📽️",
                FileTypeCategory.Spreadsheet => "📊",
                FileTypeCategory.Archive => "🗜️",
                _ => "📦"
            };

            CenterFileName.Text = _currentCategory switch
            {
                FileTypeCategory.Image => T("Зображення", "Images"),
                FileTypeCategory.Video => T("Відео", "Video"),
                FileTypeCategory.Pdf => "PDF",
                FileTypeCategory.Document => T("Документи", "Documents"),
                FileTypeCategory.Presentation => T("Презентації", "Presentations"),
                FileTypeCategory.Spreadsheet => T("Таблиці", "Spreadsheets"),
                FileTypeCategory.Archive => T("Архіви", "Archives"),
                _ => "Halo"
            };

            CenterFileInfo.Text = _currentMode switch
            {
                ActionCategory.Convert => T("Конвертація (Shift)", "Convert (Shift)"),
                ActionCategory.Edit => T("Інструменти (Ctrl)", "Tools (Ctrl)"),
                _ => T("Дії (Alt)", "Actions (Alt)")
            };
            CenterFileInfo.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));

            BottomHintText.Text = T(
                "Тестовий режим меню • Оберіть сектор або перемикайте Shift / Ctrl / Alt",
                "Menu test mode • Click a sector or press Shift / Ctrl / Alt to test");
            BottomHintText.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));
            BottomHintPill.BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 82));
        }
        else
        {
            CenterFileName.Visibility = Visibility.Visible;
            CenterFileIcon.FontFamily = DefaultUiFont;
            CenterFileIcon.FontWeight = FontWeights.Bold;
            CenterFileIcon.FontSize = 22;
            CenterFileIcon.Foreground = new SolidColorBrush(Color.FromRgb(0, 240, 255));
            CenterFileIcon.Text = "📁";
            CenterFileName.Text = T("Вибрати файл", "Select File");
            CenterFileName.Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247));
            CenterFileInfo.Text = T("Огляд у Провіднику", "Browse Explorer");
            CenterFileInfo.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));

            BottomHintText.Text = T(
                "Клікніть по центру або сектору для вибору файлу через Провідник",
                "Click center or any sector to select a file via Explorer");
            BottomHintText.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));
            BottomHintPill.BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 82));
        }

        PositionBottomPill(centerX, centerY, outerRadius);
    }

    private void PositionBottomPill(double centerX, double centerY, double radius)
    {
        // Strictly center the 760px container around centerX so text never shifts right regardless of length
        Canvas.SetLeft(BottomHintContainer, Math.Round(centerX - BottomHintContainer.Width / 2.0));
        Canvas.SetTop(BottomHintContainer, Math.Round(centerY + radius + 20));
    }

    private List<ActionItem> GetActionsForCurrentMode(AppSettings settings)
    {
        var effectiveCat = _isPreviewMode
            ? _currentCategory
            : (_currentCategory == FileTypeCategory.Generic && string.IsNullOrEmpty(_currentFilePath)
                ? FileTypeCategory.Image // Default preview category when opened without a selected file
                : _currentCategory);

        if (_currentMode == ActionCategory.System)
        {
            // Mode 3: Дії (Alt) — applies to all files
            var systemActions = settings.Actions
                .Where(a => a.IsEnabled && a.Category == ActionCategory.System)
                .OrderBy(a => a.Order)
                .ToList();

            if (_currentFiles.Count == 0)
            {
                systemActions.Insert(0, new ActionItem
                {
                    Id = "act_pick_explorer",
                    Title = T("Вибрати файл", "Choose File"),
                    Description = T("Відкрити Провідник Windows для вибору файлу", "Open Windows Explorer to choose a file"),
                    IconKey = "📁",
                    AccentColor = "#00F0FF",
                    Category = ActionCategory.System,
                    Order = -1,
                    IsEnabled = true
                });
            }

            return systemActions;
        }

        if (_currentMode == ActionCategory.Edit)
        {
            // Mode 2: Інструменти (Ctrl) — strictly filtered by the selected file's category
            var matchedTools = settings.Actions
                .Where(a => a.IsEnabled && a.Category == ActionCategory.Edit && a.SupportedFileTypes.Contains(effectiveCat))
                .OrderBy(a => a.Order)
                .ToList();

            if (matchedTools.Count == 0)
            {
                matchedTools = settings.Actions
                    .Where(a => a.IsEnabled && a.Category == ActionCategory.Edit && a.SupportedFileTypes.Contains(FileTypeCategory.Generic))
                    .OrderBy(a => a.Order)
                    .ToList();
            }
            return matchedTools;
        }

        // Mode 1: Конвертація (Shift)
        var matchedConversions = settings.Actions
            .Where(a => a.IsEnabled && a.Category == ActionCategory.Convert && a.SupportedFileTypes.Contains(effectiveCat))
            .OrderBy(a => a.Order)
            .ToList();

        if (matchedConversions.Count == 0)
        {
            matchedConversions = settings.Actions
                .Where(a => a.IsEnabled && a.Category == ActionCategory.Convert && a.SupportedFileTypes.Contains(FileTypeCategory.Generic))
                .OrderBy(a => a.Order)
                .ToList();
        }

        return matchedConversions;
    }

    private void RebuildRadialMenu()
    {
        SectorsHost.Children.Clear();

        var settings = SettingsService.Instance.Current;
        double outerRadius = GetEffectiveOuterRadius(settings);
        double topBarOffset = 52;

        var activeActions = GetActionsForCurrentMode(settings);
        if (activeActions.Count == 0)
        {
            var defaults = DefaultActionsProvider.GetDefaults();
            DefaultActionsProvider.LocalizeActions(defaults, settings.Language);
            activeActions = defaults
                .Where(a => a.Category == _currentMode)
                .Take(6)
                .ToList();
        }

        int count = activeActions.Count;
        double gapDeg = count > 1 ? 2.8 : 0;
        double totalGap = gapDeg * count;
        double sectorSpan = (360.0 - totalGap) / count;

        double centerX = outerRadius + 24;
        double centerY = outerRadius + 24 + topBarOffset;
        Point center = new Point(centerX, centerY);

        for (int i = 0; i < count; i++)
        {
            double startAngle = i * (sectorSpan + gapDeg) + gapDeg / 2.0;
            double endAngle = startAngle + sectorSpan;

            var sector = new RadialSectorButton(
                center,
                settings.InnerRadius,
                outerRadius,
                startAngle,
                endAngle,
                activeActions[i],
                settings.ShowLabelsOnSectors,
                count,
                gapDeg);

            sector.ActionTriggered += OnActionSelected;
            sector.SectorHovered += OnSectorHovered;
            SectorsHost.Children.Add(sector);
        }
    }

    private void OnSectorHovered(ActionItem? action)
    {
        if (action != null)
        {
            UpdateCenterHubInfo(action);
        }
        else
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                bool isAnyOver = SectorsHost.Children.OfType<RadialSectorButton>().Any(s => s.IsMouseOver);
                if (!isAnyOver)
                {
                    UpdateCenterHubInfo(null);
                }
            }));
        }
    }

    private async void OnActionSelected(ActionItem action)
    {
        if (action.Id == "system_open_settings")
        {
            HideMenu();
            OpenSettingsRequested?.Invoke();
            return;
        }

        if (action.Id is "act_shelf_open" or "system_shelf_open")
        {
            HideMenu();
            OpenShelfRequested?.Invoke();
            return;
        }

        if (action.Id is "act_shelf_add" or "system_shelf_add")
        {
            if (_currentFiles.Count > 0 && !string.IsNullOrEmpty(_currentFilePath))
            {
                foreach (var f in _currentFiles)
                {
                    AddToShelfRequested?.Invoke(f);
                }
                OpenShelfRequested?.Invoke();
                ShowNotification(T("📥 Файл додано на полицю", "📥 File added to Shelf"));
                ScheduleHide();
            }
            else
            {
                HideMenu();
                OpenShelfRequested?.Invoke();
            }
            return;
        }

        if (action.Id == "act_pick_explorer")
        {
            var filter = GetCategoryFileFilter(_currentCategory);
            var pickDlg = new OpenFileDialog
            {
                Title = T($"Оберіть {GetCategoryTitle(_currentCategory)} для Halo", $"Select {GetCategoryTitle(_currentCategory)} for Halo"),
                Multiselect = true,
                Filter = filter
            };
            if (pickDlg.ShowDialog(this) == true && pickDlg.FileNames.Length > 0)
            {
                if (!ValidateCategoryCompatibility(pickDlg.FileNames))
                    return;

                _currentFiles = pickDlg.FileNames.ToList();
                _currentCategory = FileTypeDetector.Detect(_currentFiles[0]);
                RebuildRadialMenu();
                UpdateCenterHubInfo(null);
            }
            return;
        }

        // If no file is currently loaded when the user clicks an action, prompt them to pick file(s) strictly for this category!
        if (_currentFiles.Count == 0 || string.IsNullOrEmpty(_currentFilePath))
        {
            var filter = GetCategoryFileFilter(_currentCategory);
            var pickDlg = new OpenFileDialog
            {
                Title = T($"Оберіть {GetCategoryTitle(_currentCategory)} для дії «{action.Title}»", $"Select {GetCategoryTitle(_currentCategory)} for '{action.Title}'"),
                Multiselect = true,
                Filter = filter
            };
            if (pickDlg.ShowDialog(this) != true || pickDlg.FileNames.Length == 0)
            {
                return;
            }

            if (!ValidateCategoryCompatibility(pickDlg.FileNames))
                return;

            _currentFiles = pickDlg.FileNames.ToList();
            _currentCategory = FileTypeDetector.Detect(_currentFiles[0]);
            UpdateCenterHubInfo(null);
        }

        if (action.Id is "tool_vid_clip15" or "tool_vid_clip")
        {
            HideMenu();
            if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
            {
                var trimWindow = new VideoTrimWindow(_currentFilePath);
                trimWindow.Show();
            }
            return;
        }

        if (action.Id is "act_copy_path" or "system_copy_path")
        {
            var text = string.Join(Environment.NewLine, _currentFiles);
            Clipboard.SetText(text);
            ShowNotification(T("📋 Шлях скопійовано у буфер обміну", "📋 Path copied to clipboard"));
            ScheduleHide(450);
            return;
        }

        string? destinationFolder = null;
        if (action.Id is "act_copy_to_folder" or "act_move_to_folder")
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = action.Id == "act_copy_to_folder"
                    ? T("Оберіть папку для копіювання файлу", "Select destination folder to copy file")
                    : T("Оберіть папку для переміщення файлу", "Select destination folder to move file"),
                InitialDirectory = Path.GetDirectoryName(_currentFilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (folderDialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(folderDialog.FolderName))
            {
                return;
            }
            destinationFolder = folderDialog.FolderName;
        }

        ShowNotification(T($"Виконується: {action.Title}...", $"Running: {action.Title}..."));

        double estimatedSec = action.Id.Contains("vid") ? 7.0 :
                              action.Id.Contains("compress") ? 5.0 :
                              action.Id.Contains("ocr") ? 4.0 :
                              action.Id.Contains("pdf") ? 3.5 : 2.5;

        var progressToast = HaloToastWindow.ShowProgressToast(
            T("Halo", "Halo"),
            T($"Виконується: {action.Title}...", $"Running: {action.Title}..."),
            estimatedSec);

        var result = await ActionExecutor.ExecuteAsync(
            action,
            _currentFiles,
            onOpenSettings: _ => OpenSettingsRequested?.Invoke(),
            onAddToShelf: f => Dispatcher.Invoke(() => AddToShelfRequested?.Invoke(f)),
            destinationFolder: destinationFolder,
            onOpenShelf: () => Dispatcher.Invoke(() => OpenShelfRequested?.Invoke()));

        if (result.Success)
        {
            if (action.Id == "tool_img_ocr" && !string.IsNullOrWhiteSpace(result.OutputPath) && File.Exists(result.OutputPath))
            {
                try
                {
                    Clipboard.SetText(File.ReadAllText(result.OutputPath));
                }
                catch { }
            }
            else if (action.Id is "tool_doc_copy_text" or "tool_pres_copy_text" && !string.IsNullOrWhiteSpace(result.OutputPath))
            {
                try
                {
                    Clipboard.SetText(result.OutputPath);
                }
                catch { }
            }
            ShowNotification($"✅ {result.Message}");
            progressToast.Complete($"✅ {result.Message}");
            ScheduleHide(450);
        }
        else
        {
            ShowNotification($"❌ {result.Message}");
            progressToast.Fail($"❌ {result.Message}");
            ScheduleHide(1200);
        }
    }

    private void ScheduleHide(int delayMs = 450)
    {
        _autoHideTimer?.Stop();
        _autoHideTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(delayMs)
        };
        _autoHideTimer.Tick += (s, e) =>
        {
            _autoHideTimer?.Stop();
            HideMenu();
        };
        _autoHideTimer.Start();
    }

    public void ShowNotification(string message)
    {
        NotificationText.Text = message;

        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(140));
        NotificationPill.BeginAnimation(OpacityProperty, fadeIn);

        _notificationTimer?.Stop();
        _notificationTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.4)
        };
        _notificationTimer.Tick += (s, e) =>
        {
            _notificationTimer?.Stop();
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(220));
            NotificationPill.BeginAnimation(OpacityProperty, fadeOut);
        };
        _notificationTimer.Start();
    }

    private void ModeSwitcherPill_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void TabConvert_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        SetMode(ActionCategory.Convert);
    }

    private void TabTools_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        SetMode(ActionCategory.Edit);
    }

    private void TabActions_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        SetMode(ActionCategory.System);
    }

    private void CenterHub_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_currentFiles.Count == 0 || string.IsNullOrEmpty(_currentFilePath))
        {
            var filter = GetCategoryFileFilter(_currentCategory);
            var pickDlg = new OpenFileDialog
            {
                Title = T($"Оберіть {GetCategoryTitle(_currentCategory)} для Halo", $"Select {GetCategoryTitle(_currentCategory)} for Halo"),
                Multiselect = true,
                Filter = filter
            };
            if (pickDlg.ShowDialog(this) == true && pickDlg.FileNames.Length > 0)
            {
                if (!ValidateCategoryCompatibility(pickDlg.FileNames))
                    return;

                _currentFiles = pickDlg.FileNames.ToList();
                _currentCategory = FileTypeDetector.Detect(_currentFiles[0]);
                RebuildRadialMenu();
                UpdateCenterHubInfo(null);
                return;
            }
        }
        HideMenu();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
            return;

        if ((DateTime.UtcNow - _lastShownUtc).TotalMilliseconds < 250)
            return;

        HideMenu();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            e.Handled = true;
            HideMenu();
            return;
        }

        if (key is Key.LeftShift or Key.RightShift)
        {
            e.Handled = true;
            SetMode(ActionCategory.Convert);
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl)
        {
            e.Handled = true;
            SetMode(ActionCategory.Edit);
            return;
        }

        if (key is Key.LeftAlt or Key.RightAlt)
        {
            // Ignore Alt key during the first 300ms after summon so Alt+Space hotkey doesn't switch modes
            if ((DateTime.UtcNow - _lastShownUtc).TotalMilliseconds < 300)
                return;

            e.Handled = true;
            SetMode(ActionCategory.System);
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideMenu();
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
                if (!ValidateCategoryCompatibility(files))
                    return;

                _currentFiles = files.ToList();
                _currentCategory = FileTypeDetector.Detect(_currentFiles[0]);
                UpdateCenterHubInfo(null);
                RebuildRadialMenu();
            }
        }
    }

    private string GetCategoryFileFilter(FileTypeCategory category)
    {
        return category switch
        {
            FileTypeCategory.Spreadsheet => T(
                "Таблиці (*.xlsx;*.xls;*.csv;*.tsv;*.ods)|*.xlsx;*.xls;*.csv;*.tsv;*.ods",
                "Spreadsheets (*.xlsx;*.xls;*.csv;*.tsv;*.ods)|*.xlsx;*.xls;*.csv;*.tsv;*.ods"),
            FileTypeCategory.Image => T(
                "Зображення (*.png;*.jpg;*.jpeg;*.webp;*.avif;*.tiff;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.webp;*.avif;*.tiff;*.bmp;*.ico",
                "Images (*.png;*.jpg;*.jpeg;*.webp;*.avif;*.tiff;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.webp;*.avif;*.tiff;*.bmp;*.ico"),
            FileTypeCategory.Document => T(
                "Документи (*.docx;*.doc;*.odt;*.rtf;*.txt;*.md;*.html;*.epub)|*.docx;*.doc;*.odt;*.rtf;*.txt;*.md;*.html;*.epub",
                "Documents (*.docx;*.doc;*.odt;*.rtf;*.txt;*.md;*.html;*.epub)|*.docx;*.doc;*.odt;*.rtf;*.txt;*.md;*.html;*.epub"),
            FileTypeCategory.Pdf => T(
                "PDF документи (*.pdf)|*.pdf",
                "PDF documents (*.pdf)|*.pdf"),
            FileTypeCategory.Presentation => T(
                "Презентації (*.pptx;*.ppt;*.odp)|*.pptx;*.ppt;*.odp",
                "Presentations (*.pptx;*.ppt;*.odp)|*.pptx;*.ppt;*.odp"),
            FileTypeCategory.Video => T(
                "Відео (*.mp4;*.mkv;*.mov;*.avi;*.webm)|*.mp4;*.mkv;*.mov;*.avi;*.webm",
                "Videos (*.mp4;*.mkv;*.mov;*.avi;*.webm)|*.mp4;*.mkv;*.mov;*.avi;*.webm"),
            FileTypeCategory.Archive => T(
                "Архіви (*.zip;*.tar;*.gz;*.tgz;*.7z;*.rar)|*.zip;*.tar;*.gz;*.tgz;*.7z;*.rar",
                "Archives (*.zip;*.tar;*.gz;*.tgz;*.7z;*.rar)|*.zip;*.tar;*.gz;*.tgz;*.7z;*.rar"),
            _ => T(
                "Усі файли (*.*)|*.*|Зображення (*.png;*.jpg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|Відео (*.mp4;*.mkv;*.mov;*.avi)|*.mp4;*.mkv;*.mov;*.avi;*.webm|Документи (*.pdf;*.docx;*.txt;*.md)|*.pdf;*.docx;*.txt;*.md;*.html",
                "All Files (*.*)|*.*|Images (*.png;*.jpg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|Videos (*.mp4;*.mkv;*.mov;*.avi)|*.mp4;*.mkv;*.mov;*.avi;*.webm|Documents (*.pdf;*.docx;*.txt;*.md)|*.pdf;*.docx;*.txt;*.md;*.html")
        };
    }

    private string GetCategoryTitle(FileTypeCategory category)
    {
        return category switch
        {
            FileTypeCategory.Image => T("Зображення", "Images"),
            FileTypeCategory.Video => T("Відео", "Video"),
            FileTypeCategory.Pdf => "PDF",
            FileTypeCategory.Document => T("Документи", "Documents"),
            FileTypeCategory.Presentation => T("Презентації", "Presentations"),
            FileTypeCategory.Spreadsheet => T("Таблиці", "Spreadsheets"),
            FileTypeCategory.Archive => T("Архіви", "Archives"),
            _ => T("Файли", "Files")
        };
    }

    private bool ValidateCategoryCompatibility(IEnumerable<string> filePaths)
    {
        if (_isPreviewMode && _currentCategory != FileTypeCategory.Generic)
        {
            foreach (var path in filePaths)
            {
                var detected = FileTypeDetector.Detect(path);
                if (detected != _currentCategory)
                {
                    string expectedName = GetCategoryTitle(_currentCategory);
                    string actualName = GetCategoryTitle(detected);
                    ShowNotification(T(
                        $"⚠️ Меню лише для «{expectedName}»! Обрано: {actualName}.",
                        $"⚠️ Menu is strictly for '{expectedName}'! Selected: {actualName}."));
                    return false;
                }
            }
        }
        return true;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
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
