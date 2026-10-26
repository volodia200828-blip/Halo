using System;
using System.Windows;
using System.Windows.Media;

namespace Halo.App.Services;

public class ThemeManager
{
    private static readonly Lazy<ThemeManager> _instance = new(() => new ThemeManager());
    public static ThemeManager Instance => _instance.Value;

    public string CurrentTheme { get; private set; } = "Dark";

    public event Action<string>? ThemeChanged;

    public void ApplyTheme(string theme)
    {
        CurrentTheme = string.IsNullOrWhiteSpace(theme) ? "Dark" : theme;

        if (Application.Current == null) return;
        var res = Application.Current.Resources;
        if (res == null) return;

        if (CurrentTheme == "Light")
        {
            ApplyLightTheme(res);
        }
        else if (CurrentTheme == "Indigo")
        {
            ApplyIndigoTheme(res);
        }
        else
        {
            ApplyDarkTheme(res);
        }

        ThemeChanged?.Invoke(CurrentTheme);
    }

    private void ApplyLightTheme(ResourceDictionary res)
    {
        // Layout & Cards
        res["OrbitWindowBgBrush"] = new SolidColorBrush(Color.FromRgb(243, 244, 248));
        res["OrbitSidebarBgBrush"] = new SolidColorBrush(Color.FromRgb(233, 235, 242));
        res["OrbitCardBgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        res["OrbitCardHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(241, 239, 253));
        res["OrbitRecessedBgBrush"] = new SolidColorBrush(Color.FromRgb(240, 242, 248));
        res["OrbitSidebarDividerBrush"] = new SolidColorBrush(Color.FromRgb(214, 217, 228));
        res["OrbitCardBorderBrush"] = new SolidColorBrush(Color.FromRgb(212, 216, 228));
        res["OrbitSubtleBorderBrush"] = new SolidColorBrush(Color.FromRgb(218, 222, 234));

        // Typography (Crisp, high contrast standards WCAG AAA/AA)
        res["OrbitTextPrimary"] = new SolidColorBrush(Color.FromRgb(24, 25, 36));
        res["OrbitTextSecondary"] = new SolidColorBrush(Color.FromRgb(71, 76, 98));
        res["OrbitTextMuted"] = new SolidColorBrush(Color.FromRgb(108, 114, 138));

        // Section / Quick action icon soft backgrounds (Clean soft pastels with excellent contrast)
        res["OrbitPurpleSoftBg"] = new SolidColorBrush(Color.FromRgb(237, 233, 254));
        res["OrbitBlueSoftBg"] = new SolidColorBrush(Color.FromRgb(224, 242, 254));
        res["OrbitGreenSoftBg"] = new SolidColorBrush(Color.FromRgb(209, 250, 229));
        res["OrbitAmberSoftBg"] = new SolidColorBrush(Color.FromRgb(254, 243, 199));
        res["OrbitRoseSoftBg"] = new SolidColorBrush(Color.FromRgb(255, 228, 230));
        res["OrbitIndigoSoftBg"] = new SolidColorBrush(Color.FromRgb(224, 231, 255));
        res["OrbitOrangeSoftBg"] = new SolidColorBrush(Color.FromRgb(255, 237, 213));

        // Accents
        res["OrbitPurpleLightBrush"] = new SolidColorBrush(Color.FromRgb(109, 40, 217));
        res["OrbitCyanBrush"] = new SolidColorBrush(Color.FromRgb(2, 132, 199));
        res["OrbitGreenLightBrush"] = new SolidColorBrush(Color.FromRgb(5, 150, 105));
        res["OrbitAmberLightBrush"] = new SolidColorBrush(Color.FromRgb(217, 119, 6));
        res["OrbitRoseLightBrush"] = new SolidColorBrush(Color.FromRgb(225, 29, 72));
        res["OrbitRoseBrush"] = new SolidColorBrush(Color.FromRgb(225, 29, 72));
        res["OrbitIndigoLightBrush"] = new SolidColorBrush(Color.FromRgb(79, 70, 229));
        res["OrbitIndigoBrush"] = new SolidColorBrush(Color.FromRgb(79, 70, 229));
        res["OrbitOrangeLightBrush"] = new SolidColorBrush(Color.FromRgb(234, 88, 12));
        res["OrbitOrangeBrush"] = new SolidColorBrush(Color.FromRgb(234, 88, 12));

        // Navigation
        res["OrbitNavActiveBgGradient"] = new LinearGradientBrush(
            Color.FromRgb(221, 214, 254),
            Color.FromRgb(237, 233, 254),
            0);
        res["OrbitNavActiveBorderBrush"] = new SolidColorBrush(Color.FromRgb(139, 92, 246));
        res["OrbitNavActiveTextBrush"] = new SolidColorBrush(Color.FromRgb(59, 7, 100));

        // Hero banner
        res["OrbitHeroBannerGradient"] = new LinearGradientBrush(
            Color.FromRgb(237, 233, 254),
            Color.FromRgb(224, 231, 255),
            45);
        res["OrbitHeroBorderBrush"] = new SolidColorBrush(Color.FromRgb(167, 139, 250));
        res["OrbitHeroBadgeBgBrush"] = new SolidColorBrush(Color.FromRgb(221, 214, 254));
        res["OrbitHeroBadgeTextBrush"] = new SolidColorBrush(Color.FromRgb(91, 33, 182));
        res["OrbitHeroTitleBrush"] = new SolidColorBrush(Color.FromRgb(30, 27, 75));
        res["OrbitHeroDescBrush"] = new SolidColorBrush(Color.FromRgb(67, 56, 202));
        res["OrbitHeroRingBgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        res["OrbitLogoBoxBgBrush"] = new SolidColorBrush(Color.FromRgb(237, 233, 254));
        res["OrbitLogoBoxBorderBrush"] = new SolidColorBrush(Color.FromRgb(167, 139, 250));

        // Toggle Switch Muted/Off State (Clean, readable, muted)
        res["OrbitToggleTrackOffBrush"] = new SolidColorBrush(Color.FromRgb(226, 229, 240));
        res["OrbitToggleBorderOffBrush"] = new SolidColorBrush(Color.FromRgb(203, 210, 225));
        res["OrbitToggleThumbOffBrush"] = new SolidColorBrush(Color.FromRgb(140, 147, 168));

        // Component Logs in Light Theme (Dynamic adaptation with high contrast WCAG standards)
        res["OrbitLogCardBgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        res["OrbitLogItemBgBrush"] = new SolidColorBrush(Color.FromRgb(244, 245, 250));
        res["OrbitLogItemBorderBrush"] = new SolidColorBrush(Color.FromRgb(220, 224, 236));
        res["OrbitLogTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(24, 25, 36));
        res["OrbitLogTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(71, 76, 98));
        res["OrbitLogTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        res["OrbitLogTimestampBrush"] = new SolidColorBrush(Color.FromRgb(2, 132, 199));
    }

    private void ApplyIndigoTheme(ResourceDictionary res)
    {
        res["OrbitWindowBgBrush"] = new SolidColorBrush(Color.FromRgb(15, 20, 34));
        res["OrbitSidebarBgBrush"] = new SolidColorBrush(Color.FromRgb(11, 15, 26));
        res["OrbitCardBgBrush"] = new SolidColorBrush(Color.FromRgb(24, 32, 52));
        res["OrbitCardHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(32, 43, 68));
        res["OrbitRecessedBgBrush"] = new SolidColorBrush(Color.FromRgb(13, 18, 30));
        res["OrbitSidebarDividerBrush"] = new SolidColorBrush(Color.FromRgb(28, 38, 60));
        res["OrbitCardBorderBrush"] = new SolidColorBrush(Color.FromRgb(42, 56, 88));
        res["OrbitSubtleBorderBrush"] = new SolidColorBrush(Color.FromRgb(32, 44, 70));
        res["OrbitTextPrimary"] = new SolidColorBrush(Color.FromRgb(242, 245, 252));
        res["OrbitTextSecondary"] = new SolidColorBrush(Color.FromRgb(154, 168, 198));
        res["OrbitTextMuted"] = new SolidColorBrush(Color.FromRgb(104, 118, 150));

        ApplyDarkAccents(res);

        // Indigo specific log colors
        res["OrbitLogCardBgBrush"] = new SolidColorBrush(Color.FromRgb(24, 32, 52));
        res["OrbitLogItemBgBrush"] = new SolidColorBrush(Color.FromRgb(13, 18, 30));
        res["OrbitLogItemBorderBrush"] = new SolidColorBrush(Color.FromRgb(32, 44, 70));
        res["OrbitLogTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(242, 245, 252));
        res["OrbitLogTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(154, 168, 198));
        res["OrbitLogTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(104, 118, 150));
        res["OrbitLogTimestampBrush"] = new SolidColorBrush(Color.FromRgb(56, 189, 248));
    }

    private void ApplyDarkTheme(ResourceDictionary res)
    {
        res["OrbitWindowBgBrush"] = new SolidColorBrush(Color.FromRgb(30, 30, 36));
        res["OrbitSidebarBgBrush"] = new SolidColorBrush(Color.FromRgb(23, 23, 28));
        res["OrbitCardBgBrush"] = new SolidColorBrush(Color.FromRgb(40, 40, 50));
        res["OrbitCardHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(47, 47, 61));
        res["OrbitRecessedBgBrush"] = new SolidColorBrush(Color.FromRgb(27, 27, 33));
        res["OrbitSidebarDividerBrush"] = new SolidColorBrush(Color.FromRgb(38, 38, 48));
        res["OrbitCardBorderBrush"] = new SolidColorBrush(Color.FromRgb(53, 53, 69));
        res["OrbitSubtleBorderBrush"] = new SolidColorBrush(Color.FromRgb(44, 44, 56));
        res["OrbitTextPrimary"] = new SolidColorBrush(Color.FromRgb(242, 243, 247));
        res["OrbitTextSecondary"] = new SolidColorBrush(Color.FromRgb(160, 163, 181));
        res["OrbitTextMuted"] = new SolidColorBrush(Color.FromRgb(108, 112, 133));

        ApplyDarkAccents(res);

        // Dark log colors
        res["OrbitLogCardBgBrush"] = new SolidColorBrush(Color.FromRgb(40, 40, 50));
        res["OrbitLogItemBgBrush"] = new SolidColorBrush(Color.FromRgb(27, 27, 33));
        res["OrbitLogItemBorderBrush"] = new SolidColorBrush(Color.FromRgb(44, 44, 56));
        res["OrbitLogTextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(242, 243, 247));
        res["OrbitLogTextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(160, 163, 181));
        res["OrbitLogTextMutedBrush"] = new SolidColorBrush(Color.FromRgb(118, 122, 146));
        res["OrbitLogTimestampBrush"] = new SolidColorBrush(Color.FromRgb(56, 189, 248));
    }

    private void ApplyDarkAccents(ResourceDictionary res)
    {
        // Brightened and enhanced lightness of base icon backgrounds to prevent blending into dark backgrounds
        // (Significantly increased contrast and vibrancy against #1E1E24 / #282832)
        res["OrbitPurpleSoftBg"] = new SolidColorBrush(Color.FromRgb(61, 46, 104)); // Brightened Purple Soft Bg
        res["OrbitBlueSoftBg"] = new SolidColorBrush(Color.FromRgb(34, 66, 114));   // Brightened Blue Soft Bg
        res["OrbitGreenSoftBg"] = new SolidColorBrush(Color.FromRgb(32, 80, 65));   // Brightened Green Soft Bg
        res["OrbitAmberSoftBg"] = new SolidColorBrush(Color.FromRgb(84, 61, 31));   // Brightened Amber Soft Bg
        res["OrbitRoseSoftBg"] = new SolidColorBrush(Color.FromRgb(90, 39, 63));    // Brightened Rose Soft Bg
        res["OrbitIndigoSoftBg"] = new SolidColorBrush(Color.FromRgb(38, 40, 90));  // Document Indigo Soft Bg
        res["OrbitOrangeSoftBg"] = new SolidColorBrush(Color.FromRgb(84, 43, 20));  // Archive Orange Soft Bg

        res["OrbitPurpleLightBrush"] = new SolidColorBrush(Color.FromRgb(167, 139, 250));
        res["OrbitCyanBrush"] = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        res["OrbitGreenLightBrush"] = new SolidColorBrush(Color.FromRgb(52, 211, 153));
        res["OrbitAmberLightBrush"] = new SolidColorBrush(Color.FromRgb(251, 191, 36));
        res["OrbitRoseLightBrush"] = new SolidColorBrush(Color.FromRgb(251, 113, 133));
        res["OrbitRoseBrush"] = new SolidColorBrush(Color.FromRgb(244, 63, 94));
        res["OrbitIndigoLightBrush"] = new SolidColorBrush(Color.FromRgb(129, 140, 248));
        res["OrbitIndigoBrush"] = new SolidColorBrush(Color.FromRgb(99, 102, 241));
        res["OrbitOrangeLightBrush"] = new SolidColorBrush(Color.FromRgb(251, 146, 60));
        res["OrbitOrangeBrush"] = new SolidColorBrush(Color.FromRgb(249, 115, 22));

        res["OrbitNavActiveBgGradient"] = new LinearGradientBrush(
            Color.FromRgb(54, 43, 89),
            Color.FromRgb(37, 36, 61),
            0);
        res["OrbitNavActiveBorderBrush"] = new SolidColorBrush(Color.FromRgb(84, 66, 142));
        res["OrbitNavActiveTextBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        res["OrbitHeroBannerGradient"] = new LinearGradientBrush(
            Color.FromRgb(46, 38, 74),
            Color.FromRgb(38, 40, 52),
            45);
        res["OrbitHeroBorderBrush"] = new SolidColorBrush(Color.FromRgb(74, 60, 117));
        res["OrbitHeroBadgeBgBrush"] = new SolidColorBrush(Color.FromRgb(61, 49, 99));
        res["OrbitHeroBadgeTextBrush"] = new SolidColorBrush(Color.FromRgb(196, 181, 253));
        res["OrbitHeroTitleBrush"] = new SolidColorBrush(Color.FromRgb(242, 243, 247));
        res["OrbitHeroDescBrush"] = new SolidColorBrush(Color.FromRgb(184, 187, 224));
        res["OrbitHeroRingBgBrush"] = new SolidColorBrush(Color.FromRgb(29, 26, 46));
        res["OrbitLogoBoxBgBrush"] = new SolidColorBrush(Color.FromRgb(34, 32, 48));
        res["OrbitLogoBoxBorderBrush"] = new SolidColorBrush(Color.FromRgb(70, 58, 110));

        // Toggle Switch Muted/Off State (Crisp, predictable, muted but distinct)
        res["OrbitToggleTrackOffBrush"] = new SolidColorBrush(Color.FromRgb(42, 43, 56));
        res["OrbitToggleBorderOffBrush"] = new SolidColorBrush(Color.FromRgb(74, 77, 100));
        res["OrbitToggleThumbOffBrush"] = new SolidColorBrush(Color.FromRgb(152, 156, 180));
    }
}
