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
    private string _categoryName = "Зображення";
    private const double OuterRadius = 118;
    private const double InnerRadius = 46;

    public event Action<ActionItem>? ActionHovered;
    public event Action<ActionItem>? ActionClicked;

    public RadialDiscControl()
    {
        Width = 300;
        Height = 325;
        ClipToBounds = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        // Subtle ambient ring behind the disc
        var ambientRing = new Ellipse
        {
            Width = OuterRadius * 2 + 22,
            Height = OuterRadius * 2 + 22,
            Fill = new SolidColorBrush(Color.FromArgb(120, 23, 23, 28)),
            Stroke = new SolidColorBrush(Color.FromRgb(48, 48, 62)),
            StrokeThickness = 1.2
        };
        SetLeft(ambientRing, 150 - (OuterRadius + 11));
        SetTop(ambientRing, 142 - (OuterRadius + 11));
        Children.Add(ambientRing);

        _slicesContainer = new Canvas { Width = 300, Height = 284 };
        Children.Add(_slicesContainer);

        // Orbit-styled Center Hub (without DropShadowEffect on the parent Border so text stays 100% sharp!)
        _centerHub = new Border
        {
            Width = InnerRadius * 2 - 6,
            Height = InnerRadius * 2 - 6,
            CornerRadius = new CornerRadius(InnerRadius),
            Background = new SolidColorBrush(Color.FromRgb(23, 23, 28)),
            BorderBrush = new LinearGradientBrush(
                Color.FromRgb(168, 85, 247),
                Color.FromRgb(56, 189, 248),
                45),
            BorderThickness = new Thickness(2.2),
            Cursor = Cursors.Hand
        };

        SetLeft(_centerHub, 150 - (InnerRadius * 2 - 6) / 2.0);
        SetTop(_centerHub, 142 - (InnerRadius * 2 - 6) / 2.0);

        var hubStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(4)
        };

        _centerIcon = new TextBlock
        {
            Text = "◎",
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _centerLabel = new TextBlock
        {
            Text = "Halo",
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 72,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0)
        };

        hubStack.Children.Add(_centerIcon);
        hubStack.Children.Add(_centerLabel);
        _centerHub.Child = hubStack;

        Children.Add(_centerHub);

        // Status description below the disc (properly spaced so it never collides with the wheel)
        _hoveredActionLabel = new TextBlock
        {
            Text = "Наведіть курсор на сектор",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Width = 290
        };
        SetLeft(_hoveredActionLabel, 5);
        SetTop(_hoveredActionLabel, 294);
        Children.Add(_hoveredActionLabel);
    }

    public void SetActions(List<ActionItem> actions, string categoryName)
    {
        _actions = actions.Where(a => a.IsEnabled && a.Id != "system_shelf_add").ToList();
        _categoryName = categoryName;
        _centerIcon.Text = "◎";
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
        Point center = new Point(150, 142);

        for (int i = 0; i < count; i++)
        {
            double startAngle = i * (sectorSpan + gapDeg);
            double endAngle = startAngle + sectorSpan;
            var action = _actions[i];

            var path = new Path
            {
                StrokeThickness = 1.3,
                Stroke = new SolidColorBrush(Color.FromRgb(58, 58, 76)),
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true
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
                Color.FromRgb(42, 42, 54),
                Color.FromRgb(30, 30, 40),
                45);

            path.Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius, OuterRadius, startAngle, endAngle);

            var accentArc = new Path
            {
                Fill = new SolidColorBrush(Color.FromArgb(200, accentColor.R, accentColor.G, accentColor.B)),
                Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius + 1, InnerRadius + 4.5, startAngle + 1.5, endAngle - 1.5),
                IsHitTestVisible = false
            };

            var mid = RadialGeometryHelper.GetRadialMidpoint(center, InnerRadius + 6, OuterRadius, startAngle, endAngle);
            var iconBlock = new TextBlock
            {
                Text = action.IconKey,
                FontSize = count >= 8 ? 15 : 17,
                Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247)),
                IsHitTestVisible = false,
                TextAlignment = TextAlignment.Center
            };
            iconBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double iw = iconBlock.DesiredSize.Width > 0 ? iconBlock.DesiredSize.Width : 18;
            double ih = iconBlock.DesiredSize.Height > 0 ? iconBlock.DesiredSize.Height : 18;
            SetLeft(iconBlock, Math.Round(mid.X - iw / 2.0));
            SetTop(iconBlock, Math.Round(mid.Y - ih / 2.0));

            path.MouseEnter += (s, e) =>
            {
                path.Fill = new LinearGradientBrush(
                    Color.FromArgb(245, (byte)((54 + accentColor.R) / 2), (byte)((43 + accentColor.G) / 2), (byte)((89 + accentColor.B) / 2)),
                    Color.FromArgb(245, 37, 36, 61),
                    45);
                path.Stroke = new SolidColorBrush(Color.FromRgb(196, 181, 253));
                path.StrokeThickness = 1.8;
                path.Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius, OuterRadius + 8, startAngle, endAngle);

                _centerIcon.Text = action.IconKey;
                _centerLabel.Text = action.Title;
                _hoveredActionLabel.Text = $"{action.Title} — {action.Description}";
                _hoveredActionLabel.Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247));
                ActionHovered?.Invoke(action);
            };

            path.MouseLeave += (s, e) =>
            {
                path.Fill = new LinearGradientBrush(
                    Color.FromRgb(42, 42, 54),
                    Color.FromRgb(30, 30, 40),
                    45);
                path.Stroke = new SolidColorBrush(Color.FromRgb(58, 58, 76));
                path.StrokeThickness = 1.3;
                path.Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius, OuterRadius, startAngle, endAngle);

                _centerIcon.Text = "◎";
                _centerLabel.Text = _categoryName;
                _hoveredActionLabel.Text = $"Активно секторів: {_actions.Count}";
                _hoveredActionLabel.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));
            };

            path.MouseLeftButtonDown += (s, e) =>
            {
                ActionClicked?.Invoke(action);
            };

            _slicesContainer.Children.Add(path);
            _slicesContainer.Children.Add(accentArc);
            _slicesContainer.Children.Add(iconBlock);
        }
    }
}
