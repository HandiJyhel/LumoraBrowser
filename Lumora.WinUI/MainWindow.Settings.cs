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

    // Style Classique + onglets verticaux (2026-08-08) : TopTabsRow n'a plus
    // besoin d'accueillir une vraie barre d'onglets, seulement la bande de
    // fond des boutons systeme (min/max/fermer). 36 = hauteur standard des
    // boutons de legende WinUI (~32px a 100%) + marge de securite, a
    // confirmer visuellement - voir ApplyVerticalTabsLayout().
    private const double ReducedTitleBarHeightForVerticalTabs = 36;

    private static string NormalizeTabStripPosition(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "left" => "left",
            "right" => "right",
            "bottom" => "bottom",
            _ => "top"
        };

    private static string NormalizeBookmarksBarPosition(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "left" => "left",
            "right" => "right",
            "bottom" => "bottom",
            _ => "top"
        };

    private static bool UsesVerticalTabRail(string tabStripPosition) =>
        tabStripPosition is "left" or "right";

    private static bool UsesSideBookmarksRail(string bookmarksBarPosition) =>
        bookmarksBarPosition is "left" or "right";

    private string SelectedTabStripPosition() =>
        NormalizeTabStripPosition((TabStripPositionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? _tabStripPosition);

    private string SelectedBookmarksBarPosition() =>
        NormalizeBookmarksBarPosition((BookmarksBarPositionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? _bookmarksBarPosition);

    private void VerticalTabsSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle)
        {
            return;
        }

        _tabStripPosition = toggle.IsOn
            ? (_tabStripPosition == "right" ? "right" : "left")
            : "top";
        SetWorkspaceControlsSilently(() =>
        {
            SelectComboByTag(TabStripPositionCombo, _tabStripPosition, "top");
        });
        _verticalTabsEnabled = UsesVerticalTabRail(_tabStripPosition);
        ApplyVerticalTabsLayout();
        SaveWorkspaceUiSettings();
        StatusText.Text = _verticalTabsEnabled
            ? $"Onglets verticaux à {_tabStripPosition}."
            : "Onglets horizontaux actifs.";
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
        SaveWorkspaceUiSettings();
        StatusText.Text = _compactModeEnabled ? "Interface compacte activée." : "Interface compacte désactivée.";
    }

    private void CompactModeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullScreenMode();
    }

    private void ToggleFullScreenMode()
    {
        if (_appWindow is null)
        {
            StatusText.Text = "Plein écran indisponible.";
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
            ? "Mode plein écran activé."
            : "Mode plein écran quitté.";
    }

    private void UpdateFullScreenButton()
    {
        var immersive = IsImmersiveFullScreenActive();
        CompactModeButton.Content = new SymbolIcon(immersive ? Symbol.BackToWindow : Symbol.FullScreen);
        ToolTipService.SetToolTip(CompactModeButton, immersive ? "Quitter le plein écran" : "Plein écran");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            CompactModeButton,
            immersive ? "Quitter le plein écran" : "Plein écran");
    }

    private void CompactModeHideBookmarksSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        ApplyBookmarksBarVisibility();
        SaveWorkspaceUiSettings();
        StatusText.Text = CompactModeHideBookmarksSwitch.IsOn
            ? "Les favoris seront masqués en interface compacte."
            : "Les favoris restent visibles en interface compacte.";
    }

    private void FullScreenAutoHideChromeSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveWorkspaceUiSettings();
        ApplyFullScreenLayout();
        StatusText.Text = FullScreenAutoHideChromeSwitch.IsOn
            ? "En plein écran, les barres se révèlent au survol."
            : "En plein écran, la barre compacte reste visible.";
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
            ? "Palette Ctrl+K activée."
            : "Palette Ctrl+K désactivée.";
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
        UpdateStatusText("Options d'accessibilité appliquées.");
    }

    private void AccessibilityShortcutHelpButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatusText("Aide clavier Lumora annoncée.", announce: false);
        AnnounceAccessibilityContext(
            "Raccourcis Lumora : F6 ou Maj plus F6 pour changer de zone. " +
            "Contrôle Alt 1 à 5 pour aller directement aux onglets, à la barre d'adresse, au contenu actif, aux outils ou au compagnon. " +
            "Contrôle Alt F relit votre repère courant. Contrôle Alt R recentre le focus sur la zone utile. " +
            "Contrôle Alt S active le mode secours. Contrôle Alt X restaure l'état de confort précédent. " +
            "Contrôle K ouvre la palette de commande. Win H lance la dictée Windows dans un champ de texte. " +
            "Contrôle T nouvel onglet. Contrôle W ferme l'onglet actif. Contrôle L met le focus sur la barre d'adresse. " +
            "F5 recharge la page. Alt Gauche ou Alt Droite pour la page précédente ou suivante. " +
            "Contrôle Tab ou Contrôle Maj Tab pour changer d'onglet. F11 bascule le plein écran.");
    }

    private void TranslationEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        if (!TranslationEnabledSwitch.IsOn)
            TranslateBar.Visibility = Visibility.Collapsed;
        StatusText.Text = TranslationEnabledSwitch.IsOn ? "Traduction de page activée." : "Traduction de page désactivée.";
    }

    private void ReadAloudEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        UpdateAccessibilityComfortProfileFromControls();
        ApplyAccessibilityComfortSideEffects();
        UpdateStatusText(ReadAloudEnabledSwitch.IsOn ? "Lecture à voix haute activée." : "Lecture à voix haute désactivée.");
    }

    private void ReadingLensEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        UpdateAccessibilityComfortProfileFromControls();
        ApplyAccessibilityComfortSideEffects();
        UpdateStatusText(ReadingLensEnabledSwitch.IsOn ? "Loupe de lecture activée." : "Loupe de lecture désactivée.");
    }

    private void ReadingGuideEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        UpdateAccessibilityComfortProfileFromControls();
        ApplyAccessibilityComfortSideEffects();
        UpdateStatusText(ReadingGuideEnabledSwitch.IsOn
            ? "Guide de lecture immersif activé."
            : "Guide de lecture immersif désactivé.");
    }

    private void ReadingGuideBandHeightCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        ApplyAccessibilityComfortSideEffects();
        UpdateStatusText($"Bande de lecture réglée sur {NormalizeReadingGuideBandHeight(SelectedReadingGuideBandHeight())} px.");
    }

    private void AccessibilityTextSpacingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        _ = ApplyAccessibilityVisionToAllTabsAsync();
        UpdateStatusText("Espacement du texte des pages mis à jour.");
    }

    private void AccessibilityColorBoostSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        _ = ApplyAccessibilityVisionToAllTabsAsync();
        UpdateStatusText(AccessibilityColorBoostSwitch.IsOn
            ? "Renforcement des couleurs activé sur les pages."
            : "Renforcement des couleurs désactivé.");
    }

    private void AddressSuggestionsSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        if (!AddressSuggestionsSwitch.IsOn)
            CloseAddressSuggestions();
        StatusText.Text = AddressSuggestionsSwitch.IsOn
            ? "Suggestions de la barre d'adresse activées (calcul 100% local)."
            : "Suggestions de la barre d'adresse désactivées.";
    }

    private void SearchAssistEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        UpdateSearchAssistButtonVisibility();
        StatusText.Text = SearchAssistEnabledSwitch.IsOn
            ? "Assistant IA de recherche activé (téléchargement au premier usage)."
            : "Assistant IA de recherche désactivé.";
    }

    private void HistorySemanticSearchEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        StatusText.Text = HistorySemanticSearchEnabledSwitch.IsOn
            ? "Recherche intelligente activée (téléchargement au premier usage, ~120 Mo)."
            : "Recherche intelligente désactivée.";
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

    private void WorkspacePresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave)
        {
            return;
        }

        if (sender is not FrameworkElement { Tag: string preset } element)
        {
            return;
        }

        var parts = preset.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return;
        }

        var status = element is Button { Content: string label } && !string.IsNullOrWhiteSpace(label)
            ? $"Preset {label} appliqué."
            : "Preset d'espace Lumora appliqué.";
        ApplyWorkspacePresetImmediate(parts[0], parts[1], status);
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
            SettingsPendingText.Text = "L'avatar n'a pas pu être appliqué. Vérifiez l'image choisie.";
            return;
        }

        var wallpaperWasPending = HasPendingWallpaperChange();
        var wallpaperChanged = ApplyPendingWallpaperChange();
        if (wallpaperWasPending && !wallpaperChanged)
        {
            SettingsPendingText.Text = "Le fond d'écran n'a pas pu être appliqué. Vérifiez l'image choisie.";
            return;
        }

        var previousUsageMode = _uiSettings.UsageMode;
        SaveUiSettings();
        var modeAccentColorsChanged = ApplyPendingModeAccentColorChanges();
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
        AppearancePendingText.Text = "Options de personnalisation appliquées.";
        ClearSettingsChangesPending("Changements appliqués.");
        StatusText.Text = avatarChanged || wallpaperChanged || modeAccentColorsChanged
            ? "Personnalisation appliquée."
            : "Paramètres appliqués.";
    }

    private void ResetSettingsChangesButton_Click(object sender, RoutedEventArgs e)
    {
        ResetPendingAvatarChange();
        ResetPendingWallpaperChange();
        ResetPendingModeAccentColorChanges();
        ApplyUiSettings();
        AppearancePendingText.Text = "Changements annulés.";
        ClearSettingsChangesPending("Aucun changement en attente.");
        StatusText.Text = "Changements annulés.";
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
        SaveWorkspaceUiSettings();
        StatusText.Text = _verticalTabsCompact ? "Onglets verticaux réduits." : "Onglets verticaux élargis.";
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
        SaveWorkspaceUiSettings();
    }

    private void ApplyUiSettings()
    {
        _suppressUiSettingsSave = true;
        try
        {
            _tabStripPosition = NormalizeTabStripPosition(
                string.IsNullOrWhiteSpace(_uiSettings.TabStripPosition)
                    ? (_uiSettings.VerticalTabsEnabled ? "left" : "top")
                    : _uiSettings.TabStripPosition);
            _bookmarksBarPosition = NormalizeBookmarksBarPosition(_uiSettings.BookmarksBarPosition);
            _verticalTabsEnabled = UsesVerticalTabRail(_tabStripPosition);
            _chromeLayoutStyle = NormalizeChromeLayoutStyle(_uiSettings.ChromeLayoutStyle);
            _uiDensity = NormalizeUiDensity(_uiSettings.UiDensity);
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
            SelectComboByTag(TabStripPositionCombo, _tabStripPosition, "top");
            SelectComboByTag(BookmarksBarPositionCombo, _bookmarksBarPosition, "top");
            SelectComboByTag(ChromeLayoutStyleCombo, _chromeLayoutStyle, "classic");
            SelectComboByTag(UiDensityCombo, _uiDensity, "standard");
            IdentitySpineAutoHideSwitch.IsOn = _uiSettings.IdentitySpineAutoHide;
            InitializeModeAccentColorPickers();
            NewTabFocusSearchSwitch.IsOn = _uiSettings.NewTabFocusSearchOnOpen;
            NewTabShortcutsSwitch.IsOn = _uiSettings.NewTabShortcutsVisible;
            NewTabShortcutsBox.Text = NewTabShortcutsToText(_uiSettings.NewTabShortcuts);
            SelectComboByTag(ThemeModeCombo, _uiSettings.ThemeMode, "dark");

            AccessibilityHighContrastSwitch.IsOn = _uiSettings.AccessibilityHighContrast;
            AccessibilityLargeTextSwitch.IsOn = _uiSettings.AccessibilityLargeText;
            AccessibilityReduceMotionSwitch.IsOn = _uiSettings.AccessibilityReduceMotion;
            AccessibilityVisibleFocusSwitch.IsOn = _uiSettings.AccessibilityVisibleFocus;
            AccessibilityReduceBlueLightSwitch.IsOn = _uiSettings.AccessibilityReduceBlueLight;
            AccessibilityLargeTargetsSwitch.IsOn = _uiSettings.AccessibilityLargeTargets;
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
            ApplyChromeLayoutStyle();
            ApplyWindowBackdrop();
            ApplyAccessibilitySettings();
            ApplyUiDensity();

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
            // Le Compagnon Style Lumora se masque en mode Neutre (voir
            // UpdateIdentitySpineHomeHeroVisibility) - sans cet appel, changer
            // de mode en restant sur l'onglet d'accueil ne rafraichissait la
            // visibilite qu'au prochain changement d'onglet.
            UpdateIdentitySpineHomeHeroVisibility(CurrentTab());
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
        // La colonne identitaire possede deja sa propre presentation des
        // onglets (MainWindow.IdentitySpine.cs) : cette fonction n'a rien a
        // faire pendant qu'elle est active, et surtout ne doit jamais
        // reafficher BrowserTabs/VerticalTabsRail par-dessus (bug "double
        // onglets" signale par l'utilisateur - le declencheur reel n'etait
        // pas ici mais dans ApplyCompactModeLayout/ApplyFullScreenLayout qui
        // pouvaient l'appeler independamment).
        if (_chromeLayoutStyle == "identitySpine")
        {
            return;
        }

        _suppressTabNavigation = true;
        try
        {
            _verticalTabsEnabled = UsesVerticalTabRail(_tabStripPosition);
            var horizontalTabsVisible = !_verticalTabsEnabled;
            var tabsAtBottom = _tabStripPosition == "bottom";

            if (IsImmersiveFullScreenActive())
            {
                BrowserTabs.Visibility = Visibility.Collapsed;
                VerticalTabsRail.Visibility = Visibility.Collapsed;
                VerticalTabsResizeThumb.Visibility = Visibility.Collapsed;
                UpdateTitleBarDragRegion();
                return;
            }

            // TopTabsRow ne descend JAMAIS a 0 : c'est la bande de titre reservee
            // au drag de fenetre + aux boutons systeme (min/max/fermer), comme
            // Edge/Arc. La mettre a 0 en mode onglets verticaux faisait remonter
            // NavigationToolbar dans cette zone reservee par Windows, ce qui
            // rendait ses boutons (bouclier, favoris...) inutilisables - les
            // clics y etaient interceptes par le chrome systeme de la fenetre.
            //
            // Repris le 2026-08-08 (retour utilisateur, comparaison avec Chrome
            // onglets verticaux qui recupere cet espace) : contrairement aux 2
            // tentatives precedentes qui visaient 0px et regressaient, ceci
            // REDUIT la hauteur (52 -> ReducedTitleBarHeightForVerticalTabs) sans
            // la supprimer - la bande de fond des boutons systeme reste presente,
            // juste plus fine. Seulement quand BrowserTabs n'affiche rien dedans
            // (onglets verticaux) ; en onglets horizontaux, 52 reste necessaire
            // pour la vraie barre d'onglets.
            TopTabsRow.Height = horizontalTabsVisible
                ? new GridLength(52)
                : new GridLength(ReducedTitleBarHeightForVerticalTabs);
            Grid.SetRow(BrowserTabs, tabsAtBottom ? 5 : 1);
            Grid.SetRow(ModeChromeAccentStrip, tabsAtBottom ? 5 : 1);
            BottomTabsRow.Height = horizontalTabsVisible && tabsAtBottom
                ? new GridLength(52)
                : new GridLength(0);
            BrowserTabs.Visibility = horizontalTabsVisible ? Visibility.Visible : Visibility.Collapsed;
            VerticalTabsRail.Visibility = _verticalTabsEnabled ? Visibility.Visible : Visibility.Collapsed;
            VerticalTabsResizeThumb.Visibility = _verticalTabsEnabled ? Visibility.Visible : Visibility.Collapsed;
            var tabsOnRight = _tabStripPosition == "right";
            WorkspaceLeftTabsColumn.Width = _verticalTabsEnabled && !tabsOnRight ? GridLength.Auto : new GridLength(0);
            WorkspaceLeftTabsResizeColumn.Width = _verticalTabsEnabled && !tabsOnRight ? GridLength.Auto : new GridLength(0);
            WorkspaceRightTabsResizeColumn.Width = _verticalTabsEnabled && tabsOnRight ? GridLength.Auto : new GridLength(0);
            WorkspaceRightTabsColumn.Width = _verticalTabsEnabled && tabsOnRight ? GridLength.Auto : new GridLength(0);
            Grid.SetColumn(VerticalTabsRail, tabsOnRight ? 5 : 1);
            Grid.SetColumn(VerticalTabsResizeThumb, tabsOnRight ? 4 : 2);
            VerticalTabsRail.BorderThickness = tabsOnRight ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);
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

        _tabStripPosition = SelectedTabStripPosition();
        _bookmarksBarPosition = SelectedBookmarksBarPosition();
        _verticalTabsEnabled = UsesVerticalTabRail(_tabStripPosition);
        _uiSettings.BookmarksBarVisible = BookmarksBarSwitch.IsOn;
        _uiSettings.BookmarksBarPosition = _bookmarksBarPosition;
        _uiSettings.VerticalTabsEnabled = _verticalTabsEnabled;
        _uiSettings.TabStripPosition = _tabStripPosition;
        _uiSettings.ChromeLayoutStyle = _chromeLayoutStyle;
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
        _uiSettings.AccessibilityLargeTargets = AccessibilityLargeTargetsSwitch.IsOn;
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
            VerticalTabsCompactButton.Content = ExpandedRailAction(Symbol.ClosePane, "Réduire");
        }
        ToolTipService.SetToolTip(VerticalTabsCompactButton, _verticalTabsCompact ? "Agrandir les onglets verticaux" : "Réduire les onglets verticaux");
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

        // Meme garde que ApplyVerticalTabsLayout() : la colonne identitaire
        // gere deja sa propre presentation, cette fonction ne doit pas
        // reafficher le chrome classique par-dessus (c'etait le declencheur
        // le plus probable du bug "double onglets" - appelee par tous les
        // toggles de Reglages, le bouton "Appliquer les changements", et le
        // menu contextuel Studio Lumora).
        if (_chromeLayoutStyle == "identitySpine")
        {
            return;
        }

        NavigationRow.Height = new GridLength(ResolveNavigationRowHeight());
        TopTabsRow.Height = new GridLength(52);
        RootShell.RowDefinitions[0].Height = new GridLength(0);
        FullScreenTopBar.Visibility = Visibility.Collapsed;
        NavigationToolbar.Visibility = Visibility.Visible;
        BrowserTabs.Visibility = _verticalTabsEnabled ? Visibility.Collapsed : Visibility.Visible;
        NavigationToolbar.Padding = ResolveNavigationToolbarPadding();
        UpdateFullScreenButton();
        ApplyBookmarksBarVisibility();
        ApplyVerticalTabsLayout();
        ApplyCommandPalettePlacement();
    }

    private void ApplyBookmarksBarVisibility()
    {
        _bookmarksBarPosition = NormalizeBookmarksBarPosition(_bookmarksBarPosition);
        var hiddenByCompactChoice = _compactModeEnabled && CompactModeHideBookmarksSwitch.IsOn;
        var visible = BookmarksBarSwitch.IsOn && !hiddenByCompactChoice && !IsImmersiveFullScreenActive();

        if (_chromeLayoutStyle == "identitySpine")
        {
            // Les favoris ne disparaissent pas, ils rejoignent la colonne
            // identitaire / la capsule flottante a la place (voir
            // RenderIdentitySpineBookmarks(), appelee via RenderBookmarksBar()
            // plus bas) - les conteneurs classiques sont neutralises a plat,
            // meme logique que le reste du chrome classique en colonne
            // identitaire.
            BookmarksRow.Height = new GridLength(0);
            BookmarksBarRow.Visibility = Visibility.Collapsed;
            WorkspaceBottomBookmarksRow.Height = new GridLength(0);
            BookmarksBottomRow.Visibility = Visibility.Collapsed;
            WorkspaceLeftBookmarksColumn.Width = new GridLength(0);
            WorkspaceRightBookmarksColumn.Width = new GridLength(0);
            BookmarksSideRail.Visibility = Visibility.Collapsed;
            RenderBookmarksBar();
            return;
        }

        var topVisible = visible && _bookmarksBarPosition == "top";
        var bottomVisible = visible && _bookmarksBarPosition == "bottom";
        var sideVisible = visible && UsesSideBookmarksRail(_bookmarksBarPosition);
        var bookmarksOnRight = _bookmarksBarPosition == "right";

        var densityMetrics = ResolveUiDensityMetrics(_uiDensity);
        BookmarksRow.Height = topVisible ? new GridLength(densityMetrics.BookmarksRowHeight) : new GridLength(0);
        BookmarksBarRow.Visibility = topVisible ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceBottomBookmarksRow.Height = bottomVisible ? new GridLength(densityMetrics.BookmarksBottomRowHeight) : new GridLength(0);
        BookmarksBottomRow.Visibility = bottomVisible ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceLeftBookmarksColumn.Width = sideVisible && !bookmarksOnRight ? GridLength.Auto : new GridLength(0);
        WorkspaceRightBookmarksColumn.Width = sideVisible && bookmarksOnRight ? GridLength.Auto : new GridLength(0);
        Grid.SetColumn(BookmarksSideRail, bookmarksOnRight ? 6 : 0);
        BookmarksSideRail.BorderThickness = bookmarksOnRight ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);
        BookmarksSideRail.Visibility = sideVisible ? Visibility.Visible : Visibility.Collapsed;
        RenderBookmarksBar();
    }

    private void ApplyFullScreenLayout()
    {
        if (IsImmersiveFullScreenActive())
        {
            var autoHide = _uiSettings.FullScreenAutoHideChrome;
            RootShell.RowDefinitions[0].Height = new GridLength(0);
            FullScreenTopBar.Visibility = autoHide ? Visibility.Collapsed : Visibility.Visible;
            FullScreenTopRevealZone.Visibility = autoHide ? Visibility.Visible : Visibility.Collapsed;
            FullScreenLeftRevealZone.Visibility = autoHide && _verticalTabsEnabled && _tabStripPosition == "left" ? Visibility.Visible : Visibility.Collapsed;
            FullScreenRightRevealZone.Visibility = autoHide && _verticalTabsEnabled && _tabStripPosition == "right" ? Visibility.Visible : Visibility.Collapsed;
            TopTabsRow.Height = new GridLength(0);
            NavigationRow.Height = new GridLength(0);
            BookmarksRow.Height = new GridLength(0);
            BottomTabsRow.Height = new GridLength(0);
            WorkspaceBottomBookmarksRow.Height = new GridLength(0);
            WorkspaceLeftBookmarksColumn.Width = new GridLength(0);
            WorkspaceLeftTabsColumn.Width = new GridLength(0);
            WorkspaceLeftTabsResizeColumn.Width = new GridLength(0);
            WorkspaceRightTabsResizeColumn.Width = new GridLength(0);
            WorkspaceRightTabsColumn.Width = new GridLength(0);
            WorkspaceRightBookmarksColumn.Width = new GridLength(0);
            BrowserTabs.Visibility = Visibility.Collapsed;
            NavigationToolbar.Visibility = Visibility.Collapsed;
            BookmarksBarRow.Visibility = Visibility.Collapsed;
            BookmarksBottomRow.Visibility = Visibility.Collapsed;
            BookmarksSideRail.Visibility = Visibility.Collapsed;
            VerticalTabsRail.Visibility = !autoHide && _verticalTabsEnabled ? Visibility.Visible : Visibility.Collapsed;
            VerticalTabsResizeThumb.Visibility = Visibility.Collapsed;
            Grid.SetColumn(VerticalTabsRail, _tabStripPosition == "right" ? 4 : 0);
            Grid.SetColumnSpan(VerticalTabsRail, 3);
            VerticalTabsRail.HorizontalAlignment = _tabStripPosition == "right"
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;

            if (_chromeLayoutStyle == "identitySpine")
            {
                // Le plein ecran immersif (F11, video) doit aussi masquer la
                // colonne identitaire - sinon elle resterait affichee
                // par-dessus une video plein ecran.
                VerticalTabsRail.Visibility = Visibility.Collapsed;
                IdentitySpineHost.Visibility = Visibility.Collapsed;
                IdentitySpineAddressHost.Visibility = Visibility.Collapsed;
                IdentitySpineHomeHero.Visibility = Visibility.Collapsed;
                UpdateIdentitySpineContentInset();
            }
        }
        else
        {
            _fullScreenTopChromeHideTimer?.Stop();
            _verticalTabsRailAutoHideTimer?.Stop();
            RootShell.RowDefinitions[0].Height = new GridLength(0);
            FullScreenTopBar.Visibility = Visibility.Collapsed;
            FullScreenTopRevealZone.Visibility = Visibility.Collapsed;
            FullScreenLeftRevealZone.Visibility = Visibility.Collapsed;
            FullScreenRightRevealZone.Visibility = Visibility.Collapsed;

            if (_chromeLayoutStyle == "identitySpine")
            {
                // Symetrique de la branche immersive : on restaure la
                // colonne identitaire plutot que le chrome classique.
                // TopTabsRow = 52 (pas 0) : meme correctif que
                // EnterIdentitySpineLayout (MainWindow.IdentitySpine.cs,
                // 2026-08-07) - cette ligne reserve aussi la bande de fond
                // derriere les boutons systeme, pas seulement les onglets.
                TopTabsRow.Height = new GridLength(52);
                NavigationRow.Height = new GridLength(0);
                IdentitySpineHost.Visibility = Visibility.Visible;
                IdentitySpineAddressHost.Visibility = Visibility.Visible;
                // BUG REEL trouve en verification (2026-08-07, capture utilisateur) :
                // seule la barre de favoris revenait apres la sortie du plein ecran,
                // adresse et navigation portees disparues. Cause - NavigationToolbar
                // (la capsule adresse/back/forward/reload, reparentee ici dans
                // IdentitySpineCapsuleSlot par EnterIdentitySpineLayout) est mise en
                // Collapsed sans condition de style en entrant en plein ecran (ligne
                // ci-dessus, branche commune), mais seule la branche Classique la
                // remettait en Visible ci-dessous - IdentitySpineAddressHost (son
                // conteneur) redevenait bien visible, mais NavigationToolbar restait
                // Collapsed a l'interieur, invisible malgre un ancetre visible.
                NavigationToolbar.Visibility = Visibility.Visible;
                UpdateIdentitySpineHomeHeroVisibility(CurrentTab());
                UpdateIdentitySpineContentInset();
            }
            else
            {
                TopTabsRow.Height = new GridLength(52);
                NavigationRow.Height = new GridLength(ResolveNavigationRowHeight());
                Grid.SetColumn(VerticalTabsRail, _tabStripPosition == "right" ? 5 : 1);
                Grid.SetColumnSpan(VerticalTabsRail, 1);
                VerticalTabsRail.HorizontalAlignment = HorizontalAlignment.Stretch;
                NavigationToolbar.Visibility = Visibility.Visible;
                BrowserTabs.Visibility = _verticalTabsEnabled ? Visibility.Collapsed : Visibility.Visible;
                NavigationToolbar.Padding = ResolveNavigationToolbarPadding();
            }

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

    // Delai avant masquage automatique de la chrome plein ecran : 900ms de base
    // (350ms etait trop court pour deplacer la souris et cliquer precisement,
    // constat de l'audit accessibilite moteur/motricite, palier 0.93.x), etendu
    // a 2.5s si l'utilisateur a active "Reduire les animations" - signal le
    // plus proche deja existant pour "laissez-moi plus de temps".
    private TimeSpan FullScreenAutoHideDelay =>
        TimeSpan.FromMilliseconds(_uiSettings.AccessibilityReduceMotion ? 2500 : 900);

    private void ScheduleFullScreenTopChromeHide()
    {
        _fullScreenTopChromeHideTimer ??= new DispatcherTimer();
        _fullScreenTopChromeHideTimer.Interval = FullScreenAutoHideDelay;
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

    private void FullScreenRightRevealZone_PointerEntered(object sender, PointerRoutedEventArgs e)
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
        Grid.SetColumn(VerticalTabsRail, _tabStripPosition == "right" ? 4 : 0);
        Grid.SetColumnSpan(VerticalTabsRail, 3);
        VerticalTabsRail.HorizontalAlignment = _tabStripPosition == "right"
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
        VerticalTabsRail.Visibility = Visibility.Visible;
        VerticalTabsResizeThumb.Visibility = Visibility.Collapsed;
    }

    private void ScheduleVerticalTabsRailHide()
    {
        _verticalTabsRailAutoHideTimer ??= new DispatcherTimer();
        _verticalTabsRailAutoHideTimer.Interval = FullScreenAutoHideDelay;
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
            StartContentFullScreenWatchdog();
            StatusText.Text = "Mode plein écran immersif activé.";
            return;
        }

        if (!ReferenceEquals(_contentFullScreenCore, core)) return;

        CompleteContentFullScreenExit("Mode plein écran quitté.");
    }

    // Meme restauration que BrowserCore_ContainsFullScreenElementChanged, mais
    // declenchee par le signal JS redondant (RegisterFullScreenExitMonitorAsync)
    // plutot que par l'evenement WinRT natif. Idempotent : sans effet si
    // _contentFullScreenCore ne correspond plus au coeur emetteur.
    private void HandleContentFullScreenExitSignal(CoreWebView2? core)
    {
        if (core is null) return;
        if (!ReferenceEquals(_contentFullScreenCore, core)) return;

        CompleteContentFullScreenExit("Mode plein écran quitté.");
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

        CompleteContentFullScreenExit("Mode plein écran quitté.");
    }

    private void CompleteContentFullScreenExit(string status)
    {
        _contentFullScreenWatchdogTimer?.Stop();
        _contentFullScreenCore = null;
        RestorePresenterAfterContentFullScreen();
        ApplyFullScreenLayout();
        UpdateFullScreenButton();
        StatusText.Text = _isFullScreenMode ? "Mode plein écran Lumora actif." : status;
    }

    private void StartContentFullScreenWatchdog()
    {
        _contentFullScreenWatchdogTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _contentFullScreenWatchdogTimer.Tick -= ContentFullScreenWatchdogTimer_Tick;
        _contentFullScreenWatchdogTimer.Tick += ContentFullScreenWatchdogTimer_Tick;
        _contentFullScreenWatchdogTimer.Stop();
        _contentFullScreenWatchdogTimer.Start();
    }

    private void ContentFullScreenWatchdogTimer_Tick(object? sender, object e)
    {
        if (_contentFullScreenCore is not { } core)
        {
            _contentFullScreenWatchdogTimer?.Stop();
            return;
        }

        try
        {
            if (!core.ContainsFullScreenElement)
            {
                CompleteContentFullScreenExit("Mode plein écran quitté.");
            }
        }
        catch (Exception error)
        {
            // Coeur ferme/detruit entre deux sondes (onglet ferme pendant le
            // plein ecran, deja gere ailleurs via CloseTabView) : le timer
            // s'arrete tout seul au prochain tick puisque _contentFullScreenCore
            // aura ete remis a null par ce chemin-la.
            WinUiRuntimeTrace.Write($"Content fullscreen watchdog: sonde ignoree ({error.GetType().Name})");
        }
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
