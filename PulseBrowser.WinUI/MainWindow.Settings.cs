using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PulseBrowser.WinUI;

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
            var config = PulseConfig.Load();
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
                         "PulseBrowser", "profiles", "default"));
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
        picker.SuggestedFileName = $"pulse-backup-{DateTime.Now:yyyyMMdd-HHmm}";
        picker.FileTypeChoices.Add("Sauvegarde Pulse", new List<string> { ".pulsebackup" });

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            PulseBackup.Export(file.Path, password, _profile);
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
        picker.FileTypeFilter.Add(".pulsebackup");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var password = await PromptBackupPasswordAsync("Importer une sauvegarde", confirm: false);
        if (password is null) return;

        try
        {
            PulseBackup.Import(file.Path, password, _profile);
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
                AddTab("Accueil Pulse", "pulse://accueil", select: true);
        }
        else if (mode == "custom" && !string.IsNullOrWhiteSpace(_uiSettings.StartupUrl))
        {
            AddTab(DisplayTitle(_uiSettings.StartupUrl), _uiSettings.StartupUrl, select: true);
        }
        else
        {
            AddTab("Accueil Pulse", "pulse://accueil", select: true);
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
        _compactModeEnabled = !_compactModeEnabled;
        _suppressUiSettingsSave = true;
        CompactModeSwitch.IsOn = _compactModeEnabled;
        _suppressUiSettingsSave = false;
        ApplyCompactModeLayout();
        SaveUiSettings();
        StatusText.Text = _compactModeEnabled ? "Interface compacte activee." : "Interface compacte desactivee.";
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

    private void WindowBackdropCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.WindowBackdrop = SelectedWindowBackdropMode();
        ApplyWindowBackdrop();
        SaveUiSettings();
        StatusText.Text = _uiSettings.WindowBackdrop switch
        {
            "mica" => "Effet translucide Mica active.",
            "acrylic" => "Effet translucide Acrylic active.",
            _ => "Effet translucide desactive."
        };
    }

    private void NewTabTitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.NewTabTitle = string.IsNullOrWhiteSpace(NewTabTitleBox.Text)
            ? "Pulse"
            : NewTabTitleBox.Text.Trim();
        _uiSettings.Save(_profile.UiSettingsFile);
    }

    private void NewTabShortcutsSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.NewTabShortcutsVisible = NewTabShortcutsSwitch.IsOn;
        _uiSettings.Save(_profile.UiSettingsFile);
    }

    private void NewTabShortcutsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.NewTabShortcuts = ParseNewTabShortcuts(NewTabShortcutsBox.Text);
        _uiSettings.Save(_profile.UiSettingsFile);
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
            SelectWindowBackdropMode(_uiSettings.WindowBackdrop);
            CommandPaletteEnabledSwitch.IsOn = _uiSettings.CommandPaletteEnabled;
            CommandPaletteWebPagesSwitch.IsOn = _uiSettings.CommandPaletteOpenFromWebPages;
            CommandPaletteTextFieldsSwitch.IsOn = _uiSettings.CommandPaletteOpenFromTextFields;
            NewTabTitleBox.Text = string.IsNullOrWhiteSpace(_uiSettings.NewTabTitle) ? "Pulse" : _uiSettings.NewTabTitle;
            NewTabShortcutsSwitch.IsOn = _uiSettings.NewTabShortcutsVisible;
            NewTabShortcutsBox.Text = NewTabShortcutsToText(_uiSettings.NewTabShortcuts);
            AccessibilityHighContrastSwitch.IsOn = _uiSettings.AccessibilityHighContrast;
            AccessibilityLargeTextSwitch.IsOn = _uiSettings.AccessibilityLargeText;
            AccessibilityReduceMotionSwitch.IsOn = _uiSettings.AccessibilityReduceMotion;
            AccessibilityVisibleFocusSwitch.IsOn = _uiSettings.AccessibilityVisibleFocus;
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
            ParameterCleanerSwitch.IsOn = _uiSettings.ParameterCleanerEnabled;
            HttpsEnforcerSwitch.IsOn    = _uiSettings.HttpsEnforcerEnabled;
            CnameUncloakerSwitch.IsOn   = _uiSettings.CnameUncloakerEnabled;
            CosmeticFilterSwitch.IsOn   = _uiSettings.CosmeticFilterEnabled;
            ConsentManagerSwitch.IsOn   = _uiSettings.ConsentManagerEnabled;
            RenderPrivacyWhitelist();
        }
        finally
        {
            _suppressUiSettingsSave = false;
        }
    }

    private void ApplyVerticalTabsLayout()
    {
        _suppressTabNavigation = true;
        try
        {
            TopTabsRow.Height = _verticalTabsEnabled ? new GridLength(0) : new GridLength(38);
            BrowserTabs.Visibility = _verticalTabsEnabled ? Visibility.Collapsed : Visibility.Visible;
            VerticalTabsRail.Visibility = _verticalTabsEnabled ? Visibility.Visible : Visibility.Collapsed;
            VerticalTabsResizeThumb.Visibility = _verticalTabsEnabled ? Visibility.Visible : Visibility.Collapsed;
            ApplyVerticalTabsWidth();
            RenderVerticalTabs();
            EnforceMinWindowWidth();
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
        _uiSettings.WindowBackdrop = SelectedWindowBackdropMode();
        _uiSettings.VerticalTabsWidth = Math.Clamp(_verticalTabsExpandedWidth, VerticalTabsMinExpandedWidth, VerticalTabsMaxWidth);
        _uiSettings.CommandPaletteEnabled = CommandPaletteEnabledSwitch.IsOn;
        _uiSettings.CommandPaletteOpenFromWebPages = CommandPaletteWebPagesSwitch.IsOn;
        _uiSettings.CommandPaletteOpenFromTextFields = CommandPaletteTextFieldsSwitch.IsOn;
        if (SearchEngineCombo.SelectedItem is ComboBoxItem engineItem)
            _uiSettings.SearchEngine = engineItem.Tag?.ToString() ?? "google";
        _uiSettings.NewTabTitle = string.IsNullOrWhiteSpace(NewTabTitleBox.Text) ? "Pulse" : NewTabTitleBox.Text.Trim();
        _uiSettings.NewTabShortcutsVisible = NewTabShortcutsSwitch.IsOn;
        _uiSettings.NewTabShortcuts = ParseNewTabShortcuts(NewTabShortcutsBox.Text);
        _uiSettings.AccessibilityHighContrast = AccessibilityHighContrastSwitch.IsOn;
        _uiSettings.AccessibilityLargeText = AccessibilityLargeTextSwitch.IsOn;
        _uiSettings.AccessibilityReduceMotion = AccessibilityReduceMotionSwitch.IsOn;
        _uiSettings.AccessibilityVisibleFocus = AccessibilityVisibleFocusSwitch.IsOn;
        if (SessionTimeoutCombo.SelectedItem is ComboBoxItem timeoutItem &&
            int.TryParse(timeoutItem.Tag?.ToString(), out var tm))
            _uiSettings.SessionTimeoutMinutes = tm;
        _uiSettings.Save(_profile.UiSettingsFile);
        // StartupMode, StartupUrl et SearchEngine sont aussi mis à jour directement dans leurs handlers
    }

    private void ApplyAccessibilitySettings()
    {
        var highContrast = _uiSettings.AccessibilityHighContrast;
        var largeText = _uiSettings.AccessibilityLargeText;
        var visibleFocus = _uiSettings.AccessibilityVisibleFocus;
        var translucent = IsTranslucentChromeEnabled() && !highContrast;

        RootShell.Background = translucent
            ? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            : new SolidColorBrush(highContrast ? UiColor(0, 0, 0) : UiColor(34, 33, 31));
        SetBrush("PulseChromeSurfaceBrush", highContrast ? UiColor(0, 0, 0) : UiColor(38, 37, 34, translucent ? (byte)226 : (byte)255));
        SetBrush("PulseChromeSurfaceAltBrush", highContrast ? UiColor(18, 18, 18) : UiColor(45, 43, 40, translucent ? (byte)232 : (byte)255));
        SetBrush("PulseChromeStrokeBrush", highContrast ? UiColor(255, 255, 255) : UiColor(59, 56, 52));
        SetBrush("PulseChromeStrokeSoftBrush", highContrast ? UiColor(190, 190, 190) : UiColor(48, 46, 42));
        SetBrush("PulseAddressBackgroundBrush", highContrast ? UiColor(255, 255, 255) : UiColor(52, 49, 45, translucent ? (byte)236 : (byte)255));
        SetBrush("PulseAddressBorderBrush", highContrast ? UiColor(255, 255, 255) : UiColor(81, 76, 69));
        SetBrush("PulseAddressForegroundBrush", highContrast ? UiColor(0, 0, 0) : UiColor(245, 241, 234));
        SetBrush("PulseTextMutedBrush", highContrast ? UiColor(255, 255, 255) : UiColor(169, 163, 154));
        SetBrush("PulseAccentBrush", highContrast ? UiColor(255, 213, 0) : UiColor(225, 120, 24));

        var mainFontSize = largeText ? 16 : 14;
        var smallFontSize = largeText ? 14 : 12;
        AddressBox.FontSize = mainFontSize;
        CommandPaletteSearchBox.FontSize = mainFontSize;
        CommandPaletteHintText.FontSize = smallFontSize;
        StatusText.FontSize = smallFontSize;

        var focusThickness = visibleFocus ? new Thickness(2) : new Thickness(1);
        AddressBox.BorderThickness = focusThickness;
        CommandPaletteSearchBox.BorderThickness = focusThickness;
    }

    private void SetBrush(string key, Windows.UI.Color color)
    {
        if (RootShell.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private void ApplyVerticalTabsWidth()
    {
        VerticalTabsRail.Width = _verticalTabsCompact ? VerticalTabsCompactWidth : _verticalTabsExpandedWidth;
        ApplyVerticalTabsPresentation();
    }

    private void ApplyVerticalTabsPresentation()
    {
        VerticalTabsRail.Padding = _verticalTabsCompact ? new Thickness(7, 8, 7, 8) : new Thickness(8);
        VerticalTabsActionsPanel.Orientation = _verticalTabsCompact ? Orientation.Vertical : Orientation.Horizontal;
        VerticalTabsNewTabButton.Width = _verticalTabsCompact ? 36 : double.NaN;
        VerticalTabsNewTabButton.HorizontalAlignment = _verticalTabsCompact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        VerticalTabsNewTabLabel.Visibility = _verticalTabsCompact ? Visibility.Collapsed : Visibility.Visible;
        VerticalTabsCompactButton.Width = 36;
        VerticalTabsCompactButton.HorizontalAlignment = HorizontalAlignment.Center;
        VerticalTabsCompactButton.Content = new SymbolIcon(_verticalTabsCompact ? Symbol.OpenPane : Symbol.ClosePane);
        ToolTipService.SetToolTip(VerticalTabsCompactButton, _verticalTabsCompact ? "Agrandir les onglets verticaux" : "Reduire les onglets verticaux");
    }

    private void ApplyCompactModeLayout()
    {
        NavigationRow.Height = new GridLength(_compactModeEnabled ? 38 : 42);
        NavigationToolbar.Padding = _compactModeEnabled ? new Thickness(10, 2, 10, 3) : new Thickness(10, 4, 10, 5);
        CompactModeButton.Content = new SymbolIcon(_compactModeEnabled ? Symbol.BackToWindow : Symbol.FullScreen);
        ToolTipService.SetToolTip(CompactModeButton, _compactModeEnabled ? "Quitter l'interface compacte" : "Interface compacte");
        ApplyBookmarksBarVisibility();
        ApplyVerticalTabsLayout();
    }

    private void ApplyBookmarksBarVisibility()
    {
        var hiddenByCompactChoice = _compactModeEnabled && CompactModeHideBookmarksSwitch.IsOn;
        var visible = BookmarksBarSwitch.IsOn && !hiddenByCompactChoice;
        BookmarksRow.Height = visible ? new GridLength(28) : new GridLength(0);
        BookmarksBarRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool IsTranslucentChromeEnabled() =>
        !_uiSettings.AccessibilityHighContrast && (_uiSettings.WindowBackdrop is "mica" or "acrylic");

    private string SelectedWindowBackdropMode()
    {
        return WindowBackdropCombo.SelectedItem is ComboBoxItem item
            ? item.Tag?.ToString() ?? "solid"
            : "solid";
    }

    private void SelectWindowBackdropMode(string? mode)
    {
        var normalized = mode is "mica" or "acrylic" ? mode : "solid";
        foreach (var item in WindowBackdropCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag?.ToString() == normalized)
            {
                WindowBackdropCombo.SelectedItem = item;
                return;
            }
        }

        WindowBackdropCombo.SelectedIndex = 0;
    }

    private void ApplyWindowBackdrop()
    {
        try
        {
            SystemBackdrop = IsTranslucentChromeEnabled()
                ? _uiSettings.WindowBackdrop switch
                {
                    "mica" => new MicaBackdrop(),
                    "acrylic" => new DesktopAcrylicBackdrop(),
                    _ => null
                }
                : null;
            // Le contenu web doit rester 100 % opaque : on ne teinte JAMAIS toute la
            // fenêtre. La translucidité vient du seul backdrop Mica/Acrylic, qui n'affecte
            // que le chrome (le WebView2 dessine par-dessus, opaque).
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

}
