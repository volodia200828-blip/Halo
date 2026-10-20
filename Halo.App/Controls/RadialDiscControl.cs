using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Halo.Core.Models;

namespace Halo.App.Controls;

public class RadialDiscControl : Canvas
{
    private readonly Canvas _slicesContainer;
    private readonly Border _centerHub;
    private readonly TextBlock _centerIcon;
    private readonly TextBlock _centerLabel;
    private readonly TextBlock _hoveredActionLabel;

    private List<ActionItem> _actions = new();
    private double _outerRadius = 130;
    private double _innerRadius = 52;

    public event Action<ActionItem>? ActionHovered;
    public event Action<ActionItem>? ActionClicked;

    public RadialDiscControl()
    {
        Width = 320;
        Height = 320;
        ClipToBounds = false;

        _slicesContainer = new Canvas { Width = 320, Height = 320 };
        Children.Add(_slicesContainer);

        // Center hub
        _centerHub = new Border
        {
            Width = _innerRadius * 2 - 8,
            Height = _innerRadius * 2 - 8,
            CornerRadius = new CornerRadius(_innerRadius),
            Background = new SolidColorBrush(Color.FromArgb(240, 11, 15, 25)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)),
            BorderThickness = new Thickness(2),
            Cursor = Cursors.Hand
        };

        SetLeft(_centerHub, 160 - (_innerRadius * 2 - 8) / 2.0);
        SetTop(_centerHub, 160 - (_innerRadius * 2 - 8) / 2.0);

        var hubStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _centerIcon = new TextBlock
        {
            Text = "⭕",
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _centerLabel = new TextBlock
        {
            Text = "Halo",
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };

        hubStack.Children.Add(_centerIcon);
        hubStack.Children.Add(_centerLabel);
        _centerHub.Child = hubStack;

        Children.Add(_centerHub);

        // Hovered action label at the bottom
        _hoveredActionLabel = new TextBlock
        {
            Text = "Наведіть на сектор для огляду",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Width = 320
        };
        SetLeft(_hoveredActionLabel, 0);
        SetTop(_hoveredActionLabel, 290);
        Children.Add(_hoveredActionLabel);
    }

    public void SetActions(List<ActionItem> actions, string categoryName)
    {
        _actions = actions.Where(a => a.IsEnabled).ToList();
        _centerLabel.Text = categoryName;
        Redraw();
    }

    private void Redraw()
    {
        _slicesContainer.Children.Clear();

        if (_actions.Count == 0)
        {
            _hoveredActionLabel.Text = "Немає активних секторів";
            return;
        }

        _hoveredActionLabel.Text = $"Активно секторів: {_actions.Count}";

        int count = _actions.Count;
        double gapDeg = count > 1 ? 3.0 : 0;
        double totalGap = gapDeg * count;
        double sectorSpan = (360.0 - totalGap) / count;
        Point center = new Point(160, 160);

        for (int i = 0; i < count; i++)
        {
            double startAngle = i * (sectorSpan + gapDeg);
            double endAngle = startAngle + sectorSpan;
            var action = _actions[i];

            var path = new Path
            {
                StrokeThickness = 1.2,
                Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Cursor = Cursors.Hand
            };

            Color accentColor;
            try
            {
                accentColor = (Color)ColorConverter.ConvertFromString(action.AccentColor);
            }
            catch
            {
                accentColor = Color.FromRgb(56, 189, 248);
            }

            path.Fill = new LinearGradientBrush(
                Color.FromArgb(170, 19, 28, 46),
                Color.FromArgb(140, 15, 23, 42),
                45);

            path.Data = RadialGeometryHelper.CreatePieSlice(center, _innerRadius, _outerRadius, startAngle, endAngle);

            // Icon on slice
            var mid = RadialGeometryHelper.GetRadialMidpoint(center, _innerRadius, _outerRadius, startAngle, endAngle);
            var iconBlock = new TextBlock
            {
                Text = action.IconKey,
                FontSize = 18,
                IsHitTestVisible = false
            };
            SetLeft(iconBlock, mid.X - 10);
            SetTop(iconBlock, mid.Y - 12);

            // Hover effects
            path.MouseEnter += (s, e) =>
            {
                path.Fill = new LinearGradientBrush(
                    Color.FromArgb(220, accentColor.R, accentColor.G, accentColor.B),
                    Color.FromArgb(180, (byte)Math.Min(255, accentColor.R + 40), (byte)Math.Min(255, accentColor.G + 40), (byte)Math.Min(255, accentColor.B + 40)),
                    45);
                path.Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                path.StrokeThickness = 2.0;
                path.Data = RadialGeometryHelper.CreatePieSlice(center, _innerRadius, _outerRadius + 10, startAngle, endAngle);
                _hoveredActionLabel.Text = $"{action.IconKey} {action.Title} — {action.Description}";
                _hoveredActionLabel.Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                ActionHovered?.Invoke(action);
            };

            path.MouseLeave += (s, e) =>
            {
                path.Fill = new LinearGradientBrush(
                    Color.FromArgb(170, 19, 28, 46),
                    Color.FromArgb(140, 15, 23, 42),
                    45);
                path.Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                path.StrokeThickness = 1.2;
                path.Data = RadialGeometryHelper.CreatePieSlice(center, _innerRadius, _outerRadius, startAngle, endAngle);
                _hoveredActionLabel.Text = $"Активно секторів: {_actions.Count}";
                _hoveredActionLabel.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            };

            path.MouseLeftButtonDown += (s, e) =>
            {
                ActionClicked?.Invoke(action);
            };

            _slicesContainer.Children.Add(path);
            _slicesContainer.Children.Add(iconBlock);
        }
    }
}
