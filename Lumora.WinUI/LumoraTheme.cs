using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

internal enum LumoraWindowThemeRole
{
    WebApp,
    Incognito
}

internal static class LumoraTheme
{
    internal static bool ResolveIsDarkTheme(UiSettings settings)
    {
        var mode = settings.ThemeMode;
        if (string.Equals(mode, "light", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(mode, "dark", StringComparison.OrdinalIgnoreCase)) return true;

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int appsUseLightTheme)
            {
                return appsUseLightTheme == 0;
            }
        }
        catch { }

        return true;
    }

    internal static ElementTheme ResolveElementTheme(UiSettings settings) =>
        ResolveIsDarkTheme(settings) ? ElementTheme.Dark : ElementTheme.Light;

    internal static void ApplySecondaryWindowTheme(
        Panel root,
        AppWindow? appWindow,
        UiSettings settings,
        LumoraWindowThemeRole role)
    {
        var isDark = ResolveIsDarkTheme(settings);
        var palette = ResolvePalette(role, isDark);
        root.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        root.Background = new SolidColorBrush(palette.Background);

        SetBrush(root.Resources, "LumoraWindowBackgroundBrush", palette.Background);
        SetBrush(root.Resources, "LumoraWindowChromeBrush", palette.Chrome);
        SetBrush(root.Resources, "LumoraWindowAccentBrush", palette.Accent);
        SetBrush(root.Resources, "LumoraWindowCalloutBrush", palette.Callout);
        SetBrush(root.Resources, "LumoraWindowCalloutStrokeBrush", palette.CalloutStroke);
        SetBrush(root.Resources, "LumoraWindowSuccessBrush", palette.Success);
        SetBrush(root.Resources, "LumoraWindowDangerBrush", palette.Danger);
        SetBrush(root.Resources, "LumoraWindowTabStripBrush", palette.TabStrip);
        SetBrush(root.Resources, "LumoraWindowTextBrush", palette.Text);
        SetBrush(root.Resources, "LumoraWindowMutedTextBrush", palette.MutedText);
        SetIdentityBrush(root.Resources, palette.Accent, palette.CoolAccent);

        ApplySharedAppBrushes(
            palette.Accent,
            ResolveTextOnColor(palette.Accent),
            palette.Focus,
            palette.FocusInner,
            palette.Text,
            palette.MutedText,
            palette.Surface,
            palette.Chrome,
            palette.Stroke);

        ApplyTitleBar(appWindow, palette);
    }

    internal static void ApplySharedAppBrushes(
        Windows.UI.Color accent,
        Windows.UI.Color textOnAccent,
        Windows.UI.Color focus,
        Windows.UI.Color focusInner,
        Windows.UI.Color controlForeground,
        Windows.UI.Color controlMutedForeground,
        Windows.UI.Color controlSurface,
        Windows.UI.Color controlSurfaceRaised,
        Windows.UI.Color controlStroke)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        SetBrush(resources, "AccentFillColorDefaultBrush", accent);
        SetBrush(resources, "AccentFillColorSecondaryBrush", WithAlpha(accent, 221));
        SetBrush(resources, "AccentFillColorTertiaryBrush", WithAlpha(accent, 184));
        SetBrush(resources, "AccentFillColorDisabledBrush", WithAlpha(accent, 85));
        SetBrush(resources, "TextOnAccentFillColorPrimaryBrush", textOnAccent);
        SetBrush(resources, "NovaFocusVisualPrimaryBrush", focus);
        SetBrush(resources, "NovaFocusVisualSecondaryBrush", focusInner);
        SetBrush(resources, "NovaControlForegroundBrush", controlForeground);
        SetBrush(resources, "NovaControlMutedForegroundBrush", controlMutedForeground);
        SetBrush(resources, "NovaControlSurfaceBrush", controlSurface);
        SetBrush(resources, "NovaControlSurfaceRaisedBrush", controlSurfaceRaised);
        SetBrush(resources, "NovaControlStrokeBrush", controlStroke);
        var success = UiColor(61, 214, 136);
        var warning = UiColor(255, 185, 53);
        var danger = UiColor(229, 72, 77);
        SetBrush(resources, "NovaSuccessBrush", success);
        SetBrush(resources, "NovaSuccessSurfaceBrush", TintSurface(controlSurfaceRaised, success, 0.22));
        SetBrush(resources, "NovaWarningBrush", warning);
        SetBrush(resources, "NovaWarningSurfaceBrush", TintSurface(controlSurfaceRaised, warning, 0.20));
        SetBrush(resources, "NovaDangerBrush", danger);
        SetBrush(resources, "NovaDangerSurfaceBrush", TintSurface(controlSurfaceRaised, danger, 0.22));
    }

    internal static Windows.UI.Color ResolveTextOnColor(Windows.UI.Color background)
    {
        var luminance = (0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B);
        return luminance >= 154
            ? UiColor(13, 20, 34)
            : UiColor(255, 248, 234);
    }

    internal static Windows.UI.Color UiColor(byte r, byte g, byte b, byte a = 255) =>
        new() { A = a, R = r, G = g, B = b };

    internal static Windows.UI.Color WithAlpha(Windows.UI.Color color, byte alpha) =>
        new() { A = alpha, R = color.R, G = color.G, B = color.B };

    private static Windows.UI.Color TintSurface(Windows.UI.Color surface, Windows.UI.Color accent, double weight)
    {
        static byte Blend(byte baseValue, byte accentValue, double tintWeight) =>
            (byte)Math.Clamp((int)Math.Round(baseValue * (1 - tintWeight) + accentValue * tintWeight), 0, 255);

        return UiColor(
            Blend(surface.R, accent.R, weight),
            Blend(surface.G, accent.G, weight),
            Blend(surface.B, accent.B, weight));
    }

    private static SecondaryWindowPalette ResolvePalette(LumoraWindowThemeRole role, bool isDark) =>
        role switch
        {
            LumoraWindowThemeRole.Incognito => isDark
                ? new(
                    UiColor(16, 15, 28),
                    UiColor(32, 26, 48),
                    UiColor(24, 20, 38),
                    UiColor(71, 59, 98),
                    UiColor(246, 239, 255),
                    UiColor(193, 182, 220),
                    UiColor(180, 138, 255),
                    UiColor(115, 230, 220),
                    UiColor(176, 136, 255),
                    UiColor(13, 11, 24),
                    UiColor(54, 214, 140),
                    UiColor(229, 72, 77),
                    UiColor(41, 35, 57),
                    UiColor(82, 70, 116),
                    UiColor(16, 15, 28))
                : new(
                    UiColor(246, 243, 252),
                    UiColor(232, 224, 245),
                    UiColor(255, 255, 255),
                    UiColor(207, 194, 225),
                    UiColor(34, 26, 44),
                    UiColor(108, 95, 126),
                    UiColor(124, 86, 204),
                    UiColor(52, 166, 156),
                    UiColor(124, 86, 204),
                    UiColor(255, 255, 255),
                    UiColor(38, 145, 97),
                    UiColor(191, 59, 64),
                    UiColor(242, 236, 248),
                    UiColor(207, 194, 225),
                    UiColor(246, 243, 252)),
            _ => isDark
                ? new(
                    UiColor(9, 13, 20),
                    UiColor(16, 21, 31),
                    UiColor(20, 26, 38),
                    UiColor(47, 56, 72),
                    UiColor(242, 245, 250),
                    UiColor(171, 178, 191),
                    UiColor(230, 170, 72),
                    UiColor(86, 194, 228),
                    UiColor(195, 214, 255),
                    UiColor(9, 13, 20),
                    UiColor(61, 214, 136),
                    UiColor(229, 72, 77),
                    UiColor(24, 32, 44),
                    UiColor(49, 58, 74),
                    UiColor(14, 20, 30))
                : new(
                    UiColor(248, 246, 241),
                    UiColor(255, 253, 249),
                    UiColor(243, 239, 232),
                    UiColor(214, 209, 200),
                    UiColor(31, 29, 26),
                    UiColor(120, 113, 102),
                    UiColor(176, 111, 0),
                    UiColor(0, 129, 168),
                    UiColor(176, 111, 0),
                    UiColor(255, 255, 255),
                    UiColor(38, 145, 97),
                    UiColor(191, 59, 64),
                    UiColor(243, 239, 232),
                    UiColor(215, 207, 189),
                    UiColor(248, 246, 241))
        };

    private static void ApplyTitleBar(AppWindow? appWindow, SecondaryWindowPalette palette)
    {
        if (appWindow is null) return;

        var titleBar = appWindow.TitleBar;
        titleBar.BackgroundColor = palette.Chrome;
        titleBar.InactiveBackgroundColor = palette.TabStrip;
        titleBar.ForegroundColor = palette.Text;
        titleBar.InactiveForegroundColor = palette.MutedText;
        titleBar.ButtonBackgroundColor = palette.Chrome;
        titleBar.ButtonInactiveBackgroundColor = palette.TabStrip;
        titleBar.ButtonForegroundColor = palette.Text;
        titleBar.ButtonInactiveForegroundColor = palette.MutedText;
        titleBar.ButtonHoverBackgroundColor = palette.Surface;
        titleBar.ButtonHoverForegroundColor = palette.Text;
        titleBar.ButtonPressedBackgroundColor = palette.Stroke;
        titleBar.ButtonPressedForegroundColor = palette.Text;
    }

    private static void SetBrush(ResourceDictionary resources, string key, Windows.UI.Color color)
    {
        if (resources[key] is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private static void SetIdentityBrush(ResourceDictionary resources, Windows.UI.Color first, Windows.UI.Color second)
    {
        if (resources["LumoraWindowIdentityBrush"] is LinearGradientBrush brush && brush.GradientStops.Count >= 2)
        {
            brush.GradientStops[0].Color = first;
            brush.GradientStops[1].Color = second;
        }
    }

    private readonly record struct SecondaryWindowPalette(
        Windows.UI.Color Background,
        Windows.UI.Color Chrome,
        Windows.UI.Color Surface,
        Windows.UI.Color Stroke,
        Windows.UI.Color Text,
        Windows.UI.Color MutedText,
        Windows.UI.Color Accent,
        Windows.UI.Color CoolAccent,
        Windows.UI.Color Focus,
        Windows.UI.Color FocusInner,
        Windows.UI.Color Success,
        Windows.UI.Color Danger,
        Windows.UI.Color Callout,
        Windows.UI.Color CalloutStroke,
        Windows.UI.Color TabStrip);
}
