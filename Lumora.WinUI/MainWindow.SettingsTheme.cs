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
    private void ApplyAccessibilitySettings()
    {
        var highContrast = _uiSettings.AccessibilityHighContrast;
        var largeText = _uiSettings.AccessibilityLargeText;
        var visibleFocus = _uiSettings.AccessibilityVisibleFocus;
        var translucent = IsTranslucentChromeEnabled() && !highContrast;
        var isDark = highContrast || LumoraTheme.ResolveIsDarkTheme(_uiSettings);
        var palette = ResolveAccentPalette(isDark, highContrast);
        var appBackground = highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(9, 13, 20) : UiColor(248, 246, 241));
        var surface = highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(16, 21, 31, translucent ? (byte)226 : (byte)255) : UiColor(255, 253, 249, translucent ? (byte)226 : (byte)255));
        var surfaceAlt = highContrast ? UiColor(18, 18, 18) : (isDark ? UiColor(20, 26, 38, translucent ? (byte)232 : (byte)255) : UiColor(243, 239, 232, translucent ? (byte)232 : (byte)255));
        var surfaceRaised = highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(26, 33, 46, translucent ? (byte)238 : (byte)255) : UiColor(255, 255, 255, translucent ? (byte)238 : (byte)255));
        var overlay = highContrast ? UiColor(0, 0, 0, 210) : (isDark ? UiColor(2, 5, 12, 176) : UiColor(20, 24, 33, 92));
        var textOnAccent = highContrast ? UiColor(0, 0, 0) : LumoraTheme.ResolveTextOnColor(palette.Accent);

        RootShell.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        ApplyPreferredColorSchemeToOpenTabs();

        RootShell.Background = translucent
            ? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            : new SolidColorBrush(appBackground);
        SetBrush("NovaAppBackgroundBrush", appBackground);
        SetBrush("NovaChromeSurfaceBrush", surface);
        SetBrush("NovaChromeSurfaceAltBrush", surfaceAlt);
        SetBrush("NovaChromeSurfaceRaisedBrush", surfaceRaised);
        SetBrush("NovaChromeStrokeBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(64, 73, 91) : UiColor(214, 209, 200)));
        SetBrush("NovaChromeStrokeSoftBrush", highContrast ? UiColor(190, 190, 190) : (isDark ? UiColor(36, 44, 60) : UiColor(228, 224, 217)));
        SetBrush("NovaAddressBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(23, 33, 52, translucent ? (byte)236 : (byte)255) : UiColor(255, 255, 255, translucent ? (byte)236 : (byte)255)));
        SetBrush("NovaAddressBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(70, 83, 106) : UiColor(198, 192, 182)));
        SetBrush("NovaAddressForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 248, 234) : UiColor(31, 29, 26)));
        SetBrush("NovaChromeButtonBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(20, 27, 40, translucent ? (byte)228 : (byte)255) : UiColor(255, 252, 247, translucent ? (byte)242 : (byte)255)));
        SetBrush("NovaChromeButtonBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(56, 67, 85) : UiColor(214, 207, 195)));
        SetBrush("NovaChromeButtonForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(242, 245, 250) : UiColor(61, 55, 48)));
        SetBrush("NovaChromeButtonAccentBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(40, 72, 95, translucent ? (byte)232 : (byte)255) : UiColor(232, 246, 250, translucent ? (byte)244 : (byte)255)));
        SetBrush("NovaChromeButtonAccentBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(86, 194, 228) : UiColor(111, 183, 205)));
        SetBrush("NovaChromeButtonAccentForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(242, 245, 250) : UiColor(28, 82, 102)));
        SetBrush("NovaModuleButtonBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(24, 33, 47, translucent ? (byte)224 : (byte)255) : UiColor(251, 247, 240, translucent ? (byte)244 : (byte)255)));
        SetBrush("NovaModuleButtonBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(67, 194, 228, 120) : UiColor(216, 208, 194)));
        SetBrush("NovaModuleButtonForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(242, 245, 250) : UiColor(58, 53, 46)));
        SetBrush("NovaBookmarkButtonBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(25, 31, 44, translucent ? (byte)228 : (byte)255) : UiColor(250, 245, 236, translucent ? (byte)246 : (byte)255)));
        SetBrush("NovaBookmarkButtonBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(87, 96, 118) : UiColor(223, 204, 174)));
        SetBrush("NovaBookmarkButtonForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(245, 232, 210) : UiColor(82, 66, 43)));
        SetBrush("NovaBookmarkButtonActiveBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(74, 57, 24, translucent ? (byte)232 : (byte)255) : UiColor(255, 243, 205, translucent ? (byte)248 : (byte)255)));
        SetBrush("NovaBookmarkButtonActiveBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(230, 170, 72) : UiColor(196, 128, 12)));
        SetBrush("NovaBookmarkButtonActiveForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 233, 170) : UiColor(138, 84, 0)));
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
        SetBrush("NovaModuleHubNodeBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(195, 185, 165) : UiColor(69, 63, 56)));
        SetBrush("NovaModuleHubAccentBrush", highContrast ? UiColor(255, 213, 0) : palette.CoolAccent);
        SetBrush("NovaFocusBrush", palette.Focus);
        SetBrush("NovaFocusInnerBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(13, 20, 34) : UiColor(255, 255, 255)));
        SetBrush("NovaInfoSurfaceBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(29, 38, 56, translucent ? (byte)238 : (byte)255) : UiColor(247, 245, 240, translucent ? (byte)238 : (byte)255)));
        SetBrush("NovaPanelBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(16, 24, 38) : UiColor(250, 248, 245)));
        SetBrush("NovaTextOnAccentBrush", textOnAccent);
        SetBrush("NovaOverlayBrush", overlay);
        SetBrush("NovaChromeHaloWarmBrush", highContrast ? UiColor(255, 255, 255, 36) : (isDark ? UiColor(palette.Accent.R, palette.Accent.G, palette.Accent.B, 42) : UiColor(palette.Accent.R, palette.Accent.G, palette.Accent.B, 24)));
        SetBrush("NovaChromeHaloCoolBrush", highContrast ? UiColor(255, 255, 255, 28) : (isDark ? UiColor(palette.CoolAccent.R, palette.CoolAccent.G, palette.CoolAccent.B, 36) : UiColor(palette.CoolAccent.R, palette.CoolAccent.G, palette.CoolAccent.B, 22)));
        SetBrush("NovaChromeMistBrush", highContrast ? UiColor(255, 255, 255, 28) : (isDark ? UiColor(255, 255, 255, 18) : UiColor(255, 255, 255, 58)));
        // Halos "nebuleuse" en degrade radial (Ellipse rondes uniquement, voir
        // commentaire XAML) : cousins directs de NovaChromeHaloWarm/CoolBrush
        // ci-dessus mais types RadialGradientBrush, donc invisibles pour
        // SetBrush (qui ne gere que SolidColorBrush) - restaient figes sur les
        // couleurs sombres d'origine (0.84.0.39) quel que soit le theme actif,
        // d'ou une tache orange/cyan visible en theme clair. Meme logique
        // couleur que les deux lignes au-dessus, alpha du centre reduit en
        // clair pour eviter la tache.
        SetGlowBrush("NovaChromeHaloWarmGlowBrush", highContrast ? UiColor(255, 255, 255) : palette.Accent, highContrast ? (byte)24 : (isDark ? (byte)64 : (byte)32));
        SetGlowBrush("NovaChromeHaloCoolGlowBrush", highContrast ? UiColor(255, 255, 255) : palette.CoolAccent, highContrast ? (byte)18 : (isDark ? (byte)64 : (byte)32));
        // Verre flottant (NavigationToolbar/BookmarksBarRow, 0.84.0.40) : meme
        // angle mort - AcrylicBrush n'est pas une SolidColorBrush, teinte
        // sombre figee jamais adaptee au theme clair (panneaux muddy/gris sur
        // fond clair). Teinte claire alignee sur NovaChromeSurfaceBrush clair
        // (ligne 31 plus haut) plutot qu'une nouvelle couleur inventee.
        SetFloatingGlassBrush(highContrast, isDark);
        SetBrush("NovaBrandChipBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(20, 28, 41, translucent ? (byte)220 : (byte)236) : UiColor(255, 253, 249, translucent ? (byte)234 : (byte)246)));
        SetBrush("NovaBrandChipBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(palette.CoolAccent.R, palette.CoolAccent.G, palette.CoolAccent.B, 168) : UiColor(palette.Accent.R, palette.Accent.G, palette.Accent.B, 118)));
        SetBrush("NovaBrandChipForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(242, 245, 250) : UiColor(49, 44, 38)));
        SetBrush("NovaBrandChipMutedBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(171, 178, 191) : UiColor(118, 112, 102)));
        SetBrush("NovaChromeButtonShadowBrush", highContrast ? UiColor(255, 255, 255, 26) : (isDark ? UiColor(0, 0, 0, 82) : UiColor(123, 103, 73, 34)));
        SetBrush("NovaChromeButtonHighlightBrush", highContrast ? UiColor(255, 255, 255, 30) : (isDark ? UiColor(255, 255, 255, 28) : UiColor(255, 255, 255, 108)));
        SetBrush("NovaBookmarkBarButtonBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(21, 29, 42, translucent ? (byte)228 : (byte)244) : UiColor(255, 251, 245, translucent ? (byte)244 : (byte)255)));
        SetBrush("NovaBookmarkBarButtonBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(90, 101, 126) : UiColor(225, 212, 190)));
        SetBrush("NovaBookmarkBarButtonForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(242, 245, 250) : UiColor(74, 64, 54)));
        // Pastilles d'onglet (MainWindow.TabGroups.cs, passe "onglets" du
        // 0.84.0.36) : memes 4 brushes partagees introduites pour eviter les
        // UiColor(...) codes en dur repetes, mais jamais raccordees a
        // ApplyAccessibilitySettings - restaient figees sur les teintes
        // sombres d'origine quel que soit le theme (onglets marine meme en
        // clair, signale par capture d'ecran utilisateur).
        SetBrush("NovaTabPillActiveBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(23, 35, 52, 236) : UiColor(255, 255, 255, 236)));
        SetBrush("NovaTabPillInactiveBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(19, 27, 39, 184) : UiColor(243, 239, 232, 184)));
        SetBrush("NovaTabPillActiveBorderBrush", highContrast ? UiColor(255, 255, 255) : UiColor(palette.CoolAccent.R, palette.CoolAccent.G, palette.CoolAccent.B, 180));
        SetBrush("NovaTabPillInactiveBorderBrush", highContrast ? UiColor(190, 190, 190) : (isDark ? UiColor(61, 71, 88, 152) : UiColor(214, 209, 200, 152)));
        // Texte des pastilles d'onglet (titre/sous-titre/badge/cercle icone,
        // TabHeaderContent dans MainWindow.TabGroups.cs) : couleurs claires
        // fixes en dur (pensees pour un fond sombre), jamais theme-conscientes
        // - une fois le fond de la pastille rendu clair (correctif ci-dessus),
        // le texte clair sur fond clair devenait illisible (signale par
        // l'utilisateur juste apres le correctif de fond).
        SetBrush("NovaTabTitleActiveForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(242, 245, 250) : UiColor(31, 29, 26)));
        SetBrush("NovaTabTitleInactiveForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(214, 221, 229) : UiColor(96, 88, 76)));
        SetBrush("NovaTabSubtitleActiveForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(195, 218, 232) : UiColor(105, 96, 84)));
        SetBrush("NovaTabSubtitleInactiveForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(157, 171, 184) : UiColor(146, 137, 124)));
        SetBrush("NovaTabBadgeActiveBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 230, 104, 46) : UiColor(196, 148, 0, 40)));
        SetBrush("NovaTabBadgeActiveBorderBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 230, 104, 92) : UiColor(196, 148, 0, 90)));
        SetBrush("NovaTabBadgeActiveForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 236, 170) : UiColor(120, 86, 0)));
        SetBrush("NovaTabBadgeInactiveBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(255, 255, 255, 18) : UiColor(20, 18, 14, 16)));
        SetBrush("NovaTabBadgeInactiveBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(255, 255, 255, 28) : UiColor(20, 18, 14, 26)));
        SetBrush("NovaTabBadgeInactiveForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(214, 221, 229) : UiColor(96, 88, 76)));
        SetBrush("NovaTabIconWrapActiveBackgroundBrush", highContrast ? UiColor(0, 0, 0, 60) : (isDark ? UiColor(255, 255, 255, 34) : UiColor(20, 18, 14, 26)));
        SetBrush("NovaTabIconWrapInactiveBackgroundBrush", highContrast ? UiColor(255, 255, 255, 40) : (isDark ? UiColor(255, 255, 255, 16) : UiColor(20, 18, 14, 14)));
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
                     DetachVideoPinnedButton,
                     VideoDownloadButton,
                     ReadAloudButton,
                     ReaderModeButton,
                     SearchAssistButton,
                     NotesModuleButton,
                     TranslatePinnedButton,
                     WebAppsPinnedButton,
                     ModulesButton,
                     ModeCompanionButton,
                     ModeCompanionMemoryBox,
                     ModeCompanionSaveButton,
                     ModeCompanionPrimaryButton,
                     ModeCompanionSecondaryButton,
                     UsageModeButton,
                     DictationPinnedButton,
                     VideoDownloadStartButton,
                     MainMenuButton,
                     ShieldButton,
                     VaultQuickAccessButton,
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
        SyncSharedAppThemeResources();
        ApplyWindowTitleBarColors();
        UpdateUsageModeButtonUi();
        ApplyIconButtonSizing();
    }

    // Taille des boutons NovaChromeIconButtonStyle (barre de navigation +
    // barre plein ecran) : 32px par defaut, 44px (cible confort AAA) si
    // AccessibilityLargeTargets est active. Passe par une valeur locale sur
    // chaque bouton plutot que par un second Style/DynamicResource - WinUI ne
    // reevalue pas un {StaticResource} deja resolu dans un Setter existant,
    // alors qu'une valeur locale prend toujours le dessus sur le Style et se
    // change librement a l'execution.
    private void ApplyIconButtonSizing()
    {
        if (RootShell.Resources["NovaChromeIconButtonStyle"] is not Style chromeIconButtonStyle) return;

        var size = _uiSettings.AccessibilityLargeTargets ? 44d : 32d;
        ApplyIconButtonSizeRecursive(NavigationToolbar, chromeIconButtonStyle, size);
        ApplyIconButtonSizeRecursive(FullScreenTopBar, chromeIconButtonStyle, size);
    }

    private static void ApplyIconButtonSizeRecursive(DependencyObject root, Style chromeIconButtonStyle, double size)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button { } button && ReferenceEquals(button.Style, chromeIconButtonStyle))
            {
                button.Width = size;
                button.Height = size;
                button.MinWidth = size;
            }

            ApplyIconButtonSizeRecursive(child, chromeIconButtonStyle, size);
        }
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

    private void SetGlowBrush(string key, Windows.UI.Color color, byte centerAlpha)
    {
        if (RootShell.Resources[key] is not RadialGradientBrush gradient || gradient.GradientStops.Count != 2)
        {
            return;
        }

        gradient.GradientStops[0].Color = Windows.UI.Color.FromArgb(centerAlpha, color.R, color.G, color.B);
        gradient.GradientStops[1].Color = Windows.UI.Color.FromArgb(0, color.R, color.G, color.B);
    }

    private void SetFloatingGlassBrush(bool highContrast, bool isDark)
    {
        if (RootShell.Resources["NovaFloatingGlassBrush"] is not AcrylicBrush acrylic)
        {
            return;
        }

        if (highContrast)
        {
            acrylic.TintColor = UiColor(0, 0, 0);
            acrylic.FallbackColor = UiColor(0, 0, 0);
            return;
        }

        acrylic.TintColor = isDark ? UiColor(26, 34, 48) : UiColor(255, 253, 249);
        acrylic.FallbackColor = isDark ? UiColor(20, 26, 38, 240) : UiColor(255, 253, 249, 240);
    }

    private void ApplyUsageModeChrome(bool isDark, bool highContrast, bool translucent)
    {
        if (highContrast)
        {
            ModeChromeAccentStrip.Height = 3;
            ModeChromeAccentStrip.Opacity = 1;
            SetBrush("NovaAppBackgroundBrush", UiColor(0, 0, 0));
            SetBrush("NovaChromeSurfaceRaisedBrush", UiColor(0, 0, 0));
            SetBrush("NovaTextOnAccentBrush", UiColor(0, 0, 0));
            SetBrush("NovaOverlayBrush", UiColor(0, 0, 0, 210));
            SetBrush("NovaChromeButtonBackgroundBrush", UiColor(0, 0, 0));
            SetBrush("NovaChromeButtonBorderBrush", UiColor(255, 255, 255));
            SetBrush("NovaChromeButtonForegroundBrush", UiColor(255, 255, 255));
            SetBrush("NovaChromeButtonAccentBackgroundBrush", UiColor(255, 255, 255));
            SetBrush("NovaChromeButtonAccentBorderBrush", UiColor(255, 255, 255));
            SetBrush("NovaChromeButtonAccentForegroundBrush", UiColor(0, 0, 0));
            SetBrush("NovaModuleButtonBackgroundBrush", UiColor(0, 0, 0));
            SetBrush("NovaModuleButtonBorderBrush", UiColor(255, 255, 255));
            SetBrush("NovaModuleButtonForegroundBrush", UiColor(255, 255, 255));
            SetBrush("NovaBookmarkButtonBackgroundBrush", UiColor(0, 0, 0));
            SetBrush("NovaBookmarkButtonBorderBrush", UiColor(255, 255, 255));
            SetBrush("NovaBookmarkButtonForegroundBrush", UiColor(255, 255, 255));
            SetBrush("NovaBookmarkButtonActiveBackgroundBrush", UiColor(255, 255, 255));
            SetBrush("NovaBookmarkButtonActiveBorderBrush", UiColor(255, 255, 255));
            SetBrush("NovaBookmarkButtonActiveForegroundBrush", UiColor(0, 0, 0));
            SetBrush("NovaChromeHaloWarmBrush", UiColor(255, 255, 255, 28));
            SetBrush("NovaChromeHaloCoolBrush", UiColor(255, 255, 255, 20));
            SetBrush("NovaChromeMistBrush", UiColor(255, 255, 255, 22));
            SetBrush("NovaBrandChipBackgroundBrush", UiColor(0, 0, 0));
            SetBrush("NovaBrandChipBorderBrush", UiColor(255, 255, 255));
            SetBrush("NovaBrandChipForegroundBrush", UiColor(255, 255, 255));
            SetBrush("NovaBrandChipMutedBrush", UiColor(255, 255, 255));
            SetBrush("NovaChromeButtonShadowBrush", UiColor(255, 255, 255, 20));
            SetBrush("NovaChromeButtonHighlightBrush", UiColor(255, 255, 255, 24));
            SetBrush("NovaBookmarkBarButtonBackgroundBrush", UiColor(0, 0, 0));
            SetBrush("NovaBookmarkBarButtonBorderBrush", UiColor(255, 255, 255));
            SetBrush("NovaBookmarkBarButtonForegroundBrush", UiColor(255, 255, 255));
            SetChromeGradient(UiColor(0, 0, 0), UiColor(0, 0, 0), UiColor(0, 0, 0));
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
            ModulesButtonMark.Fill = new SolidColorBrush(UiColor(255, 255, 255));
            SetIdentityGradient(UiColor(255, 213, 0), UiColor(255, 255, 255), UiColor(0, 255, 226));
            NavigationToolbar.Opacity = 1;
            VerticalTabsRail.Opacity = 1;
            return;
        }

        var alpha = translucent ? (byte)232 : (byte)255;
        var mode = (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant();
        var chrome = ResolveModeChromePalette(mode, isDark, alpha);

        // Couleur personnalisee par mode (MainWindow.IdentitySpine.cs) :
        // strictement apres le "return" du contraste eleve ci-dessus, jamais
        // avant - le contraste eleve doit continuer a ecraser toute couleur
        // de mode/personnalisee exactement comme avant cette fonctionnalite.
        var customAccentHex = GetModeAccentColorHex(mode);
        if (!string.IsNullOrWhiteSpace(customAccentHex) && TryParseHexColor(customAccentHex, out var customAccent))
        {
            chrome = ApplyCustomModeAccent(chrome, customAccent, isDark);
        }

        RootShell.Background = new SolidColorBrush(chrome.AppBackground);
        SetBrush("NovaAppBackgroundBrush", chrome.AppBackground);
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
        SetBrush("NovaModuleHubGlassBrush", ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.14, translucent ? (byte)212 : (byte)232));
        SetBrush("NovaModuleHubStrokeBrush", ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.42, 224));
        SetBrush("NovaModuleHubNodeBrush", chrome.MutedText);
        SetBrush("NovaModuleHubAccentBrush", chrome.CoolAccent);
        SetBrush("NovaChromeButtonBackgroundBrush", isDark
            ? ChromeTint(chrome.Surface, chrome.CoolAccent, 0.045, translucent ? (byte)224 : (byte)242)
            : ChromeTint(chrome.SurfaceRaised, chrome.Accent, 0.035, translucent ? (byte)244 : (byte)255));
        SetBrush("NovaChromeButtonBorderBrush", isDark
            ? ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.18, 196)
            : ChromeTint(chrome.Stroke, chrome.Accent, 0.11, 255));
        SetBrush("NovaChromeButtonForegroundBrush", isDark
            ? chrome.Text
            : ChromeTint(chrome.Text, chrome.Accent, 0.12, 255));
        SetBrush("NovaChromeButtonAccentBackgroundBrush", isDark
            ? ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.22, translucent ? (byte)232 : (byte)255)
            : ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.16, 255));
        SetBrush("NovaChromeButtonAccentBorderBrush", isDark
            ? chrome.CoolAccent
            : ChromeTint(chrome.CoolAccent, chrome.Accent, 0.08, 255));
        SetBrush("NovaChromeButtonAccentForegroundBrush", isDark
            ? chrome.Text
            : ChromeTint(chrome.Text, chrome.CoolAccent, 0.24, 255));
        SetBrush("NovaModuleButtonBackgroundBrush", isDark
            ? ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.08, translucent ? (byte)220 : (byte)236)
            : ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.05, 255));
        SetBrush("NovaModuleButtonBorderBrush", isDark
            ? ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.22, 188)
            : ChromeTint(chrome.Stroke, chrome.CoolAccent, 0.14, 255));
        SetBrush("NovaModuleButtonForegroundBrush", isDark
            ? chrome.Text
            : ChromeTint(chrome.Text, chrome.CoolAccent, 0.14, 255));
        SetBrush("NovaBookmarkButtonBackgroundBrush", isDark
            ? ChromeTint(chrome.SurfaceRaised, chrome.Accent, 0.10, translucent ? (byte)228 : (byte)244)
            : ChromeTint(chrome.SurfaceRaised, chrome.Accent, 0.09, 255));
        SetBrush("NovaBookmarkButtonBorderBrush", isDark
            ? ChromeTint(chrome.StrokeSoft, chrome.Accent, 0.26, 204)
            : ChromeTint(chrome.Stroke, chrome.Accent, 0.22, 255));
        SetBrush("NovaBookmarkButtonForegroundBrush", isDark
            ? ChromeTint(chrome.Text, chrome.Accent, 0.16, 255)
            : ChromeTint(chrome.Text, chrome.Accent, 0.18, 255));
        SetBrush("NovaBookmarkButtonActiveBackgroundBrush", isDark
            ? ChromeTint(chrome.SurfaceRaised, chrome.Accent, 0.26, translucent ? (byte)236 : (byte)255)
            : ChromeTint(chrome.SurfaceRaised, chrome.Accent, 0.24, 255));
        SetBrush("NovaBookmarkButtonActiveBorderBrush", isDark
            ? chrome.Accent
            : ChromeTint(chrome.Accent, chrome.WarmAccent, 0.10, 255));
        SetBrush("NovaBookmarkButtonActiveForegroundBrush", isDark
            ? LumoraTheme.ResolveTextOnColor(chrome.Accent)
            : ChromeTint(chrome.Accent, chrome.Text, 0.20, 255));
        SetBrush("NovaFocusBrush", chrome.Focus);
        SetBrush("NovaFocusInnerBrush", isDark ? UiColor(13, 20, 34) : UiColor(255, 255, 255));
        SetBrush("NovaTextOnAccentBrush", LumoraTheme.ResolveTextOnColor(chrome.Accent));
        SetBrush("NovaOverlayBrush", isDark ? UiColor(2, 5, 12, 176) : UiColor(20, 24, 33, 92));
        SetBrush("NovaChromeHaloWarmBrush", isDark
            ? ChromeTint(chrome.Accent, chrome.WarmAccent, 0.20, 54)
            : ChromeTint(chrome.Accent, chrome.WarmAccent, 0.28, 28));
        SetBrush("NovaChromeHaloCoolBrush", isDark
            ? ChromeTint(chrome.CoolAccent, chrome.Focus, 0.18, 46)
            : ChromeTint(chrome.CoolAccent, chrome.Focus, 0.12, 28));
        SetBrush("NovaChromeMistBrush", isDark
            ? ChromeTint(chrome.SurfaceRaised, chrome.Text, 0.16, 26)
            : ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.08, 62));
        SetBrush("NovaBrandChipBackgroundBrush", isDark
            ? ChromeTint(chrome.SurfaceRaised, chrome.Accent, 0.08, translucent ? (byte)214 : (byte)232)
            : ChromeTint(chrome.SurfaceRaised, chrome.Text, 0.015, 246));
        SetBrush("NovaBrandChipBorderBrush", isDark
            ? ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.24, 210)
            : ChromeTint(chrome.Stroke, chrome.Accent, 0.16, 255));
        SetBrush("NovaBrandChipForegroundBrush", isDark
            ? chrome.Text
            : ChromeTint(chrome.Text, chrome.Accent, 0.10, 255));
        SetBrush("NovaBrandChipMutedBrush", isDark
            ? chrome.MutedText
            : ChromeTint(chrome.MutedText, chrome.CoolAccent, 0.08, 255));
        SetBrush("NovaChromeButtonShadowBrush", isDark
            ? UiColor(0, 0, 0, 92)
            : ChromeTint(chrome.WarmAccent, chrome.Stroke, 0.18, 36));
        SetBrush("NovaChromeButtonHighlightBrush", isDark
            ? ChromeTint(chrome.Text, chrome.CoolAccent, 0.06, 34)
            : UiColor(255, 255, 255, 112));
        SetBrush("NovaBookmarkBarButtonBackgroundBrush", isDark
            ? ChromeTint(chrome.SurfaceRaised, chrome.Accent, 0.09, translucent ? (byte)228 : (byte)244)
            : ChromeTint(chrome.SurfaceRaised, chrome.Accent, 0.05, 255));
        SetBrush("NovaBookmarkBarButtonBorderBrush", isDark
            ? ChromeTint(chrome.StrokeSoft, chrome.Accent, 0.16, 214)
            : ChromeTint(chrome.Stroke, chrome.Accent, 0.18, 255));
        SetBrush("NovaBookmarkBarButtonForegroundBrush", isDark
            ? chrome.Text
            : ChromeTint(chrome.Text, chrome.Accent, 0.12, 255));
        SetIdentityGradient(chrome.CoolAccent, chrome.Accent, chrome.WarmAccent);
        SetChromeGradient(
            ChromeTint(chrome.Surface, chrome.Accent, isDark ? 0.07 : 0.018, 255),
            ChromeTint(chrome.SurfaceRaised, isDark ? chrome.WarmAccent : chrome.Accent, isDark ? 0.05 : 0.025, 255),
            ChromeTint(chrome.SurfaceAlt, chrome.CoolAccent, isDark ? 0.08 : 0.03, 255));

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

        NavigationToolbar.Opacity = mode == "night" ? 0.94 : 1;
        VerticalTabsRail.Opacity = mode == "night" ? 0.95 : 1;
    }

    private void SyncSharedAppThemeResources()
    {
        // controlForeground reprenait NovaAddressForegroundBrush, pensee pour
        // s'associer a NovaAddressBackgroundBrush (blanc en contraste eleve) -
        // combinee a NovaControlSurfaceBrush (noir en contraste eleve, via
        // NovaChromeSurfaceBrush ci-dessous), ca donnait du texte noir sur fond
        // noir : les ComboBox/TextBox generiques du panneau Reglages en
        // devenaient illisibles en contraste eleve (confirme par capture
        // d'ecran, audit accessibilite basse vision, palier 0.93.x).
        // NovaChromeButtonForegroundBrush est deja pense pour un fond sombre
        // (blanc en contraste eleve) : bonne paire pour NovaControlSurfaceBrush.
        LumoraTheme.ApplySharedAppBrushes(
            BrushColor("NovaAccentBrush", UiColor(230, 170, 72)),
            BrushColor("NovaTextOnAccentBrush", UiColor(13, 20, 34)),
            BrushColor("NovaFocusBrush", UiColor(195, 214, 255)),
            BrushColor("NovaFocusInnerBrush", UiColor(9, 13, 20)),
            BrushColor("NovaChromeButtonForegroundBrush", UiColor(242, 245, 250)),
            BrushColor("NovaTextMutedBrush", UiColor(171, 178, 191)),
            BrushColor("NovaChromeSurfaceBrush", UiColor(16, 21, 31)),
            BrushColor("NovaChromeSurfaceRaisedBrush", UiColor(26, 33, 46)),
            BrushColor("NovaChromeStrokeBrush", UiColor(47, 56, 72)));
    }

    private void SetIdentityGradient(Windows.UI.Color first, Windows.UI.Color second, Windows.UI.Color third)
    {
        if (RootShell.Resources["NovaIdentityMarkBrush"] is LinearGradientBrush identity && identity.GradientStops.Count >= 3)
        {
            identity.GradientStops[0].Color = first;
            identity.GradientStops[1].Color = second;
            identity.GradientStops[2].Color = third;
        }
    }

    private void SetChromeGradient(Windows.UI.Color first, Windows.UI.Color second, Windows.UI.Color third)
    {
        if (RootShell.Resources["NovaChromeGradientBrush"] is LinearGradientBrush chrome && chrome.GradientStops.Count >= 3)
        {
            chrome.GradientStops[0].Color = first;
            chrome.GradientStops[1].Color = second;
            chrome.GradientStops[2].Color = third;
        }
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
                // Accent aligne sur la famille bleu-ardoise du Neutre sombre
                // (~220 degres, cf. correction 2026-07-20 plus bas) : la teinte
                // vert sarcelle d'origine (99,137,134) donnait une identite de
                // mode differente entre clair et sombre, signale par
                // l'utilisateur (barre d'adresse "bizarre" selon le theme).
                "neutral" => new(
                    UiColor(248, 248, 246), UiColor(255, 255, 253, alpha), UiColor(242, 242, 238, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(207, 209, 205), UiColor(228, 229, 224), UiColor(255, 255, 255, 246), UiColor(188, 190, 185),
                    UiColor(74, 102, 145), UiColor(74, 102, 145, 26), UiColor(180, 134, 42), UiColor(180, 134, 42, 20),
                    UiColor(134, 150, 148), UiColor(51, 70, 100), UiColor(118, 119, 113), UiColor(30, 31, 28)),
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
                UiColor(9, 13, 20), UiColor(16, 21, 31, alpha), UiColor(20, 26, 38, alpha), UiColor(26, 33, 46, alpha),
                UiColor(47, 56, 72), UiColor(30, 38, 51), UiColor(18, 25, 37, 242), UiColor(67, 79, 101),
                UiColor(120, 145, 196), UiColor(120, 145, 196, 24), UiColor(201, 158, 78), UiColor(201, 158, 78, 18),
                UiColor(219, 142, 98), UiColor(198, 214, 240), UiColor(171, 178, 191), UiColor(242, 245, 250)),
            "focus" => new(
                UiColor(8, 13, 20), UiColor(12, 18, 28, alpha), UiColor(15, 22, 34, alpha), UiColor(19, 27, 40, alpha),
                UiColor(41, 59, 76), UiColor(25, 36, 50), UiColor(15, 24, 38, 242), UiColor(63, 102, 145),
                UiColor(86, 194, 228), UiColor(86, 194, 228, 32), UiColor(255, 185, 53), UiColor(255, 185, 53, 22),
                UiColor(235, 126, 74), UiColor(190, 224, 255), UiColor(163, 178, 193), UiColor(244, 247, 250)),
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
                UiColor(8, 16, 24), UiColor(12, 23, 34, alpha), UiColor(15, 30, 44, alpha), UiColor(19, 37, 54, alpha),
                UiColor(45, 78, 102), UiColor(24, 45, 62), UiColor(14, 33, 50, 242), UiColor(58, 114, 158),
                UiColor(86, 194, 228), UiColor(86, 194, 228, 38), UiColor(255, 185, 53), UiColor(255, 185, 53, 24),
                UiColor(130, 215, 255), UiColor(190, 229, 255), UiColor(168, 190, 206), UiColor(244, 247, 252)),
            "night" => new(
                UiColor(4, 7, 12), UiColor(7, 11, 19, alpha), UiColor(10, 15, 25, alpha), UiColor(14, 20, 33, alpha),
                UiColor(42, 54, 80), UiColor(20, 27, 42), UiColor(11, 16, 27, 242), UiColor(78, 94, 132),
                UiColor(116, 145, 208), UiColor(116, 145, 208, 32), UiColor(154, 193, 255), UiColor(154, 193, 255, 22),
                UiColor(82, 103, 158), UiColor(192, 212, 255), UiColor(159, 174, 205), UiColor(235, 240, 255)),
            _ => new(
                UiColor(9, 14, 21), UiColor(14, 20, 30, alpha), UiColor(18, 25, 36, alpha), UiColor(24, 32, 44, alpha),
                UiColor(49, 58, 74), UiColor(31, 39, 52), UiColor(17, 25, 37, 242), UiColor(70, 82, 104),
                UiColor(230, 170, 72), UiColor(230, 170, 72, 42), UiColor(86, 194, 228), UiColor(86, 194, 228, 24),
                UiColor(232, 128, 86), UiColor(195, 214, 255), UiColor(170, 177, 189), UiColor(242, 245, 250))
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
                ? (UiColor(230, 170, 72), UiColor(230, 170, 72, 42), UiColor(86, 194, 228), UiColor(86, 194, 228, 24), UiColor(195, 214, 255))
                : (UiColor(176, 111, 0), UiColor(176, 111, 0, 38), UiColor(0, 129, 168), UiColor(0, 129, 168, 24), UiColor(149, 92, 0))
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
