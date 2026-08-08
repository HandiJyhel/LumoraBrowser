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

        // Depuis l'ajout de la Taille de l'interface (UiDensity), le 32d fixe
        // a ete remplace par la valeur resolue depuis le palier de densite
        // courant - AccessibilityLargeTargets garde neanmoins toujours la
        // priorite sur la densite, cf. UiDensityVisualIdentityTests.
        Assert.Contains("_uiSettings.AccessibilityLargeTargets ? 44d : ResolveUiDensityMetrics(_uiDensity).IconButtonSize", settingsThemeCode, StringComparison.Ordinal);
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
            ("ModeColorPickerCreative", "ModeColorSwatchCreativeButton", "Couleur du mode Création"),
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
        // sibling independant, decale de la largeur de la colonne, et pleine
        // largeur depuis le 2026-08-05 - cf.
        // Capsule_d_adresse_occupe_toute_la_largeur_pas_centree_ni_compacte
        // ci-dessous.
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
        Assert.Contains("Canvas.ZIndex=\"34\"", addressHostDeclaration, StringComparison.Ordinal);
    }

    [Fact]
    public void Capsule_d_adresse_occupe_toute_la_largeur_pas_centree_ni_compacte()
    {
        // Retour utilisateur du 2026-08-05 : une capsule centree au milieu de
        // l'ecran ne correspond a aucune convention des navigateurs du marche
        // (1er correctif -> HorizontalAlignment="Left"). Puis constat en
        // conditions reelles : alignee a gauche mais toujours dimensionnee a
        // son seul contenu, elle laissait un grand vide a droite ("degueulasse"),
        // incoherent avec le mode Classique ou ce meme NavigationToolbarCapsule
        // s'etire pleine largeur. Correctif final : ni Center ni Left, aucun
        // HorizontalAlignment explicite sur IdentitySpineAddressHost/
        // IdentitySpineCapsuleSlot -> Stretch par defaut, comme
        // NavigationToolbarCapsule lui-meme (Border sans HorizontalAlignment).
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        var addressHostIndex = xaml.IndexOf("x:Name=\"IdentitySpineAddressHost\"", StringComparison.Ordinal);
        Assert.True(addressHostIndex >= 0, "IdentitySpineAddressHost introuvable.");
        var addressHostDeclaration = xaml.Substring(addressHostIndex, Math.Min(300, xaml.Length - addressHostIndex));
        Assert.DoesNotContain("HorizontalAlignment=\"Center\"", addressHostDeclaration, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalAlignment=\"Left\"", addressHostDeclaration, StringComparison.Ordinal);
        Assert.Contains("Margin=\"64,8,14,0\"", addressHostDeclaration, StringComparison.Ordinal);

        var capsuleSlotIndex = xaml.IndexOf("x:Name=\"IdentitySpineCapsuleSlot\"", StringComparison.Ordinal);
        Assert.True(capsuleSlotIndex >= 0, "IdentitySpineCapsuleSlot introuvable.");
        var capsuleSlotDeclaration = xaml.Substring(capsuleSlotIndex, Math.Min(120, xaml.Length - capsuleSlotIndex));
        Assert.DoesNotContain("HorizontalAlignment=\"Left\"", capsuleSlotDeclaration, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalAlignment=\"Center\"", capsuleSlotDeclaration, StringComparison.Ordinal);

    }

    [Fact]
    public void Favoris_integres_en_haut_ont_la_meme_capsule_de_verre_que_le_mode_classique()
    {
        // Retour utilisateur du 2026-08-05 (suite) : une fois la capsule
        // d'adresse passee pleine largeur, la ligne de favoris integree
        // ("Style Lumora", position "haut") restait un StackPanel nu - sans
        // fond/bordure/ombre - alors que BookmarksBarRow (mode Classique)
        // est enveloppee dans un Border "capsule de verre"
        // (NovaFloatingGlassBrush, CornerRadius, ThemeShadow) pleine largeur.
        // IdentitySpineBookmarksTop reste le conteneur des puces (Panel avec
        // .Children, lu par RenderIdentitySpineBookmarks) ; l'habillage vit
        // desormais sur IdentitySpineBookmarksTopCard qui l'enveloppe.
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var identitySpine = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");

        var cardIndex = xaml.IndexOf("x:Name=\"IdentitySpineBookmarksTopCard\"", StringComparison.Ordinal);
        Assert.True(cardIndex >= 0, "IdentitySpineBookmarksTopCard introuvable.");
        var cardDeclaration = xaml.Substring(cardIndex, Math.Min(600, xaml.Length - cardIndex));
        Assert.Contains("CornerRadius=\"18\"", cardDeclaration, StringComparison.Ordinal);
        Assert.Contains("Background=\"{StaticResource NovaFloatingGlassBrush}\"", cardDeclaration, StringComparison.Ordinal);
        Assert.Contains("<ThemeShadow", cardDeclaration, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalAlignment=\"Left\"", cardDeclaration, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalAlignment=\"Center\"", cardDeclaration, StringComparison.Ordinal);

        var bookmarksTopIndex = xaml.IndexOf("x:Name=\"IdentitySpineBookmarksTop\"", StringComparison.Ordinal);
        Assert.True(bookmarksTopIndex >= 0, "IdentitySpineBookmarksTop introuvable.");
        Assert.True(bookmarksTopIndex > cardIndex, "IdentitySpineBookmarksTop doit etre imbrique dans la carte.");

        Assert.Contains("IdentitySpineBookmarksTopCard.Visibility = Visibility.Visible;", identitySpine, StringComparison.Ordinal);
        Assert.Contains("IdentitySpineBookmarksTopCard.Visibility = Visibility.Collapsed;", identitySpine, StringComparison.Ordinal);
    }

    [Fact]
    public void Favoris_integres_incluent_le_dossier_Autres_favoris_comme_en_classique()
    {
        // Retour utilisateur du 2026-08-05 (suite) : "il manque toujours le
        // dossier autres favoris" - RenderIdentitySpineBookmarks() ne lisait
        // que BookmarkStore.ToolbarRootId, jamais OtherRootId, contrairement
        // a RenderBookmarksBar() (Classique, MainWindow.Bookmarks.cs) qui
        // affiche toujours ce dossier des lors qu'il existe (racine seedee
        // par BookmarkStore, meme vide). Verrouille : la racine est lue, le
        // garde-fou "aucun favori" n'exclut plus l'affichage tant qu'elle
        // existe, et un separateur visuel precede sa puce (meme repere que
        // BookmarksBarRow).
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");

        Assert.Contains("BookmarkStore.OtherRootId", source, StringComparison.Ordinal);
        Assert.Contains("toolbarNodes.Count == 0 && otherRoot is null", source, StringComparison.Ordinal);
        Assert.Contains("CreateIdentitySpineBookmarksDivider(compact)", source, StringComparison.Ordinal);
        Assert.Contains("StyleAsIdentitySpineBookmarkChip(CreateBookmarkBarButton(otherRoot), compact)", source, StringComparison.Ordinal);
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
        var compactModeBodyIndex = source.IndexOf("NavigationRow.Height = new GridLength(ResolveNavigationRowHeight())", compactModeIndex, StringComparison.Ordinal);
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

    [Fact]
    public void Compagnon_hero_partage_le_rappel_de_confidentialite_du_flyout_classique()
    {
        // Retour utilisateur du 2026-08-06 : en comparant le Compagnon "en
        // grand" (IdentitySpineHomeHero) au flyout ModeCompanionFlyout du
        // mode Classique, seule vraie difference trouvee (le bouton "Garder
        // dans Lumie" est correct des DEUX cotes - Lumie est le nom du
        // compagnon, pas un mode) : le petit rappel de confidentialite en
        // bas de carte n'existait que dans le flyout Classique.
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        var heroIndex = xaml.IndexOf("x:Name=\"IdentitySpineHomeHero\"", StringComparison.Ordinal);
        Assert.True(heroIndex >= 0, "IdentitySpineHomeHero introuvable.");
        var heroEndIndex = xaml.IndexOf("x:Name=\"IdentitySpineBookmarksBottomRow\"", StringComparison.Ordinal);
        Assert.True(heroEndIndex > heroIndex, "Limite de fin du Hero introuvable.");
        var heroDeclaration = xaml.Substring(heroIndex, heroEndIndex - heroIndex);

        Assert.Contains("Compagnon local : les notes et préférences restent dans ce profil Lumora.", heroDeclaration, StringComparison.Ordinal);

        // Non-regression : le flyout Classique garde le sien.
        Assert.Contains("x:Name=\"ModeCompanionPrivacyText\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Autres_favoris_reste_ancre_a_droite_en_position_haut()
    {
        // Retour utilisateur du 2026-08-06 : en Classique, "Autres favoris"
        // vit dans une colonne Grid dediee (Grid.Column="3", colonne des
        // puces en Grid.Column="1" de largeur "*") qui le pousse toujours a
        // l'extremite droite de la ligne. En position "haut" de la colonne
        // identitaire, il etait avant ca ajoute a la suite des puces dans le
        // meme StackPanel - il suivait donc la derniere puce au lieu de
        // rester ancre a droite. Corrige par une Grid deux colonnes
        // (memes proportions "*"/"Auto") avec un hote dedie pour le dossier.
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var identitySpine = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");

        var cardIndex = xaml.IndexOf("x:Name=\"IdentitySpineBookmarksTopCard\"", StringComparison.Ordinal);
        Assert.True(cardIndex >= 0, "IdentitySpineBookmarksTopCard introuvable.");
        var cardDeclaration = xaml.Substring(cardIndex, Math.Min(2000, xaml.Length - cardIndex));
        Assert.Contains("<ColumnDefinition Width=\"*\" />", cardDeclaration, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"Auto\" />", cardDeclaration, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineBookmarksTop\"", cardDeclaration, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineBookmarksTopOtherHost\"", cardDeclaration, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"1\"", cardDeclaration, StringComparison.Ordinal);

        Assert.Contains("IdentitySpineBookmarksTopOtherHost.Children.Add(CreateIdentitySpineBookmarksDivider(compact));", identitySpine, StringComparison.Ordinal);
        Assert.Contains("IdentitySpineBookmarksTopOtherHost.Children.Add(StyleAsIdentitySpineBookmarkChip(CreateBookmarkBarButton(otherRoot), compact));", identitySpine, StringComparison.Ordinal);
        Assert.Contains("IdentitySpineBookmarksTopOtherHost.Children.Clear();", identitySpine, StringComparison.Ordinal);
    }

    [Fact]
    public void Autres_favoris_reste_ancre_a_droite_en_position_bas()
    {
        // Meme correctif que la position "haut" ci-dessus, applique a la
        // position "bas" (2026-08-06) : la ligne flottante en bas passe
        // elle aussi d'un StackPanel unique a une Grid deux colonnes, avec
        // un hote dedie pour "Autres favoris".
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var identitySpine = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");

        var rowIndex = xaml.IndexOf("x:Name=\"IdentitySpineBookmarksBottomRow\"", StringComparison.Ordinal);
        Assert.True(rowIndex >= 0, "IdentitySpineBookmarksBottomRow introuvable.");
        var rowDeclaration = xaml.Substring(rowIndex, Math.Min(1500, xaml.Length - rowIndex));
        Assert.Contains("<ColumnDefinition Width=\"*\" />", rowDeclaration, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"Auto\" />", rowDeclaration, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineBookmarksBottomHost\"", rowDeclaration, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IdentitySpineBookmarksBottomOtherHost\"", rowDeclaration, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"1\"", rowDeclaration, StringComparison.Ordinal);

        Assert.Contains("IdentitySpineBookmarksBottomOtherHost.Children.Add(CreateIdentitySpineBookmarksDivider(compact));", identitySpine, StringComparison.Ordinal);
        Assert.Contains("IdentitySpineBookmarksBottomOtherHost.Children.Add(StyleAsIdentitySpineBookmarkChip(CreateBookmarkBarButton(otherRoot), compact));", identitySpine, StringComparison.Ordinal);
        Assert.Contains("IdentitySpineBookmarksBottomOtherHost.Children.Clear();", identitySpine, StringComparison.Ordinal);
        Assert.Contains("IdentitySpineBookmarksBottomRow.Visibility = Visibility.Visible;", identitySpine, StringComparison.Ordinal);
        Assert.Contains("IdentitySpineBookmarksBottomRow.Visibility = Visibility.Collapsed;", identitySpine, StringComparison.Ordinal);
    }

    // Bug reel remonte par capture d'ecran utilisateur (2026-08-07) : apres
    // avoir quitte le plein ecran (video ou F11) en Style Lumora, seule la
    // barre de favoris revenait - adresse et navigation disparues. Cause :
    // NavigationToolbar (la capsule adresse, reparentee dans
    // IdentitySpineCapsuleSlot) passe en Collapsed sans condition de style en
    // ENTRANT en plein ecran, mais seule la branche Classique la remettait en
    // Visible en SORTANT - son ancetre IdentitySpineAddressHost redevenait
    // visible, mais elle restait Collapsed a l'interieur. Reproduit en reel
    // (CDP + UIA, profil jetable) avant correctif, absent apres.
    [Fact]
    public void Sortie_plein_ecran_restaure_aussi_la_barre_adresse_en_style_lumora()
    {
        var settings = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");
        var method = ExtractMethod(settings, "private void ApplyFullScreenLayout()");

        var identitySpineBranchIndex = method.IndexOf("if (_chromeLayoutStyle == \"identitySpine\")", StringComparison.Ordinal);
        Assert.True(identitySpineBranchIndex >= 0, "Branche identitySpine introuvable dans ApplyFullScreenLayout.");
        // Deuxieme occurrence de la condition = la branche de RESTAURATION
        // (sortie de plein ecran), la premiere etant celle d'ENTREE.
        var restoreBranchIndex = method.IndexOf(
            "if (_chromeLayoutStyle == \"identitySpine\")",
            identitySpineBranchIndex + 1,
            StringComparison.Ordinal);
        Assert.True(restoreBranchIndex >= 0, "Branche de restauration identitySpine introuvable.");
        var restoreBranch = method.Substring(restoreBranchIndex, Math.Min(2200, method.Length - restoreBranchIndex));

        Assert.Contains("IdentitySpineAddressHost.Visibility = Visibility.Visible;", restoreBranch, StringComparison.Ordinal);
        Assert.Contains("NavigationToolbar.Visibility = Visibility.Visible;", restoreBranch, StringComparison.Ordinal);
    }

    // Bug reel remonte par capture d'ecran utilisateur (2026-08-07) : entre
    // colonne masquee et colonne revelee (masquage automatique OU simple
    // bascule Classique<->Style Lumora), la page web gardait exactement le
    // meme rendu - IdentitySpineHost est un overlay (Grid.RowSpan="7", hors
    // des colonnes de ContentHost), le rendre visible ne redimensionne rien,
    // il se contente de recouvrir les 64 premiers pixels du contenu (menu
    // propre du site masque en dessous). Corrige par un Margin sur
    // ContentHost, mis a jour a chaque endroit qui bascule
    // IdentitySpineHost.Visibility.
    [Fact]
    public void Colonne_identitaire_redimensionne_le_contenu_au_lieu_de_le_recouvrir()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");
        var settingsCode = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");

        Assert.Contains("private const double IdentitySpineContentInset = 64d;", code, StringComparison.Ordinal);
        Assert.Contains("private void UpdateIdentitySpineContentInset()", code, StringComparison.Ordinal);
        Assert.Contains(
            "ContentHost.Margin = IdentitySpineHost.Visibility == Visibility.Visible\n            ? new Thickness(IdentitySpineContentInset, 0, 0, 0)\n            : new Thickness(0);",
            code, StringComparison.Ordinal);

        // Chaque endroit qui bascule IdentitySpineHost.Visibility doit aussi
        // appeler la mise a jour du Margin - sinon un chemin isole recree le
        // meme chevauchement. 7 bascules connues : ApplyChromeLayoutStyle
        // (chemin idempotent), Enter/ExitIdentitySpineLayout, Show
        // IdentitySpineChrome/IdentitySpineHideTimer_Tick (masquage
        // automatique), et les deux branches d'ApplyFullScreenLayout.
        var identitySpineCallCount = System.Text.RegularExpressions.Regex.Matches(code, "UpdateIdentitySpineContentInset\\(\\);").Count;
        var settingsCallCount = System.Text.RegularExpressions.Regex.Matches(settingsCode, "UpdateIdentitySpineContentInset\\(\\);").Count;
        Assert.Equal(5, identitySpineCallCount);
        Assert.Equal(2, settingsCallCount);
    }

    // Bug reel remonte par capture d'ecran utilisateur (2026-08-07) : les
    // boutons systeme (min/max/fermer) flottaient directement au-dessus du
    // contenu web, sans aucune bande de fond derriere eux - "pas integres a
    // la chrome". Cause : TopTabsRow passait a 0 en Style Lumora (colonne
    // identitaire remplace la bande d'onglets classique), mais cette rangee
    // sert AUSSI de fond derriere les boutons systeme
    // (ExtendsContentIntoTitleBar) - meme motif deja corrige pour le mode
    // Classique + onglets verticaux ("comme Edge/Arc", MainWindow.Settings.cs).
    [Fact]
    public void Style_lumora_reserve_aussi_la_bande_de_fond_des_boutons_systeme()
    {
        var identitySpineCode = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");
        var settingsCode = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");

        var enterMethod = ExtractMethod(identitySpineCode, "private void EnterIdentitySpineLayout()");
        Assert.Contains("TopTabsRow.Height = new GridLength(52);", enterMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("TopTabsRow.Height = new GridLength(0);", enterMethod, StringComparison.Ordinal);

        var applyFullScreen = ExtractMethod(settingsCode, "private void ApplyFullScreenLayout()");
        var identitySpineBranchIndex = applyFullScreen.IndexOf("if (_chromeLayoutStyle == \"identitySpine\")", StringComparison.Ordinal);
        var restoreBranchIndex = applyFullScreen.IndexOf(
            "if (_chromeLayoutStyle == \"identitySpine\")",
            identitySpineBranchIndex + 1,
            StringComparison.Ordinal);
        Assert.True(restoreBranchIndex >= 0, "Branche de restauration identitySpine introuvable.");
        var restoreBranch = applyFullScreen.Substring(restoreBranchIndex, Math.Min(700, applyFullScreen.Length - restoreBranchIndex));
        Assert.Contains("TopTabsRow.Height = new GridLength(52);", restoreBranch, StringComparison.Ordinal);
    }

    // Decouvrabilite du reglage (2026-08-07, retour utilisateur : "comment
    // rendre le Style Lumora parfait" -> le reglage etait a 4 clics de
    // profondeur, Parametres > Mon Lumora > Disposition, rien ne le mettait
    // en avant). Nouvelle tuile dans le menu Demarrer (ModulesFlyout), une
    // seule ouverture depuis ModulesButton au lieu de naviguer tout Parametres.
    [Fact]
    public void Style_lumora_a_sa_propre_tuile_dans_le_menu_demarrer()
    {
        var tileIds = ReadRepoFile("Lumora.WinUI", "Models", "StartMenuTiles.cs");
        var registry = ReadRepoFile("Lumora.WinUI", "StartMenuTileRegistry.cs");
        var startMenuCode = ReadRepoFile("Lumora.WinUI", "MainWindow.StartMenu.cs");
        var mainWindowCode = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("public const string ChromeStyle = \"chromeStyle\";", tileIds, StringComparison.Ordinal);
        Assert.Contains("StartMenuTileIds.ChromeStyle, \"Lumora et profil\", \"Style Lumora\"", registry, StringComparison.Ordinal);
        Assert.Contains("StartMenuTileIds.ChromeStyle => OpenChromeStyleSettings,", startMenuCode, StringComparison.Ordinal);

        var handler = ExtractMethod(mainWindowCode, "private void OpenChromeStyleSettings()");
        Assert.Contains("ShowPanel(SettingsPanel, \"Mon Lumora\");", handler, StringComparison.Ordinal);
        Assert.Contains("SettingsNavAppearance.IsChecked = true;", handler, StringComparison.Ordinal);
        Assert.Contains("AppearanceSubNavLayout.IsChecked = true;", handler, StringComparison.Ordinal);
        Assert.Contains("AppearanceSubNav_Click(AppearanceSubNavLayout, new RoutedEventArgs());", handler, StringComparison.Ordinal);
    }

    // Bug reel remonte par capture d'ecran utilisateur (2026-08-08) : en
    // Style Lumora, aucune facon visible de fermer un onglet - la colonne
    // identitaire utilise RenderIdentitySpineTabs (MainWindow.IdentitySpine.cs),
    // un rendu totalement distinct de RenderVerticalTabs (MainWindow.TabGroups.cs)
    // qui, lui, avait deja recu la pastille de fermeture toujours visible
    // (0.93.13.1-dev/0.93.13.2-dev). Ce rail-ci n'avait jamais eu de bouton de
    // fermeture depuis sa creation - seuls le clic droit et Ctrl+W marchaient.
    // Meme motif applique ici, meme gestionnaire (VerticalTabCloseButton_Click)
    // que le rail Classique - aucune logique de fermeture dupliquee. Deuxieme
    // iteration (2026-08-08, meme jour) : la premiere version en pastille
    // 16x16 d'angle a ete jugee trop serree par l'utilisateur (Chrome cite en
    // reference pour la TAILLE de cible, pas pour son "hover uniquement" -
    // deja rejete pour manque de decouvrabilite) - remplacee par une croix
    // large et centree (32x32 dans une tuile 44x44), meme reglage d'opacite
    // 0.6/1 deja prouve suffisant, validee via maquette HTML avant
    // implementation.
    [Fact]
    public void Rail_style_lumora_expose_une_croix_de_fermeture_toujours_visible()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");
        var method = ExtractMethod(code, "private void RenderIdentitySpineTabs()");

        Assert.Contains("var closeButton = new Button", method, StringComparison.Ordinal);
        Assert.Contains("Child = new SymbolIcon(Symbol.Cancel)", method, StringComparison.Ordinal);
        Assert.Contains("closeButton.Click += VerticalTabCloseButton_Click;", method, StringComparison.Ordinal);

        // Toujours visible (pas de Visibility.Collapsed sur le bouton lui-meme) :
        // seule l'opacite bouge au survol, jamais la visibilite.
        Assert.DoesNotContain("closeButton.Visibility", method, StringComparison.Ordinal);
        Assert.Contains("Opacity = 0.6", method, StringComparison.Ordinal);
        Assert.Contains("closeButton.Opacity = 1; icon.Opacity = 0.3;", method, StringComparison.Ordinal);
        Assert.Contains("closeButton.Opacity = 0.6; icon.Opacity = 1;", method, StringComparison.Ordinal);

        // Cible agrandie (2026-08-08) : 26x26 au lieu des 20x20 d'origine,
        // redescendue depuis un premier essai a 32x32 ("trop imposant" en
        // usage reel le meme jour).
        Assert.Contains("Width = 26,", method, StringComparison.Ordinal);
        Assert.Contains("Height = 26,", method, StringComparison.Ordinal);
    }

    // Retour utilisateur (2026-08-08) : la petite fleche de defilement de la
    // bande d'onglets horizontale (RepeatButton natif de
    // primitives:TabViewListView, cf. generic.xaml du SDK) reste sur les
    // jetons Fluent generiques par defaut (TextFillColorSecondaryBrush...) -
    // notre <Style TargetType="TabView"> personnalise ne retemplate que le
    // corps de la barre, pas ce sous-controle. Verrouille les alias
    // (StaticResource, pas de couleur dupliquee) vers les jetons de
    // NovaChromeIconButtonStyle pour rester coherent ET pour que le
    // contraste eleve (qui modifie NovaChromeButtonForegroundBrush en place)
    // s'applique aussi aux chevrons.
    [Fact]
    public void Chevrons_de_defilement_des_onglets_suivent_le_theme_de_la_chrome()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains(
            "<StaticResource x:Key=\"TabViewScrollButtonForeground\" ResourceKey=\"NovaChromeButtonForegroundBrush\" />",
            xaml, StringComparison.Ordinal);
        Assert.Contains(
            "<StaticResource x:Key=\"TabViewScrollButtonForegroundPointerOver\" ResourceKey=\"NovaChromeButtonForegroundBrush\" />",
            xaml, StringComparison.Ordinal);
        Assert.Contains(
            "<StaticResource x:Key=\"TabViewScrollButtonBackgroundPointerOver\" ResourceKey=\"NovaChromeButtonHighlightBrush\" />",
            xaml, StringComparison.Ordinal);
    }

    // Retour utilisateur (2026-08-08) : "je veux que ce soit vraiment neutre,
    // juste le logo et la barre de recherche, comme en mode Classique" - le
    // Compagnon (IdentitySpineHomeHero) s'affichait en Style Lumora quel que
    // soit le Mode d'usage, y compris Neutre. Corrige pour n'exempter QUE le
    // mode Neutre (les autres modes - Focus, Lecture... - gardent leur
    // Compagnon).
    [Fact]
    public void Compagnon_style_lumora_se_masque_en_mode_neutre()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");
        var method = ExtractMethod(code, "private void UpdateIdentitySpineHomeHeroVisibility(BrowserTabState? tab)");

        Assert.Contains(
            "var isNeutralMode = string.Equals(_uiSettings.UsageMode, \"neutral\", StringComparison.OrdinalIgnoreCase);",
            method, StringComparison.Ordinal);
        Assert.Contains(
            "_chromeLayoutStyle == \"identitySpine\" && isHome && !isNeutralMode",
            method, StringComparison.Ordinal);
    }

    // La visibilite doit se rafraichir tout de suite si on change de mode en
    // restant sur l'onglet d'accueil, pas seulement au prochain changement
    // d'onglet (seuls call-sites d'origine : Navigation.cs, EnterIdentitySpineLayout).
    [Fact]
    public void Changement_de_mode_rafraichit_la_visibilite_du_compagnon_sans_changer_donglet()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");

        var companionIndex = code.IndexOf("UpdateModeCompanionUi();", StringComparison.Ordinal);
        Assert.True(companionIndex >= 0, "Appel a UpdateModeCompanionUi() introuvable.");
        var afterCompanion = code.Substring(companionIndex, Math.Min(600, code.Length - companionIndex));
        Assert.Contains("UpdateIdentitySpineHomeHeroVisibility(CurrentTab());", afterCompanion, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Signature introuvable : {signature}");

        var braceOpen = source.IndexOf('{', start);
        var depth = 0;
        var i = braceOpen;
        for (; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) break;
            }
        }

        return source[start..(i + 1)];
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
