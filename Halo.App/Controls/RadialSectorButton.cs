using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Halo.Core.Models;

namespace Halo.App.Controls;

public class RadialSectorButton : Canvas
{
    private static readonly FontFamily FormatBadgeFont = new("Bahnschrift, Segoe UI Variable Display, Segoe UI");

    private readonly Path _hitSlicePath;
    private readonly Path _slicePath;
    private readonly Path _accentArcPath;
    private readonly StackPanel _labelPanel;
    private readonly TextBlock _iconBlock;
    private readonly TextBlock _titleBlock;

    private readonly Point _center;
    private readonly double _innerRadius;
    private readonly double _baseOuterRadius;
    private readonly double _startAngle;
    private readonly double _endAngle;
    private readonly double _hitStart;
    private readonly double _hitEnd;
    private readonly ActionItem _action;
    private readonly bool _showLabels;
    private readonly int _totalSectors;
    private readonly Color _baseAccent;
    private readonly bool _isTextFormatBadge;

    public event Action<ActionItem>? ActionTriggered;
    public event Action<ActionItem?>? SectorHovered;

    public ActionItem Action => _action;

    public RadialSectorButton(
        Point center,
        double innerRadius,
        double outerRadius,
        double startAngle,
        double endAngle,
        ActionItem action,
        bool showLabels = true,
        int totalSectors = 6,
        double gapDeg = 0)
    {
        _center = center;
        _innerRadius = innerRadius;
        _baseOuterRadius = outerRadius;
        _startAngle = startAngle;
        _endAngle = endAngle;
        _action = action;
        _showLabels = showLabels;
        _totalSectors = totalSectors;
        _baseAccent = ParseAccentColor(_action.AccentColor);
        _isTextFormatBadge = _action.Category == ActionCategory.Convert || IsAsciiFormatBadge(_action.IconKey);

        double halfGap = gapDeg / 2.0;
        _hitStart = startAngle - halfGap;
        _hitEnd = endAngle + halfGap;

        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Cursor = Cursors.Hand;
        Width = center.X * 2;
        Height = center.Y * 2;

        // Gapless hit-test geometry bridges the gap between visual sectors so mouse never leaves sectors during transition
        _hitSlicePath = new Path
        {
            Fill = Brushes.Transparent,
            Stroke = null,
            IsHitTestVisible = true,
            Data = RadialGeometryHelper.CreatePieSlice(_center, _innerRadius, _baseOuterRadius + 12, _hitStart, _hitEnd)
        };

        _slicePath = new Path
        {
            StrokeThickness = 1.4,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false
        };

        _accentArcPath = new Path
        {
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };

        UpdateSectorBrush(isHovered: false);
        _slicePath.Data = RadialGeometryHelper.CreatePieSlice(_center, _innerRadius, _baseOuterRadius, _startAngle, _endAngle);
        _accentArcPath.Data = RadialGeometryHelper.CreatePieSlice(_center, _innerRadius + 1, _innerRadius + 5.5, _startAngle + 1.5, _endAngle - 1.5);

        Children.Add(_hitSlicePath);
        Children.Add(_slicePath);
        Children.Add(_accentArcPath);

        bool compactMode = _totalSectors >= 8;

        _iconBlock = new TextBlock
        {
            Text = _action.IconKey,
            FontSize = GetIconFontSize(_action.IconKey, _isTextFormatBadge, compactMode),
            Foreground = new SolidColorBrush(_baseAccent),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        if (_isTextFormatBadge)
        {
            _iconBlock.FontFamily = FormatBadgeFont;
            _iconBlock.FontWeight = FontWeights.ExtraBold;
        }

        _titleBlock = new TextBlock
        {
            Text = _action.Title,
            FontSize = compactMode ? 9.5 : 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(_baseAccent),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
            // Do not repeat a subtitle underneath text format badges (e.g., JPG, PNG, DOCX)
            Visibility = (!_isTextFormatBadge && _showLabels && _totalSectors <= 9) ? Visibility.Visible : Visibility.Collapsed,
            MaxWidth = compactMode ? 56 : 76,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _labelPanel = new StackPanel
        {
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _iconBlock, _titleBlock }
        };

        Children.Add(_labelPanel);
        UpdateLabelPosition(_baseOuterRadius);

        MouseEnter += OnMouseEnterSector;
        MouseLeave += OnMouseLeaveSector;
        MouseLeftButtonDown += OnClickSector;
    }

    public static bool IsAsciiFormatBadge(string? iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey)) return false;
        return iconKey.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.');
    }

    private static double GetIconFontSize(string? iconKey, bool isTextFormat, bool compactMode)
    {
        if (!isTextFormat)
            return compactMode ? 19 : 22;

        int len = iconKey?.Length ?? 3;
        if (len >= 6) return compactMode ? 11.5 : 13.0;
        if (len >= 5) return compactMode ? 12.5 : 14.5;
        if (len == 4) return compactMode ? 14.0 : 16.0;
        return compactMode ? 16.0 : 18.5;
    }

    private static Color ParseAccentColor(string? hex)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex))
                return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            // Fallback below
        }
        return Color.FromRgb(139, 92, 246);
    }

    private void UpdateLabelPosition(double outerRadius)
    {
        // Bias radial midpoint slightly outward where the wedge is wider so labels never overlap
        double biasedInner = _innerRadius + (outerRadius - _innerRadius) * 0.14;
        var mid = RadialGeometryHelper.GetRadialMidpoint(_center, biasedInner, outerRadius, _startAngle, _endAngle);

        _labelPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = _labelPanel.DesiredSize.Width > 0 ? _labelPanel.DesiredSize.Width : 54;
        double h = _labelPanel.DesiredSize.Height > 0 ? _labelPanel.DesiredSize.Height : 34;

        SetLeft(_labelPanel, Math.Round(mid.X - w / 2.0));
        SetTop(_labelPanel, Math.Round(mid.Y - h / 2.0));
    }

    private void UpdateSectorBrush(bool isHovered)
    {
        _accentArcPath.Fill = new SolidColorBrush(Color.FromArgb(isHovered ? (byte)255 : (byte)205, _baseAccent.R, _baseAccent.G, _baseAccent.B));

        if (isHovered)
        {
            // Orbit active gradient blended with sector accent
            _slicePath.Fill = new LinearGradientBrush(
                Color.FromArgb(245, (byte)((54 + _baseAccent.R) / 2), (byte)((43 + _baseAccent.G) / 2), (byte)((89 + _baseAccent.B) / 2)),
                Color.FromArgb(245, 37, 36, 61),
                45);
            _slicePath.Stroke = new SolidColorBrush(_baseAccent);
            _slicePath.StrokeThickness = 2.0;
        }
        else
        {
            // Deep Orbit Card surface (#282832 -> #1E1E26)
            _slicePath.Fill = new LinearGradientBrush(
                Color.FromArgb(242, 40, 40, 52),
                Color.FromArgb(242, 28, 28, 37),
                45);
            _slicePath.Stroke = new SolidColorBrush(Color.FromRgb(58, 58, 76));
            _slicePath.StrokeThickness = 1.3;
        }
    }

    private void OnMouseEnterSector(object sender, MouseEventArgs e)
    {
        UpdateSectorBrush(true);
        if (_iconBlock != null)
        {
            _iconBlock.Foreground = Brushes.White;
        }
        if (_titleBlock != null)
        {
            _titleBlock.Foreground = Brushes.White;
        }
        AnimateOuterRadius(_baseOuterRadius + 11);
        SectorHovered?.Invoke(_action);
    }

    private void OnMouseLeaveSector(object sender, MouseEventArgs e)
    {
        UpdateSectorBrush(false);
        if (_iconBlock != null)
        {
            _iconBlock.Foreground = new SolidColorBrush(_baseAccent);
        }
        if (_titleBlock != null)
        {
            _titleBlock.Foreground = new SolidColorBrush(_baseAccent);
        }
        AnimateOuterRadius(_baseOuterRadius);
        SectorHovered?.Invoke(null);
    }

    private void AnimateOuterRadius(double targetRadius)
    {
        _slicePath.Data = RadialGeometryHelper.CreatePieSlice(_center, _innerRadius, targetRadius, _startAngle, _endAngle);
        _hitSlicePath.Data = RadialGeometryHelper.CreatePieSlice(_center, _innerRadius, targetRadius + 12, _hitStart, _hitEnd);
        UpdateLabelPosition(targetRadius);
    }

    private void OnClickSector(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ActionTriggered?.Invoke(_action);
    }
}
