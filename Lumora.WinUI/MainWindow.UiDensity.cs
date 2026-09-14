using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// Taille de l'interface (boutons de la barre d'outils, ligne d'outils,
// barre d'adresse, barre de favoris) : "comfortable" (tailles historiques
// de Lumora) | "standard" (nouveau defaut) | "dense". Reglage independant
// du Mode d'usage et de _compactModeEnabled ("Interface compacte" = mode
// plein ecran/immersif, pas une densite de boutons - les deux peuvent
// coexister, voir les regles de priorite ci-dessous). Nouveau fichier
// plutot qu'un ajout a MainWindow.Settings.cs/SettingsTheme.cs (deja
// volumineux), meme logique de decoupage que MainWindow.TabGroups.cs.
public sealed partial class MainWindow
{
    private static string NormalizeUiDensity(string? value) => value switch
    {
        "comfortable" => "comfortable",
        "dense" => "dense",
        _ => "standard"
    };

    private readonly record struct UiDensityMetrics(
        double IconButtonSize,
        double ModulesButtonSize,
        double NavigationRowHeight,
        Thickness NavigationToolbarPadding,
        double AddressBoxMinHeight,
        Thickness AddressBoxPadding,
        double AddressBoxCornerRadius,
        double AddressIdentityBadgeSize,
        double AddressIdentityBadgeCornerRadius,
        Thickness AddressIdentityBadgeMargin,
        double AddressIdentityIconSize,
        double BookmarksRowHeight,
        double BookmarksBottomRowHeight,
        double BookmarkChipHeight,
        Thickness BookmarkChipPaddingText,
        Thickness BookmarkChipPaddingIcon,
        double BookmarkChipCornerRadius,
        double BookmarkChipFontSize,
        double BookmarksOverflowChipSize,
        double BookmarksOverflowChipCornerRadius,
        double FooterPillMinHeight,
        double FooterPillPaddingHorizontal,
        double FooterBoldFontSize,
        double FooterLabelFontSize,
        double FooterIconFontSize,
        double FooterChevronFontSize,
        double FooterAccentBarHeight,
        double FooterDividerHeight,
        double StatusTextFontSize);

    // "comfortable" reprend a l'identique les valeurs historiques de Lumora
    // (aucune regression pour qui choisit ce palier). "standard"/"dense" sont
    // des paliers intermediaires/reduits, verifies en conditions reelles.
    // AddressIdentityBadgeMargin/IconSize (2026-08-08) : marge et icone du
    // badge d'identite de la barre d'adresse restaient figees (10/12) pendant
    // que le badge retrecissait autour en standard/dense - "l'impression que
    // tu as tout decale" (retour utilisateur). Reduites ici dans la meme
    // proportion que AddressIdentityBadgeSize plutot que figees.
    private static UiDensityMetrics ResolveUiDensityMetrics(string density) => NormalizeUiDensity(density) switch
    {
        "comfortable" => new UiDensityMetrics(
            IconButtonSize: 32, ModulesButtonSize: 40,
            NavigationRowHeight: 72, NavigationToolbarPadding: new Thickness(12, 6, 12, 7),
            AddressBoxMinHeight: 46, AddressBoxPadding: new Thickness(54, 4, 18, 4), AddressBoxCornerRadius: 22,
            AddressIdentityBadgeSize: 30, AddressIdentityBadgeCornerRadius: 15,
            AddressIdentityBadgeMargin: new Thickness(10, 0, 0, 0), AddressIdentityIconSize: 12,
            BookmarksRowHeight: 60, BookmarksBottomRowHeight: 42,
            // Puces resserrees (2026-09-10, session "interface") : 36/14 lisait
            // encore "boule" a cote des puces quasi rectangulaires de Chrome -
            // ecarts entre paliers conserves a l'identique, voir maquette
            // "Lumora Epure" (palier standard).
            BookmarkChipHeight: 32, BookmarkChipPaddingText: new Thickness(10, 0, 12, 0), BookmarkChipPaddingIcon: new Thickness(8, 0, 8, 0),
            BookmarkChipCornerRadius: 8, BookmarkChipFontSize: 13,
            BookmarksOverflowChipSize: 32, BookmarksOverflowChipCornerRadius: 8,
            FooterPillMinHeight: 30, FooterPillPaddingHorizontal: 10,
            FooterBoldFontSize: 11.5, FooterLabelFontSize: 10.5, FooterIconFontSize: 11.5,
            FooterChevronFontSize: 8, FooterAccentBarHeight: 14, FooterDividerHeight: 12,
            StatusTextFontSize: 12),
        "dense" => new UiDensityMetrics(
            IconButtonSize: 24, ModulesButtonSize: 32,
            NavigationRowHeight: 50, NavigationToolbarPadding: new Thickness(8, 3, 8, 4),
            AddressBoxMinHeight: 40, AddressBoxPadding: new Thickness(44, 2, 14, 2), AddressBoxCornerRadius: 16,
            AddressIdentityBadgeSize: 22, AddressIdentityBadgeCornerRadius: 11,
            AddressIdentityBadgeMargin: new Thickness(7, 0, 0, 0), AddressIdentityIconSize: 9,
            BookmarksRowHeight: 44, BookmarksBottomRowHeight: 34,
            // Voir note du palier "comfortable" ci-dessus (memes ecarts conserves).
            BookmarkChipHeight: 22, BookmarkChipPaddingText: new Thickness(6, 0, 8, 0), BookmarkChipPaddingIcon: new Thickness(4, 0, 4, 0),
            BookmarkChipCornerRadius: 4, BookmarkChipFontSize: 11,
            BookmarksOverflowChipSize: 22, BookmarksOverflowChipCornerRadius: 4,
            FooterPillMinHeight: 24, FooterPillPaddingHorizontal: 7,
            FooterBoldFontSize: 10, FooterLabelFontSize: 9, FooterIconFontSize: 10,
            FooterChevronFontSize: 7, FooterAccentBarHeight: 11, FooterDividerHeight: 10,
            StatusTextFontSize: 11),
        _ => new UiDensityMetrics(
            IconButtonSize: 28, ModulesButtonSize: 36,
            NavigationRowHeight: 60, NavigationToolbarPadding: new Thickness(10, 4, 10, 5),
            AddressBoxMinHeight: 44, AddressBoxPadding: new Thickness(48, 3, 16, 3), AddressBoxCornerRadius: 18,
            AddressIdentityBadgeSize: 26, AddressIdentityBadgeCornerRadius: 13,
            AddressIdentityBadgeMargin: new Thickness(9, 0, 0, 0), AddressIdentityIconSize: 10,
            BookmarksRowHeight: 52, BookmarksBottomRowHeight: 38,
            BookmarkChipHeight: 26, BookmarkChipPaddingText: new Thickness(8, 0, 10, 0), BookmarkChipPaddingIcon: new Thickness(6, 0, 6, 0),
            BookmarkChipCornerRadius: 6, BookmarkChipFontSize: 12,
            BookmarksOverflowChipSize: 26, BookmarksOverflowChipCornerRadius: 6,
            FooterPillMinHeight: 27, FooterPillPaddingHorizontal: 9,
            FooterBoldFontSize: 11, FooterLabelFontSize: 10, FooterIconFontSize: 11,
            FooterChevronFontSize: 7.5, FooterAccentBarHeight: 13, FooterDividerHeight: 11,
            StatusTextFontSize: 11.5),
    };

    // Palier "ultra-compact" (2026-09-12, retour utilisateur : "Interface
    // compacte" ne changeait jusqu'ici QUE NavigationRowHeight/Padding -
    // 60->58px en Standard, un ecart de 2px imperceptible, "j'ai pas
    // l'impression qu'il soit si compact que ca"). Jamais choisi via
    // UiDensityCombo (n'entre pas dans NormalizeUiDensity/UiSettings.UiDensity,
    // qui restent Standard/Confortable/Dense) - active UNIQUEMENT par
    // CompactModeEnabled, qui prend le dessus sur TOUTES les dimensions de la
    // densite choisie, pas seulement la ligne d'outils. Valeurs approuvees sur
    // maquette Artifact (comparaison a l'echelle Standard/Dense/Ultra) avant
    // codage, plus petites que Dense sur chaque dimension.
    // AddressBoxMinHeight/Padding corriges le 2026-09-12 (2e retour utilisateur,
    // meme session) : 34/(36,1,10,1) rendait le texte "prend toute la barre,
    // deborde, plus centre" - le FontSize de AddressBox (18, XAML, JAMAIS
    // touche par la densite, y compris en Dense) ne retrecit lui-meme jamais.
    // Retrecir le remplissage haut/bas plus vite que la taille de texte fixe
    // qu'il entoure (4px confortable -> 1px ici) l'a rendu visuellement
    // "colle" au bord de la pilule des que le padding est devenu trop faible
    // pour laisser respirer un texte qui, lui, n'a pas change. Corrige en
    // gardant une marge verticale proche de celle de "Dense" (2px) plutot que
    // de continuer a la reduire pour un gain qui n'existe pas cote texte.
    // NavigationRowHeight remonte dans la foulee (42->46) : le 1er correctif
    // (juste AddressBoxMinHeight/Padding) restait SANS EFFET mesure en direct
    // (34px avant et apres) - NavigationRowHeight est une hauteur de ligne
    // FIXE (pas Auto), et 42px moins le rembourrage de la ligne d'outils
    // (NavigationToolbarPadding) ne laissait que ~37px de haut disponibles a
    // AddressBox, un plafond plus bas que le nouveau MinHeight (38) qui
    // l'annulait silencieusement. Toutes les autres paliers (Dense 50/40,
    // Standard 60/44, Confortable 72/46) gardent un ecart net entre les 2 -
    // Ultra en avait un de 8 (42/34), agrandi ici a 8 aussi (46/38) plutot que
    // de repartir de 4 comme la 1ere correction l'aurait fait sans le remarquer.
    private static readonly UiDensityMetrics UltraCompactMetrics = new(
        IconButtonSize: 20, ModulesButtonSize: 26,
        NavigationRowHeight: 46, NavigationToolbarPadding: new Thickness(6, 3, 6, 4),
        AddressBoxMinHeight: 38, AddressBoxPadding: new Thickness(34, 3, 10, 3), AddressBoxCornerRadius: 16,
        AddressIdentityBadgeSize: 18, AddressIdentityBadgeCornerRadius: 9,
        AddressIdentityBadgeMargin: new Thickness(5, 0, 0, 0), AddressIdentityIconSize: 7,
        BookmarksRowHeight: 36, BookmarksBottomRowHeight: 28,
        BookmarkChipHeight: 18, BookmarkChipPaddingText: new Thickness(5, 0, 6, 0), BookmarkChipPaddingIcon: new Thickness(3, 0, 3, 0),
        BookmarkChipCornerRadius: 3, BookmarkChipFontSize: 10,
        BookmarksOverflowChipSize: 18, BookmarksOverflowChipCornerRadius: 3,
        FooterPillMinHeight: 20, FooterPillPaddingHorizontal: 6,
        FooterBoldFontSize: 9.5, FooterLabelFontSize: 8.5, FooterIconFontSize: 9,
        FooterChevronFontSize: 6.5, FooterAccentBarHeight: 9, FooterDividerHeight: 8,
        StatusTextFontSize: 10.5);

    // Point d'entree UNIQUE a utiliser partout a la place de
    // ResolveUiDensityMetrics(_uiDensity) directement (8 appels avant cette
    // passe, tous mis a jour) : garantit que CompactModeEnabled prend le
    // dessus de facon uniforme, plutot que le mecanisme precedent qui ne
    // couvrait que 2 des ~29 dimensions de UiDensityMetrics.
    private UiDensityMetrics ResolveEffectiveUiDensityMetrics() =>
        _compactModeEnabled ? UltraCompactMetrics : ResolveUiDensityMetrics(_uiDensity);

    // Extension du mode compact aux onglets (2026-09-12, retour utilisateur :
    // "il faut que le mode compact fonctionne partout... sur la barre des
    // onglets que ca soit en vertical ou en horizontal") - jusqu'ici
    // CompactModeEnabled ne touchait NI TopTabsRow ni le rail vertical, une
    // exclusion deliberee et documentee depuis l'origine de la Densite
    // d'interface (ni Standard/Confortable/Dense ne les ont jamais fait
    // varier). Portee confirmee avec l'utilisateur : Compact SEUL les
    // rétrécit desormais, Standard/Confortable/Dense restent inchanges pour
    // les onglets (comportement historique). Egalement volontairement limite
    // aux elements les plus visibles (hauteur de ligne, favicon, bouton
    // fermer, largeur du rail) - pas chaque detail (poignee de glissement,
    // badge de groupe), decision actee avec l'utilisateur plutot que
    // reprendre tout MainWindow.TabGroups.cs au pixel pres.
    private const double CompactHorizontalTabRowHeight = 40;
    private const double CompactHorizontalTabIconWrapSize = 18;
    private const double CompactHorizontalTabIconSize = 11;
    private const double CompactHorizontalTabPinnedWrapSize = 22;
    private const double CompactHorizontalTabPinnedIconSize = 13;
    private const double CompactVerticalTabsIconOnlyRailWidth = 30;
    private const double CompactVerticalTabsExpandedRailWidth = 150;
    private const double CompactVerticalTabTileWidth = 22;
    private const double CompactVerticalTabTileHeight = 20;
    private const double CompactVerticalTabCompactIconSize = 11;
    private const double CompactVerticalTabCompactCloseSize = 14;
    private const double CompactVerticalTabRowIconSize = 12;
    private const double CompactVerticalTabCloseSize = 18;

    private double ResolveNavigationRowHeight() => ResolveEffectiveUiDensityMetrics().NavigationRowHeight;

    private Thickness ResolveNavigationToolbarPadding() => ResolveEffectiveUiDensityMetrics().NavigationToolbarPadding;

    private string SelectedUiDensity() =>
        NormalizeUiDensity((UiDensityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? _uiDensity);

    private void UiDensityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave)
        {
            return;
        }

        ApplyUiDensityImmediate(SelectedUiDensity());
    }

    private void ApplyUiDensityImmediate(string density)
    {
        _uiDensity = NormalizeUiDensity(density);

        SetWorkspaceControlsSilently(() =>
        {
            SelectComboByTag(UiDensityCombo, _uiDensity, "standard");
        });

        ApplyUiDensity();
        SaveWorkspaceUiSettings();
        UpdateStatusText($"Taille de l'interface : {UiDensityLabel(_uiDensity)}.");
    }

    private static string UiDensityLabel(string density) => NormalizeUiDensity(density) switch
    {
        "comfortable" => "confortable",
        "dense" => "dense",
        _ => "standard"
    };

    // Applique le palier de densite courant a la barre d'outils, la barre
    // d'adresse et la barre de favoris. Ne touche pas AddressBox.FontSize
    // (pilote separement par AccessibilityLargeText, voir
    // ApplyAccessibilitySettings() dans MainWindow.SettingsTheme.cs) ni la
    // barre d'onglets (TopTabsRow, hors perimetre de ce reglage) ni la
    // fenetre Incognito (styles/reglages totalement separes).
    private void ApplyUiDensity()
    {
        var metrics = ResolveEffectiveUiDensityMetrics();

        ModulesButton.Width = metrics.ModulesButtonSize;
        ModulesButton.Height = metrics.ModulesButtonSize;

        AddressBox.MinHeight = metrics.AddressBoxMinHeight;
        AddressBox.Padding = metrics.AddressBoxPadding;
        AddressBox.CornerRadius = new CornerRadius(metrics.AddressBoxCornerRadius);
        AddressBoxOutlineBorder.CornerRadius = new CornerRadius(metrics.AddressBoxCornerRadius);
        AddressBoxTintBorder.CornerRadius = new CornerRadius(metrics.AddressBoxCornerRadius - 1);
        AddressIdentityBadge.Width = metrics.AddressIdentityBadgeSize;
        AddressIdentityBadge.Height = metrics.AddressIdentityBadgeSize;
        AddressIdentityBadge.CornerRadius = new CornerRadius(metrics.AddressIdentityBadgeCornerRadius);
        // Retour utilisateur (2026-08-08) : marge et icone du badge restaient
        // figees pendant que le badge lui-meme retrecissait autour - la barre
        // d'adresse "en petit" avait l'air decalee/mal alignee. Les deux
        // suivent maintenant la meme proportion que AddressIdentityBadgeSize.
        AddressIdentityBadge.Margin = metrics.AddressIdentityBadgeMargin;
        AddressIdentityIconGrid.Width = metrics.AddressIdentityIconSize;
        AddressIdentityIconGrid.Height = metrics.AddressIdentityIconSize;

        // ApplyCompactModeLayout() relit ResolveNavigationRowHeight()/
        // ResolveNavigationToolbarPadding() (priorite mode compact) et
        // appelle deja ApplyBookmarksBarVisibility() (relit BookmarksRow/
        // WorkspaceBottomBookmarksRow + reconstruit les puces via
        // RenderBookmarksBar()) - meme cascade que CompactModeSwitch_Toggled,
        // idempotente comme le reste d'ApplyUiSettings().
        ApplyCompactModeLayout();
        ApplyIconButtonSizing();
        ApplyFooterDensity(metrics);
    }

    // Barre du bas : demande explicite utilisateur 2026-08-09 ("elle aussi
    // doit etre plus petite, moyenne ou plus grande") - jusqu'ici hors du
    // perimetre de la Taille de l'interface (tailles figees en dur). Depuis
    // le 2026-08-10, la barre du bas porte 3 menus independants (Mode,
    // Compagnon, Accessibilite - defusion demandee explicitement) : les 3
    // suivent la meme metrique, plus de UsageModeDividerBorder (disparu avec
    // le bouton fusionne). "Comfortable" reprend les valeurs historiques
    // (aucune regression), standard/dense reduisent dans des proportions
    // comparables a IconButtonSize/NavigationRowHeight.
    private void ApplyFooterDensity(UiDensityMetrics metrics)
    {
        // Pastilles -> icone + point d'etat (2026-09-10, session "interface") :
        // les 3 boutons suivent desormais NovaChromeIconButtonStyle (meme
        // famille que les boutons de la barre d'outils) plutot que la largeur
        // variable en pilule (FooterPillMinHeight/PaddingHorizontal devenus
        // sans objet ici, encore utilises par ApplyIconButtonSizing ailleurs -
        // non retires). Reutilise IconButtonSize (deja partage par la barre
        // d'outils) plutot que MinHeight+Padding pour continuer de suivre la
        // Taille de l'interface - sans quoi la demande explicite du 2026-08-09
        // ("elle aussi doit etre plus petite, moyenne ou plus grande")
        // cesserait de s'appliquer a ces 3 boutons. Le point reprend
        // FooterAccentBarHeight comme diametre (Width ET Height, pas
        // seulement Height comme avant) pour rester un cercle net a chaque
        // palier plutot que s'ovaliser.
        // Les 3 pilules (Mode d'usage/Compagnon/Accessibilite) partagent
        // exactement les memes reglages - boucle plutot que le meme bloc
        // recopie 3 fois (nettoyage 2026-09-14, trouve en audit : un 4e
        // pilule ou une correction future n'aurait autrement qu'un risque
        // reel d'oubli sur une des 3 copies).
        var footerIconSize = metrics.IconButtonSize;
        foreach (var button in new[] { ModeUsageButton, CompanionButton, AccessibilityMenuButton })
        {
            button.Width = footerIconSize;
            button.Height = footerIconSize;
            button.MinWidth = footerIconSize;
        }
        // Filet de securite : meme taille posee explicitement sur le Grid
        // interne (icone + point), au cas ou HorizontalContentAlignment=
        // Stretch sur le bouton ne suffirait pas a lui seul a le faire
        // occuper tout le bouton (ContentPresenter/Grid par defaut sinon
        // "colle" au contenu, ce qui centre le point sur l'icone au lieu de
        // l'envoyer dans le coin).
        foreach (var grid in new[] { ModeUsageIconGrid, CompanionIconGrid, AccessibilityQuickIconGrid })
        {
            grid.Width = footerIconSize;
            grid.Height = footerIconSize;
        }

        var dotSize = metrics.FooterAccentBarHeight;
        foreach (var dot in new[] { UsageModeAccentBar, CompanionAccentBar, AccessibilityQuickAccentBar })
        {
            dot.Width = dotSize;
            dot.Height = dotSize;
            dot.CornerRadius = new CornerRadius(dotSize / 2);
        }

        foreach (var icon in new[] { UsageModeIcon, CompanionIcon, AccessibilityQuickIcon })
        {
            icon.FontSize = metrics.FooterIconFontSize;
        }
        foreach (var chevron in new[] { UsageModeChevronIcon, CompanionChevronIcon, AccessibilityChevronIcon })
        {
            chevron.FontSize = metrics.FooterChevronFontSize;
        }

        foreach (var label in new[] { UsageModeLabelText, CompanionLabelText, AccessibilityQuickLabelText })
        {
            label.FontSize = metrics.FooterLabelFontSize;
        }
        foreach (var bold in new[] { UsageModeCurrentText, CompanionStateText, AccessibilityQuickCurrentText })
        {
            bold.FontSize = metrics.FooterBoldFontSize;
        }

        StatusText.FontSize = metrics.StatusTextFontSize;
    }
}
