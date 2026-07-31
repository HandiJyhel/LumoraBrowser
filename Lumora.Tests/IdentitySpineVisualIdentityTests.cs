using Xunit;

namespace Lumora.Tests;

// Meme style que UsageModeVisualIdentityTests/AccessibilityRegressionTests :
// assertions structurelles sur le source (pas d'execution UI). Couvre la
// colonne verticale identitaire (style de disposition "identitySpine",
// MainWindow.IdentitySpine.cs) ajoutee en 0.93.6.0-dev.
public sealed class IdentitySpineVisualIdentityTests
{
    [Fact]
    public void Colonne_retrecie_ne_reduit_pas_les_cibles_tactiles_deja_auditees()
    {
        // Demande utilisateur du 2026-07-28 ("ca prend un peu de place") :
        // colonne resserree de 76 a 64px et marges/espacements retrecis, MAIS
        // sans toucher aux dimensions de cibles cliquables (44px pastilles
        // d'onglet) - la marge de manoeuvre vient des espaces vides autour,
        // pas des cibles elles-memes.
        // Boutons NovaChromeIconButtonStyle : retour a 32px par defaut le
        // 2026-07-29 (l'aide "44px cible AAA" du 2026-07-28 avait ete imposee
        // a tout le monde par defaut - retour utilisateur : une aide
        // d'accessibilite se choisit). La cible AAA reste disponible, mais
        // opt-in via le reglage AccessibilityLargeTargets + ApplyIconButtonSizing()
        // (MainWindow.SettingsTheme.cs), pas via un Setter XAML fixe.
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");
        var settingsThemeCode = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsTheme.cs");

        var hostIndex = xaml.IndexOf("x:Name=\"IdentitySpineHost\"", StringComparison.Ordinal);
        Assert.True(hostIndex >= 0, "IdentitySpineHost introuvable.");
        var hostDeclaration = xaml.Substring(hostIndex, Math.Min(300, xaml.Length - hostIndex));
        Assert.Contains("Width=\"64\"", hostDeclaration, StringComparison.Ordinal);

        Assert.Contains("Width = 44,\n                Height = 44,", code, StringComparison.Ordinal);

        var iconButtonStyleIndex = xaml.IndexOf("x:Key=\"NovaChromeIconButtonStyle\"", StringComparison.Ordinal);
        Assert.True(iconButtonStyleIndex >= 0, "NovaChromeIconButtonStyle introuvable.");
        var iconButtonStyle = xaml.Substring(iconButtonStyleIndex, Math.Min(400, xaml.Length - iconButtonStyleIndex));
        Assert.Contains("Setter Property=\"Width\" Value=\"32\"", iconButtonStyle, StringComparison.Ordinal);

        Assert.Contains("_uiSettings.AccessibilityLargeTargets ? 44d : 32d", settingsThemeCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Colonne_identitaire_et_capsule_existent_dans_le_xaml()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("<Grid x:Name=\"IdentitySpineHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NavigationToolbarCapsule\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineAddressHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineFooterHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineTabItems\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ChromeLayoutStyleCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"identitySpine\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Disposition_classique_reste_disponible_a_cote_de_la_colonne_identitaire()
    {
        // Non-regression explicite : le style de disposition classique et le
        // reglage "Onglets verticaux" existant restent presents et inchanges,
        // la colonne identitaire s'ajoute sans les remplacer.
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("x:Name=\"VerticalTabsSwitch\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"TabStripPositionCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BrowserTabs\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"classic\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Six_pastilles_de_couleur_par_mode_exposent_un_nom_accessible_explicite()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        foreach (var (pickerName, swatchName, automationName) in new[]
        {
            ("ModeColorPickerNeutral", "ModeColorSwatchNeutralButton", "Couleur du mode Neutre"),
            ("ModeColorPickerFocus", "ModeColorSwatchFocusButton", "Couleur du mode Focus"),
            ("ModeColorPickerReading", "ModeColorSwatchReadingButton", "Couleur du mode Lecture"),
            ("ModeColorPickerCreative", "ModeColorSwatchCreativeButton", "Couleur du mode Creation"),
            ("ModeColorPickerResearch", "ModeColorSwatchResearchButton", "Couleur du mode Recherche"),
            ("ModeColorPickerNight", "ModeColorSwatchNightButton", "Couleur du mode Nuit"),
        })
        {
            Assert.Contains($"x:Name=\"{pickerName}\"", xaml, StringComparison.Ordinal);
            Assert.Contains($"x:Name=\"{swatchName}\"", xaml, StringComparison.Ordinal);
            Assert.Contains($"AutomationProperties.Name=\"{automationName}\"", xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Couleur_personnalisee_est_lue_apres_le_court_circuit_du_contraste_eleve()
    {
        // Garantie critique : ApplyUsageModeChrome() retourne tot quand le
        // contraste eleve est actif, AVANT de lire _uiSettings.UsageMode ou
        // toute couleur personnalisee. La lecture de la couleur par mode doit
        // rester strictement apres ce court-circuit, jamais avant - sinon le
        // contraste eleve n'ecraserait plus une couleur choisie par
        // l'utilisateur (regression du garde-fou deja documente pour le
        // Mode d'usage lui-meme, cf. UsageMode.cs).
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsTheme.cs");

        var highContrastBranchMarker = source.IndexOf(
            "UsageModeAccentBar.Background = new SolidColorBrush(UiColor(255, 213, 0));",
            StringComparison.Ordinal);
        var customAccentReadMarker = source.IndexOf("GetModeAccentColorHex(mode)", StringComparison.Ordinal);

        Assert.True(highContrastBranchMarker >= 0, "Marqueur de la branche contraste eleve introuvable.");
        Assert.True(customAccentReadMarker >= 0, "Lecture de la couleur personnalisee introuvable.");
        Assert.True(
            customAccentReadMarker > highContrastBranchMarker,
            "La couleur personnalisee par mode doit etre lue apres la branche de contraste eleve, jamais avant.");
    }

    [Fact]
    public void Derivation_du_degrade_reste_une_seule_couleur_choisie_par_mode()
    {
        // Garde-fou explicite demande par l'utilisateur : une seule couleur
        // par mode, le second ton du degrade toujours derive automatiquement
        // (pas de deuxieme selecteur de couleur).
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");

        Assert.Contains("DeriveModeAccentTones(", source, StringComparison.Ordinal);
        Assert.Contains("ApplyCustomModeAccent(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ModeAccentColorNeutralCool", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ModeAccentColorNeutralWarm", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Capsule_d_adresse_flotte_a_part_de_la_colonne_plutot_que_confinee_dedans()
    {
        // Regression du relooking : la premiere version reparentait la
        // capsule DANS la colonne (Grid.Row="1" de IdentitySpineHost),
        // ce qui la faisait deborder. Elle doit desormais etre un overlay
        // sibling independant, centre, decale de la largeur de la colonne.
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.DoesNotContain(
            "<Grid x:Name=\"IdentitySpineAddressHost\" Grid.Row=\"1\" Margin=\"4,0,4,10\" />",
            xaml,
            StringComparison.Ordinal);

        var hostIndex = xaml.IndexOf("x:Name=\"IdentitySpineHost\"", StringComparison.Ordinal);
        Assert.True(hostIndex >= 0, "IdentitySpineHost introuvable.");
        var hostDeclaration = xaml.Substring(hostIndex, Math.Min(300, xaml.Length - hostIndex));
        Assert.Contains("Width=\"64\"", hostDeclaration, StringComparison.Ordinal);

        var addressHostIndex = xaml.IndexOf("x:Name=\"IdentitySpineAddressHost\"", StringComparison.Ordinal);
        Assert.True(addressHostIndex >= 0, "IdentitySpineAddressHost introuvable.");
        var addressHostDeclaration = xaml.Substring(addressHostIndex, Math.Min(300, xaml.Length - addressHostIndex));
        Assert.Contains("Grid.RowSpan=\"7\"", addressHostDeclaration, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Center\"", addressHostDeclaration, StringComparison.Ordinal);
        Assert.Contains("Canvas.ZIndex=\"34\"", addressHostDeclaration, StringComparison.Ordinal);
    }

    [Fact]
    public void Ecran_compagnon_natif_existe_et_reutilise_les_donnees_du_mode()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var identitySpine = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");
        var usageMode = ReadRepoFile("Lumora.WinUI", "MainWindow.UsageMode.cs");

        Assert.Contains("x:Name=\"IdentitySpineHomeHero\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineHeroTitleText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineHeroObjectiveBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineHeroPrimaryButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineHeroSecondaryButton\"", xaml, StringComparison.Ordinal);
        // Meme habillage que le compagnon natif existant, pas un nouveau
        // langage visuel.
        Assert.Contains("Style=\"{StaticResource NovaFlyoutSectionCardStyle}\"", xaml, StringComparison.Ordinal);

        // Reutilisation stricte : aucun texte de mode duplique en dur, tout
        // vient de GetCurrentModeCompanion()/RunModeCompanionActionAsync deja
        // existants.
        Assert.Contains("UpdateIdentitySpineHeroUi(mode, label, companion)", usageMode, StringComparison.Ordinal);
        Assert.Contains("GetCurrentModeCompanion()", identitySpine, StringComparison.Ordinal);
        Assert.Contains("RunModeCompanionActionAsync(", identitySpine, StringComparison.Ordinal);
        Assert.Contains("SaveCompanionMemoryAndNotify(", identitySpine, StringComparison.Ordinal);
    }

    [Fact]
    public void Ecran_compagnon_ne_s_affiche_que_sur_l_accueil_en_colonne_identitaire()
    {
        // Non-regression : le hero ne doit jamais recouvrir un vrai site, ni
        // s'afficher en disposition classique.
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");

        Assert.Contains(
            "_chromeLayoutStyle == \"identitySpine\" && isHome",
            source,
            StringComparison.Ordinal);
        Assert.Contains("tab.Address.Equals(\"lumora://accueil\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Fonctions_de_chrome_classique_ne_reaffichent_plus_le_chrome_par_dessus_la_colonne()
    {
        // Regression du bug "double onglets" remonte par l'utilisateur :
        // ApplyVerticalTabsLayout()/ApplyCompactModeLayout() pouvaient
        // reafficher BrowserTabs/TopTabsRow par-dessus la colonne identitaire
        // active, depuis des declencheurs independants (Reglages, Studio
        // Lumora, bouton Appliquer). Verifie que chacune retourne bien avant
        // de toucher au chrome classique quand la colonne est active.
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");

        var verticalTabsIndex = source.IndexOf("private void ApplyVerticalTabsLayout()", StringComparison.Ordinal);
        Assert.True(verticalTabsIndex >= 0, "ApplyVerticalTabsLayout() introuvable.");
        var verticalTabsGuardIndex = source.IndexOf("_chromeLayoutStyle == \"identitySpine\"", verticalTabsIndex, StringComparison.Ordinal);
        var verticalTabsBodyIndex = source.IndexOf("_suppressTabNavigation = true;", verticalTabsIndex, StringComparison.Ordinal);
        Assert.True(verticalTabsGuardIndex >= 0 && verticalTabsGuardIndex < verticalTabsBodyIndex,
            "ApplyVerticalTabsLayout() doit verifier _chromeLayoutStyle avant d'appliquer le chrome classique.");

        var compactModeIndex = source.IndexOf("private void ApplyCompactModeLayout()", StringComparison.Ordinal);
        Assert.True(compactModeIndex >= 0, "ApplyCompactModeLayout() introuvable.");
        var compactModeGuardIndex = source.IndexOf("_chromeLayoutStyle == \"identitySpine\"", compactModeIndex, StringComparison.Ordinal);
        var compactModeBodyIndex = source.IndexOf("NavigationRow.Height = new GridLength(_compactModeEnabled", compactModeIndex, StringComparison.Ordinal);
        Assert.True(compactModeGuardIndex >= 0 && compactModeGuardIndex < compactModeBodyIndex,
            "ApplyCompactModeLayout() doit verifier _chromeLayoutStyle avant d'appliquer le chrome classique.");
    }

    [Fact]
    public void Favoris_integres_existent_pour_les_4_positions()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("x:Name=\"IdentitySpineCapsuleSlot\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineBookmarksTop\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineBookmarksBottomHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineBookmarksRightHost\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineFavoritesSection\"", xaml, StringComparison.Ordinal);

        // Non-regression : les conteneurs classiques existent toujours,
        // rien n'a ete supprime pour construire l'integration.
        Assert.Contains("x:Name=\"BookmarksBarRow\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BookmarksBottomRow\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BookmarksSideRail\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBookmarksBar_resynchronise_les_favoris_integres()
    {
        // Meme patron que RenderVerticalTabs()/RenderIdentitySpineTabs() :
        // aucun call-site supplementaire a traquer pour garder les favoris
        // de la colonne identitaire a jour.
        var bookmarks = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");
        var identitySpine = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");

        Assert.Contains("RenderIdentitySpineBookmarks()", bookmarks, StringComparison.Ordinal);
        Assert.Contains("CreateBookmarkBarButton(node)", identitySpine, StringComparison.Ordinal);
    }

    [Fact]
    public void Masquage_automatique_existe_et_reutilise_le_delai_du_plein_ecran()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var identitySpine = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");

        Assert.Contains("x:Name=\"IdentitySpineAutoHideSwitch\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineLeftRevealZone\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineTopRevealZone\"", xaml, StringComparison.Ordinal);

        Assert.Contains("private void ScheduleIdentitySpineHide()", identitySpine, StringComparison.Ordinal);
        Assert.Contains("private void ShowIdentitySpineChrome()", identitySpine, StringComparison.Ordinal);
        // Reutilisation du delai existant, pas une nouvelle duree inventee.
        Assert.Contains("Interval = FullScreenAutoHideDelay", identitySpine, StringComparison.Ordinal);
        // Opt-in : jamais actif par defaut.
        Assert.Contains("public bool IdentitySpineAutoHide { get; set; } = false;", ReadRepoFile("Lumora.WinUI", "Models", "UiSettings.cs"), StringComparison.Ordinal);

        // Non-regression : les zones de revelation du plein ecran existant
        // restent inchangees.
        Assert.Contains("x:Name=\"FullScreenLeftRevealZone\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FullScreenTopRevealZone\"", xaml, StringComparison.Ordinal);
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
