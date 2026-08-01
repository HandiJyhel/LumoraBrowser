using Xunit;

namespace Lumora.Tests;

public sealed class AccessibilityRegressionTests
{
    [Fact]
    public void Recherche_accueil_n_utilise_plus_un_champ_temporairement_verrouille()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.NewTabHome.cs");

        Assert.Contains("<input id=\"q\" value=\"\" {{(_uiSettings.NewTabFocusSearchOnOpen ? \"autofocus\" : \"\")}}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("readonly {{(_uiSettings.NewTabFocusSearchOnOpen ? \"autofocus\" : \"\")}}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("setTimeout(unlockSearch,500);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("function unlockSearch()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Actions_dynamiques_exposent_des_noms_accessibles_explicites()
    {
        var usageMode = ReadRepoFile("Lumora.WinUI", "MainWindow.UsageMode.cs");
        var webApps = ReadRepoFile("Lumora.WinUI", "MainWindow.WebApps.cs");
        var history = ReadRepoFile("Lumora.WinUI", "MainWindow.History.cs");
        var savedGroups = ReadRepoFile("Lumora.WinUI", "MainWindow.SavedTabGroups.cs");
        var siteControl = ReadRepoFile("Lumora.WinUI", "MainWindow.SiteControl.cs");
        var sessions = ReadRepoFile("Lumora.WinUI", "MainWindow.Sessions.cs");
        var passkeys = ReadRepoFile("Lumora.WinUI", "MainWindow.Passkeys.cs");
        var wallet = ReadRepoFile("Lumora.WinUI", "MainWindow.Wallet.cs");
        var vaultQuickAccess = ReadRepoFile("Lumora.WinUI", "MainWindow.VaultQuickAccess.cs");
        var identitySpine = ReadRepoFile("Lumora.WinUI", "MainWindow.IdentitySpine.cs");

        Assert.Contains("Retirer {moduleName} de la barre de modules", usageMode, StringComparison.Ordinal);
        Assert.Contains("Épingler {moduleName} dans la barre de modules", usageMode, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(renameBtn", webApps, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(deleteBtn", webApps, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(openBtn", webApps, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(openBtn, $\"Ouvrir le téléchargement", history, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(removeBtn, $\"Retirer le téléchargement", history, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(openButton, $\"Ouvrir le groupe enregistré", savedGroups, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(deleteButton, $\"Supprimer le groupe enregistré", savedGroups, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(combo, $\"{descriptor.Label} pour {rootDomain}\")", siteControl, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(trustToggle, $\"Politique de session pour {rootDomain}\")", sessions, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(forgetBtn, $\"Oublier les cookies du site {rootDomain}\")", sessions, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(deleteBtn, $\"Supprimer la clé d'accès pour {entry.Origin}\")", passkeys, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(editBtn, $\"Modifier la carte {title}\")", wallet, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(fillBtn, $\"Utiliser la carte {title} sur la page active\")", wallet, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(unlockBtn, \"Deverrouiller le coffre\")", vaultQuickAccess, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(copyTotpBtn, $\"Copier le code TOTP pour", vaultQuickAccess, StringComparison.Ordinal);
        Assert.Contains("ApplyNovaControlAccessibility(button, $\"Onglet {tab.Title}\")", identitySpine, StringComparison.Ordinal);
    }

    [Fact]
    public void Accessibilite_grande_taille_redeclenche_les_panneaux_dynamiques()
    {
        var settingsTheme = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsTheme.cs");
        var windowChrome = ReadRepoFile("Lumora.WinUI", "MainWindow.WindowChrome.cs");
        var sessions = ReadRepoFile("Lumora.WinUI", "MainWindow.Sessions.cs");
        var passkeys = ReadRepoFile("Lumora.WinUI", "MainWindow.Passkeys.cs");
        var wallet = ReadRepoFile("Lumora.WinUI", "MainWindow.Wallet.cs");
        var siteControl = ReadRepoFile("Lumora.WinUI", "MainWindow.SiteControl.cs");
        var xamlCs = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("private void RefreshAccessibilitySensitivePanels()", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("RenderDownloads();", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("RenderSavedTabGroups();", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("RefreshWebAppsPanel();", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("RenderPasskeysPanel();", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("RefreshWalletPanel();", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("RefreshVaultPanel();", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("_ = RefreshSessionsPanelAsync();", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("_ = RefreshSiteControlAsync(site);", settingsTheme, StringComparison.Ordinal);
        Assert.Contains("private double AccessibilityBodyFontSize()", windowChrome, StringComparison.Ordinal);
        Assert.Contains("private double AccessibilitySecondaryFontSize()", windowChrome, StringComparison.Ordinal);
        Assert.Contains("FontSize = AccessibilityBodyFontSize()", sessions, StringComparison.Ordinal);
        Assert.Contains("FontSize = AccessibilitySecondaryFontSize()", passkeys, StringComparison.Ordinal);
        Assert.Contains("FontSize = AccessibilitySecondaryFontSize()", wallet, StringComparison.Ordinal);
        Assert.Contains("FontSize = AccessibilityBodyFontSize()", siteControl, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetHelpText(", siteControl, StringComparison.Ordinal);
        Assert.Contains("UpdateStatusText(DescribePanelStatus(visiblePanel, status));", xamlCs, StringComparison.Ordinal);
        Assert.Contains("private void FocusVisiblePanelEntryPoint(FrameworkElement visiblePanel)", xamlCs, StringComparison.Ordinal);
        Assert.Contains("private FrameworkElement? FindFirstFocusableDescendant(DependencyObject root)", xamlCs, StringComparison.Ordinal);
        Assert.Contains("DispatcherQueue.TryEnqueue(() =>", xamlCs, StringComparison.Ordinal);
        Assert.Contains("private void UpdateStatusText(", xamlCs, StringComparison.Ordinal);
        Assert.Contains("RaiseNotificationEvent(", xamlCs, StringComparison.Ordinal);
        Assert.Contains("AutomationNotificationProcessing.MostRecent", xamlCs, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"État Lumora\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"Annonce les changements importants de navigation, de sécurité et d'accessibilité.\"", xaml, StringComparison.Ordinal);
        Assert.Contains("UpdateStatusText(\"Sessions de la visite précédente purgées.\")", sessions, StringComparison.Ordinal);
        Assert.Contains("UpdateStatusText(\"Clés d'accès indisponibles en mode invité.\")", passkeys, StringComparison.Ordinal);
        Assert.Contains("UpdateStatusText(\"Portefeuille indisponible en mode invité.\")", wallet, StringComparison.Ordinal);
    }

    [Fact]
    public void Navigation_clavier_par_zones_rend_le_shell_plus_traversable()
    {
        var accessibilityNavigation = ReadRepoFile("Lumora.WinUI", "MainWindow.AccessibilityNavigation.cs");
        var accessibilityQuickActions = ReadRepoFile("Lumora.WinUI", "MainWindow.AccessibilityQuickActions.cs");
        var accessibilityContext = ReadRepoFile("Lumora.WinUI", "MainWindow.AccessibilityContext.cs");
        var accessibilityRescue = ReadRepoFile("Lumora.WinUI", "MainWindow.AccessibilityRescue.cs");
        var xamlCs = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var settings = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");
        var comfortProfiles = ReadRepoFile("Lumora.WinUI", "MainWindow.ComfortProfiles.cs");

        Assert.Contains("RegisterAccessibilityZoneAccelerators();", xamlCs, StringComparison.Ordinal);
        Assert.Contains("RegisterAccessibilityQuickActionAccelerators();", xamlCs, StringComparison.Ordinal);
        Assert.Contains("RegisterAccessibilityContextAccelerators();", xamlCs, StringComparison.Ordinal);
        Assert.Contains("RegisterAccessibilityRescueAccelerators();", xamlCs, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.F6", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("VirtualKeyModifiers.Shift", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.Number1", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.Number5", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("AccessibilityShellZone.Content", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("private bool FocusAccessibilityShellZone(AccessibilityShellZone zone, bool announce)", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("AnnounceAccessibilityContext($\"Zone clavier : {selected.Label}. {selected.Hint}\")", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("\"Barre d'adresse\"", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("\"Compagnon et statut\"", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("CurrentAccessibilityContentPanel()", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("LoginOverlay.Visibility != Visibility.Visible", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("CommandPaletteOverlay.Visibility != Visibility.Visible", accessibilityNavigation, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityShortcutHelpButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Faire annoncer les raccourcis", xaml, StringComparison.Ordinal);
        Assert.Contains("Faire annoncer les raccourcis Lumora", xaml, StringComparison.Ordinal);
        Assert.Contains("AccessibilityShortcutHelpButton_Click", settings, StringComparison.Ordinal);
        Assert.Contains("UpdateStatusText(\"Aide clavier Lumora annoncée.\", announce: false);", settings, StringComparison.Ordinal);
        Assert.Contains("Raccourcis Lumora : F6 ou Maj plus F6 pour changer de zone.", settings, StringComparison.Ordinal);
        Assert.Contains("Contrôle Alt F relit votre repère courant.", settings, StringComparison.Ordinal);
        Assert.Contains("Contrôle Alt R recentre le focus sur la zone utile.", settings, StringComparison.Ordinal);
        Assert.Contains("Contrôle Alt S active le mode secours.", settings, StringComparison.Ordinal);
        Assert.Contains("Contrôle Alt X restaure l'état de confort précédent.", settings, StringComparison.Ordinal);
        // Menus "Mode" et "Confort" fusionnes en un seul point d'entree
        // (UsageModeButton/UsageModeFlyout) a la demande explicite de
        // l'utilisateur - AccessibilityQuickButton/AccessibilityQuickFlyout
        // n'existent plus en tant qu'elements XAML autonomes.
        Assert.DoesNotContain("x:Name=\"AccessibilityQuickButton\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"AccessibilityQuickFlyout\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"UsageModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Opening=\"AccessibilityQuickFlyout_Opening\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Alt+6 et Ctrl+Alt+8", xaml, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Alt+F", xaml, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Alt+R", xaml, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Alt+S", xaml, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Alt+X", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityQuickContextText\"", xaml, StringComparison.Ordinal);
        // Les boutons "Ou suis-je"/"Recentrer le focus" ont ete retires du
        // flyout visuel le 2026-07-20 (public general, pas les utilisateurs de
        // lecteur d'ecran vises) : seuls les raccourcis clavier Ctrl+Alt+F/R
        // restent, deja verrouilles plus haut et geres directement par les
        // accelerateurs (AnnounceAccessibilityNavigationContext /
        // RestoreAccessibilityFocusAnchor), sans passer par un bouton du flyout.
        Assert.DoesNotContain("x:Name=\"AccessibilityQuickWhereAmIButton\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"AccessibilityQuickRefocusButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityQuickRescueButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityQuickRestoreButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityQuickOpenSettingsButton\"", xaml, StringComparison.Ordinal);
        // Le mode secours n'a plus qu'un seul bouton d'activation dans le flyout
        // (l'encadre "Mode secours" dedie) : plus de doublon "preset" generique
        // qui faisait exactement la meme chose sous un autre bouton.
        Assert.DoesNotContain("AccessibilityQuickRescuePresetButton", xaml, StringComparison.Ordinal);
        Assert.Contains("AccessibilityQuickOpenSettingsButton_Click", accessibilityQuickActions, StringComparison.Ordinal);
        Assert.Contains("AccessibilityQuickPresetButton_Click", accessibilityQuickActions, StringComparison.Ordinal);
        Assert.Contains("AccessibilityQuickToggleButton_Click", accessibilityQuickActions, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.Number6", accessibilityQuickActions, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.Number8", accessibilityQuickActions, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.Number0", accessibilityQuickActions, StringComparison.Ordinal);
        Assert.DoesNotContain("VirtualKey.Number7", accessibilityQuickActions, StringComparison.Ordinal);
        Assert.DoesNotContain("VirtualKey.Number9", accessibilityQuickActions, StringComparison.Ordinal);
        Assert.Contains("DescribeAccessibilityComfortState()", accessibilityQuickActions, StringComparison.Ordinal);
        Assert.Contains("AnnounceAccessibilityComfortState()", accessibilityQuickActions, StringComparison.Ordinal);
        Assert.Contains("UpdateAccessibilityQuickButtonUi();", comfortProfiles, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.F", accessibilityContext, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.R", accessibilityContext, StringComparison.Ordinal);
        Assert.Contains("DescribeAccessibilityNavigationContext(bool includeShortcuts = true)", accessibilityContext, StringComparison.Ordinal);
        Assert.Contains("AnnounceAccessibilityNavigationContext()", accessibilityContext, StringComparison.Ordinal);
        Assert.Contains("RestoreAccessibilityFocusAnchor()", accessibilityContext, StringComparison.Ordinal);
        Assert.Contains("ResolveAccessibilityShellZoneFromFocus()", accessibilityContext, StringComparison.Ordinal);
        Assert.Contains("FocusManager.GetFocusedElement(Content.XamlRoot)", accessibilityContext, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.S", accessibilityRescue, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.X", accessibilityRescue, StringComparison.Ordinal);
        Assert.Contains("ActivateAccessibilityRescueMode()", accessibilityRescue, StringComparison.Ordinal);
        Assert.Contains("RestoreAccessibilityRescueSnapshot()", accessibilityRescue, StringComparison.Ordinal);
        Assert.Contains("CaptureAccessibilityRescueSnapshotIfNeeded()", accessibilityRescue, StringComparison.Ordinal);
        Assert.Contains("ApplyAccessibilityComfortSnapshot(", accessibilityRescue, StringComparison.Ordinal);
    }

    [Fact]
    public void Confort_propose_des_profils_prets_a_l_emploi()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var settings = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");
        var comfortProfiles = ReadRepoFile("Lumora.WinUI", "MainWindow.ComfortProfiles.cs");
        var accessibilityRescue = ReadRepoFile("Lumora.WinUI", "MainWindow.AccessibilityRescue.cs");
        var readingGuide = ReadRepoFile("Lumora.WinUI", "MainWindow.ReadingGuide.cs");
        var accessibilityVision = ReadRepoFile("Lumora.WinUI", "MainWindow.AccessibilityVision.cs");
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.Navigation.cs");
        var uiSettings = ReadRepoFile("Lumora.WinUI", "Models", "UiSettings.cs");

        Assert.Contains("public string AccessibilityComfortProfile { get; set; } = \"balanced\";", uiSettings, StringComparison.Ordinal);
        Assert.Contains("public bool AccessibilityReadingGuideEnabled { get; set; }", uiSettings, StringComparison.Ordinal);
        Assert.Contains("public int AccessibilityReadingGuideBandHeight { get; set; } = 160;", uiSettings, StringComparison.Ordinal);
        Assert.Contains("AccessibilityComfortProfileRadio", xaml, StringComparison.Ordinal);
        // "Mode calme" (un seul reglage) et "Lecture profonde" (combinaison
        // artificielle) retires le 2026-07-20 : un "profil" doit combiner
        // plusieurs aides pour une situation reelle, pas exposer un seul
        // interrupteur ou un melange arbitraire.
        Assert.DoesNotContain("Mode calme", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Lecture profonde", xaml, StringComparison.Ordinal);
        Assert.Contains("Vision fatiguee", xaml, StringComparison.Ordinal);
        Assert.Contains("Mode secours", xaml, StringComparison.Ordinal);
        // Le bouton micro "Dictee vocale" ne faisait rien de plus qu'attenuer
        // son opacite : retire le 2026-07-20, plus de toggle Confort dedie.
        Assert.DoesNotContain("AccessibilityVoiceDictationSwitch", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessibilityVoiceDictationEnabled", uiSettings, StringComparison.Ordinal);
        // Aides ciblees (vraie accessibilite par situation, pas du confort
        // generique) : espacement du texte (WCAG 1.4.12) et renforcement des
        // couleurs, appliques par script injecte a chaque navigation comme le
        // confort par site et le guide de lecture.
        Assert.Contains("public string AccessibilityTextSpacing { get; set; } = \"normal\";", uiSettings, StringComparison.Ordinal);
        Assert.Contains("public bool AccessibilityColorBoostEnabled { get; set; }", uiSettings, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityTextSpacingCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityColorBoostSwitch\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private async Task ApplyAccessibilityVisionToAllTabsAsync()", accessibilityVision, StringComparison.Ordinal);
        Assert.Contains("letter-spacing", accessibilityVision, StringComparison.Ordinal);
        Assert.Contains("\"saturate(1.45)\"", accessibilityVision, StringComparison.Ordinal);
        Assert.Contains("\"contrast(1.08)\"", accessibilityVision, StringComparison.Ordinal);
        Assert.Contains("_ = ApplyAccessibilityVisionAsync(sender);", navigation, StringComparison.Ordinal);
        // Reduire la lumiere bleue (fatigue visuelle) est deliberement oppose
        // au contraste renforce (basse vision) : demande utilisateur le
        // 2026-07-20 apres avoir constate que "Vision fatiguee" reutilisait a
        // tort la palette de contraste force, illisible par endroits pour un
        // usage qui cherche l'inverse (moins d'agressivite visuelle).
        Assert.Contains("public bool AccessibilityReduceBlueLight { get; set; }", uiSettings, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityReduceBlueLightSwitch\"", xaml, StringComparison.Ordinal);
        Assert.Contains("\"sepia(0.18)\"", accessibilityVision, StringComparison.Ordinal);
        Assert.Contains("ReduceBlueLight: true", comfortProfiles, StringComparison.Ordinal);

        // Le profil "Vision fatiguee" ne force plus le contraste renforce
        // (contraste renforce = basse vision, oppose a la fatigue visuelle) :
        // on isole son bloc de preset et on verifie HighContrast: false dedans.
        var visionPresetStart = comfortProfiles.IndexOf("\"vision\",", StringComparison.Ordinal);
        Assert.True(visionPresetStart >= 0, "Preset 'vision' introuvable.");
        var visionPresetEnd = comfortProfiles.IndexOf("\"rescue\",", visionPresetStart, StringComparison.Ordinal);
        Assert.True(visionPresetEnd > visionPresetStart, "Preset 'rescue' introuvable apres 'vision'.");
        var visionPresetBlock = comfortProfiles[visionPresetStart..visionPresetEnd];
        Assert.Contains("HighContrast: false", visionPresetBlock, StringComparison.Ordinal);
        Assert.Contains("ReduceBlueLight: true", visionPresetBlock, StringComparison.Ordinal);
        // Le mode secours n'est plus un choix de la liste de profils : c'est
        // une action d'urgence a part, avec sa propre carte.
        Assert.DoesNotContain("<RadioButton Tag=\"rescue\">", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityRescueActivateButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityRescueRestoreButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccessibilityRescueStatusText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AccessibilityRescueActivateButton_Click", accessibilityRescue, StringComparison.Ordinal);
        Assert.Contains("AccessibilityRescueRestoreButton_Click", accessibilityRescue, StringComparison.Ordinal);
        // La bande "160" ne s'appelle plus "Confort" (collision avec le nom de
        // toute la rubrique) : renommee "Standard".
        Assert.Contains("<ComboBoxItem Content=\"Standard\" Tag=\"160\" />", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ComboBoxItem Content=\"Confort\" Tag=\"160\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("ReadingGuideEnabledSwitch", xaml, StringComparison.Ordinal);
        Assert.Contains("ReadingGuideBandHeightCombo", xaml, StringComparison.Ordinal);
        Assert.Contains("AccessibilityComfortProfileRadio_SelectionChanged", comfortProfiles, StringComparison.Ordinal);
        Assert.Contains("ApplyAccessibilityComfortProfile(string profileKey)", comfortProfiles, StringComparison.Ordinal);
        Assert.Contains("ResolveAccessibilityComfortProfileFromControls()", comfortProfiles, StringComparison.Ordinal);
        Assert.Contains("ApplyReadingGuideToAllTabsAsync()", comfortProfiles, StringComparison.Ordinal);
        Assert.Contains("\"rescue\"", comfortProfiles, StringComparison.Ordinal);
        // Point d'application unique : preset, snapshot secours et toggles
        // individuels passent tous par la meme methode desormais, pour eviter
        // qu'un chemin oublie un effet de bord (bug du 2026-07-20).
        Assert.Contains("private void ApplyAccessibilityComfortSideEffects()", comfortProfiles, StringComparison.Ordinal);
        Assert.Contains("ApplyAccessibilityComfortSideEffects();", settings, StringComparison.Ordinal);
        Assert.Contains("BuildReadingGuideScript(bool enabled, int bandHeight, bool highContrast)", readingGuide, StringComparison.Ordinal);
        Assert.Contains("_uiSettings.AccessibilityComfortProfile = ResolveAccessibilityComfortProfileFromControls();", settings, StringComparison.Ordinal);
        Assert.Contains("_uiSettings.AccessibilityReadingGuideEnabled = ReadingGuideEnabledSwitch.IsOn;", settings, StringComparison.Ordinal);
        Assert.Contains("_uiSettings.AccessibilityReadingGuideBandHeight = SelectedReadingGuideBandHeight();", settings, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ApplySettingsChangesButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Appliquer les changements\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PasskeysWindowsSettingsButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Ouvrir les paramètres Windows des clés d'accès\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"Gérez vos clés d'accès locales et ouvrez les paramètres Windows dédiés.\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WalletAddCardButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Ajouter une carte au portefeuille\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.HelpText=\"Ajoutez une carte locale, modifiez-la ou utilisez-la sur la page active.\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Confort_par_site_est_memorise_et_reapplique()
    {
        var uiSettings = ReadRepoFile("Lumora.WinUI", "Models", "UiSettings.cs");
        var siteComfortPolicy = ReadRepoFile("Lumora.WinUI", "SiteComfort", "SiteComfortPolicy.cs");
        var siteComfort = ReadRepoFile("Lumora.WinUI", "MainWindow.SiteComfort.cs");
        var siteControl = ReadRepoFile("Lumora.WinUI", "MainWindow.SiteControl.cs");
        var navigation = ReadRepoFile("Lumora.WinUI", "MainWindow.Navigation.cs");
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("public List<SiteComfortRule> SiteComfortRules { get; set; } = new();", uiSettings, StringComparison.Ordinal);
        Assert.Contains("internal sealed class SiteComfortRule", siteComfortPolicy, StringComparison.Ordinal);
        Assert.Contains("SetZoomPercent(IList<SiteComfortRule> rules, string rootDomain, int zoomPercent)", siteComfortPolicy, StringComparison.Ordinal);
        Assert.Contains("SetLargeText(IList<SiteComfortRule> rules, string rootDomain, bool enabled)", siteComfortPolicy, StringComparison.Ordinal);
        Assert.Contains("SetReduceMotion(IList<SiteComfortRule> rules, string rootDomain, bool enabled)", siteComfortPolicy, StringComparison.Ordinal);
        Assert.Contains("ApplySiteComfortAsync(WebView2? view, string? address)", siteComfort, StringComparison.Ordinal);
        Assert.Contains("BuildSiteComfortScript(int zoomPercent, bool largeText, bool reduceMotion)", siteComfort, StringComparison.Ordinal);
        Assert.Contains("RefreshSiteComfortUi(site.RootDomain);", siteControl, StringComparison.Ordinal);
        Assert.Contains("_ = ApplySiteComfortAsync(sender, address);", navigation, StringComparison.Ordinal);
        Assert.Contains("SiteControlComfortZoomCombo", xaml, StringComparison.Ordinal);
        Assert.Contains("SiteControlComfortLargeTextToggle", xaml, StringComparison.Ordinal);
        Assert.Contains("SiteControlComfortReduceMotionToggle", xaml, StringComparison.Ordinal);
        Assert.Contains("Réinitialiser le confort de ce site", xaml, StringComparison.Ordinal);
        // Le confort par site utilise un mecanisme (CSS injecte) totalement
        // independant du confort global (chrome WinUI) : le texte doit le
        // dire explicitement pour ne pas laisser croire a un lien qui n'existe pas.
        Assert.Contains("indépendant du Confort global", siteComfort, StringComparison.Ordinal);
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
