using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Halo.Core.Models;
using Halo.Core.Services;

namespace Halo.App.Controls;

public class RadialDiscControl : Canvas
{
    private static readonly FontFamily FormatBadgeFont = new("Bahnschrift, Segoe UI Variable Display, Segoe UI");
    private static readonly FontFamily DefaultUiFont = new("Segoe UI Variable Display, Segoe UI");

    private readonly Canvas _slicesContainer;
    private readonly Border _centerHub;
    private readonly TextBlock _centerIcon;
    private readonly TextBlock _centerLabel;

    private List<ActionItem> _actions = new();
    private string _categoryName = "Зображення";
    private const double OuterRadius = 120;
    private const double InnerRadius = 46;

    public event Action<ActionItem>? ActionHovered;
    public event Action<ActionItem>? ActionClicked;

    private static bool IsEn => string.Equals(SettingsService.CurrentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
    private static string T(string uk, string en) => IsEn ? en : uk;

    public RadialDiscControl()
    {
        Width = 300;
        Height = 300;
        ClipToBounds = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        // Subtle ambient ring behind the disc (perfectly centered at 150, 150)
        double ringSize = OuterRadius * 2 + 24;
        var ambientRing = new Ellipse
        {
            Width = ringSize,
            Height = ringSize,
            Fill = new SolidColorBrush(Color.FromArgb(120, 23, 23, 28)),
            Stroke = new SolidColorBrush(Color.FromRgb(48, 48, 62)),
            StrokeThickness = 1.2
        };
        SetLeft(ambientRing, 150 - ringSize / 2.0);
        SetTop(ambientRing, 150 - ringSize / 2.0);
        Children.Add(ambientRing);

        _slicesContainer = new Canvas { Width = 300, Height = 300 };
        Children.Add(_slicesContainer);

        // Orbit-styled Center Hub (perfectly centered at 150, 150)
        double hubSize = InnerRadius * 2 - 4;
        _centerHub = new Border
        {
            Width = hubSize,
            Height = hubSize,
            CornerRadius = new CornerRadius(InnerRadius),
            Background = new SolidColorBrush(Color.FromRgb(23, 23, 28)),
            BorderBrush = new LinearGradientBrush(
                Color.FromRgb(168, 85, 247),
                Color.FromRgb(56, 189, 248),
                45),
            BorderThickness = new Thickness(2.2),
            Cursor = Cursors.Hand,
            ClipToBounds = true
        };

        SetLeft(_centerHub, 150 - hubSize / 2.0);
        SetTop(_centerHub, 150 - hubSize / 2.0);

        var hubStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(4, 2, 4, 2),
            MaxWidth = 76
        };

        _centerIcon = new TextBlock
        {
            Text = "◎",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 72,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _centerLabel = new TextBlock
        {
            Text = "Halo",
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 247)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 72,
            MaxHeight = 26,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            LineHeight = 11.5,
            Margin = new Thickness(0, 1, 0, 0)
        };

        hubStack.Children.Add(_centerIcon);
        hubStack.Children.Add(_centerLabel);
        _centerHub.Child = hubStack;

        Children.Add(_centerHub);
    }

    public void SetActions(List<ActionItem> actions, string categoryName)
    {
        _actions = actions.Where(a => a.IsEnabled).ToList();
        _categoryName = categoryName;
        ResetCenterHub();
        Redraw();
    }

    private void ResetCenterHub()
    {
        _centerIcon.FontFamily = DefaultUiFont;
        _centerIcon.FontSize = 16;
        _centerIcon.FontWeight = FontWeights.Bold;
        _centerIcon.Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250));
        _centerIcon.Text = "◎";
        _centerLabel.Text = _categoryName;
        _centerLabel.Foreground = new SolidColorBrush(Color.FromRgb(160, 163, 181));
    }

    private void Redraw()
    {
        _slicesContainer.Children.Clear();

        if (_actions.Count == 0)
        {
            return;
        }

        int count = _actions.Count;
        double gapDeg = count > 1 ? 3.5 : 0;
        double totalGap = gapDeg * count;
        double sectorSpan = (360.0 - totalGap) / count;
        Point center = new Point(150, 150);

        for (int i = 0; i < count; i++)
        {
            // Symmetrical angle calculation with gap split across the vertical axis
            double startAngle = i * (sectorSpan + gapDeg) + gapDeg / 2.0;
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
                Fill = new SolidColorBrush(Color.FromArgb(205, accentColor.R, accentColor.G, accentColor.B)),
                Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius + 1, InnerRadius + 4.5, startAngle + 1.5, endAngle - 1.5),
                IsHitTestVisible = false
            };

            bool isTextBadge = action.Category == ActionCategory.Convert || RadialSectorButton.IsAsciiFormatBadge(action.IconKey);
            double fontSize = isTextBadge
                ? (action.IconKey.Length >= 6 ? 9.5 : action.IconKey.Length >= 4 ? 11.0 : (count >= 8 ? 12.0 : 13.5))
                : (count >= 8 ? 15 : 17);

            var mid = RadialGeometryHelper.GetRadialMidpoint(center, InnerRadius + 8, OuterRadius, startAngle, endAngle);
            var iconBlock = new TextBlock
            {
                Text = action.IconKey,
                FontSize = fontSize,
                FontFamily = isTextBadge ? FormatBadgeFont : DefaultUiFont,
                FontWeight = isTextBadge ? FontWeights.ExtraBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(accentColor),
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
                path.Stroke = new SolidColorBrush(accentColor);
                path.StrokeThickness = 1.8;
                path.Data = RadialGeometryHelper.CreatePieSlice(center, InnerRadius, OuterRadius + 8, startAngle, endAngle);
                iconBlock.Foreground = Brushes.White;

                _centerIcon.FontFamily = isTextBadge ? FormatBadgeFont : DefaultUiFont;
                _centerIcon.FontWeight = isTextBadge ? FontWeights.ExtraBold : FontWeights.Bold;
                _centerIcon.FontSize = isTextBadge ? 14 : 16;
                _centerIcon.Foreground = new SolidColorBrush(accentColor);
                _centerIcon.Text = action.IconKey;
                _centerLabel.Text = isTextBadge ? T("Конвертація", "Convert") : action.Title;
                _centerLabel.Foreground = new SolidColorBrush(accentColor);

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
                iconBlock.Foreground = new SolidColorBrush(accentColor);

                ResetCenterHub();
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
