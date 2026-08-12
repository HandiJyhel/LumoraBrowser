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
            "_uiSettings.AccessibilityLargeTargets ? 44d : ResolveUiDensityMetrics(_uiDensity).IconButtonSize",
            settingsThemeCode,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Mode_compact_garde_priorite_sur_la_densite_pour_la_ligne_d_outils()
    {
        // Regle de priorite actee : CompactModeEnabled ("Interface compacte",
        // mode plein ecran/immersif) garde sa hauteur reduite existante
        // (58px) quelle que soit la densite choisie - centralise dans
        // ResolveNavigationRowHeight()/ResolveNavigationToolbarPadding().
        var densityCode = ReadRepoFile("Lumora.WinUI", "MainWindow.UiDensity.cs");

        Assert.Contains("_compactModeEnabled ? 58 : ResolveUiDensityMetrics(_uiDensity).NavigationRowHeight", densityCode, StringComparison.Ordinal);
        Assert.Contains("_compactModeEnabled ? new Thickness(10, 3, 10, 4) : ResolveUiDensityMetrics(_uiDensity).NavigationToolbarPadding", densityCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Densite_confortable_reprend_exactement_les_tailles_historiques()
    {
        // "Confortable" doit rester un choix a zero regression pour qui le
        // selectionne : les valeurs doivent correspondre aux anciennes
        // tailles fixes (32px boutons, 72px ligne d'outils, 42px barre
        // d'adresse, 36px puces de favoris) toujours documentees ici.
        var densityCode = ReadRepoFile("Lumora.WinUI", "MainWindow.UiDensity.cs");

        var comfortableIndex = densityCode.IndexOf("\"comfortable\" => new UiDensityMetrics(", StringComparison.Ordinal);
        Assert.True(comfortableIndex >= 0, "Palier \"comfortable\" introuvable dans ResolveUiDensityMetrics.");
        var comfortableBlock = densityCode.Substring(comfortableIndex, Math.Min(700, densityCode.Length - comfortableIndex));

        Assert.Contains("IconButtonSize: 32", comfortableBlock, StringComparison.Ordinal);
        Assert.Contains("NavigationRowHeight: 72", comfortableBlock, StringComparison.Ordinal);
        Assert.Contains("AddressBoxMinHeight: 42", comfortableBlock, StringComparison.Ordinal);
        Assert.Contains("BookmarkChipHeight: 36", comfortableBlock, StringComparison.Ordinal);
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
        // suivent tous les 3 la meme metrique.
        var densityCode = ReadRepoFile("Lumora.WinUI", "MainWindow.UiDensity.cs");

        Assert.Contains("ApplyFooterDensity(metrics);", densityCode, StringComparison.Ordinal);
        Assert.Contains("ModeUsageButton.MinHeight = metrics.FooterPillMinHeight;", densityCode, StringComparison.Ordinal);
        Assert.Contains("CompanionButton.MinHeight = metrics.FooterPillMinHeight;", densityCode, StringComparison.Ordinal);
        Assert.Contains("AccessibilityMenuButton.MinHeight = metrics.FooterPillMinHeight;", densityCode, StringComparison.Ordinal);
        Assert.Contains("StatusText.FontSize = metrics.StatusTextFontSize;", densityCode, StringComparison.Ordinal);

        var comfortableIndex = densityCode.IndexOf("\"comfortable\" => new UiDensityMetrics(", StringComparison.Ordinal);
        Assert.True(comfortableIndex >= 0, "Palier \"comfortable\" introuvable dans ResolveUiDensityMetrics.");
        var comfortableBlock = densityCode.Substring(comfortableIndex, Math.Min(1400, densityCode.Length - comfortableIndex));

        Assert.Contains("FooterPillMinHeight: 30", comfortableBlock, StringComparison.Ordinal);
        Assert.Contains("FooterBoldFontSize: 11.5", comfortableBlock, StringComparison.Ordinal);
        Assert.Contains("StatusTextFontSize: 12", comfortableBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Fenetre_incognito_et_barre_d_onglets_restent_hors_perimetre()
    {
        // Non-regression explicite : ce reglage ne doit toucher ni la fenetre
        // Incognito (styles/reglages totalement separes) ni TopTabsRow (la
        // barre d'onglets, hors demande utilisateur initiale).
        var incognitoCode = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");
        var settingsCode = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");

        Assert.DoesNotContain("UiDensity", incognitoCode, StringComparison.Ordinal);
        Assert.Contains("TopTabsRow.Height = new GridLength(52);", settingsCode, StringComparison.Ordinal);
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
