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
            BookmarkChipHeight: 36, BookmarkChipPaddingText: new Thickness(12, 0, 14, 0), BookmarkChipPaddingIcon: new Thickness(10, 0, 10, 0),
            BookmarkChipCornerRadius: 14, BookmarkChipFontSize: 13,
            BookmarksOverflowChipSize: 36, BookmarksOverflowChipCornerRadius: 14,
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
            BookmarkChipHeight: 26, BookmarkChipPaddingText: new Thickness(8, 0, 10, 0), BookmarkChipPaddingIcon: new Thickness(6, 0, 6, 0),
            BookmarkChipCornerRadius: 10, BookmarkChipFontSize: 11,
            BookmarksOverflowChipSize: 26, BookmarksOverflowChipCornerRadius: 10,
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
            BookmarkChipHeight: 30, BookmarkChipPaddingText: new Thickness(10, 0, 12, 0), BookmarkChipPaddingIcon: new Thickness(8, 0, 8, 0),
            BookmarkChipCornerRadius: 12, BookmarkChipFontSize: 12,
            BookmarksOverflowChipSize: 30, BookmarksOverflowChipCornerRadius: 12,
            FooterPillMinHeight: 27, FooterPillPaddingHorizontal: 9,
            FooterBoldFontSize: 11, FooterLabelFontSize: 10, FooterIconFontSize: 11,
            FooterChevronFontSize: 7.5, FooterAccentBarHeight: 13, FooterDividerHeight: 11,
            StatusTextFontSize: 11.5),
    };

    // Priorite "mode compact gagne" sur la ligne d'outils : CompactModeEnabled
    // (Interface compacte, mode plein ecran/immersif) garde sa taille reduite
    // existante (58px) quelle que soit la densite choisie - la densite ne
    // s'applique a la ligne d'outils que si le mode compact est desactive.
    // Centralise ici la formule auparavant dupliquee dans
    // ApplyCompactModeLayout() et ApplyFullScreenLayout()
    // (MainWindow.Settings.cs).
    private double ResolveNavigationRowHeight() =>
        _compactModeEnabled ? 58 : ResolveUiDensityMetrics(_uiDensity).NavigationRowHeight;

    private Thickness ResolveNavigationToolbarPadding() =>
        _compactModeEnabled ? new Thickness(10, 3, 10, 4) : ResolveUiDensityMetrics(_uiDensity).NavigationToolbarPadding;

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
        var metrics = ResolveUiDensityMetrics(_uiDensity);

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
        var pillPadding = new Thickness(metrics.FooterPillPaddingHorizontal, 0, metrics.FooterPillPaddingHorizontal, 0);

        ModeUsageButton.MinHeight = metrics.FooterPillMinHeight;
        ModeUsageButton.Padding = pillPadding;
        CompanionButton.MinHeight = metrics.FooterPillMinHeight;
        CompanionButton.Padding = pillPadding;
        AccessibilityMenuButton.MinHeight = metrics.FooterPillMinHeight;
        AccessibilityMenuButton.Padding = pillPadding;

        UsageModeAccentBar.Height = metrics.FooterAccentBarHeight;
        CompanionAccentBar.Height = metrics.FooterAccentBarHeight;
        AccessibilityQuickAccentBar.Height = metrics.FooterAccentBarHeight;

        UsageModeIcon.FontSize = metrics.FooterIconFontSize;
        CompanionIcon.FontSize = metrics.FooterIconFontSize;
        AccessibilityQuickIcon.FontSize = metrics.FooterIconFontSize;
        UsageModeChevronIcon.FontSize = metrics.FooterChevronFontSize;
        CompanionChevronIcon.FontSize = metrics.FooterChevronFontSize;
        AccessibilityChevronIcon.FontSize = metrics.FooterChevronFontSize;

        UsageModeLabelText.FontSize = metrics.FooterLabelFontSize;
        CompanionLabelText.FontSize = metrics.FooterLabelFontSize;
        AccessibilityQuickLabelText.FontSize = metrics.FooterLabelFontSize;
        UsageModeCurrentText.FontSize = metrics.FooterBoldFontSize;
        CompanionStateText.FontSize = metrics.FooterBoldFontSize;
        AccessibilityQuickCurrentText.FontSize = metrics.FooterBoldFontSize;

        StatusText.FontSize = metrics.StatusTextFontSize;
    }
}
