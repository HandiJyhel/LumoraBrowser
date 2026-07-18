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

    // ── Stockage ──────────────────────────────────────────────────────────────

    private async void ChangeFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode) { StatusText.Text = "Indisponible en mode invite."; return; }

        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        var newPath = folder.Path;
        if (string.Equals(newPath, _profile.ProfileDir, StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "Ce dossier est deja le dossier actuel.";
            return;
        }

        try
        {
            CopyProfileTo(newPath);
            var config = LumoraConfig.Load();
            config.CustomProfilePath = newPath;
            config.Save();
            StorageCurrentFolderText.Text = newPath;
            StorageRestartBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de la copie : {ex.Message}";
        }
    }

    private static void CopyProfileTo(string destDir)
    {
        var srcDir = new DirectoryInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Lumora", "profiles", "default"));
        if (!srcDir.Exists) return;

        foreach (var srcFile in srcDir.GetFiles("*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(srcDir.FullName, srcFile.FullName);
            var destFile = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            srcFile.CopyTo(destFile, overwrite: true);
        }
    }

    private async void ExportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode) { StatusText.Text = "Indisponible en mode invite."; return; }

        var password = await PromptBackupPasswordAsync("Exporter une sauvegarde", confirm: true);
        if (password is null) return;

        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = $"lumora-backup-{DateTime.Now:yyyyMMdd-HHmm}";
        picker.FileTypeChoices.Add("Sauvegarde Lumora", new List<string> { ".lumorabackup", ".novabackup" });

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            LumoraBackup.Export(file.Path, password, _profile);
            StatusText.Text = "Sauvegarde exportee avec succes.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de l'export : {ex.Message}";
        }
    }

    private async void ImportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode) { StatusText.Text = "Indisponible en mode invite."; return; }

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add(".lumorabackup");
        picker.FileTypeFilter.Add(".novabackup");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var password = await PromptBackupPasswordAsync("Importer une sauvegarde", confirm: false);
        if (password is null) return;

        try
        {
            LumoraBackup.Import(file.Path, password, _profile);
            ReloadBookmarks();
            StorageImportBar.IsOpen = true;
            StatusText.Text = "Sauvegarde importee.";
        }
        catch (CryptographicException)
        {
            StatusText.Text = "Mot de passe incorrect ou sauvegarde corrompue.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de l'import : {ex.Message}";
        }
    }

    private void StorageRestartCloseButton_Click(object sender, RoutedEventArgs e) =>
        Application.Current.Exit();

    private async void ClearBrowsingDataButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _browserView?.CoreWebView2;
        if (core is null) { StatusText.Text = "Moteur web non initialise."; return; }
        try
        {
            await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite);
            StatusText.Text = "Donnees de navigation supprimees (cookies, cache, sessions).";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de la suppression : {ex.Message}";
        }
    }

    private async Task<string?> PromptBackupPasswordAsync(string title, bool confirm)
    {
        var panel = new StackPanel { Spacing = 8 };
        var pwBox = new PasswordBox { PlaceholderText = "Mot de passe de la sauvegarde", MinWidth = 280 };
        panel.Children.Add(pwBox);

        PasswordBox? pwBox2 = null;
        if (confirm)
        {
            var hint = new TextBlock
            {
                Text = "Conservez ce mot de passe : il sera necessaire pour restaurer vos donnees.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                FontSize = 12
            };
            panel.Children.Add(hint);
            pwBox2 = new PasswordBox { PlaceholderText = "Confirmer le mot de passe", MinWidth = 280 };
            panel.Children.Add(pwBox2);
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "Confirmer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;
        if (string.IsNullOrEmpty(pwBox.Password)) return null;
        if (pwBox2 is not null && pwBox.Password != pwBox2.Password)
        {
            StatusText.Text = "Les mots de passe ne correspondent pas.";
            return null;
        }
        return pwBox.Password;
    }

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
        SaveUiSettings();
        ApplyAccessibilitySettings();
        StatusText.Text = "Options d'accessibilite appliquees.";
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
        SaveUiSettings();
        UpdateReadAloudButtonVisibility();
        if (!ReadAloudEnabledSwitch.IsOn) _readAloudService.Stop();
        StatusText.Text = ReadAloudEnabledSwitch.IsOn ? "Lecture a voix haute activee." : "Lecture a voix haute desactivee.";
    }

    private void ReadingLensEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        SaveUiSettings();
        UpdateReadingLensButtonVisibility();
        StatusText.Text = ReadingLensEnabledSwitch.IsOn ? "Loupe de lecture activee." : "Loupe de lecture desactivee.";
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
            AccessibilityVoiceDictationSwitch.IsOn = _uiSettings.AccessibilityVoiceDictationEnabled;
            ReadAloudEnabledSwitch.IsOn = _uiSettings.ReadAloudEnabled;
            UpdateReadAloudButtonVisibility();
            ReadingLensEnabledSwitch.IsOn = _uiSettings.AccessibilityReadingLensEnabled;
            UpdateReadingLensButtonVisibility();
            TranslationEnabledSwitch.IsOn = _uiSettings.TranslationEnabled;
            SearchAssistEnabledSwitch.IsOn = _uiSettings.SearchAssistEnabled;
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
            CosmeticFilterSwitch.IsOn   = _uiSettings.CosmeticFilterEnabled;
            ConsentManagerSwitch.IsOn   = _uiSettings.ConsentManagerEnabled;
            RenderPrivacyWhitelist();
            UpdateModulesPinUi();
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
        _uiSettings.AccessibilityVoiceDictationEnabled = AccessibilityVoiceDictationSwitch.IsOn;
        _uiSettings.ReadAloudEnabled = ReadAloudEnabledSwitch.IsOn;
        _uiSettings.AccessibilityReadingLensEnabled = ReadingLensEnabledSwitch.IsOn;
        _uiSettings.TranslationEnabled = TranslationEnabledSwitch.IsOn;
        _uiSettings.SearchAssistEnabled = SearchAssistEnabledSwitch.IsOn;
        _uiSettings.AddressBarSuggestionsEnabled = AddressSuggestionsSwitch.IsOn;
        if (SessionTimeoutCombo.SelectedItem is ComboBoxItem timeoutItem &&
            int.TryParse(timeoutItem.Tag?.ToString(), out var tm))
            _uiSettings.SessionTimeoutMinutes = tm;
        _uiSettings.Save(_profile.UiSettingsFile);
        // StartupMode, StartupUrl et SearchEngine sont aussi mis à jour directement dans leurs handlers
    }

    // "system" est resolu une fois par appel (pas d'ecoute live du changement de
    // theme Windows en cours de session : re-ouvrir Parametres ou changer de
    // reglage suffit a la reprendre en compte).
    private bool ResolveIsDarkTheme()
    {
        var mode = _uiSettings.ThemeMode;
        if (string.Equals(mode, "light", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(mode, "dark", StringComparison.OrdinalIgnoreCase)) return true;

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int appsUseLightTheme)
            {
                return appsUseLightTheme == 0;
            }
        }
        catch { }

        return true; // repli : sombre (identite historique Lumora)
    }

    private void ApplyAccessibilitySettings()
    {
        var highContrast = _uiSettings.AccessibilityHighContrast;
        var largeText = _uiSettings.AccessibilityLargeText;
        var visibleFocus = _uiSettings.AccessibilityVisibleFocus;
        var translucent = IsTranslucentChromeEnabled() && !highContrast;
        var isDark = highContrast || ResolveIsDarkTheme();
        var palette = ResolveAccentPalette(isDark, highContrast);

        RootShell.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;

        RootShell.Background = translucent
            ? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            : new SolidColorBrush(highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(13, 24, 34) : UiColor(250, 248, 244)));
        SetBrush("NovaChromeSurfaceBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(20, 32, 42, translucent ? (byte)226 : (byte)255) : UiColor(255, 255, 255, translucent ? (byte)226 : (byte)255)));
        SetBrush("NovaChromeSurfaceAltBrush", highContrast ? UiColor(18, 18, 18) : (isDark ? UiColor(26, 39, 49, translucent ? (byte)232 : (byte)255) : UiColor(242, 239, 234, translucent ? (byte)232 : (byte)255)));
        SetBrush("NovaChromeStrokeBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(64, 84, 91) : UiColor(214, 209, 200)));
        SetBrush("NovaChromeStrokeSoftBrush", highContrast ? UiColor(190, 190, 190) : (isDark ? UiColor(36, 52, 60) : UiColor(228, 224, 217)));
        SetBrush("NovaAddressBackgroundBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(23, 40, 52, translucent ? (byte)236 : (byte)255) : UiColor(255, 255, 255, translucent ? (byte)236 : (byte)255)));
        SetBrush("NovaAddressBorderBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(70, 97, 106) : UiColor(198, 192, 182)));
        SetBrush("NovaAddressForegroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(255, 248, 234) : UiColor(31, 29, 26)));
        SetBrush("NovaTextMutedBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(195, 185, 165) : UiColor(120, 113, 102)));
        SetBrush("NovaAccentBrush", palette.Accent);
        SetBrush("NovaAccentSoftBrush", palette.AccentSoft);
        SetBrush("NovaCoolAccentBrush", palette.CoolAccent);
        SetBrush("NovaCoolAccentSoftBrush", palette.CoolAccentSoft);
        SetBrush("NovaCompanionGlassBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(25, 39, 49, 214) : UiColor(255, 255, 255, 228)));
        SetBrush("NovaCompanionStrokeBrush", highContrast ? UiColor(255, 255, 255) : palette.CoolAccentSoft);
        SetBrush("NovaModeSelectorGlassBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(22, 35, 45, 208) : UiColor(255, 255, 255, 224)));
        SetBrush("NovaModeSelectorStrokeBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(52, 68, 76, 160) : UiColor(214, 209, 200, 180)));
        SetBrush("NovaModuleHubGlassBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(22, 35, 45, 214) : UiColor(255, 255, 255, 226)));
        SetBrush("NovaModuleHubStrokeBrush", highContrast ? UiColor(255, 255, 255) : palette.CoolAccentSoft);
        SetBrush("NovaModuleHubNodeBrush", highContrast ? UiColor(255, 255, 255) : (isDark ? UiColor(195, 185, 165) : UiColor(84, 78, 70)));
        SetBrush("NovaModuleHubAccentBrush", highContrast ? UiColor(255, 213, 0) : palette.CoolAccent);
        SetBrush("NovaFocusBrush", palette.Focus);
        SetBrush("NovaFocusInnerBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(13, 24, 34) : UiColor(255, 255, 255)));
        SetBrush("NovaInfoSurfaceBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(29, 46, 56, translucent ? (byte)238 : (byte)255) : UiColor(247, 245, 240, translucent ? (byte)238 : (byte)255)));
        SetBrush("NovaPanelBackgroundBrush", highContrast ? UiColor(0, 0, 0) : (isDark ? UiColor(16, 29, 38) : UiColor(250, 248, 245)));
        ApplyUsageModeChrome(isDark, highContrast, translucent);

        var mainFontSize = largeText ? 16 : 14;
        var smallFontSize = largeText ? 14 : 12;
        AddressBox.FontSize = mainFontSize;
        CommandPaletteSearchBox.FontSize = mainFontSize;
        CommandPaletteHintText.FontSize = smallFontSize;
        StatusText.FontSize = smallFontSize;
        ProfileStatusText.FontSize = smallFontSize;

        foreach (var textBlock in new[] { CredentialSaveText, AutoFillText, WalletFillText, SessionKeepText })
        {
            textBlock.FontSize = mainFontSize;
        }

        var focusThickness = visibleFocus ? new Thickness(2) : new Thickness(1);
        AddressBox.BorderThickness = focusThickness;
        CommandPaletteSearchBox.BorderThickness = focusThickness;

        foreach (var control in new Control[]
                 {
                     AddressBox,
                     CommandPaletteSearchBox,
                     CompactModeButton,
                     FullScreenExitButton,
                     DetachVideoButton,
                     DetachVideoPinnedButton,
                     VideoDownloadButton,
                     ReadAloudButton,
                     ReaderModeButton,
                     SearchAssistButton,
                     NotesModuleButton,
                     TranslatePinnedButton,
                     TranslateModuleButton,
                     WebAppsPinnedButton,
                     WebAppsQuickButton,
                     ModulesButton,
                     ModeCompanionButton,
                     ModeCompanionMemoryBox,
                     ModeCompanionSaveButton,
                     ModeCompanionPrimaryButton,
                     ModeCompanionSecondaryButton,
                     UsageModeButton,
                     DictationPinnedButton,
                     VideoDownloadStartButton,
                     NavigationMenuButton,
                     MainMenuButton,
                     ShieldButton,
                     VaultQuickAccessButton,
                     MicDictationButton,
                     CredentialSaveAccept,
                     CredentialSaveDismiss,
                     AutoFillAccept,
                     AutoFillDismiss,
                     WalletFillAccept,
                     WalletFillDismiss,
                     SessionKeepAccept,
                     SessionKeepDismiss,
                     BackToPageButton,
                     ProfileStatusButton,
                     ApplySettingsChangesButton
                 })
        {
            ApplyNovaControlAccessibility(control);
        }

        UpdateDictationButtonVisibility();
        ApplyWindowTitleBarColors();
    }

    private void SetBrush(string key, Windows.UI.Color color)
    {
        if (RootShell.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private void ApplyUsageModeChrome(bool isDark, bool highContrast, bool translucent)
    {
        if (highContrast)
        {
            ModeChromeAccentStrip.Height = 3;
            ModeChromeAccentStrip.Opacity = 1;
            UsageModeButton.Background = new SolidColorBrush(UiColor(0, 0, 0));
            UsageModeButton.BorderBrush = new SolidColorBrush(UiColor(255, 255, 255));
            UsageModeButton.Foreground = new SolidColorBrush(UiColor(255, 255, 255));
            ModeCompanionButton.Background = new SolidColorBrush(UiColor(0, 0, 0));
            ModeCompanionButton.BorderBrush = new SolidColorBrush(UiColor(255, 255, 255));
            ModeCompanionButton.Foreground = new SolidColorBrush(UiColor(255, 255, 255));
            UsageModeAccentBar.Background = new SolidColorBrush(UiColor(255, 213, 0));
            ModeCompanionAccentDot.Background = new SolidColorBrush(UiColor(0, 255, 226));
            ModeCompanionGlow.Background = new SolidColorBrush(UiColor(0, 0, 0));
            ModulesButton.Background = new SolidColorBrush(UiColor(0, 0, 0));
            ModulesButton.BorderBrush = new SolidColorBrush(UiColor(255, 255, 255));
            ModulesButton.Foreground = new SolidColorBrush(UiColor(255, 255, 255));
            SetIdentityGradient(UiColor(255, 213, 0), UiColor(255, 255, 255), UiColor(0, 255, 226));
            NavigationToolbar.Opacity = 1;
            VerticalTabsRail.Opacity = 1;
            return;
        }

        var alpha = translucent ? (byte)232 : (byte)255;
        var mode = (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant();
        var chrome = ResolveModeChromePalette(mode, isDark, alpha);

        RootShell.Background = new SolidColorBrush(chrome.AppBackground);
        SetBrush("NovaChromeSurfaceBrush", chrome.Surface);
        SetBrush("NovaChromeSurfaceAltBrush", chrome.SurfaceAlt);
        SetBrush("NovaChromeSurfaceRaisedBrush", chrome.SurfaceRaised);
        SetBrush("NovaChromeStrokeBrush", chrome.Stroke);
        SetBrush("NovaChromeStrokeSoftBrush", chrome.StrokeSoft);
        SetBrush("NovaAddressBackgroundBrush", chrome.AddressBackground);
        SetBrush("NovaAddressBorderBrush", chrome.AddressBorder);
        SetBrush("NovaAddressForegroundBrush", chrome.Text);
        SetBrush("NovaTextMutedBrush", chrome.MutedText);
        SetBrush("NovaAccentBrush", chrome.Accent);
        SetBrush("NovaAccentSoftBrush", chrome.AccentSoft);
        SetBrush("NovaCoolAccentBrush", chrome.CoolAccent);
        SetBrush("NovaCoolAccentSoftBrush", chrome.CoolAccentSoft);
        SetBrush("NovaCompanionGlassBrush", ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.10, translucent ? (byte)218 : (byte)236));
        SetBrush("NovaCompanionStrokeBrush", ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.24, 190));
        SetBrush("NovaModeSelectorGlassBrush", ChromeTint(chrome.Surface, chrome.Accent, 0.055, translucent ? (byte)214 : (byte)232));
        SetBrush("NovaModeSelectorStrokeBrush", ChromeTint(chrome.StrokeSoft, chrome.Accent, 0.13, 178));
        SetBrush("NovaModuleHubGlassBrush", ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.07, translucent ? (byte)212 : (byte)232));
        SetBrush("NovaModuleHubStrokeBrush", ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.18, 178));
        SetBrush("NovaModuleHubNodeBrush", chrome.MutedText);
        SetBrush("NovaModuleHubAccentBrush", chrome.CoolAccent);
        SetBrush("NovaFocusBrush", chrome.Focus);
        SetBrush("NovaFocusInnerBrush", isDark ? UiColor(13, 24, 34) : UiColor(255, 255, 255));
        SetIdentityGradient(chrome.CoolAccent, chrome.Accent, chrome.WarmAccent);

        ModeChromeAccentStrip.Height = mode switch
        {
            "neutral" => 1,
            "focus" => 3,
            "research" => 3,
            "night" => 1.5,
            _ => 2
        };
        ModeChromeAccentStrip.Opacity = mode switch
        {
            "neutral" => 0.42,
            "night" => 0.68,
            _ => 0.95
        };
        UsageModeButton.Background = new SolidColorBrush(ChromeTint(chrome.Surface, chrome.Accent, 0.055, translucent ? (byte)214 : (byte)232));
        UsageModeButton.BorderBrush = new SolidColorBrush(ChromeTint(chrome.StrokeSoft, chrome.Accent, 0.13, 178));
        UsageModeButton.Foreground = new SolidColorBrush(chrome.Text);
        UsageModeAccentBar.Background = new SolidColorBrush(chrome.Accent);
        ModeCompanionButton.Background = new SolidColorBrush(ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.10, translucent ? (byte)218 : (byte)236));
        ModeCompanionButton.BorderBrush = new SolidColorBrush(ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.24, 190));
        ModeCompanionButton.Foreground = new SolidColorBrush(chrome.Text);
        ModeCompanionAccentDot.Background = new SolidColorBrush(chrome.CoolAccent);
        ModeCompanionGlow.Background = new SolidColorBrush(ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.18, translucent ? (byte)160 : (byte)190));
        ModulesButton.Background = new SolidColorBrush(ChromeTint(chrome.SurfaceRaised, chrome.CoolAccent, 0.07, translucent ? (byte)212 : (byte)232));
        ModulesButton.BorderBrush = new SolidColorBrush(ChromeTint(chrome.StrokeSoft, chrome.CoolAccent, 0.18, 178));
        ModulesButton.Foreground = new SolidColorBrush(chrome.Text);

        NavigationToolbar.Opacity = mode == "night" ? 0.94 : 1;
        VerticalTabsRail.Opacity = mode == "night" ? 0.95 : 1;
    }

    private void SetIdentityGradient(Windows.UI.Color first, Windows.UI.Color second, Windows.UI.Color third)
    {
        if (RootShell.Resources["NovaIdentityMarkBrush"] is LinearGradientBrush identity && identity.GradientStops.Count >= 3)
        {
            identity.GradientStops[0].Color = first;
            identity.GradientStops[1].Color = second;
            identity.GradientStops[2].Color = third;
        }

        if (RootShell.Resources["NovaChromeGradientBrush"] is LinearGradientBrush chrome && chrome.GradientStops.Count >= 3)
        {
            chrome.GradientStops[0].Color = BlendForChrome(first, 0.14);
            chrome.GradientStops[1].Color = BlendForChrome(second, 0.18);
            chrome.GradientStops[2].Color = BlendForChrome(third, 0.16);
        }
    }

    private static Windows.UI.Color BlendForChrome(Windows.UI.Color color, double weight)
    {
        byte Blend(byte baseValue, byte accentValue) =>
            (byte)Math.Clamp((int)Math.Round(baseValue * (1 - weight) + accentValue * weight), 0, 255);

        return UiColor(Blend(18, color.R), Blend(29, color.G), Blend(39, color.B), 255);
    }

    private static Windows.UI.Color ChromeTint(Windows.UI.Color surface, Windows.UI.Color accent, double weight, byte alpha)
    {
        byte Blend(byte baseValue, byte accentValue) =>
            (byte)Math.Clamp((int)Math.Round(baseValue * (1 - weight) + accentValue * weight), 0, 255);

        return UiColor(Blend(surface.R, accent.R), Blend(surface.G, accent.G), Blend(surface.B, accent.B), alpha);
    }

    private static ModeChromePalette ResolveModeChromePalette(string mode, bool isDark, byte alpha)
    {
        if (!isDark)
        {
            return mode switch
            {
                "neutral" => new(
                    UiColor(248, 248, 246), UiColor(255, 255, 253, alpha), UiColor(242, 242, 238, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(207, 209, 205), UiColor(228, 229, 224), UiColor(255, 255, 255, 246), UiColor(188, 190, 185),
                    UiColor(99, 137, 134), UiColor(99, 137, 134, 26), UiColor(180, 134, 42), UiColor(180, 134, 42, 20),
                    UiColor(134, 150, 148), UiColor(69, 93, 91), UiColor(118, 119, 113), UiColor(30, 31, 28)),
                "focus" => new(
                    UiColor(247, 248, 246), UiColor(250, 250, 248, alpha), UiColor(239, 242, 240, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(190, 198, 196), UiColor(224, 229, 226), UiColor(255, 255, 255, 246), UiColor(93, 130, 129),
                    UiColor(87, 176, 168), UiColor(87, 176, 168, 40), UiColor(255, 185, 53), UiColor(255, 185, 53, 34),
                    UiColor(235, 126, 74), UiColor(0, 96, 90), UiColor(86, 120, 116), UiColor(23, 32, 34)),
                "reading" => new(
                    UiColor(250, 247, 240), UiColor(255, 252, 246, alpha), UiColor(246, 241, 232, alpha), UiColor(255, 253, 249, alpha),
                    UiColor(213, 202, 184), UiColor(234, 226, 212), UiColor(255, 252, 246, 246), UiColor(176, 136, 79),
                    UiColor(216, 166, 93), UiColor(216, 166, 93, 42), UiColor(115, 162, 143), UiColor(115, 162, 143, 30),
                    UiColor(245, 204, 145), UiColor(118, 79, 28), UiColor(128, 108, 82), UiColor(36, 31, 25)),
                "creative" => new(
                    UiColor(249, 246, 251), UiColor(255, 251, 255, alpha), UiColor(244, 238, 249, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(208, 196, 218), UiColor(230, 224, 236), UiColor(255, 255, 255, 246), UiColor(153, 103, 173),
                    UiColor(205, 111, 214), UiColor(205, 111, 214, 42), UiColor(255, 185, 53), UiColor(255, 185, 53, 34),
                    UiColor(67, 219, 209), UiColor(112, 57, 137), UiColor(123, 96, 137), UiColor(34, 24, 42)),
                "research" => new(
                    UiColor(244, 249, 250), UiColor(249, 253, 253, alpha), UiColor(237, 246, 247, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(184, 209, 211), UiColor(219, 232, 233), UiColor(255, 255, 255, 246), UiColor(42, 137, 145),
                    UiColor(67, 180, 190), UiColor(67, 180, 190, 42), UiColor(255, 185, 53), UiColor(255, 185, 53, 30),
                    UiColor(112, 206, 215), UiColor(0, 94, 105), UiColor(82, 124, 128), UiColor(18, 35, 38)),
                "night" => new(
                    UiColor(235, 239, 247), UiColor(246, 248, 252, alpha), UiColor(232, 237, 247, alpha), UiColor(250, 252, 255, alpha),
                    UiColor(184, 194, 214), UiColor(218, 225, 239), UiColor(248, 250, 255, 246), UiColor(88, 111, 158),
                    UiColor(118, 145, 205), UiColor(118, 145, 205, 38), UiColor(154, 193, 255), UiColor(154, 193, 255, 28),
                    UiColor(82, 103, 158), UiColor(50, 72, 122), UiColor(98, 111, 138), UiColor(24, 31, 48)),
                _ => new(
                    UiColor(250, 248, 244), UiColor(255, 255, 255, alpha), UiColor(242, 239, 234, alpha), UiColor(255, 255, 255, alpha),
                    UiColor(214, 209, 200), UiColor(228, 224, 217), UiColor(255, 255, 255, 246), UiColor(198, 192, 182),
                    UiColor(176, 111, 0), UiColor(176, 111, 0, 38), UiColor(0, 128, 122), UiColor(0, 128, 122, 28),
                    UiColor(235, 126, 74), UiColor(149, 92, 0), UiColor(120, 113, 102), UiColor(31, 29, 26))
            };
        }

        return mode switch
        {
            "neutral" => new(
                UiColor(10, 17, 23), UiColor(16, 25, 31, alpha), UiColor(19, 30, 37, alpha), UiColor(24, 37, 44, alpha),
                UiColor(48, 62, 68), UiColor(31, 43, 49), UiColor(21, 34, 42, 242), UiColor(62, 82, 88),
                UiColor(107, 157, 150), UiColor(107, 157, 150, 28), UiColor(193, 148, 60), UiColor(193, 148, 60, 20),
                UiColor(128, 146, 142), UiColor(185, 210, 205), UiColor(168, 176, 171), UiColor(242, 246, 240)),
            "focus" => new(
                UiColor(8, 14, 20), UiColor(12, 19, 26, alpha), UiColor(14, 23, 30, alpha), UiColor(17, 27, 35, alpha),
                UiColor(52, 75, 84), UiColor(25, 39, 47), UiColor(15, 26, 34, 242), UiColor(63, 112, 119),
                UiColor(67, 219, 209), UiColor(67, 219, 209, 36), UiColor(255, 185, 53), UiColor(255, 185, 53, 24),
                UiColor(235, 126, 74), UiColor(128, 232, 220), UiColor(165, 189, 188), UiColor(255, 248, 234)),
            "reading" => new(
                UiColor(18, 24, 23), UiColor(24, 30, 28, alpha), UiColor(31, 38, 34, alpha), UiColor(37, 45, 40, alpha),
                UiColor(77, 81, 66), UiColor(45, 52, 44), UiColor(31, 40, 36, 242), UiColor(112, 96, 65),
                UiColor(245, 199, 122), UiColor(245, 199, 122, 34), UiColor(122, 197, 171), UiColor(122, 197, 171, 24),
                UiColor(255, 225, 162), UiColor(255, 218, 150), UiColor(203, 192, 166), UiColor(255, 248, 234)),
            "creative" => new(
                UiColor(16, 17, 31), UiColor(22, 24, 41, alpha), UiColor(31, 31, 52, alpha), UiColor(39, 37, 62, alpha),
                UiColor(84, 71, 107), UiColor(47, 45, 68), UiColor(29, 29, 48, 242), UiColor(115, 91, 138),
                UiColor(204, 118, 226), UiColor(204, 118, 226, 38), UiColor(255, 185, 53), UiColor(255, 185, 53, 28),
                UiColor(67, 219, 209), UiColor(238, 177, 255), UiColor(203, 188, 217), UiColor(255, 248, 234)),
            "research" => new(
                UiColor(8, 20, 25), UiColor(12, 29, 35, alpha), UiColor(15, 38, 45, alpha), UiColor(18, 47, 54, alpha),
                UiColor(50, 96, 103), UiColor(24, 57, 64), UiColor(13, 42, 50, 242), UiColor(57, 135, 146),
                UiColor(67, 219, 209), UiColor(67, 219, 209, 42), UiColor(255, 185, 53), UiColor(255, 185, 53, 26),
                UiColor(115, 231, 255), UiColor(128, 242, 255), UiColor(169, 207, 210), UiColor(255, 248, 234)),
            "night" => new(
                UiColor(4, 7, 12), UiColor(7, 11, 19, alpha), UiColor(10, 15, 25, alpha), UiColor(14, 20, 33, alpha),
                UiColor(42, 54, 80), UiColor(20, 27, 42), UiColor(11, 16, 27, 242), UiColor(78, 94, 132),
                UiColor(116, 145, 208), UiColor(116, 145, 208, 32), UiColor(154, 193, 255), UiColor(154, 193, 255, 22),
                UiColor(82, 103, 158), UiColor(192, 212, 255), UiColor(159, 174, 205), UiColor(235, 240, 255)),
            _ => new(
                UiColor(13, 24, 34), UiColor(20, 32, 42, alpha), UiColor(26, 39, 49, alpha), UiColor(34, 49, 58, alpha),
                UiColor(64, 84, 91), UiColor(36, 52, 60), UiColor(23, 40, 52, 242), UiColor(70, 97, 106),
                UiColor(255, 185, 53), UiColor(255, 185, 53, 48), UiColor(67, 219, 209), UiColor(67, 219, 209, 34),
                UiColor(235, 126, 74), UiColor(255, 230, 104), UiColor(195, 185, 165), UiColor(255, 248, 234))
        };
    }

    private (Windows.UI.Color Accent, Windows.UI.Color AccentSoft, Windows.UI.Color CoolAccent, Windows.UI.Color CoolAccentSoft, Windows.UI.Color Focus)
        ResolveAccentPalette(bool isDark, bool highContrast)
    {
        if (highContrast)
        {
            return (UiColor(255, 213, 0), UiColor(255, 213, 0, 68), UiColor(0, 255, 226), UiColor(0, 255, 226, 56), UiColor(255, 255, 0));
        }

        return (_uiSettings.AccentPalette ?? "lumora").ToLowerInvariant() switch
        {
            "ocean" => isDark
                ? (UiColor(92, 188, 255), UiColor(92, 188, 255, 48), UiColor(137, 226, 214), UiColor(137, 226, 214, 34), UiColor(128, 218, 255))
                : (UiColor(0, 103, 171), UiColor(0, 103, 171, 38), UiColor(0, 133, 127), UiColor(0, 133, 127, 28), UiColor(0, 96, 150)),
            "forest" => isDark
                ? (UiColor(128, 204, 124), UiColor(128, 204, 124, 48), UiColor(86, 208, 194), UiColor(86, 208, 194, 34), UiColor(175, 222, 132))
                : (UiColor(48, 122, 61), UiColor(48, 122, 61, 38), UiColor(0, 128, 119), UiColor(0, 128, 119, 28), UiColor(43, 107, 48)),
            "ember" => isDark
                ? (UiColor(235, 126, 74), UiColor(235, 126, 74, 48), UiColor(246, 206, 104), UiColor(246, 206, 104, 34), UiColor(255, 181, 108))
                : (UiColor(181, 73, 40), UiColor(181, 73, 40, 38), UiColor(166, 126, 0), UiColor(166, 126, 0, 28), UiColor(160, 59, 30)),
            _ => isDark
                ? (UiColor(255, 185, 53), UiColor(255, 185, 53, 48), UiColor(67, 219, 209), UiColor(67, 219, 209, 34), UiColor(255, 230, 104))
                : (UiColor(176, 111, 0), UiColor(176, 111, 0, 38), UiColor(0, 128, 122), UiColor(0, 128, 122, 28), UiColor(149, 92, 0))
        };
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

    private bool IsTranslucentChromeEnabled() => false;

    private void ApplyWindowBackdrop()
    {
        try
        {
            _uiSettings.WindowBackdrop = "solid";
            SystemBackdrop = null;
            // Le contenu web doit rester 100 % opaque : on ne teinte jamais la
            // fenetre. L'ancien effet translucide est retire de l'UI utilisateur.
            RemoveWindowLayeredAlpha();
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Window backdrop skipped: {error.GetType().Name}");
            SystemBackdrop = null;
            RemoveWindowLayeredAlpha();
        }

        ApplyWindowTitleBarColors();
        ApplyAccessibilitySettings();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    private const int GwlExstyle = -20;
    private const long WsExLayered = 0x00080000L;

    // Retire l'éventuel style « layered » d'une session précédente : le contenu web ne
    // doit jamais être translucide (voir ApplyWindowBackdrop).
    private void RemoveWindowLayeredAlpha()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (hwnd == nint.Zero) return;

            var exStyle = (long)GetWindowLongPtr(hwnd, GwlExstyle);
            SetWindowLongPtr(hwnd, GwlExstyle, (nint)(exStyle & ~WsExLayered));
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Window layered alpha reset skipped: {error.GetType().Name}");
        }
    }

    private static string NewTabShortcutsToText(IEnumerable<NewTabShortcut> shortcuts) =>
        Settings.NewTabShortcutText.ToText(shortcuts.Select(s => (s.Title, s.Url)));

    private static List<NewTabShortcut> ParseNewTabShortcuts(string text) =>
        Settings.NewTabShortcutText.Parse(text).Select(s => new NewTabShortcut(s.Title, s.Url)).ToList();

    private sealed record ModeChromePalette(
        Windows.UI.Color AppBackground,
        Windows.UI.Color Surface,
        Windows.UI.Color SurfaceAlt,
        Windows.UI.Color SurfaceRaised,
        Windows.UI.Color Stroke,
        Windows.UI.Color StrokeSoft,
        Windows.UI.Color AddressBackground,
        Windows.UI.Color AddressBorder,
        Windows.UI.Color Accent,
        Windows.UI.Color AccentSoft,
        Windows.UI.Color CoolAccent,
        Windows.UI.Color CoolAccentSoft,
        Windows.UI.Color WarmAccent,
        Windows.UI.Color Focus,
        Windows.UI.Color MutedText,
        Windows.UI.Color Text);
}
