using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Halo.Core.Models;

namespace Halo.App.Controls;

public class RadialSectorButton : Canvas
{
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
    private readonly ActionItem _action;
    private readonly bool _showLabels;
    private readonly int _totalSectors;

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
        int totalSectors = 6)
    {
        _center = center;
        _innerRadius = innerRadius;
        _baseOuterRadius = outerRadius;
        _startAngle = startAngle;
        _endAngle = endAngle;
        _action = action;
        _showLabels = showLabels;
        _totalSectors = totalSectors;

        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Cursor = Cursors.Hand;
        Width = center.X * 2;
        Height = center.Y * 2;

        _slicePath = new Path
        {
            StrokeThickness = 1.4,
            SnapsToDevicePixels = true
        };

        _accentArcPath = new Path
        {
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };

        UpdateSectorBrush(isHovered: false);
        _slicePath.Data = RadialGeometryHelper.CreatePieSlice(_center, _innerRadius, _baseOuterRadius, _startAngle, _endAngle);
        _accentArcPath.Data = RadialGeometryHelper.CreatePieSlice(_center, _innerRadius + 1, _innerRadius + 5.5, _startAngle + 1.5, _endAngle - 1.5);

        Children.Add(_slicePath);
        Children.Add(_accentArcPath);

        bool compactMode = _totalSectors >= 8;

        _iconBlock = new TextBlock
        {
            Text = _action.IconKey,
            FontSize = compactMode ? 19 : 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        _titleBlock = new TextBlock
        {
            Text = _action.Title,
            FontSize = compactMode ? 9.5 : 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
            Visibility = (_showLabels && _totalSectors <= 9) ? Visibility.Visible : Visibility.Collapsed,
            MaxWidth = compactMode ? 54 : 76,
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

    private void UpdateLabelPosition(double outerRadius)
    {
        // Bias radial midpoint slightly outward (57%) where the wedge is wider so labels never overlap
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
        Color baseAccent;
        try
        {
            baseAccent = (Color)ColorConverter.ConvertFromString(_action.AccentColor);
        }
        catch
        {
            baseAccent = Color.FromRgb(139, 92, 246);
        }

        _accentArcPath.Fill = new SolidColorBrush(Color.FromArgb(isHovered ? (byte)255 : (byte)190, baseAccent.R, baseAccent.G, baseAccent.B));

        if (isHovered)
        {
            // Orbit active gradient blended with sector accent
            _slicePath.Fill = new LinearGradientBrush(
                Color.FromArgb(245, (byte)((54 + baseAccent.R) / 2), (byte)((43 + baseAccent.G) / 2), (byte)((89 + baseAccent.B) / 2)),
                Color.FromArgb(245, 37, 36, 61),
                45);
            _slicePath.Stroke = new SolidColorBrush(Color.FromRgb(196, 181, 253));
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
        if (_titleBlock != null)
        {
            _titleBlock.Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247));
        }
        AnimateOuterRadius(_baseOuterRadius);
        SectorHovered?.Invoke(null);
    }

    private void AnimateOuterRadius(double targetRadius)
    {
        _slicePath.Data = RadialGeometryHelper.CreatePieSlice(_center, _innerRadius, targetRadius, _startAngle, _endAngle);
        UpdateLabelPosition(targetRadius);
    }

    private void OnClickSector(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ActionTriggered?.Invoke(_action);
    }
}
