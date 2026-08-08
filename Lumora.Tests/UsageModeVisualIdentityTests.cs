using Xunit;

namespace Lumora.Tests;

public sealed class UsageModeVisualIdentityTests
{
    [Fact]
    public void Accueil_lumora_garde_une_signature_visuelle_par_mode()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.NewTabHome.cs");

        Assert.Contains("mode-visual", source, StringComparison.Ordinal);
        Assert.Contains("NewTabThemeVariablesCss()", source, StringComparison.Ordinal);
        Assert.Contains("NewTabIsDarkTheme()", source, StringComparison.Ordinal);
        Assert.Contains("--nt-search-bg", source, StringComparison.Ordinal);
        Assert.Contains("--nt-shortcut-border", source, StringComparison.Ordinal);
        Assert.Contains("--nt-add-shortcut-shadow", source, StringComparison.Ordinal);
        Assert.Contains("NewTabModeSignatureBarCss", source, StringComparison.Ordinal);
        Assert.Contains("mode-neutral", source, StringComparison.Ordinal);
        Assert.Contains("neutral-clock", source, StringComparison.Ordinal);
        Assert.Contains("neutral-logo", source, StringComparison.Ordinal);
        Assert.Contains("balanced-home", source, StringComparison.Ordinal);
        Assert.Contains("balanced-dock", source, StringComparison.Ordinal);
        Assert.Contains("NewTabModePanelPatternCss", source, StringComparison.Ordinal);
        Assert.Contains("lumoraFocusBeat", source, StringComparison.Ordinal);
        Assert.Contains("lumoraCreativeSpark", source, StringComparison.Ordinal);
        Assert.Contains("lumoraResearchScan", source, StringComparison.Ordinal);
        Assert.Contains("motion-static .brand", source, StringComparison.Ordinal);
        Assert.Contains(".motion-static .mode-visual span", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Chrome_lumora_applique_la_palette_du_mode_actif()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var bookmarks = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");
        var settingsTheme = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsTheme.cs");
        var windowChrome = ReadRepoFile("Lumora.WinUI", "MainWindow.WindowChrome.cs");
        var appXaml = ReadRepoFile("Lumora.WinUI", "App.xaml");

        Assert.Contains("ModeChromeAccentStrip", xaml, StringComparison.Ordinal);
        Assert.Contains("ApplyUsageModeChrome", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("ResolveModeChromePalette", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("ModeChromePalette", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("SetIdentityGradient", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("UsageModeButton.Background", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("NovaOverlayBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaTextOnAccentBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaChromeButtonBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaChromeButtonAccentForegroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaModuleButtonBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaBookmarkIconButtonStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaBookmarkFontIconStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaChromeSymbolIconStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaChromeFontIconStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaModuleFontIconStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaRaisedIconButtonTemplate", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaRaisedBookmarkBarButtonTemplate", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaBookmarkBarButtonStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaChromeButtonShadowBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaChromeButtonHighlightBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaBookmarkBarButtonBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaChromeHaloWarmBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaChromeHaloCoolBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaChromeMistBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaBrandChipBackgroundBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaBrandChipBorderBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("TabView.TabStripHeader", xaml, StringComparison.Ordinal);
        Assert.Contains("lumière locale", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Lumora\"", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaBookmarkButtonForegroundBrush", bookmarks, StringComparison.Ordinal);
        Assert.Contains("NovaBookmarkButtonActiveForegroundBrush", bookmarks, StringComparison.Ordinal);
        Assert.Contains("NovaBookmarkButtonActiveBackgroundBrush", bookmarks, StringComparison.Ordinal);
        Assert.Contains("Style = (Style)RootShell.Resources[\"NovaBookmarkBarButtonStyle\"]", bookmarks, StringComparison.Ordinal);
        // Depuis l'ajout de la Taille de l'interface (UiDensity), les valeurs
        // fixes 36/13 sont devenues des metriques resolues depuis le palier
        // de densite courant (36/13 restent les valeurs du palier
        // "comfortable", cf. UiDensityVisualIdentityTests).
        Assert.Contains("Height = metrics.BookmarkChipHeight", bookmarks, StringComparison.Ordinal);
        Assert.Contains("FontSize = metrics.BookmarkChipFontSize", bookmarks, StringComparison.Ordinal);
        Assert.DoesNotContain("_bookmarkStarDefaultForeground", bookmarks, StringComparison.Ordinal);
        Assert.Contains("SyncSharedAppThemeResources()", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("SetChromeGradient(", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("NovaChromeGradientBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("SetBrush(\"NovaChromeButtonBackgroundBrush\"", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("SetBrush(\"NovaModuleButtonBackgroundBrush\"", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("SetBrush(\"NovaBookmarkButtonBackgroundBrush\"", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("SetBrush(\"NovaBookmarkButtonActiveBackgroundBrush\"", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("SetBrush(\"NovaBrandChipBackgroundBrush\"", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("SetBrush(\"NovaChromeHaloWarmBrush\"", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("SetBrush(\"NovaChromeButtonShadowBrush\"", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("SetBrush(\"NovaBookmarkBarButtonBackgroundBrush\"", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("Style TargetType=\"ComboBox\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("Style TargetType=\"ToggleSwitch\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("Style TargetType=\"InfoBar\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("ApplyWindowTitleBarColors();", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("BrushColor(\"NovaChromeSurfaceBrush\"", windowChrome, StringComparison.Ordinal);
    }

    [Fact]
    public void Chrome_lumora_devient_composable_pour_les_onglets_et_les_favoris()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var settings = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");
        var bookmarks = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");
        var uiSettings = ReadRepoFile("Lumora.WinUI", "Models", "UiSettings.cs");

        Assert.Contains("TabStripPositionCombo", xaml, StringComparison.Ordinal);
        Assert.Contains("BookmarksBarPositionCombo", xaml, StringComparison.Ordinal);
        Assert.Contains("WorkspacePresetButton_Click", xaml, StringComparison.Ordinal);
        Assert.Contains("WorkspaceLeftBookmarksColumn", xaml, StringComparison.Ordinal);
        Assert.Contains("WorkspaceRightTabsColumn", xaml, StringComparison.Ordinal);
        Assert.Contains("BookmarksSideRail", xaml, StringComparison.Ordinal);
        Assert.Contains("BookmarksBottomRow", xaml, StringComparison.Ordinal);
        Assert.Contains("FullScreenRightRevealZone", xaml, StringComparison.Ordinal);
        Assert.Contains("TabStripPosition { get; set; } = \"top\"", uiSettings, StringComparison.Ordinal);
        Assert.Contains("BookmarksBarPosition { get; set; } = \"top\"", uiSettings, StringComparison.Ordinal);
        Assert.Contains("NormalizeTabStripPosition", settings, StringComparison.Ordinal);
        Assert.Contains("NormalizeBookmarksBarPosition", settings, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(BrowserTabs, tabsAtBottom ? 5 : 1);", settings, StringComparison.Ordinal);
        Assert.Contains("WorkspaceBottomBookmarksRow.Height", settings, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(BookmarksSideRail, bookmarksOnRight ? 6 : 0);", settings, StringComparison.Ordinal);
        Assert.Contains("BookmarksBottomBarPanel.Children.Clear()", bookmarks, StringComparison.Ordinal);
        Assert.Contains("BookmarksSideBarPanel.Children.Clear()", bookmarks, StringComparison.Ordinal);
        Assert.Contains("UsesSideBookmarksRail(_bookmarksBarPosition)", bookmarks, StringComparison.Ordinal);
    }

    [Fact]
    public void Fenetres_secondaires_reutilisent_une_palette_lumora_partagee()
    {
        var theme = ReadRepoFile("Lumora.WinUI", "LumoraTheme.cs");
        var incognitoXaml = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml");
        var incognitoCodeBehind = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");
        var appWindowXaml = ReadRepoFile("Lumora.WinUI", "LumoraAppWindow.xaml");
        var appWindowCodeBehind = ReadRepoFile("Lumora.WinUI", "LumoraAppWindow.xaml.cs");

        Assert.Contains("ApplySecondaryWindowTheme", theme, StringComparison.Ordinal);
        Assert.Contains("ApplySharedAppBrushes", theme, StringComparison.Ordinal);
        Assert.Contains("ResolveTextOnColor", theme, StringComparison.Ordinal);
        Assert.Contains("TintSurface", theme, StringComparison.Ordinal);
        Assert.Contains("IncognitoToolbarButtonStyle", incognitoXaml, StringComparison.Ordinal);
        Assert.Contains("LumoraWindowAccentBrush", incognitoXaml, StringComparison.Ordinal);
        Assert.Contains("LumoraTheme.ApplySecondaryWindowTheme", incognitoCodeBehind, StringComparison.Ordinal);
        Assert.Contains("LumoraWebAppActionButtonStyle", appWindowXaml, StringComparison.Ordinal);
        Assert.Contains("LumoraWindowIdentityBrush", appWindowXaml, StringComparison.Ordinal);
        Assert.Contains("LumoraTheme.ApplySecondaryWindowTheme", appWindowCodeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void Accueil_lumora_affiche_un_outil_et_une_presentation_par_mode()
    {
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.NewTabHome.cs");
        var webMessaging = ReadRepoFile("Lumora.WinUI", "MainWindow.WebMessaging.cs");
        var uiSettings = ReadRepoFile("Lumora.WinUI", "Models", "UiSettings.cs");
        var usageMode = ReadRepoFile("Lumora.WinUI", "MainWindow.UsageMode.cs");
        var settings = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");

        Assert.Contains("NewTabModeIntroHtml", navigation, StringComparison.Ordinal);
        Assert.Contains("LastIntroducedUsageMode", uiSettings, StringComparison.Ordinal);
        Assert.Contains("newtab_mode_intro_dismiss", webMessaging, StringComparison.Ordinal);
        Assert.Contains("newtab_mode_quick_note", webMessaging, StringComparison.Ordinal);
        Assert.Contains("SetCompanionMemory(mode, content)", webMessaging, StringComparison.Ordinal);
        Assert.Contains("CompanionFocusObjective", uiSettings, StringComparison.Ordinal);
        Assert.Contains("CompanionCreativePostIt", uiSettings, StringComparison.Ordinal);
        Assert.Contains("Post-it local", navigation, StringComparison.Ordinal);
        Assert.Contains("Capture d'idée", navigation, StringComparison.Ordinal);
        Assert.Contains("Objectif de maintenant", navigation, StringComparison.Ordinal);
        Assert.Contains("Source ou piste à vérifier", navigation, StringComparison.Ordinal);
        Assert.Contains("Rappel pour plus tard", navigation, StringComparison.Ordinal);
        Assert.Contains("_uiSettings.LastIntroducedUsageMode = string.Empty", usageMode, StringComparison.Ordinal);
        Assert.Contains("_uiSettings.LastIntroducedUsageMode = string.Empty", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void Modes_lumora_ont_un_compagnon_permanent_et_un_accueil_aere()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.NewTabHome.cs");
        var usageMode = ReadRepoFile("Lumora.WinUI", "MainWindow.UsageMode.cs");
        var settings = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");
        var settingsTheme = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsTheme.cs");

        Assert.Contains("ModeCompanionButton", xaml, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionFlyout", xaml, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionMemoryBox", xaml, StringComparison.Ordinal);
        Assert.Contains("Lumie", xaml, StringComparison.Ordinal);
        Assert.Contains("UsageModeLabelText", xaml, StringComparison.Ordinal);
        Assert.Contains("UsageModeCurrentText", xaml, StringComparison.Ordinal);
        Assert.Contains("UsageModeAccentBar", xaml, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionAccentDot", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaCompanionPillButtonStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaCompanionGlassBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaModuleHubButtonStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("NovaModuleHubAccentBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("ChromeTint", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionPrimaryButton_Click", usageMode, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionSaveButton_Click", usageMode, StringComparison.Ordinal);
        Assert.Contains("RunModeCompanionActionAsync", usageMode, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionDefinition", usageMode, StringComparison.Ordinal);
        Assert.Contains("CompanionMemory(string mode)", usageMode, StringComparison.Ordinal);
        Assert.Contains("UpdateModeCompanionUi();", settings, StringComparison.Ordinal);
        Assert.Contains(".home-grid", navigation, StringComparison.Ordinal);
        Assert.Contains(".home-primary", navigation, StringComparison.Ordinal);
        Assert.Contains(".mode-side", navigation, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns:minmax(420px,1.04fr) minmax(380px,.82fr)", navigation, StringComparison.Ordinal);
    }

    [Fact]
    public void Mode_equilibre_reste_distinct_du_mode_neutre()
    {
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.NewTabHome.cs");
        var webMessaging = ReadRepoFile("Lumora.WinUI", "MainWindow.WebMessaging.cs");

        Assert.Contains("\"balanced\" => \"balanced\"", navigation, StringComparison.Ordinal);
        Assert.Contains("\"balanced\" => \"balanced\"", webMessaging, StringComparison.Ordinal);
        Assert.Contains("if (NewTabUsageMode() == \"balanced\")", navigation, StringComparison.Ordinal);
        Assert.Contains("Navigation quotidienne", navigation, StringComparison.Ordinal);
        Assert.Contains("balanced-action", navigation, StringComparison.Ordinal);
        Assert.Contains("modeAction('bookmarks')", navigation, StringComparison.Ordinal);
        Assert.Contains("modeAction('history')", navigation, StringComparison.Ordinal);
        Assert.Contains("modeAction('modules')", navigation, StringComparison.Ordinal);
    }

    [Fact]
    public void Mode_neutre_reste_la_base_avec_raccourcis_et_acces_rapide()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.NewTabHome.cs");
        var usageMode = ReadRepoFile("Lumora.WinUI", "MainWindow.UsageMode.cs");
        var webMessaging = ReadRepoFile("Lumora.WinUI", "MainWindow.WebMessaging.cs");
        var uiSettings = ReadRepoFile("Lumora.WinUI", "Models", "UiSettings.cs");
        var settingsTheme = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsTheme.cs");
        var wizard = ReadRepoFile("Lumora.WinUI", "MainWindow.SetupWizard.cs");

        Assert.Contains("UsageModeNeutralButton", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Neutre\" Tag=\"neutral\"", xaml, StringComparison.Ordinal);
        Assert.Contains("WizardUsageNeutral", xaml, StringComparison.Ordinal);
        Assert.Contains("NewTabHomeContentHtml", navigation, StringComparison.Ordinal);
        Assert.Contains("NewTabSearchFormHtml(\"neutral-search\")", navigation, StringComparison.Ordinal);
        Assert.Contains("{{NewTabShortcutsHtml()}}", navigation, StringComparison.Ordinal);
        Assert.Contains("neutral-logo", navigation, StringComparison.Ordinal);
        Assert.Contains("neutral-name", navigation, StringComparison.Ordinal);
        Assert.Contains("if (mode == \"neutral\" || mode == \"balanced\")", navigation, StringComparison.Ordinal);
        Assert.Contains("NewTabUsageMode() != \"neutral\"", navigation, StringComparison.Ordinal);
        Assert.Contains("PinnedModuleIds.Clear()", usageMode, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionButton.Visibility = Visibility.Visible", usageMode, StringComparison.Ordinal);
        Assert.Contains("Ajouter un raccourci", usageMode, StringComparison.Ordinal);
        Assert.Contains("\"neutral\" => \"neutral\"", webMessaging, StringComparison.Ordinal);
        Assert.Contains("UsageMode { get; set; } = \"neutral\"", uiSettings, StringComparison.Ordinal);
        Assert.Contains("\"neutral\" => 1", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("WizardUsageNeutral.IsChecked = mode == \"neutral\"", wizard, StringComparison.Ordinal);
    }

    [Fact]
    public void Accueil_equilibre_evite_la_pile_de_cartes_lourde()
    {
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.NewTabHome.cs");
        var balancedStart = navigation.IndexOf("if (NewTabUsageMode() == \"balanced\")", StringComparison.Ordinal);
        var fallbackStart = navigation.IndexOf("return $$\"\"\"", balancedStart, StringComparison.Ordinal);

        Assert.True(balancedStart >= 0, "Bloc balanced introuvable.");
        Assert.True(fallbackStart > balancedStart, "Fin de bloc balanced introuvable.");

        var balancedSection = navigation.Substring(balancedStart, fallbackStart - balancedStart);

        Assert.Contains("balanced-mode-panel", navigation, StringComparison.Ordinal);
        Assert.Contains("grid-template-areas:\"copy visual\" \"actions actions\"", navigation, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns:repeat(2,minmax(0,1fr))", navigation, StringComparison.Ordinal);
        Assert.DoesNotContain("{{NewTabModeIntroHtml()}}", balancedSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Chrome_bas_regroupe_mode_et_lumie()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var statusBarStart = xaml.IndexOf("<Grid x:Name=\"StatusBarRow\"", StringComparison.Ordinal);
        var navigationToolbarStart = xaml.IndexOf("<Grid x:Name=\"NavigationToolbar\"", StringComparison.Ordinal);

        Assert.True(statusBarStart >= 0, "StatusBarRow introuvable.");
        Assert.True(navigationToolbarStart >= 0, "NavigationToolbar introuvable.");

        var statusBarSection = xaml.Substring(statusBarStart);
        var navigationSection = xaml.Substring(
            navigationToolbarStart,
            statusBarStart > navigationToolbarStart
                ? statusBarStart - navigationToolbarStart
                : xaml.Length - navigationToolbarStart);

        Assert.Contains("ModeCompanionButton", statusBarSection, StringComparison.Ordinal);
        Assert.Contains("UsageModeButton", statusBarSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ModeCompanionButton", navigationSection, StringComparison.Ordinal);
        Assert.DoesNotContain("UsageModeButton", navigationSection, StringComparison.Ordinal);

        // Le profil a ete deplace en en-tete du menu Demarrer (0.93.5.0-dev,
        // "comme un vrai menu demarrer Windows") : il ne doit plus etre dans
        // la barre de statut basse.
        Assert.DoesNotContain("ProfileStatusButton", statusBarSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_demarrer_porte_le_profil_en_entete()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var menuStart = xaml.IndexOf("<StackPanel x:Name=\"ModulesFlyoutRoot\"", StringComparison.Ordinal);
        var searchBoxStart = xaml.IndexOf("x:Name=\"StartMenuSearchBox\"", StringComparison.Ordinal);

        Assert.True(menuStart >= 0, "ModulesFlyoutRoot introuvable.");
        Assert.True(searchBoxStart >= 0, "StartMenuSearchBox introuvable.");
        Assert.True(searchBoxStart > menuStart, "Le profil doit precéder la recherche dans le menu Démarrer.");

        var headerSection = xaml.Substring(menuStart, searchBoxStart - menuStart);
        Assert.Contains("ProfileStatusButton", headerSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_profil_bas_propose_gestion_utilisateur_et_reglages()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var mainWindow = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var profile = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("ProfileStatusFlyout", xaml, StringComparison.Ordinal);
        Assert.Contains("Paramètres utilisateur", xaml, StringComparison.Ordinal);
        Assert.Contains("Changer d'utilisateur", xaml, StringComparison.Ordinal);
        Assert.Contains("Créer un utilisateur", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.ToolTip=\"Ouvrir le menu profil\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private void OpenProfileSettings()", mainWindow, StringComparison.Ordinal);
        Assert.Contains("SettingsNavProfile.IsChecked = true", mainWindow, StringComparison.Ordinal);
        Assert.Contains("UpdateProfileFlyoutUi()", profile, StringComparison.Ordinal);
        Assert.Contains("ShowProfilePickerOverlay()", profile, StringComparison.Ordinal);
        Assert.Contains("ShowCreateProfileOverlay()", profile, StringComparison.Ordinal);
    }

    [Fact]
    public void Version_projet_est_alignee_sur_0_93_17_2()
    {
        var mainWindow = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var agents = ReadRepoFile("AGENTS.md");
        var cleanArtifactScript = ReadRepoFile("scripts", "build-clean-test-artifact.ps1");
        var installerScript = ReadRepoFile("scripts", "build-installer.ps1");

        Assert.Contains("0.93.17.2-dev", mainWindow, StringComparison.Ordinal);
        Assert.Contains("0.93.17.2-dev", agents, StringComparison.Ordinal);
        Assert.Contains("0.93.17.2-dev", cleanArtifactScript, StringComparison.Ordinal);
        Assert.Contains("0.93.17.2-dev", installerScript, StringComparison.Ordinal);
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
