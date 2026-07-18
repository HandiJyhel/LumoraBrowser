using Xunit;

namespace Lumora.Tests;

public sealed class UsageModeVisualIdentityTests
{
    [Fact]
    public void Accueil_lumora_garde_une_signature_visuelle_par_mode()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.Navigation.cs");

        Assert.Contains("mode-visual", source, StringComparison.Ordinal);
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
        var settings = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");
        var mainWindow = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("ModeChromeAccentStrip", xaml, StringComparison.Ordinal);
        Assert.Contains("ApplyUsageModeChrome", settings, StringComparison.Ordinal);
        Assert.Contains("ResolveModeChromePalette", settings, StringComparison.Ordinal);
        Assert.Contains("ModeChromePalette", settings, StringComparison.Ordinal);
        Assert.Contains("SetIdentityGradient", settings, StringComparison.Ordinal);
        Assert.Contains("UsageModeButton.Background", settings, StringComparison.Ordinal);
        Assert.Contains("ApplyWindowTitleBarColors();", settings, StringComparison.Ordinal);
        Assert.Contains("BrushColor(\"NovaChromeSurfaceBrush\"", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void Accueil_lumora_affiche_un_outil_et_une_presentation_par_mode()
    {
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.Navigation.cs");
        var webMessaging = ReadRepoFile("Lumora.WinUI", "MainWindow.WebMessaging.cs");
        var uiSettings = ReadRepoFile("Lumora.WinUI", "Models", "UiSettings.cs");
        var mainWindow = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
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
        Assert.Contains("_uiSettings.LastIntroducedUsageMode = string.Empty", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_uiSettings.LastIntroducedUsageMode = string.Empty", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void Modes_lumora_ont_un_compagnon_permanent_et_un_accueil_aere()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.Navigation.cs");
        var mainWindow = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var settings = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");

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
        Assert.Contains("NavigationMenuDividerColumn", xaml, StringComparison.Ordinal);
        Assert.Contains("NavigationMenuButtonColumn", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"21\"", xaml, StringComparison.Ordinal);
        Assert.Contains("NavigationMenuButton", xaml, StringComparison.Ordinal);
        Assert.Contains("ChromeTint", settings, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionPrimaryButton_Click", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionSaveButton_Click", mainWindow, StringComparison.Ordinal);
        Assert.Contains("RunModeCompanionActionAsync", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionDefinition", mainWindow, StringComparison.Ordinal);
        Assert.Contains("CompanionMemory(string mode)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("UpdateModeCompanionUi();", settings, StringComparison.Ordinal);
        Assert.Contains(".home-grid", navigation, StringComparison.Ordinal);
        Assert.Contains(".home-primary", navigation, StringComparison.Ordinal);
        Assert.Contains(".mode-side", navigation, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns:minmax(420px,1.04fr) minmax(380px,.82fr)", navigation, StringComparison.Ordinal);
    }

    [Fact]
    public void Mode_equilibre_reste_distinct_du_mode_neutre()
    {
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.Navigation.cs");
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
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.Navigation.cs");
        var mainWindow = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var webMessaging = ReadRepoFile("Lumora.WinUI", "MainWindow.WebMessaging.cs");
        var uiSettings = ReadRepoFile("Lumora.WinUI", "Models", "UiSettings.cs");
        var settings = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");
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
        Assert.Contains("PinnedModuleIds.Clear()", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ModeCompanionButton.Visibility = Visibility.Visible", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Ajouter un raccourci", mainWindow, StringComparison.Ordinal);
        Assert.Contains("\"neutral\" => \"neutral\"", webMessaging, StringComparison.Ordinal);
        Assert.Contains("UsageMode { get; set; } = \"neutral\"", uiSettings, StringComparison.Ordinal);
        Assert.Contains("\"neutral\" => 1", settings, StringComparison.Ordinal);
        Assert.Contains("WizardUsageNeutral.IsChecked = mode == \"neutral\"", wizard, StringComparison.Ordinal);
    }

    [Fact]
    public void Accueil_equilibre_evite_la_pile_de_cartes_lourde()
    {
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.Navigation.cs");
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
    public void Chrome_bas_regroupe_mode_lumie_et_profil()
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
        Assert.Contains("ProfileStatusButton", statusBarSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ModeCompanionButton", navigationSection, StringComparison.Ordinal);
        Assert.DoesNotContain("UsageModeButton", navigationSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_profil_bas_propose_gestion_utilisateur_et_reglages()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var mainWindow = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var profile = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("ProfileStatusFlyout", xaml, StringComparison.Ordinal);
        Assert.Contains("Parametres utilisateur", xaml, StringComparison.Ordinal);
        Assert.Contains("Changer d'utilisateur", xaml, StringComparison.Ordinal);
        Assert.Contains("Creer un utilisateur", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.ToolTip=\"Ouvrir le menu profil\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private void OpenProfileSettings()", mainWindow, StringComparison.Ordinal);
        Assert.Contains("SettingsNavProfile.IsChecked = true", mainWindow, StringComparison.Ordinal);
        Assert.Contains("UpdateProfileFlyoutUi()", profile, StringComparison.Ordinal);
        Assert.Contains("ShowProfilePickerOverlay()", profile, StringComparison.Ordinal);
        Assert.Contains("ShowCreateProfileOverlay()", profile, StringComparison.Ordinal);
    }

    [Fact]
    public void Version_projet_est_alignee_sur_0_83_22()
    {
        var mainWindow = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var agents = ReadRepoFile("AGENTS.md");
        var cleanArtifactScript = ReadRepoFile("scripts", "build-clean-test-artifact.ps1");
        var installerScript = ReadRepoFile("scripts", "build-installer.ps1");

        Assert.Contains("0.83.19-dev", mainWindow, StringComparison.Ordinal);
        Assert.Contains("0.83.19-dev", agents, StringComparison.Ordinal);
        Assert.Contains("0.83.19-dev", cleanArtifactScript, StringComparison.Ordinal);
        Assert.Contains("0.83.19-dev", installerScript, StringComparison.Ordinal);
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
