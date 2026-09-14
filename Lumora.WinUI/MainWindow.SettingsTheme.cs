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
        // Couleur d'accentuation globale (MainWindow.AccentColor.cs) :
        // strictement apres le contraste eleve (deja exclu par le parametre
        // "highContrast" passe a TryResolveGlobalAccentOverride via cette
        // garde) - le contraste eleve doit continuer a ecraser toute couleur
        // personnalisee. Alimente ici la palette GENERIQUE (pas liee au Mode
        // d'usage) : bouton principal, dossiers de favoris, pastille
        // d'onglet active - voir plus bas dans cette methode et
        // ApplyUsageModeChrome pour le chrome du mode.
        var hasCustomAccent = TryResolveGlobalAccentOverride(out var customAccentColor) && !highContrast;
        if (hasCustomAccent)
        {
            palette = ApplyCustomAccentToPalette(palette, customAccentColor, isDark);
        }
        var appBackground = highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(13, 13, 13) : UiColor(248, 246, 241));
        var surface = highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(19, 19, 19, translucent ? (byte)226 : (byte)255) : UiColor(255, 253, 249, translucent ? (byte)226 : (byte)255));
        var surfaceAlt = highContrast ? UiColor(18, 18, 18) : (isDark ? UiColor(24, 24, 24, translucent ? (byte)232 : (byte)255) : UiColor(243, 239, 232, translucent ? (byte)232 : (byte)255));
        var surfaceRaised = highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(31, 31, 31, translucent ? (byte)238 : (byte)255) : UiColor(255, 255, 255, translucent ? (byte)238 : (byte)255));
        var overlay = highContrast ? UiColor(0, 0, 0, 210) : (isDark ? UiColor(6, 6, 6, 176) : UiColor(20, 24, 33, 92));
        var textOnAccent = highContrast ? UiColor(0, 0, 0) : LumoraTheme.ResolveTextOnColor(palette.Accent);
        // Palette C du Contraste renforce (retour utilisateur 2026-08-08,
        // "c'est moche" sur le blanc pur en bordure de CHAQUE controle au
        // repos) : le blanc/jaune redevient un signal rare (focus, etat
        // actif/selectionne), les bordures et le texte "attenue" au repos
        // passent a ce gris - deja la valeur de NovaChromeStrokeSoftBrush
        // plus bas, reprise ici comme etalon plutot que d'inventer un 2e
        // gris legerement different.
        var hcRestBorder = UiColor(190, 190, 190);

        RootShell.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        ApplyPreferredColorSchemeToOpenTabs();

        RootShell.Background = translucent
            ? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            : new SolidColorBrush(appBackground);
        SetBrush("NovaAppBackgroundBrush", appBackground);
        SetBrush("NovaChromeSurfaceBrush", surface);
        SetBrush("NovaChromeSurfaceAltBrush", surfaceAlt);
        SetBrush("NovaChromeSurfaceRaisedBrush", surfaceRaised);
        SetBrush("NovaChromeStrokeBrush", highContrast ? hcRestBorder : (isDark ? UiColor(74, 74, 74) : UiColor(214, 209, 200)));
        SetBrush("NovaChromeStrokeSoftBrush", highContrast ? UiColor(190, 190, 190) : (isDark ? UiColor(42, 42, 42) : UiColor(228, 224, 217)));
        SetBrush("NovaAddressBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(27, 27, 27, translucent ? (byte)236 : (byte)255) : UiColor(255, 255, 255, translucent ? (byte)236 : (byte)255)));
        SetBrush("NovaAddressBorderBrush", highContrast ? hcRestBorder : (isDark ? UiColor(76, 76, 76) : UiColor(198, 192, 182)));
        SetBrush("NovaAddressForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 248, 234) : UiColor(31, 29, 26)));
        // Retour utilisateur (2026-08-08) : titres de panneaux invisibles en
        // Contraste renforce - NovaAddressForegroundBrush est pense pour le
        // texte SUR la barre d'adresse (fond blanc en contraste eleve, donc
        // texte noir), mais etait aussi reutilise comme couleur de texte
        // generale un peu partout (titres de panneaux, badges, menus) sur des
        // surfaces qui restent NOIRES en contraste eleve - noir sur noir.
        // Meme valeur que NovaAddressForegroundBrush en dehors du contraste
        // eleve (theme clair/sombre, puis teinte de mode ci-dessous), mais
        // blanc plutot que noir en contraste eleve. Ne PAS repointer
        // NovaAddressForegroundBrush lui-meme vers du blanc : il reste
        // correct pour l'usage qui lui a donne son nom (AddressBox,
        // NovaCommandPaletteSearchBoxStyle - texte sur fond blanc).
        SetBrush("NovaPrimaryTextBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(255, 248, 234) : UiColor(31, 29, 26)));
        SetBrush("NovaChromeButtonBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(20, 27, 40, translucent ? (byte)228 : (byte)255) : UiColor(255, 252, 247, translucent ? (byte)242 : (byte)255)));
        SetBrush("NovaChromeButtonBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(56, 67, 85) : UiColor(214, 207, 195)));
        SetBrush("NovaChromeButtonForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(244, 244, 244) : UiColor(61, 55, 48)));
        // Fond/bordure au repos des icones de la ligne d'outils (2026-09-01,
        // revision graphique globale) : transparent hors contraste eleve
        // (l'icone seule suffit, le survol reste signale par
        // NovaChromeButtonHighlightBrush) ; opaque + bordure blanche en
        // contraste eleve, comme les autres controles, pour rester reperable.
        SetBrush("NovaChromeIconRestBackgroundBrush", highContrast ? UiColor(0, 0, 0) : UiColor(0, 0, 0, 0));
        SetBrush("NovaChromeIconRestBorderBrush", highContrast ? UiColor(255, 255, 255) : UiColor(0, 0, 0, 0));
        SetBrush("NovaChromeButtonAccentBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(40, 72, 95, translucent ? (byte)232 : (byte)255) : UiColor(232, 246, 250, translucent ? (byte)244 : (byte)255)));
        SetBrush("NovaChromeButtonAccentBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(86, 194, 228) : UiColor(111, 183, 205)));
        SetBrush("NovaChromeButtonAccentForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(244, 244, 244) : UiColor(28, 82, 102)));
        SetBrush("NovaModuleButtonBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(24, 33, 47, translucent ? (byte)224 : (byte)255) : UiColor(251, 247, 240, translucent ? (byte)244 : (byte)255)));
        SetBrush("NovaModuleButtonBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(67, 194, 228, 120) : UiColor(216, 208, 194)));
        SetBrush("NovaModuleButtonForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(244, 244, 244) : UiColor(58, 53, 46)));
        // Etoile de favori au repos (2026-09-01) : meme traitement transparent
        // que les autres icones de la ligne d'adresse - seule la variante
        // Active (page deja en favori, brushes plus bas) garde un
        // remplissage accent, cf. UpdateBookmarkStar() dans
        // MainWindow.Bookmarks.cs.
        SetBrush("NovaBookmarkButtonBackgroundBrush", highContrast ? UiColor(0, 0, 0) : UiColor(0, 0, 0, 0));
        SetBrush("NovaBookmarkButtonBorderBrush", highContrast ? UiColor(255, 255, 255) : UiColor(0, 0, 0, 0));
        SetBrush("NovaBookmarkButtonForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(245, 232, 210) : UiColor(82, 66, 43)));
        SetBrush("NovaBookmarkButtonActiveBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(74, 57, 24, translucent ? (byte)232 : (byte)255) : UiColor(255, 243, 205, translucent ? (byte)248 : (byte)255)));
        SetBrush("NovaBookmarkButtonActiveBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(230, 170, 72) : UiColor(196, 128, 12)));
        SetBrush("NovaBookmarkButtonActiveForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 233, 170) : UiColor(138, 84, 0)));
        // Familles de sens (chantier identite visuelle, 2026-08-10) : les
        // valeurs sombres sont celles deja posees dans App.xaml (design-time
        // default) - redeclarees ici pour que le clair (contraste ~2:1 avec
        // les memes valeurs, mesure insuffisant) bascule sur des teintes
        // plus foncees de la meme famille, memes couleurs deja utilisees
        // ailleurs dans ce fichier (Bookmark actif clair, cool accent clair)
        // plutot que d'en inventer de nouvelles. Contraste eleve : memes 3
        // couleurs signal que les barres d'accent du bas.
        SetAppBrush("NovaTileFamilyContentBrush", highContrast ? UiColor(255, 213, 0) : (isDark ? UiColor(230, 170, 72) : UiColor(196, 128, 12)));
        SetAppBrush("NovaTileFamilyProtectionBrush", highContrast ? UiColor(255, 120, 140) : (isDark ? UiColor(226, 137, 127) : UiColor(190, 70, 55)));
        SetAppBrush("NovaTileFamilyToolsBrush", highContrast ? UiColor(0, 255, 226) : (isDark ? UiColor(86, 194, 228) : UiColor(0, 129, 168)));
        // Blanc pur ici videait le mot "attenue" de son sens (texte secondaire
        // aussi voyant que le texte principal) - passe au meme gris que les
        // bordures au repos, cf. hcRestBorder plus haut.
        SetBrush("NovaTextMutedBrush", highContrast ? hcRestBorder : (isDark ? UiColor(195, 185, 165) : UiColor(120, 113, 102)));
        SetBrush("NovaAccentBrush", palette.Accent);
        SetBrush("NovaAccentSoftBrush", palette.AccentSoft);
        SetBrush("NovaCoolAccentBrush", palette.CoolAccent);
        SetBrush("NovaCoolAccentSoftBrush", palette.CoolAccentSoft);
        SetBrush("NovaCompanionGlassBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(29, 29, 29, 214) : UiColor(255, 255, 255, 228)));
        SetBrush("NovaCompanionStrokeBrush", highContrast ? hcRestBorder : palette.CoolAccentSoft);
        SetBrush("NovaModeSelectorGlassBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(26, 26, 26, 208) : UiColor(255, 255, 255, 224)));
        SetBrush("NovaModeSelectorStrokeBrush", highContrast ? hcRestBorder : (isDark ? UiColor(52, 60, 76, 160) : UiColor(214, 209, 200, 180)));
        SetBrush("NovaModuleHubGlassBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(26, 26, 26, 214) : UiColor(255, 255, 255, 226)));
        SetBrush("NovaModuleHubStrokeBrush", highContrast ? hcRestBorder : palette.CoolAccentSoft);
        SetBrush("NovaModuleHubNodeBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(195, 185, 165) : UiColor(69, 63, 56)));
        SetBrush("NovaModuleHubAccentBrush", highContrast ? UiColor(255, 213, 0) : palette.CoolAccent);
        SetBrush("NovaFocusBrush", palette.Focus);
        SetBrush("NovaFocusInnerBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(13, 13, 13) : UiColor(255, 255, 255)));
        SetBrush("NovaInfoSurfaceBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(34, 34, 34, translucent ? (byte)238 : (byte)255) : UiColor(247, 245, 240, translucent ? (byte)238 : (byte)255)));
        SetBrush("NovaPanelBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(20, 20, 20) : UiColor(250, 248, 245)));
        SetBrush("NovaTextOnAccentBrush", textOnAccent);
        SetBrush("NovaOverlayBrush", overlay);
        // Alpha alignes sur ceux d'ApplyUsageModeChrome (28/20/22 - valeurs
        // reellement visibles avant consolidation, cf. commentaire plus bas :
        // cette fonction s'executait en premier et etait donc silencieusement
        // ecrasee par ApplyUsageModeChrome pour ces 3 cles, avec des alpha
        // legerement differents des siens (36/28/28) - jamais un vrai
        // probleme visible (ecart mineur sur un halo translucide), mais deux
        // valeurs differentes pour la meme cle est exactement le genre de
        // piege qu'une seule ecriture evite.
        SetBrush("NovaChromeHaloWarmBrush", highContrast ? UiColor(255, 255, 255, 28) : (isDark ? UiColor(palette.Accent.R, palette.Accent.G, palette.Accent.B, 42) : UiColor(palette.Accent.R, palette.Accent.G, palette.Accent.B, 24)));
        SetBrush("NovaChromeHaloCoolBrush", highContrast ? UiColor(255, 255, 255, 20) : (isDark ? UiColor(palette.CoolAccent.R, palette.CoolAccent.G, palette.CoolAccent.B, 36) : UiColor(palette.CoolAccent.R, palette.CoolAccent.G, palette.CoolAccent.B, 22)));
        SetBrush("NovaChromeMistBrush", highContrast ? UiColor(255, 255, 255, 22) : (isDark ? UiColor(255, 255, 255, 18) : UiColor(255, 255, 255, 58)));
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
        SetBrush("NovaBrandChipBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(24, 24, 24, translucent ? (byte)220 : (byte)236) : UiColor(255, 253, 249, translucent ? (byte)234 : (byte)246)));
        SetBrush("NovaBrandChipBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(palette.CoolAccent.R, palette.CoolAccent.G, palette.CoolAccent.B, 168) : UiColor(palette.Accent.R, palette.Accent.G, palette.Accent.B, 118)));
        SetBrush("NovaBrandChipForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(244, 244, 244) : UiColor(49, 44, 38)));
        SetBrush("NovaBrandChipMutedBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(178, 178, 178) : UiColor(118, 112, 102)));
        // Alpha alignes sur ApplyUsageModeChrome (20/24), meme remarque que
        // NovaChromeHalo*/NovaChromeMistBrush plus haut.
        SetBrush("NovaChromeButtonShadowBrush", highContrast ? UiColor(255, 255, 255, 20) : (isDark ? UiColor(0, 0, 0, 82) : UiColor(123, 103, 73, 34)));
        SetBrush("NovaChromeButtonHighlightBrush", highContrast ? UiColor(255, 255, 255, 24) : (isDark ? UiColor(255, 255, 255, 28) : UiColor(255, 255, 255, 108)));
        // Puce de favori au repos (2026-09-01) : transparente hors contraste
        // eleve, meme traitement que les icones de la ligne d'outils - voir
        // NovaChromeIconRestBackgroundBrush plus haut.
        SetBrush("NovaBookmarkBarButtonBackgroundBrush", highContrast ? UiColor(0, 0, 0) : UiColor(0, 0, 0, 0));
        SetBrush("NovaBookmarkBarButtonBorderBrush", highContrast ? UiColor(255, 255, 255) : UiColor(0, 0, 0, 0));
        SetBrush("NovaBookmarkBarButtonForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(244, 244, 244) : UiColor(74, 64, 54)));
        // Valeur par defaut de secours, ecrasee par ApplyUsageModeChrome
        // juste apres (meme convention que NovaBookmarkBarButtonForegroundBrush
        // ci-dessus) - voir la aussi pour le vrai boost couleur d'accentuation.
        SetBrush("NovaBookmarkFolderGlyphBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(244, 244, 244) : UiColor(74, 64, 54)));
        // Bouton principal (ex. "Appliquer les changements") : cle WinUI
        // standard, statique dans App.xaml (jamais ecrite dynamiquement avant
        // cette fonctionnalite) - ne la retinter QUE si une couleur
        // d'accentuation personnalisee est active, jamais par defaut (le
        // reste de l'identite Lumora - ambre #FFB935 - reste inchange sans
        // reglage explicite de l'utilisateur).
        if (hasCustomAccent)
        {
            // SetAppBrush (Application.Current.Resources), pas SetBrush
            // (RootShell.Resources) : ces 4 cles vivent dans App.xaml, jamais
            // ajoutees littéralement a RootShell.Resources - un indexeur
            // SetBrush dessus plante le processus SANS exception catchable,
            // piege deja documente (voir MainWindow.Bookmarks.cs,
            // NovaAccentBrush vs AccentFillColorDefaultBrush).
            SetAppBrush("AccentFillColorDefaultBrush", palette.Accent);
            SetAppBrush("AccentFillColorSecondaryBrush", WithAlpha(palette.Accent, 221));
            SetAppBrush("AccentFillColorTertiaryBrush", WithAlpha(palette.Accent, 184));
            SetAppBrush("TextOnAccentFillColorPrimaryBrush", LumoraTheme.ResolveTextOnColor(palette.Accent));
        }
        // Pastilles d'onglet (MainWindow.TabGroups.cs, passe "onglets" du
        // 0.84.0.36) : memes 4 brushes partagees introduites pour eviter les
        // UiColor(...) codes en dur repetes, mais jamais raccordees a
        // ApplyAccessibilitySettings - restaient figees sur les teintes
        // sombres d'origine quel que soit le theme (onglets marine meme en
        // clair, signale par capture d'ecran utilisateur).
        SetBrush("NovaTabPillActiveBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(30, 30, 30, 236) : UiColor(255, 255, 255, 236)));
        SetBrush("NovaTabPillInactiveBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(22, 22, 22, 184) : UiColor(243, 239, 232, 184)));
        SetBrush("NovaTabPillActiveBorderBrush", highContrast ? UiColor(255, 255, 255) : UiColor(palette.CoolAccent.R, palette.CoolAccent.G, palette.CoolAccent.B, 180));
        SetBrush("NovaTabPillInactiveBorderBrush", highContrast ? UiColor(190, 190, 190) : (isDark ? UiColor(58, 58, 58, 152) : UiColor(214, 209, 200, 152)));
        // Texte des pastilles d'onglet (titre/sous-titre/badge/cercle icone,
        // TabHeaderContent dans MainWindow.TabGroups.cs) : couleurs claires
        // fixes en dur (pensees pour un fond sombre), jamais theme-conscientes
        // - une fois le fond de la pastille rendu clair (correctif ci-dessus),
        // le texte clair sur fond clair devenait illisible (signale par
        // l'utilisateur juste apres le correctif de fond).
        SetBrush("NovaTabTitleActiveForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(244, 244, 244) : UiColor(31, 29, 26)));
        SetBrush("NovaTabTitleInactiveForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(221, 221, 221) : UiColor(96, 88, 76)));
        SetBrush("NovaTabSubtitleActiveForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(195, 218, 232) : UiColor(105, 96, 84)));
        SetBrush("NovaTabSubtitleInactiveForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(157, 171, 184) : UiColor(146, 137, 124)));
        SetBrush("NovaTabBadgeActiveBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 230, 104, 46) : UiColor(196, 148, 0, 40)));
        SetBrush("NovaTabBadgeActiveBorderBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 230, 104, 92) : UiColor(196, 148, 0, 90)));
        SetBrush("NovaTabBadgeActiveForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 236, 170) : UiColor(120, 86, 0)));
        SetBrush("NovaTabBadgeInactiveBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(255, 255, 255, 18) : UiColor(20, 18, 14, 16)));
        SetBrush("NovaTabBadgeInactiveBorderBrush", highContrast ? hcRestBorder : (isDark ? UiColor(255, 255, 255, 28) : UiColor(20, 18, 14, 26)));
        SetBrush("NovaTabBadgeInactiveForegroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(221, 221, 221) : UiColor(96, 88, 76)));
        SetBrush("NovaTabIconWrapActiveBackgroundBrush", highContrast ? UiColor(0, 0, 0, 60) : (isDark ? UiColor(255, 255, 255, 34) : UiColor(20, 18, 14, 26)));
        SetBrush("NovaTabIconWrapInactiveBackgroundBrush", highContrast ? UiColor(255, 255, 255, 40) : (isDark ? UiColor(255, 255, 255, 16) : UiColor(20, 18, 14, 14)));
        ApplyUsageModeChrome(isDark, highContrast, translucent);

        var mainFontSize = largeText ? 16 : 14;
        var smallFontSize = largeText ? 14 : 12;
        // Taille dediee, plus grande que mainFontSize (2026-08-22), gardee
        // a 18px minimum pour rester lisible avec les hampes fines de l'URL.
        AddressBox.FontSize = largeText ? 20 : 18;
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
                     ModeCompanionMemoryBox,
                     ModeCompanionSaveButton,
                     ModeCompanionPrimaryButton,
                     ModeCompanionSecondaryButton,
                     ModeUsageButton,
                     CompanionButton,
                     CompanionToggleButton,
                     AccessibilityMenuButton,
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
        UpdateAccessibilityQuickButtonUi();
        ApplyIconButtonSizing();
    }

    // Taille des boutons NovaChromeIconButtonStyle ET NovaModuleIconButtonStyle
    // (barre de navigation + barre plein ecran) : suit la Taille de l'interface
    // (UiDensity, voir MainWindow.UiDensity.cs) sauf si AccessibilityLargeTargets
    // est active, auquel cas 44px (cible confort AAA) prime toujours sur la
    // densite choisie. Passe par une valeur locale sur chaque bouton plutot que
    // par un second Style/DynamicResource - WinUI ne reevalue pas un
    // {StaticResource} deja resolu dans un Setter existant, alors qu'une
    // valeur locale prend toujours le dessus sur le Style et se change
    // librement a l'execution.
    //
    // Les 2 styles couverts (2026-09-12, retour utilisateur : boutons "module"
    // et "chrome" de la meme barre pas a la meme echelle) - avant ce
    // correctif, seul NovaChromeIconButtonStyle etait resize ici : les boutons
    // module restaient figes a leur taille de base quel que soit le reglage de
    // Densite/Confort AAA, ce qui aurait recree le meme ecart d'echelle a
    // chaque changement de reglage.
    private void ApplyIconButtonSizing()
    {
        if (RootShell.Resources["NovaChromeIconButtonStyle"] is not Style chromeIconButtonStyle) return;
        if (RootShell.Resources["NovaModuleIconButtonStyle"] is not Style moduleIconButtonStyle) return;

        var size = _uiSettings.AccessibilityLargeTargets ? 44d : ResolveEffectiveUiDensityMetrics().IconButtonSize;
        var styles = new[] { chromeIconButtonStyle, moduleIconButtonStyle };
        ApplyIconButtonSizeRecursive(NavigationToolbar, styles, size);
        ApplyIconButtonSizeRecursive(FullScreenTopBar, styles, size);
        // Rail d'onglets verticaux (2026-09-12, retour utilisateur "Interface
        // compacte" incomplete) : VerticalTabsCompactButton/VerticalTabsMuteAllButton
        // partagent NovaChromeIconButtonStyle mais n'etaient jamais parcourus ici -
        // seuls NavigationToolbar/FullScreenTopBar l'etaient, d'ou des boutons de
        // rail restes a taille normale malgre le reglage active.
        ApplyIconButtonSizeRecursive(VerticalTabsRail, styles, size);
    }

    private static void ApplyIconButtonSizeRecursive(DependencyObject root, IReadOnlyList<Style> iconButtonStyles, double size)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button { } button && iconButtonStyles.Any(style => ReferenceEquals(button.Style, style)))
            {
                button.Width = size;
                button.Height = size;
                button.MinWidth = size;
            }

            ApplyIconButtonSizeRecursive(child, iconButtonStyles, size);
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

    // Meme forme que SetBrush, mais cible Application.Current.Resources : les
    // 3 brushes de famille du chantier identite visuelle (Content/Protection/
    // Tools, 2026-08-10) vivent la plutot que dans RootShell.Resources, car
    // StartMenuFamilyKeyToBrushConverter (un convertisseur XAML, sans
    // reference a la fenetre) doit pouvoir les resoudre - voir
    // Converters/StartMenuFamilyKeyToBrushConverter.cs.
    private static void SetAppBrush(string key, Windows.UI.Color color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush)
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

        acrylic.TintColor = isDark ? UiColor(32, 32, 32) : UiColor(255, 253, 249);
        acrylic.FallbackColor = isDark ? UiColor(24, 24, 24, 240) : UiColor(255, 253, 249, 240);
    }

    private void ApplyUsageModeChrome(bool isDark, bool highContrast, bool translucent)
    {
        if (highContrast)
        {
            // Consolidation (2026-08-08, retour utilisateur "on remet des
            // couches sur des couches, refais ca au propre") : cette branche
            // ecrivait ~30 cles de brush deja ecrites a l'identique par
            // ApplyAccessibilitySettings (qui s'execute juste avant et
            // appelle cette fonction) - deux sources de verite pour la meme
            // couleur, exactement le risque qui a cause l'oubli du point 1
            // (ModulesButton non reinitialise). Ne reste ici que ce qui n'a
            // PAS d'equivalent generique : l'accent visuel du Mode d'usage
            // (bande de couleur, degrade de la colonne identitaire) et les
            // boutons dont la couleur depend du mode courant
            // (ModeUsageButton/CompanionButton/AccessibilityMenuButton -
            // Focus/Lecture/etc. ont chacun leur teinte, impossible a
            // exprimer par un {StaticResource} statique). ModulesButton en a
            // ete retire : il n'a jamais ete teinte par mode (toujours la
            // meme "chrome" generique), sa couleur venait deja entierement de
            // NovaChromeButtonBackgroundBrush/BorderBrush/ForegroundBrush
            // via le Style de son bouton - cette reecriture directe etait
            // superflue et c'est elle qui restait collee au bouton une fois
            // le contraste eleve desactive (bug du point 1, desormais
            // impossible puisque plus rien ne l'ecrit ici).
            var hcRestBorder = UiColor(190, 190, 190);
            ModeChromeAccentStrip.Height = 3;
            ModeChromeAccentStrip.Opacity = 1;
            SetChromeGradient(UiColor(0, 0, 0), UiColor(0, 0, 0), UiColor(0, 0, 0));
            ModeUsageButton.Background = new SolidColorBrush(UiColor(0, 0, 0));
            ModeUsageButton.BorderBrush = new SolidColorBrush(hcRestBorder);
            ModeUsageButton.Foreground = new SolidColorBrush(UiColor(255, 255, 255));
            CompanionButton.Background = new SolidColorBrush(UiColor(0, 0, 0));
            CompanionButton.BorderBrush = new SolidColorBrush(hcRestBorder);
            CompanionButton.Foreground = new SolidColorBrush(UiColor(255, 255, 255));
            AccessibilityMenuButton.Background = new SolidColorBrush(UiColor(0, 0, 0));
            AccessibilityMenuButton.BorderBrush = new SolidColorBrush(hcRestBorder);
            AccessibilityMenuButton.Foreground = new SolidColorBrush(UiColor(255, 255, 255));
            UsageModeAccentBar.Background = new SolidColorBrush(UiColor(255, 213, 0));
            CompanionAccentBar.Background = new SolidColorBrush(UiColor(255, 120, 140));
            AccessibilityQuickAccentBar.Background = new SolidColorBrush(UiColor(0, 255, 226));
            SetIdentityGradient(UiColor(255, 213, 0), UiColor(255, 255, 255), UiColor(0, 255, 226));
            // Familles de sens (chantier identite visuelle, 2026-08-10) : en
            // contraste eleve, reprend exactement les 3 memes couleurs signal
            // deja utilisees juste au-dessus pour les barres d'accent du bas
            // (Mode/Compagnon/Accessibilite) - aucune nouvelle teinte, pas de
            // risque de lisibilite propre a ce mode.
            SetAppBrush("NovaTileFamilyContentBrush", UiColor(255, 213, 0));
            SetAppBrush("NovaTileFamilyProtectionBrush", UiColor(255, 120, 140));
            SetAppBrush("NovaTileFamilyToolsBrush", UiColor(0, 255, 226));
            NavigationToolbar.Opacity = 1;
            VerticalTabsRail.Opacity = 1;
            return;
        }

        // Filet defensif (bug d'origine corrige a la racine le 2026-08-08 -
        // consolidation d'ApplyUsageModeChrome plus haut, qui n'ecrit plus
        // jamais de valeur locale sur ModulesButton). Garde par prudence :
        // si un futur appel venait a reintroduire une ecriture directe sur
        // ModulesButton.Background/BorderBrush/Foreground ailleurs, ce
        // ClearValue empeche qu'elle reste collee apres desactivation du
        // contraste eleve, comme au 1er signalement de l'utilisateur.
        ModulesButton.ClearValue(Control.BackgroundProperty);
        ModulesButton.ClearValue(Control.BorderBrushProperty);
        ModulesButton.ClearValue(Control.ForegroundProperty);
        // NB (2026-08-10, toujours vrai le 2026-08-12 apres le retour de
        // ModulesButtonMark en Path vectoriel - voir plus haut dans ce meme
        // fichier) : ModulesButtonMark.Fill n'est JAMAIS ecrit ailleurs dans
        // le code - c'est une valeur locale posee une seule fois en XAML
        // (NovaIdentityMarkBrush). Le ClearValue qui etait ici l'effacait donc
        // pour de bon a chaque application de theme, rendant le symbole du
        // bouton Demarrer invisible en permanence (regression signalee par
        // l'utilisateur - "il y a meme plus l'icone"). Retire : rien ne
        // justifie de le filet defensif sur cette propriete tant que rien
        // d'autre ne lui ecrit de valeur locale.

        var alpha = translucent ? (byte)232 : (byte)255;
        var mode = (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant();
        var chrome = ResolveModeChromePalette(mode, isDark, alpha);

        // Couleur d'accentuation globale (MainWindow.AccentColor.cs) :
        // strictement apres le "return" du contraste eleve ci-dessus, jamais
        // avant - le contraste eleve doit continuer a ecraser toute couleur
        // personnalisee. Globale depuis le 2026-09-14 (session "3.5") :
        // s'applique quel que soit "mode" ci-dessus (avant : une couleur
        // differente par mode, cf. l'ancien GetModeAccentColorHex(mode)).
        var hasCustomModeAccent = TryResolveGlobalAccentOverride(out var customAccent);
        if (hasCustomModeAccent)
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
        // Meme teinte de mode que NovaAddressForegroundBrush ci-dessus pour
        // les usages generaux (voir ApplyAccessibilitySettings) - la branche
        // contraste eleve, elle, retourne plus haut sans repasser ici : sa
        // valeur blanche posee par ApplyAccessibilitySettings n'est jamais
        // ecrasee, comme les autres brushes consolidees ce jour-la.
        SetBrush("NovaPrimaryTextBrush", chrome.Text);
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
        // Fond/bordure au repos des icones de la ligne d'outils : transparent
        // quel que soit le Mode d'usage (highContrast deja exclu par le
        // return anticipe ci-dessus) - voir la meme cle dans
        // ApplyAccessibilitySettings().
        SetBrush("NovaChromeIconRestBackgroundBrush", UiColor(0, 0, 0, 0));
        SetBrush("NovaChromeIconRestBorderBrush", UiColor(0, 0, 0, 0));
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
        // Etoile de favori au repos : transparente comme les autres icones de
        // la ligne d'adresse (highContrast deja exclu par le return anticipe
        // ci-dessus) - seule la variante Active plus bas garde un
        // remplissage teinte par le Mode d'usage.
        SetBrush("NovaBookmarkButtonBackgroundBrush", UiColor(0, 0, 0, 0));
        SetBrush("NovaBookmarkButtonBorderBrush", UiColor(0, 0, 0, 0));
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
        // Familles de sens (chantier identite visuelle, 2026-08-10) : valeurs
        // FIXES, pas derivees de chrome.Accent/CoolAccent comme Bookmark
        // ci-dessus - contrairement a la couleur de mode (Focus/Lecture/...),
        // le sens "protection"/"contenu"/"outils" doit rester le meme quel
        // que soit le mode d'usage actif. Le contraste eleve est deja gere
        // plus haut dans ApplyUsageModeChrome (return anticipe) : rien a
        // faire ici pour ce cas.
        SetAppBrush("NovaTileFamilyContentBrush", isDark ? UiColor(230, 170, 72) : UiColor(196, 128, 12));
        SetAppBrush("NovaTileFamilyProtectionBrush", isDark ? UiColor(226, 137, 127) : UiColor(190, 70, 55));
        SetAppBrush("NovaTileFamilyToolsBrush", isDark ? UiColor(86, 194, 228) : UiColor(0, 129, 168));
        SetBrush("NovaFocusBrush", chrome.Focus);
        SetBrush("NovaFocusInnerBrush", isDark ? UiColor(13, 13, 13) : UiColor(255, 255, 255));
        SetBrush("NovaTextOnAccentBrush", LumoraTheme.ResolveTextOnColor(chrome.Accent));
        SetBrush("NovaOverlayBrush", isDark ? UiColor(6, 6, 6, 176) : UiColor(20, 24, 33, 92));
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
        // Puce de favori au repos : transparente quel que soit le Mode
        // d'usage (highContrast deja exclu par le return anticipe) - voir la
        // meme cle dans ApplyAccessibilitySettings().
        SetBrush("NovaBookmarkBarButtonBackgroundBrush", UiColor(0, 0, 0, 0));
        SetBrush("NovaBookmarkBarButtonBorderBrush", UiColor(0, 0, 0, 0));
        SetBrush("NovaBookmarkBarButtonForegroundBrush", isDark
            ? chrome.Text
            : ChromeTint(chrome.Text, chrome.Accent, 0.12, 255));
        // Icone de dossier de favori UNIQUEMENT (Path.Fill, voir
        // BookmarkIconElement dans MainWindow.Bookmarks.cs - cle separee de
        // NovaBookmarkBarButtonForegroundBrush ci-dessus, qui reste le texte
        // du favori, jamais teinte) : quand une couleur d'accentuation
        // personnalisee est active, prend directement chrome.Accent - retour
        // utilisateur explicite ("que l'icone du dossier prenne la couleur",
        // "pas de couleurs de fond") - AUCUN fond derriere
        // (NovaBookmarkBarButtonBackgroundBrush reste transparent, jamais
        // touche ici). Sans couleur personnalisee, meme valeur que
        // NovaBookmarkBarButtonForegroundBrush - apparence inchangee.
        SetBrush("NovaBookmarkFolderGlyphBrush", hasCustomModeAccent
            ? chrome.Accent
            : (isDark ? chrome.Text : ChromeTint(chrome.Text, chrome.Accent, 0.12, 255)));
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
        ModeUsageButton.Background = new SolidColorBrush(ChromeTint(chrome.Surface, chrome.Accent, 0.055, translucent ? (byte)214 : (byte)232));
        ModeUsageButton.BorderBrush = new SolidColorBrush(ChromeTint(chrome.StrokeSoft, chrome.Accent, 0.13, 178));
        ModeUsageButton.Foreground = new SolidColorBrush(chrome.Text);
        UsageModeAccentBar.Background = new SolidColorBrush(chrome.Accent);

        // Compagnon et Accessibilite (2026-08-10, defusion des 3 menus) : le
        // fond tinte suit le meme Cool Accent que celui deja utilise pour ce
        // genre de pill (translucide, coherent avec le reste du chrome) - la
        // distinction visuelle entre les 2 menus vient de leur barre d'accent
        // et de leur icone (CompanionAccentBar en rose fixe, non mode-tinte,
        // AccessibilityQuickAccentBar en Cool Accent comme avant), pas de leur
        // fond.
        CompanionButton.Background = new SolidColorBrush(ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.10, translucent ? (byte)218 : (byte)236));
        CompanionButton.BorderBrush = new SolidColorBrush(ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.24, 190));
        CompanionButton.Foreground = new SolidColorBrush(chrome.Text);
        AccessibilityMenuButton.Background = new SolidColorBrush(ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.10, translucent ? (byte)218 : (byte)236));
        AccessibilityMenuButton.BorderBrush = new SolidColorBrush(ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.24, 190));
        AccessibilityMenuButton.Foreground = new SolidColorBrush(chrome.Text);
        AccessibilityQuickAccentBar.Background = new SolidColorBrush(chrome.CoolAccent);

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
            BrushColor("NovaTextOnAccentBrush", UiColor(13, 13, 13)),
            BrushColor("NovaFocusBrush", UiColor(195, 214, 255)),
            BrushColor("NovaFocusInnerBrush", UiColor(13, 13, 13)),
            BrushColor("NovaChromeButtonForegroundBrush", UiColor(244, 244, 244)),
            BrushColor("NovaTextMutedBrush", UiColor(178, 178, 178)),
            BrushColor("NovaChromeSurfaceBrush", UiColor(19, 19, 19)),
            BrushColor("NovaChromeSurfaceRaisedBrush", UiColor(31, 31, 31)),
            BrushColor("NovaChromeStrokeBrush", UiColor(58, 58, 58)));
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

        // Fond/surfaces "Noir neutre" PARTAGES entre "neutral" et "focus"
        // (2026-09-14 : le mode Focus a ete aligne sur Neutre faute
        // d'atmosphere distincte comme Lecture/Creation/Recherche/Nuit,
        // Accent/CoolAccent/WarmAccent/Focus - l'identite cyan du mode -
        // restent propres a chacun). Extrait en valeurs communes plutot que
        // recopie 2 fois en litteral (nettoyage 2026-09-14, doublon reel
        // trouve en audit) : ce type de valeur avait deja legerement divergee
        // par le passe entre ces 2 memes branches (8,13,20 contre 9,13,20,
        // voir l'historique de ce fichier) - un partage direct empeche
        // desormais ce genre de derive silencieuse.
        var neutralizedBackground = UiColor(13, 13, 13);
        var neutralizedSurface = UiColor(19, 19, 19, alpha);
        var neutralizedSurfaceAlt = UiColor(24, 24, 24, alpha);
        var neutralizedSurfaceRaised = UiColor(31, 31, 31, alpha);
        var neutralizedStroke = UiColor(58, 58, 58);
        var neutralizedStrokeSoft = UiColor(38, 38, 38);
        var neutralizedAddressBackground = UiColor(21, 21, 21, 242);
        var neutralizedAddressBorder = UiColor(74, 74, 74);

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
                neutralizedBackground, neutralizedSurface, neutralizedSurfaceAlt, neutralizedSurfaceRaised,
                neutralizedStroke, neutralizedStrokeSoft, neutralizedAddressBackground, neutralizedAddressBorder,
                UiColor(120, 145, 196), UiColor(120, 145, 196, 24), UiColor(201, 158, 78), UiColor(201, 158, 78, 18),
                UiColor(219, 142, 98), UiColor(198, 214, 240), UiColor(178, 178, 178), UiColor(244, 244, 244)),
            "focus" => new(
                neutralizedBackground, neutralizedSurface, neutralizedSurfaceAlt, neutralizedSurfaceRaised,
                neutralizedStroke, neutralizedStrokeSoft, neutralizedAddressBackground, neutralizedAddressBorder,
                UiColor(86, 194, 228), UiColor(86, 194, 228, 32), UiColor(255, 185, 53), UiColor(255, 185, 53, 22),
                UiColor(235, 126, 74), UiColor(190, 224, 255), UiColor(178, 178, 178), UiColor(244, 244, 244)),
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
                UiColor(14, 14, 14), UiColor(20, 20, 20, alpha), UiColor(24, 24, 24, alpha), UiColor(29, 29, 29, alpha),
                UiColor(58, 58, 58), UiColor(38, 38, 38), UiColor(20, 20, 20, 242), UiColor(75, 75, 75),
                UiColor(230, 170, 72), UiColor(230, 170, 72, 42), UiColor(86, 194, 228), UiColor(86, 194, 228, 24),
                UiColor(232, 128, 86), UiColor(195, 214, 255), UiColor(179, 179, 179), UiColor(244, 244, 244))
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
