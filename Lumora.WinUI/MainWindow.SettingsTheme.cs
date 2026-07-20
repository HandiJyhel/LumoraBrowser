using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lumora.WinUI;

// Moteur de theme clair/sombre, palette d'accent, couleurs du mode d'usage,
// et P/Invoke de l'arriere-plan de fenetre (meme thematique visuelle).
// Extrait de MainWindow.Settings.cs (god file) : ne depend que de
// _uiSettings et des ressources XAML, aucun changement de comportement.
public sealed partial class MainWindow
{
    // "system" est resolu une fois par appel (pas d'ecoute live du changement de
    // theme Windows en cours de session : re-ouvrir Parametres ou changer de
    // reglage suffit a la reprendre en compte).
    private bool ResolveIsDarkTheme()
    {
        var mode = _uiSettings.ThemeMode;
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

        return true; // repli : sombre (identite historique Lumora)
    }

    private void ApplyAccessibilitySettings()
    {
        var highContrast = _uiSettings.AccessibilityHighContrast;
        var largeText = _uiSettings.AccessibilityLargeText;
        var visibleFocus = _uiSettings.AccessibilityVisibleFocus;
        var translucent = IsTranslucentChromeEnabled() && !highContrast;
        var isDark = highContrast || ResolveIsDarkTheme();
        var palette = ResolveAccentPalette(isDark, highContrast);

        RootShell.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;

        RootShell.Background = translucent
            ? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            : new SolidColorBrush(highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(13, 20, 34) : UiColor(250, 248, 244)));
        SetBrush("NovaChromeSurfaceBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(20, 28, 42, translucent ? (byte)226 : (byte)255) : UiColor(255, 255, 255, translucent ? (byte)226 : (byte)255)));
        SetBrush("NovaChromeSurfaceAltBrush", highContrast ? UiColor(18, 18, 18) : (isDark ? UiColor(26, 34, 49, translucent ? (byte)232 : (byte)255) : UiColor(242, 239, 234, translucent ? (byte)232 : (byte)255)));
        SetBrush("NovaChromeStrokeBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(64, 73, 91) : UiColor(214, 209, 200)));
        SetBrush("NovaChromeStrokeSoftBrush", highContrast ? UiColor(190, 190, 190) : (isDark ? UiColor(36, 44, 60) : UiColor(228, 224, 217)));
        SetBrush("NovaAddressBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(23, 33, 52, translucent ? (byte)236 : (byte)255) : UiColor(255, 255, 255, translucent ? (byte)236 : (byte)255)));
        SetBrush("NovaAddressBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(70, 83, 106) : UiColor(198, 192, 182)));
        SetBrush("NovaAddressForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 248, 234) : UiColor(31, 29, 26)));
        SetBrush("NovaTextMutedBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(195, 185, 165) : UiColor(120, 113, 102)));
        SetBrush("NovaAccentBrush", palette.Accent);
        SetBrush("NovaAccentSoftBrush", palette.AccentSoft);
        SetBrush("NovaCoolAccentBrush", palette.CoolAccent);
        SetBrush("NovaCoolAccentSoftBrush", palette.CoolAccentSoft);
        SetBrush("NovaCompanionGlassBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(25, 33, 49, 214) : UiColor(255, 255, 255, 228)));
        SetBrush("NovaCompanionStrokeBrush", highContrast ? UiColor(255, 255, 255) : palette.CoolAccentSoft);
        SetBrush("NovaModeSelectorGlassBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(22, 30, 45, 208) : UiColor(255, 255, 255, 224)));
        SetBrush("NovaModeSelectorStrokeBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(52, 60, 76, 160) : UiColor(214, 209, 200, 180)));
        SetBrush("NovaModuleHubGlassBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(22, 30, 45, 214) : UiColor(255, 255, 255, 226)));
        SetBrush("NovaModuleHubStrokeBrush", highContrast ? UiColor(255, 255, 255) : palette.CoolAccentSoft);
        SetBrush("NovaModuleHubNodeBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(195, 185, 165) : UiColor(84, 78, 70)));
        SetBrush("NovaModuleHubAccentBrush", highContrast ? UiColor(255, 213, 0) : palette.CoolAccent);
        SetBrush("NovaFocusBrush", palette.Focus);
        SetBrush("NovaFocusInnerBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(13, 20, 34) : UiColor(255, 255, 255)));
        SetBrush("NovaInfoSurfaceBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(29, 38, 56, translucent ? (byte)238 : (byte)255) : UiColor(247, 245, 240, translucent ? (byte)238 : (byte)255)));
        SetBrush("NovaPanelBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(16, 24, 38) : UiColor(250, 248, 245)));
        ApplyUsageModeChrome(isDark, highContrast, translucent);

        var mainFontSize = largeText ? 16 : 14;
        var smallFontSize = largeText ? 14 : 12;
        AddressBox.FontSize = mainFontSize;
        CommandPaletteSearchBox.FontSize = mainFontSize;
        CommandPaletteHintText.FontSize = smallFontSize;
        StatusText.FontSize = smallFontSize;
        ProfileStatusText.FontSize = smallFontSize;

        foreach (var textBlock in new[] { CredentialSaveText, AutoFillText, WalletFillText, SessionKeepText })
        {
            textBlock.FontSize = mainFontSize;
        }

        var focusThickness = visibleFocus ? new Thickness(2) : new Thickness(1);
        AddressBox.BorderThickness = focusThickness;
        CommandPaletteSearchBox.BorderThickness = focusThickness;

        foreach (var control in new Control[]
                 {
                     AddressBox,
                     CommandPaletteSearchBox,
                     CompactModeButton,
                     FullScreenExitButton,
                     DetachVideoButton,
                     DetachVideoPinnedButton,
                     VideoDownloadButton,
                     ReadAloudButton,
                     ReaderModeButton,
                     SearchAssistButton,
                     NotesModuleButton,
                     TranslatePinnedButton,
                     TranslateModuleButton,
                     WebAppsPinnedButton,
                     WebAppsQuickButton,
                     ModulesButton,
                     ModeCompanionButton,
                     ModeCompanionMemoryBox,
                     ModeCompanionSaveButton,
                     ModeCompanionPrimaryButton,
                     ModeCompanionSecondaryButton,
                     UsageModeButton,
                     DictationPinnedButton,
                     VideoDownloadStartButton,
                     NavigationMenuButton,
                     MainMenuButton,
                     ShieldButton,
                     VaultQuickAccessButton,
                     MicDictationButton,
                     CredentialSaveAccept,
                     CredentialSaveDismiss,
                     AutoFillAccept,
                     AutoFillDismiss,
                     WalletFillAccept,
                     WalletFillDismiss,
                     SessionKeepAccept,
                     SessionKeepDismiss,
                     BackToPageButton,
                     ProfileStatusButton,
                     ApplySettingsChangesButton
                 })
        {
            ApplyNovaControlAccessibility(control);
        }

        RefreshAccessibilitySensitivePanels();
        UpdateDictationButtonVisibility();
        _ = ApplyReadingGuideToAllTabsAsync();
        ApplyWindowTitleBarColors();
        UpdateUsageModeButtonUi();
    }

    private void RefreshAccessibilitySensitivePanels()
    {
        if (DownloadsPanel.Visibility == Visibility.Visible)
        {
            RenderDownloads();
        }

        if (SavedTabGroupsPanel.Visibility == Visibility.Visible)
        {
            RenderSavedTabGroups();
        }

        if (WebAppsPanel.Visibility == Visibility.Visible)
        {
            RefreshWebAppsPanel();
        }

        if (PasskeysPanel.Visibility == Visibility.Visible)
        {
            RenderPasskeysPanel();
        }

        if (WalletPanel.Visibility == Visibility.Visible)
        {
            RefreshWalletPanel();
        }

        if (VaultPanel.Visibility == Visibility.Visible)
        {
            RefreshVaultPanel();
        }

        if (SessionsPanel.Visibility == Visibility.Visible)
        {
            _ = RefreshSessionsPanelAsync();
        }

        if (SiteControlPanel.Visibility == Visibility.Visible && CurrentSite() is { } site)
        {
            _ = RefreshSiteControlAsync(site);
        }
    }

    private void SetBrush(string key, Windows.UI.Color color)
    {
        if (RootShell.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private void ApplyUsageModeChrome(bool isDark, bool highContrast, bool translucent)
    {
        if (highContrast)
        {
            ModeChromeAccentStrip.Height = 3;
            ModeChromeAccentStrip.Opacity = 1;
            UsageModeButton.Background = new SolidColorBrush(UiColor(0, 0, 0));
            UsageModeButton.BorderBrush = new SolidColorBrush(UiColor(255, 255, 255));
            UsageModeButton.Foreground = new SolidColorBrush(UiColor(255, 255, 255));
            ModeCompanionButton.Background = new SolidColorBrush(UiColor(0, 0, 0));
            ModeCompanionButton.BorderBrush = new SolidColorBrush(UiColor(255, 255, 255));
            ModeCompanionButton.Foreground = new SolidColorBrush(UiColor(255, 255, 255));
            UsageModeAccentBar.Background = new SolidColorBrush(UiColor(255, 213, 0));
            ModeCompanionAccentDot.Background = new SolidColorBrush(UiColor(0, 255, 226));
            ModeCompanionGlow.Background = new SolidColorBrush(UiColor(0, 0, 0));
            ModulesButton.Background = new SolidColorBrush(UiColor(0, 0, 0));
            ModulesButton.BorderBrush = new SolidColorBrush(UiColor(255, 255, 255));
            ModulesButton.Foreground = new SolidColorBrush(UiColor(255, 255, 255));
            SetIdentityGradient(UiColor(255, 213, 0), UiColor(255, 255, 255), UiColor(0, 255, 226));
            NavigationToolbar.Opacity = 1;
            VerticalTabsRail.Opacity = 1;
            return;
        }

        var alpha = translucent ? (byte)232 : (byte)255;
        var mode = (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant();
        var chrome = ResolveModeChromePalette(mode, isDark, alpha);

        RootShell.Background = new SolidColorBrush(chrome.AppBackground);
        SetBrush("NovaChromeSurfaceBrush", chrome.Surface);
        SetBrush("NovaChromeSurfaceAltBrush", chrome.SurfaceAlt);
        SetBrush("NovaChromeSurfaceRaisedBrush", chrome.SurfaceRaised);
        SetBrush("NovaChromeStrokeBrush", chrome.Stroke);
        SetBrush("NovaChromeStrokeSoftBrush", chrome.StrokeSoft);
        SetBrush("NovaAddressBackgroundBrush", chrome.AddressBackground);
        SetBrush("NovaAddressBorderBrush", chrome.AddressBorder);
        SetBrush("NovaAddressForegroundBrush", chrome.Text);
        SetBrush("NovaTextMutedBrush", chrome.MutedText);
        SetBrush("NovaAccentBrush", chrome.Accent);
        SetBrush("NovaAccentSoftBrush", chrome.AccentSoft);
        SetBrush("NovaCoolAccentBrush", chrome.CoolAccent);
        SetBrush("NovaCoolAccentSoftBrush", chrome.CoolAccentSoft);
        SetBrush("NovaCompanionGlassBrush", ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.10, translucent ? (byte)218 : (byte)236));
        SetBrush("NovaCompanionStrokeBrush", ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.24, 190));
        SetBrush("NovaModeSelectorGlassBrush", ChromeTint(chrome.Surface, chrome.Accent, 0.055, translucent ? (byte)214 : (byte)232));
        SetBrush("NovaModeSelectorStrokeBrush", ChromeTint(chrome.StrokeSoft, chrome.Accent, 0.13, 178));
        SetBrush("NovaModuleHubGlassBrush", ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.07, translucent ? (byte)212 : (byte)232));
        SetBrush("NovaModuleHubStrokeBrush", ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.18, 178));
        SetBrush("NovaModuleHubNodeBrush", chrome.MutedText);
        SetBrush("NovaModuleHubAccentBrush", chrome.CoolAccent);
        SetBrush("NovaFocusBrush", chrome.Focus);
        SetBrush("NovaFocusInnerBrush", isDark ? UiColor(13, 20, 34) : UiColor(255, 255, 255));
        SetIdentityGradient(chrome.CoolAccent, chrome.Accent, chrome.WarmAccent);

        ModeChromeAccentStrip.Height = mode switch
        {
            "neutral" => 1,
            "focus" => 3,
            "research" => 3,
            "night" => 1.5,
            _ => 2
        };
        ModeChromeAccentStrip.Opacity = mode switch
        {
            "neutral" => 0.42,
            "night" => 0.68,
            _ => 0.95
        };
        UsageModeButton.Background = new SolidColorBrush(ChromeTint(chrome.Surface, chrome.Accent, 0.055, translucent ? (byte)214 : (byte)232));
        UsageModeButton.BorderBrush = new SolidColorBrush(ChromeTint(chrome.StrokeSoft, chrome.Accent, 0.13, 178));
        UsageModeButton.Foreground = new SolidColorBrush(chrome.Text);
        UsageModeAccentBar.Background = new SolidColorBrush(chrome.Accent);
        ModeCompanionButton.Background = new SolidColorBrush(ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.10, translucent ? (byte)218 : (byte)236));
        ModeCompanionButton.BorderBrush = new SolidColorBrush(ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.24, 190));
        ModeCompanionButton.Foreground = new SolidColorBrush(chrome.Text);
        ModeCompanionAccentDot.Background = new SolidColorBrush(chrome.CoolAccent);
        ModeCompanionGlow.Background = new SolidColorBrush(ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.18, translucent ? (byte)160 : (byte)190));
        ModulesButton.Background = new SolidColorBrush(ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.07, translucent ? (byte)212 : (byte)232));
        ModulesButton.BorderBrush = new SolidColorBrush(ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.18, 178));
        ModulesButton.Foreground = new SolidColorBrush(chrome.Text);

        NavigationToolbar.Opacity = mode == "night" ? 0.94 : 1;
        VerticalTabsRail.Opacity = mode == "night" ? 0.95 : 1;
    }

    private void SetIdentityGradient(Windows.UI.Color first, Windows.UI.Color second, Windows.UI.Color third)
    {
        if (RootShell.Resources["NovaIdentityMarkBrush"] is LinearGradientBrush identity && identity.GradientStops.Count >= 3)
        {
            identity.GradientStops[0].Color = first;
            identity.GradientStops[1].Color = second;
            identity.GradientStops[2].Color = third;
        }

        if (RootShell.Resources["NovaChromeGradientBrush"] is LinearGradientBrush chrome && chrome.GradientStops.Count >= 3)
        {
            chrome.GradientStops[0].Color = BlendForChrome(first, 0.14);
            chrome.GradientStops[1].Color = BlendForChrome(second, 0.18);
            chrome.GradientStops[2].Color = BlendForChrome(third, 0.16);
        }
    }

    private static Windows.UI.Color BlendForChrome(Windows.UI.Color color, double weight)
    {
        byte Blend(byte baseValue, byte accentValue) =>
            (byte)Math.Clamp((int)Math.Round(baseValue * (1 - weight) + accentValue * weight), 0, 255);

        return UiColor(Blend(18, color.R), Blend(29, color.G), Blend(39, color.B), 255);
    }

    private static Windows.UI.Color ChromeTint(Windows.UI.Color surface, Windows.UI.Color accent, double weight, byte alpha)
    {
        byte Blend(byte baseValue, byte accentValue) =>
            (byte)Math.Clamp((int)Math.Round(baseValue * (1 - weight) + accentValue * weight), 0, 255);

        return UiColor(Blend(surface.R, accent.R), Blend(surface.G, accent.G), Blend(surface.B, accent.B), alpha);
    }

    private static ModeChromePalette ResolveModeChromePalette(string mode, bool isDark, byte alpha)
    {
        if (!isDark)
        {
            return mode switch
            {
                "neutral" => new(
                    UiColor(248, 248, 246), UiColor(255, 255, 253, alpha), UiColor(242, 242, 238, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(207, 209, 205), UiColor(228, 229, 224), UiColor(255, 255, 255, 246), UiColor(188, 190, 185),
                    UiColor(99, 137, 134), UiColor(99, 137, 134, 26), UiColor(180, 134, 42), UiColor(180, 134, 42, 20),
                    UiColor(134, 150, 148), UiColor(69, 93, 91), UiColor(118, 119, 113), UiColor(30, 31, 28)),
                "focus" => new(
                    UiColor(247, 248, 246), UiColor(250, 250, 248, alpha), UiColor(239, 242, 240, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(190, 198, 196), UiColor(224, 229, 226), UiColor(255, 255, 255, 246), UiColor(93, 130, 129),
                    UiColor(87, 176, 168), UiColor(87, 176, 168, 40), UiColor(255, 185, 53), UiColor(255, 185, 53, 34),
                    UiColor(235, 126, 74), UiColor(0, 96, 90), UiColor(86, 120, 116), UiColor(23, 32, 34)),
                "reading" => new(
                    UiColor(250, 247, 240), UiColor(255, 252, 246, alpha), UiColor(246, 241, 232, alpha), UiColor(255, 253, 249, alpha),
                    UiColor(213, 202, 184), UiColor(234, 226, 212), UiColor(255, 252, 246, 246), UiColor(176, 136, 79),
                    UiColor(216, 166, 93), UiColor(216, 166, 93, 42), UiColor(115, 162, 143), UiColor(115, 162, 143, 30),
                    UiColor(245, 204, 145), UiColor(118, 79, 28), UiColor(128, 108, 82), UiColor(36, 31, 25)),
                "creative" => new(
                    UiColor(249, 246, 251), UiColor(255, 251, 255, alpha), UiColor(244, 238, 249, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(208, 196, 218), UiColor(230, 224, 236), UiColor(255, 255, 255, 246), UiColor(153, 103, 173),
                    UiColor(205, 111, 214), UiColor(205, 111, 214, 42), UiColor(255, 185, 53), UiColor(255, 185, 53, 34),
                    UiColor(67, 219, 209), UiColor(112, 57, 137), UiColor(123, 96, 137), UiColor(34, 24, 42)),
                "research" => new(
                    UiColor(244, 249, 250), UiColor(249, 253, 253, alpha), UiColor(237, 246, 247, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(184, 209, 211), UiColor(219, 232, 233), UiColor(255, 255, 255, 246), UiColor(42, 137, 145),
                    UiColor(67, 180, 190), UiColor(67, 180, 190, 42), UiColor(255, 185, 53), UiColor(255, 185, 53, 30),
                    UiColor(112, 206, 215), UiColor(0, 94, 105), UiColor(82, 124, 128), UiColor(18, 35, 38)),
                "night" => new(
                    UiColor(235, 239, 247), UiColor(246, 248, 252, alpha), UiColor(232, 237, 247, alpha), UiColor(250, 252, 255, alpha),
                    UiColor(184, 194, 214), UiColor(218, 225, 239), UiColor(248, 250, 255, 246), UiColor(88, 111, 158),
                    UiColor(118, 145, 205), UiColor(118, 145, 205, 38), UiColor(154, 193, 255), UiColor(154, 193, 255, 28),
                    UiColor(82, 103, 158), UiColor(50, 72, 122), UiColor(98, 111, 138), UiColor(24, 31, 48)),
                _ => new(
                    UiColor(250, 248, 244), UiColor(255, 255, 255, alpha), UiColor(242, 239, 234, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(214, 209, 200), UiColor(228, 224, 217), UiColor(255, 255, 255, 246), UiColor(198, 192, 182),
                    UiColor(176, 111, 0), UiColor(176, 111, 0, 38), UiColor(0, 128, 122), UiColor(0, 128, 122, 28),
                    UiColor(235, 126, 74), UiColor(149, 92, 0), UiColor(120, 113, 102), UiColor(31, 29, 26))
            };
        }

        return mode switch
        {
            // Palette du mode "Neutre" (dark) corrigee le 2026-07-20 : les
            // teintes d'origine avaient toutes le canal vert plus proche du
            // bleu que du rouge (quasi-cyan, ~200 degres de teinte), et
            // l'accent etait carrement du vert-sauge (107,157,150 - le vert y
            // etait le canal dominant, pas le bleu). Signale par l'utilisateur
            // ("mode sombre verdatre") : rebalance vers un bleu-ardoise net
            // (~220 degres) en gardant la meme luminosite approximative.
            "neutral" => new(
                UiColor(10, 15, 23), UiColor(16, 21, 31, alpha), UiColor(19, 25, 37, alpha), UiColor(24, 31, 44, alpha),
                UiColor(48, 55, 68), UiColor(31, 37, 49), UiColor(21, 28, 42, 242), UiColor(62, 71, 88),
                UiColor(110, 130, 170), UiColor(110, 130, 170, 28), UiColor(193, 148, 60), UiColor(193, 148, 60, 20),
                UiColor(200, 190, 170), UiColor(195, 210, 225), UiColor(172, 176, 180), UiColor(242, 244, 248)),
            "focus" => new(
                UiColor(8, 14, 20), UiColor(12, 19, 26, alpha), UiColor(14, 23, 30, alpha), UiColor(17, 27, 35, alpha),
                UiColor(52, 75, 84), UiColor(25, 39, 47), UiColor(15, 26, 34, 242), UiColor(63, 112, 119),
                UiColor(67, 219, 209), UiColor(67, 219, 209, 36), UiColor(255, 185, 53), UiColor(255, 185, 53, 24),
                UiColor(235, 126, 74), UiColor(128, 232, 220), UiColor(165, 189, 188), UiColor(255, 248, 234)),
            "reading" => new(
                UiColor(18, 24, 23), UiColor(24, 30, 28, alpha), UiColor(31, 38, 34, alpha), UiColor(37, 45, 40, alpha),
                UiColor(77, 81, 66), UiColor(45, 52, 44), UiColor(31, 40, 36, 242), UiColor(112, 96, 65),
                UiColor(245, 199, 122), UiColor(245, 199, 122, 34), UiColor(122, 197, 171), UiColor(122, 197, 171, 24),
                UiColor(255, 225, 162), UiColor(255, 218, 150), UiColor(203, 192, 166), UiColor(255, 248, 234)),
            "creative" => new(
                UiColor(16, 17, 31), UiColor(22, 24, 41, alpha), UiColor(31, 31, 52, alpha), UiColor(39, 37, 62, alpha),
                UiColor(84, 71, 107), UiColor(47, 45, 68), UiColor(29, 29, 48, 242), UiColor(115, 91, 138),
                UiColor(204, 118, 226), UiColor(204, 118, 226, 38), UiColor(255, 185, 53), UiColor(255, 185, 53, 28),
                UiColor(67, 219, 209), UiColor(238, 177, 255), UiColor(203, 188, 217), UiColor(255, 248, 234)),
            "research" => new(
                UiColor(8, 20, 25), UiColor(12, 29, 35, alpha), UiColor(15, 38, 45, alpha), UiColor(18, 47, 54, alpha),
                UiColor(50, 96, 103), UiColor(24, 57, 64), UiColor(13, 42, 50, 242), UiColor(57, 135, 146),
                UiColor(67, 219, 209), UiColor(67, 219, 209, 42), UiColor(255, 185, 53), UiColor(255, 185, 53, 26),
                UiColor(115, 231, 255), UiColor(128, 242, 255), UiColor(169, 207, 210), UiColor(255, 248, 234)),
            "night" => new(
                UiColor(4, 7, 12), UiColor(7, 11, 19, alpha), UiColor(10, 15, 25, alpha), UiColor(14, 20, 33, alpha),
                UiColor(42, 54, 80), UiColor(20, 27, 42), UiColor(11, 16, 27, 242), UiColor(78, 94, 132),
                UiColor(116, 145, 208), UiColor(116, 145, 208, 32), UiColor(154, 193, 255), UiColor(154, 193, 255, 22),
                UiColor(82, 103, 158), UiColor(192, 212, 255), UiColor(159, 174, 205), UiColor(235, 240, 255)),
            _ => new(
                UiColor(13, 24, 34), UiColor(20, 32, 42, alpha), UiColor(26, 39, 49, alpha), UiColor(34, 49, 58, alpha),
                UiColor(64, 84, 91), UiColor(36, 52, 60), UiColor(23, 40, 52, 242), UiColor(70, 97, 106),
                UiColor(255, 185, 53), UiColor(255, 185, 53, 48), UiColor(67, 219, 209), UiColor(67, 219, 209, 34),
                UiColor(235, 126, 74), UiColor(255, 230, 104), UiColor(195, 185, 165), UiColor(255, 248, 234))
        };
    }

    private (Windows.UI.Color Accent, Windows.UI.Color AccentSoft, Windows.UI.Color CoolAccent, Windows.UI.Color CoolAccentSoft, Windows.UI.Color Focus)
        ResolveAccentPalette(bool isDark, bool highContrast)
    {
        if (highContrast)
        {
            return (UiColor(255, 213, 0), UiColor(255, 213, 0, 68), UiColor(0, 255, 226), UiColor(0, 255, 226, 56), UiColor(255, 255, 0));
        }

        return (_uiSettings.AccentPalette ?? "lumora").ToLowerInvariant() switch
        {
            "ocean" => isDark
                ? (UiColor(92, 188, 255), UiColor(92, 188, 255, 48), UiColor(137, 226, 214), UiColor(137, 226, 214, 34), UiColor(128, 218, 255))
                : (UiColor(0, 103, 171), UiColor(0, 103, 171, 38), UiColor(0, 133, 127), UiColor(0, 133, 127, 28), UiColor(0, 96, 150)),
            "forest" => isDark
                ? (UiColor(128, 204, 124), UiColor(128, 204, 124, 48), UiColor(86, 208, 194), UiColor(86, 208, 194, 34), UiColor(175, 222, 132))
                : (UiColor(48, 122, 61), UiColor(48, 122, 61, 38), UiColor(0, 128, 119), UiColor(0, 128, 119, 28), UiColor(43, 107, 48)),
            "ember" => isDark
                ? (UiColor(235, 126, 74), UiColor(235, 126, 74, 48), UiColor(246, 206, 104), UiColor(246, 206, 104, 34), UiColor(255, 181, 108))
                : (UiColor(181, 73, 40), UiColor(181, 73, 40, 38), UiColor(166, 126, 0), UiColor(166, 126, 0, 28), UiColor(160, 59, 30)),
            _ => isDark
                ? (UiColor(255, 185, 53), UiColor(255, 185, 53, 48), UiColor(67, 219, 209), UiColor(67, 219, 209, 34), UiColor(255, 230, 104))
                : (UiColor(176, 111, 0), UiColor(176, 111, 0, 38), UiColor(0, 128, 122), UiColor(0, 128, 122, 28), UiColor(149, 92, 0))
        };
    }


    private bool IsTranslucentChromeEnabled() => false;

    private void ApplyWindowBackdrop()
    {
        try
        {
            _uiSettings.WindowBackdrop = "solid";
            SystemBackdrop = null;
            // Le contenu web doit rester 100 % opaque : on ne teinte jamais la
            // fenetre. L'ancien effet translucide est retire de l'UI utilisateur.
            RemoveWindowLayeredAlpha();
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Window backdrop skipped: {error.GetType().Name}");
            SystemBackdrop = null;
            RemoveWindowLayeredAlpha();
        }

        ApplyWindowTitleBarColors();
        ApplyAccessibilitySettings();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    private const int GwlExstyle = -20;
    private const long WsExLayered = 0x00080000L;

    // Retire l'éventuel style « layered » d'une session précédente : le contenu web ne
    // doit jamais être translucide (voir ApplyWindowBackdrop).
    private void RemoveWindowLayeredAlpha()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (hwnd == nint.Zero) return;

            var exStyle = (long)GetWindowLongPtr(hwnd, GwlExstyle);
            SetWindowLongPtr(hwnd, GwlExstyle, (nint)(exStyle & ~WsExLayered));
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Window layered alpha reset skipped: {error.GetType().Name}");
        }
    }



    private sealed record ModeChromePalette(
        Windows.UI.Color AppBackground,
        Windows.UI.Color Surface,
        Windows.UI.Color SurfaceAlt,
        Windows.UI.Color SurfaceRaised,
        Windows.UI.Color Stroke,
        Windows.UI.Color StrokeSoft,
        Windows.UI.Color AddressBackground,
        Windows.UI.Color AddressBorder,
        Windows.UI.Color Accent,
        Windows.UI.Color AccentSoft,
        Windows.UI.Color CoolAccent,
        Windows.UI.Color CoolAccentSoft,
        Windows.UI.Color WarmAccent,
        Windows.UI.Color Focus,
        Windows.UI.Color MutedText,
        Windows.UI.Color Text);
}
