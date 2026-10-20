using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Halo.Core.Models;

namespace Halo.App.Controls;

public class RadialSectorButton : Canvas
{
    private readonly Path _slicePath;
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

    public event Action<ActionItem>? ActionTriggered;

    public ActionItem Action => _action;

    public RadialSectorButton(
        Point center,
        double innerRadius,
        double outerRadius,
        double startAngle,
        double endAngle,
        ActionItem action,
        bool showLabels = true)
    {
        _center = center;
        _innerRadius = innerRadius;
        _baseOuterRadius = outerRadius;
        _startAngle = startAngle;
        _endAngle = endAngle;
        _action = action;
        _showLabels = showLabels;

        Cursor = Cursors.Hand;
        Width = center.X * 2;
        Height = center.Y * 2;

        _slicePath = new Path
        {
            StrokeThickness = 1.5,
            Stroke = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255))
        };
        UpdateSectorBrush(isHovered: false);
        _slicePath.Data = RadialGeometryHelper.CreatePieSlice(_center, _innerRadius, _baseOuterRadius, _startAngle, _endAngle);

        Children.Add(_slicePath);

        // Content (Icon + Title)
        _iconBlock = new TextBlock
        {
            Text = _action.IconKey,
            FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        _titleBlock = new TextBlock
        {
            Text = _action.Title,
            FontSize = 11,
            FontWeight = FontWeights.Medium,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0),
            Visibility = _showLabels ? Visibility.Visible : Visibility.Collapsed,
            MaxWidth = 85,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _labelPanel = new StackPanel
        {
            IsHitTestVisible = false,
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
        var mid = RadialGeometryHelper.GetRadialMidpoint(_center, _innerRadius, outerRadius, _startAngle, _endAngle);
        _labelPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = _labelPanel.DesiredSize.Width > 0 ? _labelPanel.DesiredSize.Width : 60;
        double h = _labelPanel.DesiredSize.Height > 0 ? _labelPanel.DesiredSize.Height : 40;

        SetLeft(_labelPanel, mid.X - w / 2.0);
        SetTop(_labelPanel, mid.Y - h / 2.0);
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
            baseAccent = Color.FromRgb(59, 130, 246);
        }

        if (isHovered)
        {
            _slicePath.Fill = new LinearGradientBrush(
                Color.FromArgb(220, baseAccent.R, baseAccent.G, baseAccent.B),
                Color.FromArgb(170, (byte)Math.Min(255, baseAccent.R + 40), (byte)Math.Min(255, baseAccent.G + 40), (byte)Math.Min(255, baseAccent.B + 40)),
                45);
            _slicePath.Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
            _slicePath.StrokeThickness = 2.0;
        }
        else
        {
            _slicePath.Fill = new LinearGradientBrush(
                Color.FromArgb(170, 30, 41, 59),
                Color.FromArgb(150, 15, 23, 42),
                45);
            _slicePath.Stroke = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            _slicePath.StrokeThickness = 1.0;
        }
    }

    private void OnMouseEnterSector(object sender, MouseEventArgs e)
    {
        UpdateSectorBrush(true);
        AnimateOuterRadius(_baseOuterRadius + 14);
    }

    private void OnMouseLeaveSector(object sender, MouseEventArgs e)
    {
        UpdateSectorBrush(false);
        AnimateOuterRadius(_baseOuterRadius);
    }

    private void AnimateOuterRadius(double targetRadius)
    {
        // Re-generate slice geometry with target radius
        _slicePath.Data = RadialGeometryHelper.CreatePieSlice(_center, _innerRadius, targetRadius, _startAngle, _endAngle);
        UpdateLabelPosition(targetRadius);
    }

    private void OnClickSector(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ActionTriggered?.Invoke(_action);
    }
}
