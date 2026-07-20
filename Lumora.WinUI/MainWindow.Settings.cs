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

public sealed partial class MainWindow
{
    // ── Settings ─────────────────────────────────────────────────────────────

    private void VerticalTabsSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle)
        {
            return;
        }

        _verticalTabsEnabled = toggle.IsOn;
        ApplyVerticalTabsLayout();
        SaveUiSettings();
        StatusText.Text = _verticalTabsEnabled ? "Onglets verticaux actives." : "Onglets horizontaux actifs.";
    }

    private void ApplyStartupPage()
    {
        var mode = _uiSettings.StartupMode;
        if (mode == "restore")
        {
            if (!RestoreTabSession())
                AddTab("Accueil Lumora", "lumora://accueil", select: true);
        }
        else if (mode == "custom" && !string.IsNullOrWhiteSpace(_uiSettings.StartupUrl))
        {
            AddTab(DisplayTitle(_uiSettings.StartupUrl), _uiSettings.StartupUrl, select: true);
        }
        else
        {
            AddTab("Accueil Lumora", "lumora://accueil", select: true);
        }
    }

    private void StartupModeRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (StartupModeRadio.SelectedItem is RadioButton rb)
        {
            _uiSettings.StartupMode = rb.Tag?.ToString() ?? "restore";
            StartupUrlBox.Visibility = _uiSettings.StartupMode == "custom" ? Visibility.Visible : Visibility.Collapsed;
            _uiSettings.Save(_profile.UiSettingsFile);
        }
    }

    private void StartupUrlBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.StartupUrl = StartupUrlBox.Text.Trim();
        _uiSettings.Save(_profile.UiSettingsFile);
    }

    private void SearchEngineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (SearchEngineCombo.SelectedItem is ComboBoxItem item)
        {
            _uiSettings.SearchEngine = item.Tag?.ToString() ?? "google";
            _uiSettings.Save(_profile.UiSettingsFile);
        }
    }

    private void CompactModeSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle) return;
        if (_suppressUiSettingsSave) return;
        _compactModeEnabled = toggle.IsOn;
        ApplyCompactModeLayout();
        SaveUiSettings();
        StatusText.Text = _compactModeEnabled ? "Interface compacte activee." : "Interface compacte desactivee.";
    }

    private void CompactModeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullScreenMode();
    }

    private void ToggleFullScreenMode()
    {
        if (_appWindow is null)
        {
            StatusText.Text = "Plein ecran indisponible.";
            return;
        }

        if (_contentFullScreenCore is not null && !_isFullScreenMode)
        {
            _ = ExitContentFullScreenAsync();
            return;
        }

        _isFullScreenMode = !_isFullScreenMode;
        _appWindow.SetPresenter(_isFullScreenMode
            ? AppWindowPresenterKind.FullScreen
            : AppWindowPresenterKind.Overlapped);
        ApplyFullScreenLayout();
        UpdateFullScreenButton();
        StatusText.Text = _isFullScreenMode
            ? "Mode plein ecran active."
            : "Mode plein ecran quitte.";
    }

    private void UpdateFullScreenButton()
    {
        var immersive = IsImmersiveFullScreenActive();
        CompactModeButton.Content = new SymbolIcon(immersive ? Symbol.BackToWindow : Symbol.FullScreen);
        ToolTipService.SetToolTip(CompactModeButton, immersive ? "Quitter le plein ecran" : "Plein ecran");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            CompactModeButton,
            immersive ? "Quitter le plein ecran" : "Plein ecran");
    }

    private void CompactModeHideBookmarksSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        ApplyBookmarksBarVisibility();
        SaveUiSettings();
        StatusText.Text = CompactModeHideBookmarksSwitch.IsOn
            ? "Les favoris seront masques en interface compacte."
            : "Les favoris restent visibles en interface compacte.";
    }

    private void FullScreenAutoHideChromeSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        ApplyFullScreenLayout();
        StatusText.Text = FullScreenAutoHideChromeSwitch.IsOn
            ? "En plein ecran, les barres se revelent au survol."
            : "En plein ecran, la barre compacte reste visible.";
    }

    private void NewTabTitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        MarkAppearanceOptionsPending();
    }

    private void NewTabFocusSearchSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        MarkAppearanceOptionsPending();
    }

    private void NewTabShortcutsSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        MarkAppearanceOptionsPending();
    }

    private void NewTabShortcutsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        MarkAppearanceOptionsPending();
    }

    private void CommandPaletteEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.CommandPaletteEnabled = CommandPaletteEnabledSwitch.IsOn;
        SaveUiSettings();
        StatusText.Text = _uiSettings.CommandPaletteEnabled
            ? "Palette Ctrl+K activee."
            : "Palette Ctrl+K desactivee.";
    }

    private void CommandPaletteContextSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
    }

    private void AccessibilitySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        UpdateAccessibilityComfortProfileFromControls();
        ApplyAccessibilityComfortSideEffects();
        UpdateStatusText("Options d'accessibilite appliquees.");
    }

    private void AccessibilityShortcutHelpButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatusText("Aide clavier Lumora annoncee.", announce: false);
        AnnounceAccessibilityContext(
            "Raccourcis Lumora : F6 ou Maj plus F6 pour changer de zone. " +
            "Controle Alt 1 a 5 pour aller directement aux onglets, a la barre d'adresse, au contenu actif, aux outils ou au compagnon. " +
            "Controle Alt F relit votre repere courant. Controle Alt R recentre le focus sur la zone utile. " +
            "Controle Alt S active le mode secours. Controle Alt X restaure l'etat de confort precedent. " +
            "Controle K ouvre la palette de commande. Win H lance la dictee Windows dans un champ de texte.");
    }

    private void TranslationEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        if (!TranslationEnabledSwitch.IsOn)
            TranslateBar.Visibility = Visibility.Collapsed;
        StatusText.Text = TranslationEnabledSwitch.IsOn ? "Traduction de page activee." : "Traduction de page desactivee.";
    }

    private void ReadAloudEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        UpdateAccessibilityComfortProfileFromControls();
        ApplyAccessibilityComfortSideEffects();
        UpdateStatusText(ReadAloudEnabledSwitch.IsOn ? "Lecture a voix haute activee." : "Lecture a voix haute desactivee.");
    }

    private void ReadingLensEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        UpdateAccessibilityComfortProfileFromControls();
        ApplyAccessibilityComfortSideEffects();
        UpdateStatusText(ReadingLensEnabledSwitch.IsOn ? "Loupe de lecture activee." : "Loupe de lecture desactivee.");
    }

    private void ReadingGuideEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        UpdateAccessibilityComfortProfileFromControls();
        ApplyAccessibilityComfortSideEffects();
        UpdateStatusText(ReadingGuideEnabledSwitch.IsOn
            ? "Guide de lecture immersif active."
            : "Guide de lecture immersif desactive.");
    }

    private void ReadingGuideBandHeightCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        ApplyAccessibilityComfortSideEffects();
        UpdateStatusText($"Bande de lecture reglee sur {NormalizeReadingGuideBandHeight(SelectedReadingGuideBandHeight())} px.");
    }

    private void AccessibilityTextSpacingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        _ = ApplyAccessibilityVisionToAllTabsAsync();
        UpdateStatusText("Espacement du texte des pages mis a jour.");
    }

    private void AccessibilityColorBoostSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        _ = ApplyAccessibilityVisionToAllTabsAsync();
        UpdateStatusText(AccessibilityColorBoostSwitch.IsOn
            ? "Renforcement des couleurs active sur les pages."
            : "Renforcement des couleurs desactive.");
    }

    private void AddressSuggestionsSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        if (!AddressSuggestionsSwitch.IsOn)
            CloseAddressSuggestions();
        StatusText.Text = AddressSuggestionsSwitch.IsOn
            ? "Suggestions de la barre d'adresse activees (calcul 100% local)."
            : "Suggestions de la barre d'adresse desactivees.";
    }

    private void SearchAssistEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        UpdateSearchAssistButtonVisibility();
        StatusText.Text = SearchAssistEnabledSwitch.IsOn
            ? "Assistant IA de recherche active (telechargement au premier usage)."
            : "Assistant IA de recherche desactive.";
    }

    private void HistorySemanticSearchEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        StatusText.Text = HistorySemanticSearchEnabledSwitch.IsOn
            ? "Recherche intelligente activee (telechargement au premier usage, ~120 Mo)."
            : "Recherche intelligente desactivee.";
    }

    private void ThemeModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        MarkAppearanceOptionsPending();
    }

    private void AppearanceOption_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        MarkAppearanceOptionsPending();
    }

    private void MarkAppearanceOptionsPending()
    {
        MarkSettingsChangesPending("Changements de personnalisation en attente.");
        StatusText.Text = "Options de personnalisation en attente de validation.";
    }

    private void MarkSettingsChangesPending(string message)
    {
        SettingsPendingText.Text = message;
        AppearancePendingText.Text = "Changements en attente. Cliquez sur Appliquer les changements.";
    }

    private void ClearSettingsChangesPending(string message)
    {
        SettingsPendingText.Text = message;
    }

    private void ApplySettingsChangesButton_Click(object sender, RoutedEventArgs e)
    {
        var avatarWasPending = HasPendingAvatarChange();
        var avatarChanged = ApplyPendingAvatarChange();
        if (avatarWasPending && !avatarChanged)
        {
            SettingsPendingText.Text = "L'avatar n'a pas pu etre applique. Verifiez l'image choisie.";
            return;
        }

        var previousUsageMode = _uiSettings.UsageMode;
        SaveUiSettings();
        if (!string.Equals(previousUsageMode, _uiSettings.UsageMode, StringComparison.OrdinalIgnoreCase))
        {
            _uiSettings.LastIntroducedUsageMode = string.Empty;
            ApplyUsageModePreset(_uiSettings.UsageMode);
            _uiSettings.Save(_profile.UiSettingsFile);
            ApplyUiSettings();
        }

        ApplyAccessibilitySettings();
        ApplyBookmarksBarVisibility();
        ApplyCompactModeLayout();
        ApplyFullScreenLayout();
        RefreshNovaHomePages();
        AppearancePendingText.Text = "Options de personnalisation appliquees.";
        ClearSettingsChangesPending("Changements appliques.");
        StatusText.Text = avatarChanged
            ? "Personnalisation appliquee, avatar compris."
            : "Parametres appliques.";
    }

    private void ResetSettingsChangesButton_Click(object sender, RoutedEventArgs e)
    {
        ResetPendingAvatarChange();
        ApplyUiSettings();
        AppearancePendingText.Text = "Changements annules.";
        ClearSettingsChangesPending("Aucun changement en attente.");
        StatusText.Text = "Changements annules.";
    }

    // ── Resize fenêtre : MinWidth dynamique ──────────────────────────────────

    private void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
    {
        ApplyTitleBarSafeArea();
        if (args.DidSizeChange)
            EnforceMinWindowWidth();
    }

    private void EnforceMinWindowWidth()
    {
        if (_appWindow is null) return;
        var railW = _verticalTabsEnabled
            ? (int)(_verticalTabsCompact ? VerticalTabsCompactWidth : _verticalTabsExpandedWidth)
            : 0;
        var minW = railW + 620; // zone browser minimum : 620 px
        if (_appWindow.Size.Width < minW)
            _appWindow.Resize(new SizeInt32(minW, _appWindow.Size.Height));
    }

    // ── Onglets verticaux compact ─────────────────────────────────────────────

    private void VerticalTabsCompactButton_Click(object sender, RoutedEventArgs e)
    {
        _verticalTabsCompact = !_verticalTabsCompact;
        ApplyVerticalTabsWidth();
        RenderVerticalTabs();
        SaveUiSettings();
        StatusText.Text = _verticalTabsCompact ? "Onglets verticaux reduits." : "Onglets verticaux elargis.";
    }

    private void VerticalTabsResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        // Pendant le drag : uniquement mettre à jour la largeur, pas de re-render ni de sauvegarde
        // (RenderVerticalTabs est trop lent pour être appelé à chaque micro-événement)
        var currentWidth = double.IsNaN(VerticalTabsRail.Width) ? VerticalTabsRail.ActualWidth : VerticalTabsRail.Width;
        var targetWidth = Math.Clamp(currentWidth + e.HorizontalChange, VerticalTabsCompactWidth, VerticalTabsMaxWidth);

        _verticalTabsCompact = targetWidth <= VerticalTabsCompactWidth + 24;
        if (!_verticalTabsCompact)
            _verticalTabsExpandedWidth = Math.Clamp(targetWidth, VerticalTabsMinExpandedWidth, VerticalTabsMaxWidth);

        VerticalTabsRail.Width = targetWidth;
        ApplyVerticalTabsPresentation();
    }

    private static void SetCursorSizeWestEast()
    {
        var hCursor = LoadCursor(0, 32644); // IDC_SIZEWE
        SetCursor(hCursor);
    }

    private static void RestoreDefaultCursor()
    {
        var hCursor = LoadCursor(0, 32512); // IDC_ARROW
        SetCursor(hCursor);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint LoadCursor(nint hInstance, int lpCursorName);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint SetCursor(nint hCursor);

    private void VerticalTabsResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        // Fin du drag : re-render des onglets et sauvegarde
        RenderVerticalTabs();
        SaveUiSettings();
    }

    private void ApplyUiSettings()
    {
        _suppressUiSettingsSave = true;
        try
        {
            _verticalTabsEnabled = _uiSettings.VerticalTabsEnabled;
            _verticalTabsCompact = _uiSettings.VerticalTabsCompact;
            _compactModeEnabled = _uiSettings.CompactModeEnabled;
            _verticalTabsExpandedWidth = Math.Clamp(_uiSettings.VerticalTabsWidth, VerticalTabsMinExpandedWidth, VerticalTabsMaxWidth);
            BookmarksBarSwitch.IsOn = _uiSettings.BookmarksBarVisible;
            VerticalTabsSwitch.IsOn = _verticalTabsEnabled;
            CompactModeSwitch.IsOn = _compactModeEnabled;
            CompactModeHideBookmarksSwitch.IsOn = _uiSettings.CompactModeHidesBookmarks;
            FullScreenAutoHideChromeSwitch.IsOn = _uiSettings.FullScreenAutoHideChrome;
            _uiSettings.WindowBackdrop = "solid";
            CommandPaletteEnabledSwitch.IsOn = _uiSettings.CommandPaletteEnabled;
            CommandPaletteWebPagesSwitch.IsOn = _uiSettings.CommandPaletteOpenFromWebPages;
            CommandPaletteTextFieldsSwitch.IsOn = _uiSettings.CommandPaletteOpenFromTextFields;
            NewTabTitleBox.Text = BrandingText.NormalizeLegacyProductTitle(_uiSettings.NewTabTitle);
            SelectComboByTag(AccentPaletteCombo, _uiSettings.AccentPalette, "lumora");
            SelectComboByTag(NewTabStyleCombo, _uiSettings.NewTabStyle, "signature");
            SelectComboByTag(UsageModeCombo, _uiSettings.UsageMode, "neutral");
            SelectComboByTag(ModulesUsageModeCombo, _uiSettings.UsageMode, "neutral");
            SelectComboByTag(PersonalizationMotionCombo, _uiSettings.PersonalizationMotionStyle, "luminous");
            NewTabFocusSearchSwitch.IsOn = _uiSettings.NewTabFocusSearchOnOpen;
            NewTabShortcutsSwitch.IsOn = _uiSettings.NewTabShortcutsVisible;
            NewTabShortcutsBox.Text = NewTabShortcutsToText(_uiSettings.NewTabShortcuts);
            SelectComboByTag(ThemeModeCombo, _uiSettings.ThemeMode, "dark");

            AccessibilityHighContrastSwitch.IsOn = _uiSettings.AccessibilityHighContrast;
            AccessibilityLargeTextSwitch.IsOn = _uiSettings.AccessibilityLargeText;
            AccessibilityReduceMotionSwitch.IsOn = _uiSettings.AccessibilityReduceMotion;
            AccessibilityVisibleFocusSwitch.IsOn = _uiSettings.AccessibilityVisibleFocus;
            AccessibilityReduceBlueLightSwitch.IsOn = _uiSettings.AccessibilityReduceBlueLight;
            ReadAloudEnabledSwitch.IsOn = _uiSettings.ReadAloudEnabled;
            UpdateReadAloudButtonVisibility();
            ReadingLensEnabledSwitch.IsOn = _uiSettings.AccessibilityReadingLensEnabled;
            UpdateReadingLensButtonVisibility();
            ReadingGuideEnabledSwitch.IsOn = _uiSettings.AccessibilityReadingGuideEnabled;
            SelectComboByTag(ReadingGuideBandHeightCombo, NormalizeReadingGuideBandHeight(_uiSettings.AccessibilityReadingGuideBandHeight).ToString(), "160");
            SelectComboByTag(AccessibilityTextSpacingCombo, _uiSettings.AccessibilityTextSpacing, "normal");
            AccessibilityColorBoostSwitch.IsOn = _uiSettings.AccessibilityColorBoostEnabled;
            UpdateAccessibilityComfortProfileFromControls();
            TranslationEnabledSwitch.IsOn = _uiSettings.TranslationEnabled;
            SearchAssistEnabledSwitch.IsOn = _uiSettings.SearchAssistEnabled;
            HistorySemanticSearchEnabledSwitch.IsOn = _uiSettings.HistorySemanticSearchEnabled;
            AddressSuggestionsSwitch.IsOn = _uiSettings.AddressBarSuggestionsEnabled;
            UpdateSearchAssistButtonVisibility();
            ApplyCompactModeLayout();
            ApplyVerticalTabsLayout();
            ApplyWindowBackdrop();
            ApplyAccessibilitySettings();

            // Démarrage
            var startupMode = _uiSettings.StartupMode;
            foreach (var item in StartupModeRadio.Items)
            {
                if (item is RadioButton rb && rb.Tag?.ToString() == startupMode)
                {
                    StartupModeRadio.SelectedItem = rb;
                    break;
                }
            }
            StartupUrlBox.Text = _uiSettings.StartupUrl ?? string.Empty;
            StartupUrlBox.Visibility = startupMode == "custom" ? Visibility.Visible : Visibility.Collapsed;

            // Moteur de recherche
            foreach (var item in SearchEngineCombo.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag?.ToString() == _uiSettings.SearchEngine)
                {
                    SearchEngineCombo.SelectedItem = item;
                    break;
                }
            }
            if (SearchEngineCombo.SelectedItem is null && SearchEngineCombo.Items.Count > 0)
                SearchEngineCombo.SelectedIndex = 0;

            // Verrouillage automatique
            foreach (var item in SessionTimeoutCombo.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag?.ToString() == _uiSettings.SessionTimeoutMinutes.ToString())
                {
                    SessionTimeoutCombo.SelectedItem = item;
                    break;
                }
            }
            if (SessionTimeoutCombo.SelectedItem is null)
                SessionTimeoutCombo.SelectedIndex = 0;

            // Confidentialité
            SessionPurgeSwitch.IsOn     = _uiSettings.SessionPurgeEnabled;
            NetworkBlockerSwitch.IsOn   = _uiSettings.NetworkBlockerEnabled;
            TelemetryBlockerSwitch.IsOn = _uiSettings.TelemetryBlockerEnabled;
            SmartScreenSwitch.IsOn      = _uiSettings.SmartScreenEnabled;
            PopupBlockerSwitch.IsOn     = _uiSettings.PopupBlockerEnabled;
            StrictAdBlockSwitch.IsOn    = _uiSettings.StrictAdBlockEnabled;
            ParameterCleanerSwitch.IsOn = _uiSettings.ParameterCleanerEnabled;
            HttpsEnforcerSwitch.IsOn    = _uiSettings.HttpsEnforcerEnabled;
            CnameUncloakerSwitch.IsOn   = _uiSettings.CnameUncloakerEnabled;
            WebRtcLeakProtectionSwitch.IsOn = _uiSettings.WebRtcLeakProtectionEnabled;
            GeolocationSpoofingSwitch.IsOn = _uiSettings.GeolocationSpoofingEnabled;
            GeolocationLatitudeBox.Text = _uiSettings.GeolocationSpoofLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            GeolocationLongitudeBox.Text = _uiSettings.GeolocationSpoofLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            FingerprintProtectionSwitch.IsOn = _uiSettings.FingerprintProtectionEnabled;
            ModulesWebRtcSwitch.IsOn = _uiSettings.WebRtcLeakProtectionEnabled;
            ModulesGeolocationSwitch.IsOn = _uiSettings.GeolocationSpoofingEnabled;
            ModulesFingerprintSwitch.IsOn = _uiSettings.FingerprintProtectionEnabled;
            CosmeticFilterSwitch.IsOn   = _uiSettings.CosmeticFilterEnabled;
            ConsentManagerSwitch.IsOn   = _uiSettings.ConsentManagerEnabled;
            RenderPrivacyWhitelist();
            UpdateModulesPinUi();
            UpdateUsageModeButtonUi();
            UpdateModeCompanionUi();
            UpdateAccessibilityQuickButtonUi();
        }
        finally
        {
            _suppressUiSettingsSave = false;
        }
    }

    private static void SelectComboByTag(ComboBox combo, string? tag, string fallbackTag)
    {
        combo.SelectedItem = combo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            ?? combo.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), fallbackTag, StringComparison.OrdinalIgnoreCase));

        if (combo.SelectedItem is null && combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private void ApplyVerticalTabsLayout()
    {
        _suppressTabNavigation = true;
        try
        {
            if (IsImmersiveFullScreenActive())
            {
                BrowserTabs.Visibility = Visibility.Collapsed;
                VerticalTabsRail.Visibility = Visibility.Collapsed;
                VerticalTabsResizeThumb.Visibility = Visibility.Collapsed;
                UpdateTitleBarDragRegion();
                return;
            }

            // TopTabsRow garde TOUJOURS sa hauteur (38) : c'est la bande de titre
            // reservee au drag de fenetre + aux boutons systeme (min/max/fermer),
            // comme Edge/Arc. La mettre a 0 en mode onglets verticaux faisait
            // remonter NavigationToolbar dans cette zone reservee par Windows,
            // ce qui rendait ses boutons (bouclier, favoris...) inutilisables —
            // les clics y etaient interceptes par le chrome systeme de la fenetre.
            BrowserTabs.Visibility = _verticalTabsEnabled ? Visibility.Collapsed : Visibility.Visible;
            VerticalTabsRail.Visibility = _verticalTabsEnabled ? Visibility.Visible : Visibility.Collapsed;
            VerticalTabsResizeThumb.Visibility = _verticalTabsEnabled ? Visibility.Visible : Visibility.Collapsed;
            ApplyVerticalTabsWidth();
            RenderVerticalTabs();
            EnforceMinWindowWidth();
            ApplyTitleBarSafeArea();
            UpdateTitleBarDragRegion();
        }
        finally
        {
            _suppressTabNavigation = false;
        }
    }

    private void SaveUiSettings()
    {
        if (_suppressUiSettingsSave)
        {
            return;
        }

        _uiSettings.BookmarksBarVisible = BookmarksBarSwitch.IsOn;
        _uiSettings.VerticalTabsEnabled = _verticalTabsEnabled;
        _uiSettings.VerticalTabsCompact = _verticalTabsCompact;
        _uiSettings.CompactModeEnabled = _compactModeEnabled;
        _uiSettings.CompactModeHidesBookmarks = CompactModeHideBookmarksSwitch.IsOn;
        _uiSettings.FullScreenAutoHideChrome = FullScreenAutoHideChromeSwitch.IsOn;
        _uiSettings.WindowBackdrop = "solid";
        if (ThemeModeCombo.SelectedItem is ComboBoxItem themeItem)
            _uiSettings.ThemeMode = themeItem.Tag?.ToString() ?? "dark";
        if (AccentPaletteCombo.SelectedItem is ComboBoxItem accentItem)
            _uiSettings.AccentPalette = accentItem.Tag?.ToString() ?? "lumora";
        _uiSettings.VerticalTabsWidth = Math.Clamp(_verticalTabsExpandedWidth, VerticalTabsMinExpandedWidth, VerticalTabsMaxWidth);
        _uiSettings.CommandPaletteEnabled = CommandPaletteEnabledSwitch.IsOn;
        _uiSettings.CommandPaletteOpenFromWebPages = CommandPaletteWebPagesSwitch.IsOn;
        _uiSettings.CommandPaletteOpenFromTextFields = CommandPaletteTextFieldsSwitch.IsOn;
        if (SearchEngineCombo.SelectedItem is ComboBoxItem engineItem)
            _uiSettings.SearchEngine = engineItem.Tag?.ToString() ?? "google";
        _uiSettings.NewTabTitle = BrandingText.NormalizeLegacyProductTitle(NewTabTitleBox.Text);
        if (NewTabStyleCombo.SelectedItem is ComboBoxItem newTabStyleItem)
            _uiSettings.NewTabStyle = newTabStyleItem.Tag?.ToString() ?? "signature";
        if (UsageModeCombo.SelectedItem is ComboBoxItem usageModeItem)
            _uiSettings.UsageMode = usageModeItem.Tag?.ToString() ?? "neutral";
        SelectComboByTag(ModulesUsageModeCombo, _uiSettings.UsageMode, "neutral");
        if (PersonalizationMotionCombo.SelectedItem is ComboBoxItem motionItem)
            _uiSettings.PersonalizationMotionStyle = motionItem.Tag?.ToString() ?? "luminous";
        _uiSettings.NewTabFocusSearchOnOpen = NewTabFocusSearchSwitch.IsOn;
        _uiSettings.NewTabShortcutsVisible = NewTabShortcutsSwitch.IsOn;
        _uiSettings.NewTabShortcuts = ParseNewTabShortcuts(NewTabShortcutsBox.Text);
        _uiSettings.AccessibilityHighContrast = AccessibilityHighContrastSwitch.IsOn;
        _uiSettings.AccessibilityLargeText = AccessibilityLargeTextSwitch.IsOn;
        _uiSettings.AccessibilityReduceMotion = AccessibilityReduceMotionSwitch.IsOn;
        _uiSettings.AccessibilityVisibleFocus = AccessibilityVisibleFocusSwitch.IsOn;
        _uiSettings.AccessibilityReduceBlueLight = AccessibilityReduceBlueLightSwitch.IsOn;
        _uiSettings.AccessibilityComfortProfile = ResolveAccessibilityComfortProfileFromControls();
        _uiSettings.ReadAloudEnabled = ReadAloudEnabledSwitch.IsOn;
        _uiSettings.AccessibilityReadingLensEnabled = ReadingLensEnabledSwitch.IsOn;
        _uiSettings.AccessibilityReadingGuideEnabled = ReadingGuideEnabledSwitch.IsOn;
        _uiSettings.AccessibilityReadingGuideBandHeight = SelectedReadingGuideBandHeight();
        _uiSettings.AccessibilityTextSpacing = (AccessibilityTextSpacingCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "normal";
        _uiSettings.AccessibilityColorBoostEnabled = AccessibilityColorBoostSwitch.IsOn;
        _uiSettings.TranslationEnabled = TranslationEnabledSwitch.IsOn;
        _uiSettings.SearchAssistEnabled = SearchAssistEnabledSwitch.IsOn;
        _uiSettings.HistorySemanticSearchEnabled = HistorySemanticSearchEnabledSwitch.IsOn;
        _uiSettings.AddressBarSuggestionsEnabled = AddressSuggestionsSwitch.IsOn;
        if (SessionTimeoutCombo.SelectedItem is ComboBoxItem timeoutItem &&
            int.TryParse(timeoutItem.Tag?.ToString(), out var tm))
            _uiSettings.SessionTimeoutMinutes = tm;
        _uiSettings.Save(_profile.UiSettingsFile);
        // StartupMode, StartupUrl et SearchEngine sont aussi mis à jour directement dans leurs handlers
    }

    private void ApplyVerticalTabsWidth()
    {
        VerticalTabsRail.Width = _verticalTabsCompact ? VerticalTabsCompactWidth : _verticalTabsExpandedWidth;
        ApplyVerticalTabsPresentation();
    }

    private void ApplyVerticalTabsPresentation()
    {
        VerticalTabsRail.Padding = _verticalTabsCompact ? new Thickness(4, 8, 4, 8) : new Thickness(8);
        VerticalTabsActionsPanel.Orientation = _verticalTabsCompact ? Orientation.Vertical : Orientation.Horizontal;
        VerticalTabsNewTabButton.Width = _verticalTabsCompact ? 54 : double.NaN;
        VerticalTabsNewTabButton.Height = _verticalTabsCompact ? 50 : double.NaN;
        VerticalTabsNewTabButton.HorizontalAlignment = _verticalTabsCompact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        VerticalTabsNewTabLabel.Visibility = _verticalTabsCompact ? Visibility.Collapsed : Visibility.Visible;
        VerticalTabsNewTabButton.Content = _verticalTabsCompact
            ? CompactRailAction(Symbol.Add, "Onglet")
            : ExpandedRailAction(Symbol.Add, "Nouvel onglet");

        VerticalTabsCompactButton.Width = _verticalTabsCompact ? 54 : double.NaN;
        VerticalTabsCompactButton.Height = _verticalTabsCompact ? 50 : double.NaN;
        VerticalTabsCompactButton.HorizontalAlignment = _verticalTabsCompact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        if (_verticalTabsCompact)
        {
            VerticalTabsCompactButton.Content = CompactRailAction(Symbol.OpenPane, "Liste");
        }
        else
        {
            VerticalTabsCompactButton.Content = ExpandedRailAction(Symbol.ClosePane, "Reduire");
        }
        ToolTipService.SetToolTip(VerticalTabsCompactButton, _verticalTabsCompact ? "Agrandir les onglets verticaux" : "Reduire les onglets verticaux");
    }

    private FrameworkElement ExpandedRailAction(Symbol symbol, string label)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        content.Children.Add(new SymbolIcon(symbol));
        content.Children.Add(new TextBlock { Text = label });
        return content;
    }

    private FrameworkElement CompactRailAction(Symbol symbol, string label)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(new SymbolIcon
        {
            Symbol = symbol,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return new Border
        {
            Width = 46,
            Height = 42,
            CornerRadius = new CornerRadius(8),
            Background = (Brush)RootShell.Resources["NovaAccentSoftBrush"],
            BorderBrush = (Brush)RootShell.Resources["NovaChromeStrokeBrush"],
            BorderThickness = new Thickness(1),
            Child = content
        };
    }

    private void ApplyCompactModeLayout()
    {
        if (IsImmersiveFullScreenActive())
        {
            ApplyFullScreenLayout();
            return;
        }

        NavigationRow.Height = new GridLength(_compactModeEnabled ? 38 : 42);
        TopTabsRow.Height = new GridLength(38);
        RootShell.RowDefinitions[0].Height = new GridLength(0);
        FullScreenTopBar.Visibility = Visibility.Collapsed;
        NavigationToolbar.Visibility = Visibility.Visible;
        BrowserTabs.Visibility = _verticalTabsEnabled ? Visibility.Collapsed : Visibility.Visible;
        NavigationToolbar.Padding = _compactModeEnabled ? new Thickness(10, 2, 10, 3) : new Thickness(10, 4, 10, 5);
        UpdateFullScreenButton();
        ApplyBookmarksBarVisibility();
        ApplyVerticalTabsLayout();
        ApplyCommandPalettePlacement();
    }

    private void ApplyBookmarksBarVisibility()
    {
        var hiddenByCompactChoice = _compactModeEnabled && CompactModeHideBookmarksSwitch.IsOn;
        var visible = BookmarksBarSwitch.IsOn && !hiddenByCompactChoice && !IsImmersiveFullScreenActive();
        BookmarksRow.Height = visible ? new GridLength(28) : new GridLength(0);
        BookmarksBarRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyFullScreenLayout()
    {
        if (IsImmersiveFullScreenActive())
        {
            var autoHide = _uiSettings.FullScreenAutoHideChrome;
            RootShell.RowDefinitions[0].Height = new GridLength(0);
            FullScreenTopBar.Visibility = autoHide ? Visibility.Collapsed : Visibility.Visible;
            FullScreenTopRevealZone.Visibility = autoHide ? Visibility.Visible : Visibility.Collapsed;
            FullScreenLeftRevealZone.Visibility = autoHide && _verticalTabsEnabled ? Visibility.Visible : Visibility.Collapsed;
            TopTabsRow.Height = new GridLength(0);
            NavigationRow.Height = new GridLength(0);
            BookmarksRow.Height = new GridLength(0);
            VerticalTabsColumn.Width = new GridLength(0);
            VerticalTabsResizeColumn.Width = new GridLength(0);
            BrowserTabs.Visibility = Visibility.Collapsed;
            NavigationToolbar.Visibility = Visibility.Collapsed;
            BookmarksBarRow.Visibility = Visibility.Collapsed;
            VerticalTabsRail.Visibility = !autoHide && _verticalTabsEnabled ? Visibility.Visible : Visibility.Collapsed;
            VerticalTabsResizeThumb.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(VerticalTabsRail, 3);
            VerticalTabsRail.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            _fullScreenTopChromeHideTimer?.Stop();
            _verticalTabsRailAutoHideTimer?.Stop();
            RootShell.RowDefinitions[0].Height = new GridLength(0);
            FullScreenTopBar.Visibility = Visibility.Collapsed;
            FullScreenTopRevealZone.Visibility = Visibility.Collapsed;
            FullScreenLeftRevealZone.Visibility = Visibility.Collapsed;
            TopTabsRow.Height = new GridLength(38);
            NavigationRow.Height = new GridLength(_compactModeEnabled ? 38 : 42);
            VerticalTabsColumn.Width = GridLength.Auto;
            VerticalTabsResizeColumn.Width = GridLength.Auto;
            Grid.SetColumnSpan(VerticalTabsRail, 1);
            VerticalTabsRail.HorizontalAlignment = HorizontalAlignment.Stretch;
            NavigationToolbar.Visibility = Visibility.Visible;
            BrowserTabs.Visibility = _verticalTabsEnabled ? Visibility.Collapsed : Visibility.Visible;
            NavigationToolbar.Padding = _compactModeEnabled ? new Thickness(10, 2, 10, 3) : new Thickness(10, 4, 10, 5);
            ApplyBookmarksBarVisibility();
            ApplyVerticalTabsLayout();
        }

        ApplyCommandPalettePlacement();
        UpdateTitleBarDragRegion();
    }

    private void ApplyCommandPalettePlacement()
    {
        if (IsImmersiveFullScreenActive())
        {
            CommandPaletteCard.Width = 540;
            CommandPaletteCard.HorizontalAlignment = HorizontalAlignment.Right;
            CommandPaletteCard.Margin = new Thickness(0, 48, 24, 0);
            return;
        }

        CommandPaletteCard.Width = 700;
        CommandPaletteCard.HorizontalAlignment = HorizontalAlignment.Center;
        CommandPaletteCard.Margin = new Thickness(0, 54, 0, 0);
    }

    private void FullScreenTopRevealZone_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        ShowFullScreenTopChrome();

    private void FullScreenTopBar_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        ShowFullScreenTopChrome();

    private void FullScreenTopBar_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (IsImmersiveFullScreenActive() && _uiSettings.FullScreenAutoHideChrome)
        {
            ScheduleFullScreenTopChromeHide();
        }
    }

    private void ShowFullScreenTopChrome()
    {
        if (!IsImmersiveFullScreenActive()) return;
        _fullScreenTopChromeHideTimer?.Stop();
        FullScreenAddressText.Text = DisplayAddressForBar(CurrentTab()?.Address ?? string.Empty);
        FullScreenTopBar.Visibility = Visibility.Visible;
    }

    private void ScheduleFullScreenTopChromeHide()
    {
        _fullScreenTopChromeHideTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _fullScreenTopChromeHideTimer.Tick -= FullScreenTopChromeHideTimer_Tick;
        _fullScreenTopChromeHideTimer.Tick += FullScreenTopChromeHideTimer_Tick;
        _fullScreenTopChromeHideTimer.Stop();
        _fullScreenTopChromeHideTimer.Start();
    }

    private void FullScreenTopChromeHideTimer_Tick(object? sender, object e)
    {
        _fullScreenTopChromeHideTimer?.Stop();
        if (IsImmersiveFullScreenActive() && _uiSettings.FullScreenAutoHideChrome)
        {
            FullScreenTopBar.Visibility = Visibility.Collapsed;
        }
    }

    private void FullScreenLeftRevealZone_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!IsImmersiveFullScreenActive() || !_uiSettings.FullScreenAutoHideChrome || !_verticalTabsEnabled) return;
        ShowVerticalTabsRailImmersive();
    }

    private void VerticalTabsRail_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (IsImmersiveFullScreenActive() && _uiSettings.FullScreenAutoHideChrome)
        {
            _verticalTabsRailAutoHideTimer?.Stop();
        }
    }

    private void VerticalTabsRail_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (IsImmersiveFullScreenActive() && _uiSettings.FullScreenAutoHideChrome)
        {
            ScheduleVerticalTabsRailHide();
        }
    }

    private void ShowVerticalTabsRailImmersive()
    {
        _verticalTabsRailAutoHideTimer?.Stop();
        Grid.SetColumnSpan(VerticalTabsRail, 3);
        VerticalTabsRail.HorizontalAlignment = HorizontalAlignment.Left;
        VerticalTabsRail.Visibility = Visibility.Visible;
        VerticalTabsResizeThumb.Visibility = Visibility.Collapsed;
    }

    private void ScheduleVerticalTabsRailHide()
    {
        _verticalTabsRailAutoHideTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _verticalTabsRailAutoHideTimer.Tick -= VerticalTabsRailHideTimer_Tick;
        _verticalTabsRailAutoHideTimer.Tick += VerticalTabsRailHideTimer_Tick;
        _verticalTabsRailAutoHideTimer.Stop();
        _verticalTabsRailAutoHideTimer.Start();
    }

    private void VerticalTabsRailHideTimer_Tick(object? sender, object e)
    {
        _verticalTabsRailAutoHideTimer?.Stop();
        if (IsImmersiveFullScreenActive() && _uiSettings.FullScreenAutoHideChrome)
        {
            VerticalTabsRail.Visibility = Visibility.Collapsed;
        }
    }

    private bool IsImmersiveFullScreenActive() =>
        _isFullScreenMode || _contentFullScreenCore is not null;

    private void BrowserCore_ContainsFullScreenElementChanged(object? sender, object e)
    {
        if (sender is not CoreWebView2 core) return;

        if (core.ContainsFullScreenElement)
        {
            if (_contentFullScreenCore is null)
            {
                CapturePresenterStateBeforeContentFullScreen();
            }

            _contentFullScreenCore = core;
            _appWindow?.SetPresenter(AppWindowPresenterKind.FullScreen);
            ApplyFullScreenLayout();
            UpdateFullScreenButton();
            StatusText.Text = "Mode plein ecran immersif active.";
            return;
        }

        if (!ReferenceEquals(_contentFullScreenCore, core)) return;

        CompleteContentFullScreenExit("Mode plein ecran quitte.");
    }

    // Meme restauration que BrowserCore_ContainsFullScreenElementChanged, mais
    // declenchee par le signal JS redondant (RegisterFullScreenExitMonitorAsync)
    // plutot que par l'evenement WinRT natif. Idempotent : sans effet si
    // _contentFullScreenCore ne correspond plus au coeur emetteur.
    private void HandleContentFullScreenExitSignal(CoreWebView2? core)
    {
        if (core is null) return;
        if (!ReferenceEquals(_contentFullScreenCore, core)) return;

        CompleteContentFullScreenExit("Mode plein ecran quitte.");
    }

    private async Task ExitContentFullScreenAsync()
    {
        var core = _contentFullScreenCore ?? _browserView?.CoreWebView2;
        if (core is not null)
        {
            try
            {
                await core.ExecuteScriptAsync("if(document.fullscreenElement){document.exitFullscreen();}");
            }
            catch (Exception error)
            {
                WinUiRuntimeTrace.Write($"Content fullscreen exit skipped: {error.GetType().Name}");
            }
        }

        CompleteContentFullScreenExit("Mode plein ecran quitte.");
    }

    private void CompleteContentFullScreenExit(string status)
    {
        _contentFullScreenCore = null;
        RestorePresenterAfterContentFullScreen();
        ApplyFullScreenLayout();
        UpdateFullScreenButton();
        StatusText.Text = _isFullScreenMode ? "Mode plein ecran Lumora actif." : status;
    }

    private void CapturePresenterStateBeforeContentFullScreen()
    {
        var presenter = _appWindow?.Presenter;
        _wasLumoraFullScreenBeforeContentFullScreen = _isFullScreenMode;
        _presenterKindBeforeContentFullScreen = presenter?.Kind;
        _overlappedStateBeforeContentFullScreen = presenter is OverlappedPresenter overlapped
            ? overlapped.State
            : null;
    }

    private void RestorePresenterAfterContentFullScreen()
    {
        if (_appWindow is null) return;

        var presenterKind = _presenterKindBeforeContentFullScreen;
        var overlappedState = _overlappedStateBeforeContentFullScreen;
        var wasLumoraFullScreen = _wasLumoraFullScreenBeforeContentFullScreen;
        _presenterKindBeforeContentFullScreen = null;
        _overlappedStateBeforeContentFullScreen = null;
        _wasLumoraFullScreenBeforeContentFullScreen = false;

        if (wasLumoraFullScreen)
        {
            _isFullScreenMode = true;
            _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            return;
        }

        _isFullScreenMode = false;

        if (presenterKind is null ||
            presenterKind == AppWindowPresenterKind.Overlapped ||
            presenterKind == AppWindowPresenterKind.FullScreen)
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (_appWindow.Presenter is OverlappedPresenter overlapped)
            {
                if (overlappedState == OverlappedPresenterState.Maximized)
                {
                    overlapped.Maximize();
                }
                else
                {
                    overlapped.Restore();
                }
            }
            return;
        }

        _appWindow.SetPresenter(presenterKind.Value);
    }

    private void ExitImmersiveFullScreenFromKeyboard()
    {
        if (_contentFullScreenCore is not null)
        {
            _ = ExitContentFullScreenAsync();
            return;
        }

        if (_isFullScreenMode)
        {
            ToggleFullScreenMode();
        }
    }

    private static string NewTabShortcutsToText(IEnumerable<NewTabShortcut> shortcuts) =>
        Settings.NewTabShortcutText.ToText(shortcuts.Select(s => (s.Title, s.Url)));

    private static List<NewTabShortcut> ParseNewTabShortcuts(string text) =>
        Settings.NewTabShortcutText.Parse(text).Select(s => new NewTabShortcut(s.Title, s.Url)).ToList();
}
