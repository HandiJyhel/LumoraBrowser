using Xunit;

namespace Lumora.Tests;

// Meme style que UsageModeVisualIdentityTests :
// assertions structurelles sur le source (pas d'execution UI). Couvre la
// Taille de l'interface (UiDensity, MainWindow.UiDensity.cs) - reglage
// utilisateur pour la taille des boutons de la barre d'outils, de la barre
// d'adresse, de la barre de favoris et (depuis le 2026-08-09, demande
// explicite utilisateur) de la barre du bas (StatusBarRow/ApplyFooterDensity),
// hors fenetre Incognito et hors barre d'onglets (hors perimetre).
public sealed class UiDensityVisualIdentityTests
{
    [Fact]
    public void UiDensityCombo_existe_dans_le_xaml_avec_ses_trois_paliers()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("x:Name=\"UiDensityCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"comfortable\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"standard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"dense\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessibilityLargeTargets_garde_priorite_sur_la_densite_pour_les_boutons_icone()
    {
        // Regle de priorite actee avec l'utilisateur : la densite ne doit
        // jamais defaire silencieusement l'accessibilite (chantier 0.93.x en
        // cours). Verrouille que ApplyIconButtonSizing() consulte toujours
        // AccessibilityLargeTargets en premier, la densite en repli.
        var settingsThemeCode = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsTheme.cs");

        Assert.Contains(
            "_uiSettings.AccessibilityLargeTargets ? 44d : ResolveEffectiveUiDensityMetrics().IconButtonSize",
            settingsThemeCode,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Mode_compact_est_un_4e_palier_complet_plus_petit_que_dense()
    {
        // Regle de priorite actee : CompactModeEnabled ("Interface compacte")
        // prend le dessus sur la densite choisie via
        // ResolveEffectiveUiDensityMetrics - depuis le 2026-09-12 (retour
        // utilisateur : l'ancien mecanisme ne changeait que 2 des ~29
        // dimensions, un ecart de 2px en Standard, "j'ai pas l'impression
        // qu'il soit si compact que ca"), un vrai 4e palier UltraCompactMetrics
        // couvre TOUTES les dimensions, plus petit que Dense sur chacune.
        var densityCode = ReadRepoFile("Lumora.WinUI", "MainWindow.UiDensity.cs");

        Assert.Contains(
            "_compactModeEnabled ? UltraCompactMetrics : ResolveUiDensityMetrics(_uiDensity)",
            densityCode,
            StringComparison.Ordinal);

        var ultraIndex = densityCode.IndexOf("UltraCompactMetrics = new(", StringComparison.Ordinal);
        Assert.True(ultraIndex >= 0, "UltraCompactMetrics introuvable dans MainWindow.UiDensity.cs.");
        var ultraBlock = densityCode.Substring(ultraIndex, Math.Min(1200, densityCode.Length - ultraIndex));

        // Plus petit que Dense (IconButtonSize 24, NavigationRowHeight 50,
        // AddressBoxMinHeight 40, BookmarkChipHeight 22) sur chaque dimension.
        Assert.Contains("IconButtonSize: 20", ultraBlock, StringComparison.Ordinal);
        // 42 -> 46 (2026-09-12, meme session) : ligne fixe (pas Auto), le 1er
        // correctif du remplissage de AddressBox restait SANS EFFET tant que
        // la ligne elle-meme (moins son propre rembourrage) plafonnait sous
        // le nouveau AddressBoxMinHeight - mesure en direct (34px avant/apres,
        // aucun changement) avant de comprendre la cause reelle.
        Assert.Contains("NavigationRowHeight: 46", ultraBlock, StringComparison.Ordinal);
        // 34 -> 38 (2026-09-12, 2e retour utilisateur meme session) : le texte
        // de AddressBox (FontSize 18, jamais retreci par la densite) rendait
        // "deborde, plus centre" avec un remplissage vertical trop reduit
        // (1px) - remonte pres du niveau "Dense" (2px) plutot que continuer a
        // le reduire pour un gain qui n'existe pas cote texte.
        Assert.Contains("AddressBoxMinHeight: 38", ultraBlock, StringComparison.Ordinal);
        Assert.Contains("BookmarkChipHeight: 18", ultraBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Densite_confortable_garde_la_barre_adresse_non_rognante()
    {
        // "Confortable" doit rester un choix a zero regression pour qui le
        // selectionne : les tailles historiques restent, mais la barre
        // d'adresse garde la hauteur utile necessaire au texte 18px SemiBold.
        var densityCode = ReadRepoFile("Lumora.WinUI", "MainWindow.UiDensity.cs");

        var comfortableIndex = densityCode.IndexOf("\"comfortable\" => new UiDensityMetrics(", StringComparison.Ordinal);
        Assert.True(comfortableIndex >= 0, "Palier \"comfortable\" introuvable dans ResolveUiDensityMetrics.");
        // 700 -> 1000 (2026-09-10) : commentaire ajoute au-dessus de
        // BookmarkChipHeight (revision favoris, session "interface") pousse
        // ce champ plus loin dans le bloc.
        var comfortableBlock = densityCode.Substring(comfortableIndex, Math.Min(1000, densityCode.Length - comfortableIndex));

        Assert.Contains("IconButtonSize: 32", comfortableBlock, StringComparison.Ordinal);
        Assert.Contains("NavigationRowHeight: 72", comfortableBlock, StringComparison.Ordinal);
        Assert.Contains("AddressBoxMinHeight: 46", comfortableBlock, StringComparison.Ordinal);
        Assert.Contains("AddressBoxPadding: new Thickness(54, 4, 18, 4)", comfortableBlock, StringComparison.Ordinal);
        // 36 -> 32 (2026-09-10, session "interface") : puces de favoris
        // resserrees, ecarts entre paliers conserves - voir maquette
        // "Lumora Epure".
        Assert.Contains("BookmarkChipHeight: 32", comfortableBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Barre_du_bas_suit_desormais_la_taille_de_l_interface()
    {
        // Demande explicite utilisateur (2026-08-09, chantier "menu confort") :
        // la barre du bas (StatusBarRow) etait jusque-la hors perimetre de la
        // Taille de l'interface (tailles figees en dur). "Confortable" reprend
        // les valeurs historiques (aucune regression), meme principe que les
        // autres paliers deja verrouilles ci-dessus. Depuis le 2026-08-10 la
        // barre porte 3 menus independants (Mode/Compagnon/Accessibilite) qui
        // suivent tous les 3 la meme metrique. Depuis le 2026-09-10 (session
        // "interface"), les 3 pastilles etiquetees sont devenues des boutons
        // icone+point (NovaChromeIconButtonStyle) : la taille ne passe plus
        // par FooterPillMinHeight/Padding mais reutilise IconButtonSize (deja
        // partage par la barre d'outils) - le principe verrouille par ce test
        // (la barre du bas suit la Taille de l'interface) reste inchange,
        // seul le mecanisme change. Depuis le 2026-09-14 (nettoyage), les 3
        // boutons sont poses via UNE SEULE boucle plutot que 3 blocs recopies
        // - le test verifie desormais que les 3 sont bien dans le MEME
        // tableau (donc ne peuvent plus diverger entre eux) et que le corps
        // de la boucle applique bien footerIconSize.
        var densityCode = ReadRepoFile("Lumora.WinUI", "MainWindow.UiDensity.cs");

        Assert.Contains("ApplyFooterDensity(metrics);", densityCode, StringComparison.Ordinal);
        Assert.Contains("var footerIconSize = metrics.IconButtonSize;", densityCode, StringComparison.Ordinal);
        Assert.Contains("foreach (var button in new[] { ModeUsageButton, CompanionButton, AccessibilityMenuButton })", densityCode, StringComparison.Ordinal);
        Assert.Contains("button.Width = footerIconSize;", densityCode, StringComparison.Ordinal);
        Assert.Contains("StatusText.FontSize = metrics.StatusTextFontSize;", densityCode, StringComparison.Ordinal);

        var comfortableIndex = densityCode.IndexOf("\"comfortable\" => new UiDensityMetrics(", StringComparison.Ordinal);
        Assert.True(comfortableIndex >= 0, "Palier \"comfortable\" introuvable dans ResolveUiDensityMetrics.");
        // 1400 -> 1700 (2026-09-10) : meme raison que ci-dessus.
        var comfortableBlock = densityCode.Substring(comfortableIndex, Math.Min(1700, densityCode.Length - comfortableIndex));

        Assert.Contains("IconButtonSize: 32", comfortableBlock, StringComparison.Ordinal);
        Assert.Contains("FooterBoldFontSize: 11.5", comfortableBlock, StringComparison.Ordinal);
        Assert.Contains("StatusTextFontSize: 12", comfortableBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Fenetre_incognito_reste_hors_perimetre_onglets_suivent_desormais_le_mode_compact()
    {
        // Non-regression explicite : la fenetre Incognito (styles/reglages
        // totalement separes) reste hors de portee. La barre d'onglets, elle,
        // a rejoint le PERIMETRE DU MODE COMPACT SEUL le 2026-09-12 (retour
        // utilisateur : "il faut que le mode compact fonctionne partout... sur
        // la barre des onglets") - Standard/Confortable/Dense continuent de
        // ne JAMAIS la faire varier (52 reste le repli quand
        // CompactModeEnabled est desactive), seul CompactModeEnabled la
        // retrecit desormais (CompactHorizontalTabRowHeight).
        var incognitoCode = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");
        var settingsCode = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");

        Assert.DoesNotContain("UiDensity", incognitoCode, StringComparison.Ordinal);
        Assert.Contains("new GridLength(_compactModeEnabled ? CompactHorizontalTabRowHeight : 52)", settingsCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Interface_compacte_retrecit_le_rail_vertical_et_ses_boutons_daction()
    {
        // Rail vertical (ApplyVerticalTabsWidth, MainWindow.Settings.cs) et
        // boutons d'action du rail (ApplyIconButtonSizing, MainWindow.SettingsTheme.cs)
        // ne suivaient ni la Densite ni CompactModeEnabled avant le
        // 2026-09-12 : VerticalTabsRail n'etait jamais parcouru par
        // ApplyIconButtonSizeRecursive (seuls NavigationToolbar/FullScreenTopBar
        // l'etaient), d'ou des boutons de rail restes a taille normale malgre
        // le reglage active (capture d'ecran utilisateur a l'appui).
        var settingsCode = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");
        var settingsThemeCode = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsTheme.cs");

        Assert.Contains(
            "_compactModeEnabled\n            ? (_verticalTabsCompact ? CompactVerticalTabsIconOnlyRailWidth : CompactVerticalTabsExpandedRailWidth)",
            settingsCode,
            StringComparison.Ordinal);
        Assert.Contains("ApplyIconButtonSizeRecursive(VerticalTabsRail, styles, size);", settingsThemeCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Interface_compacte_retrecit_favicon_et_bouton_fermer_des_onglets()
    {
        // Barre d'onglets horizontale (TabHeaderContent) et verticale
        // (RenderVerticalTabs) : favicon et bouton fermer suivent desormais
        // CompactModeEnabled - portee volontairement limitee aux elements les
        // plus visibles (pas la poignee de glissement ni le badge de groupe),
        // decision actee avec l'utilisateur plutot que reprendre tout le
        // rendu au pixel pres.
        var tabGroupsCode = ReadRepoFile("Lumora.WinUI", "MainWindow.TabGroups.cs");

        Assert.Contains("_compactModeEnabled ? CompactHorizontalTabIconWrapSize : 24", tabGroupsCode, StringComparison.Ordinal);
        Assert.Contains("_compactModeEnabled ? CompactHorizontalTabIconSize : 14", tabGroupsCode, StringComparison.Ordinal);
        Assert.Contains("_compactModeEnabled ? CompactVerticalTabCompactIconSize : 14", tabGroupsCode, StringComparison.Ordinal);
        Assert.Contains("_compactModeEnabled ? CompactVerticalTabCloseSize : 26", tabGroupsCode, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Fichier projet introuvable: {string.Join(Path.DirectorySeparatorChar, segments)}");
    }
}
