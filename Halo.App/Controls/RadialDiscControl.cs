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
    private const double OuterRadius = 126;
    private const double InnerRadius = 50;

    public event Action<ActionItem>? ActionHovered;
    public event Action<ActionItem>? ActionClicked;

    public RadialDiscControl()
    {
        Width = 320;
        Height = 320;
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        _slicesContainer = new Canvas { Width = 320, Height = 320 };
        Children.Add(_slicesContainer);

        // Center hub with neon glow
        _centerHub = new Border
        {
            Width = InnerRadius * 2 - 6,
            Height = InnerRadius * 2 - 6,
            CornerRadius = new CornerRadius(InnerRadius),
            Background = new SolidColorBrush(Color.FromArgb(245, 11, 15, 25)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 240, 255)),
            BorderThickness = new Thickness(1.5),
            Cursor = Cursors.Hand
        };
        _centerHub.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 16,
            Color = Color.FromRgb(0, 240, 255),
            Opacity = 0.4,
            ShadowDepth = 0
        };

        SetLeft(_centerHub, 160 - (InnerRadius * 2 - 6) / 2.0);
        SetTop(_centerHub, 160 - (InnerRadius * 2 - 6) / 2.0);

        var hubStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _centerIcon = new TextBlock
        {
            Text = "⭕",
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _centerLabel = new TextBlock
        {
            Text = "Halo",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 240, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };

        hubStack.Children.Add(_centerIcon);
        hubStack.Children.Add(_centerLabel);
        _centerHub.Child = hubStack;

        Children.Add(_centerHub);

        // Status description below the disc
        _hoveredActionLabel = new TextBlock
        {
            Text = "Наведіть на сектор для огляду",
            FontSize = 11.5,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Width = 320
        };
        SetLeft(_hoveredActionLabel, 0);
        SetTop(_hoveredActionLabel, 292);
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
                StrokeThickness = 1.0,
                Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Cursor = Cursors.Hand
            };

            Color accentColor;
            try
            {
                accentColor = (Color)ColorConverter.ConvertFromString(action.AccentColor);
            }
            catch
            {
                accentColor = Color.FromRgb(0, 240, 255);
            }

            path.Fill = new LinearGradientBrush(
                Color.FromArgb(170, 16, 23, 38),
                Color.FromArgb(130, 10, 15, 26),
                45);

            path.Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius, OuterRadius, startAngle, endAngle);

            // Icon on slice
            var mid = RadialGeometryHelper.GetRadialMidpoint(center, InnerRadius, OuterRadius, startAngle, endAngle);
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
                    Color.FromArgb(230, accentColor.R, accentColor.G, accentColor.B),
                    Color.FromArgb(180, (byte)Math.Min(255, accentColor.R + 40), (byte)Math.Min(255, accentColor.G + 40), (byte)Math.Min(255, accentColor.B + 40)),
                    45);
                path.Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                path.StrokeThickness = 1.5;
                path.Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius, OuterRadius + 10, startAngle, endAngle);
                _hoveredActionLabel.Text = $"{action.IconKey} {action.Title} — {action.Description}";
                _hoveredActionLabel.Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                ActionHovered?.Invoke(action);
            };

            path.MouseLeave += (s, e) =>
            {
                path.Fill = new LinearGradientBrush(
                    Color.FromArgb(170, 16, 23, 38),
                    Color.FromArgb(130, 10, 15, 26),
                    45);
                path.Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                path.StrokeThickness = 1.0;
                path.Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius, OuterRadius, startAngle, endAngle);
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
