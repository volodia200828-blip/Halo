using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
    private const double OuterRadius = 124;
    private const double InnerRadius = 48;

    public event Action<ActionItem>? ActionHovered;
    public event Action<ActionItem>? ActionClicked;

    public RadialDiscControl()
    {
        Width = 310;
        Height = 325;
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        // Subtle ambient ring behind the disc
        var ambientRing = new Ellipse
        {
            Width = OuterRadius * 2 + 24,
            Height = OuterRadius * 2 + 24,
            Fill = new SolidColorBrush(Color.FromArgb(80, 27, 27, 33)),
            Stroke = new SolidColorBrush(Color.FromRgb(48, 48, 62)),
            StrokeThickness = 1
        };
        SetLeft(ambientRing, 155 - (OuterRadius + 12));
        SetTop(ambientRing, 150 - (OuterRadius + 12));
        Children.Add(ambientRing);

        _slicesContainer = new Canvas { Width = 310, Height = 300 };
        Children.Add(_slicesContainer);

        // Orbit-styled Center Hub
        _centerHub = new Border
        {
            Width = InnerRadius * 2 - 6,
            Height = InnerRadius * 2 - 6,
            CornerRadius = new CornerRadius(InnerRadius),
            Background = new SolidColorBrush(Color.FromRgb(27, 27, 34)),
            BorderBrush = new LinearGradientBrush(
                Color.FromRgb(168, 85, 247),
                Color.FromRgb(56, 189, 248),
                45),
            BorderThickness = new Thickness(2),
            Cursor = Cursors.Hand,
            Effect = new DropShadowEffect
            {
                BlurRadius = 18,
                Color = Color.FromRgb(139, 92, 246),
                Opacity = 0.35,
                ShadowDepth = 0
            }
        };

        SetLeft(_centerHub, 155 - (InnerRadius * 2 - 6) / 2.0);
        SetTop(_centerHub, 150 - (InnerRadius * 2 - 6) / 2.0);

        var hubStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _centerIcon = new TextBlock
        {
            Text = "◎",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _centerLabel = new TextBlock
        {
            Text = "Halo",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247)),
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
            Text = "Наведіть курсор на сектор",
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Width = 310
        };
        SetLeft(_hoveredActionLabel, 0);
        SetTop(_hoveredActionLabel, 296);
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
        double gapDeg = count > 1 ? 3.5 : 0;
        double totalGap = gapDeg * count;
        double sectorSpan = (360.0 - totalGap) / count;
        Point center = new Point(155, 150);

        for (int i = 0; i < count; i++)
        {
            double startAngle = i * (sectorSpan + gapDeg);
            double endAngle = startAngle + sectorSpan;
            var action = _actions[i];

            var path = new Path
            {
                StrokeThickness = 1.2,
                Stroke = new SolidColorBrush(Color.FromRgb(58, 58, 74)),
                Cursor = Cursors.Hand
            };

            Color accentColor;
            try
            {
                accentColor = (Color)ColorConverter.ConvertFromString(action.AccentColor);
            }
            catch
            {
                accentColor = Color.FromRgb(139, 92, 246);
            }

            path.Fill = new LinearGradientBrush(
                Color.FromRgb(46, 46, 58),
                Color.FromRgb(35, 35, 45),
                45);

            path.Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius, OuterRadius, startAngle, endAngle);

            var mid = RadialGeometryHelper.GetRadialMidpoint(center, InnerRadius, OuterRadius, startAngle, endAngle);
            var iconBlock = new TextBlock
            {
                Text = action.IconKey,
                FontSize = 17,
                Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247)),
                IsHitTestVisible = false
            };
            SetLeft(iconBlock, mid.X - 10);
            SetTop(iconBlock, mid.Y - 11);

            path.MouseEnter += (s, e) =>
            {
                path.Fill = new LinearGradientBrush(
                    Color.FromArgb(235, accentColor.R, accentColor.G, accentColor.B),
                    Color.FromArgb(190, (byte)Math.Max(0, accentColor.R - 30), (byte)Math.Max(0, accentColor.G - 30), (byte)Math.Max(0, accentColor.B - 10)),
                    45);
                path.Stroke = new SolidColorBrush(Color.FromRgb(242, 243, 247));
                path.StrokeThickness = 1.6;
                path.Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius, OuterRadius + 9, startAngle, endAngle);
                _hoveredActionLabel.Text = $"{action.IconKey}  {action.Title} — {action.Description}";
                _hoveredActionLabel.Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247));
                ActionHovered?.Invoke(action);
            };

            path.MouseLeave += (s, e) =>
            {
                path.Fill = new LinearGradientBrush(
                    Color.FromRgb(46, 46, 58),
                    Color.FromRgb(35, 35, 45),
                    45);
                path.Stroke = new SolidColorBrush(Color.FromRgb(58, 58, 74));
                path.StrokeThickness = 1.2;
                path.Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius, OuterRadius, startAngle, endAngle);
                _hoveredActionLabel.Text = $"Активно секторів: {_actions.Count}";
                _hoveredActionLabel.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));
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
