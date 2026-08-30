using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private int _wizardStep;

    private void ShowSetupWizard()
    {
        _wizardStep = 0;
        _suppressUiSettingsSave = true;
        WizardSearchGoogle.IsChecked    = _uiSettings.SearchEngine is not ("duckduckgo" or "brave" or "bing");
        WizardSearchDuckDuckGo.IsChecked = _uiSettings.SearchEngine == "duckduckgo";
        WizardSearchBrave.IsChecked     = _uiSettings.SearchEngine == "brave";
        WizardSearchBing.IsChecked      = _uiSettings.SearchEngine == "bing";
        SelectWizardTabsOrientation();
        SelectWizardUsageMode(_uiSettings.UsageMode);
        SelectWizardModules();
        ResetPendingAvatarChange();
        RefreshWizardAvatarUi();
        WizardAvatarPendingText.Visibility = Visibility.Collapsed;
        SelectWizardSessionTimeout();
        _suppressUiSettingsSave = false;
        UpdateWizardStep();
        SetupWizardOverlay.Visibility = Visibility.Visible;
    }

    private void UpdateWizardStep()
    {
        WizardStepIndicator.Text = $"Étape {_wizardStep + 1} / 8";
        WizardStep0.Visibility = _wizardStep == 0 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep1.Visibility = _wizardStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep2.Visibility = _wizardStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep3.Visibility = _wizardStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep4.Visibility = _wizardStep == 4 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep5.Visibility = _wizardStep == 5 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep6.Visibility = _wizardStep == 6 ? Visibility.Visible : Visibility.Collapsed;
        WizardStep7.Visibility = _wizardStep == 7 ? Visibility.Visible : Visibility.Collapsed;
        WizardPrevButton.IsEnabled = _wizardStep > 0;
        WizardNextButton.Content = _wizardStep == 7 ? "Terminer" : "Suivant";

        if (_wizardStep == 7)
        {
            WizardSummarySearch.Text = "Moteur de recherche : " + _uiSettings.SearchEngine switch
            {
                "duckduckgo" => "DuckDuckGo",
                "brave"      => "Brave Search",
                "bing"       => "Bing",
                _            => "Google",
            };
            WizardSummaryTabs.Text = "Disposition des onglets : "
                + (WizardTabsVertical.IsChecked == true ? "Verticaux" : "Horizontaux");
            WizardSummaryUsage.Text = "Mode d'usage : " + UsageModeDisplayName(_uiSettings.UsageMode);
            WizardSummaryModules.Text = _uiSettings.PinnedModuleIds.Count == 0
                ? "Modules visibles : aucun module impose"
                : "Modules visibles : " + string.Join(", ", _uiSettings.PinnedModuleIds.Select(ModuleDisplayName));
            WizardSummaryAvatar.Text = "Photo de profil : "
                + (HasPendingAvatarChange() ? (_pendingAvatarRemoval ? "aucune" : "ajoutée") : (FindAvatarFile() is null ? "aucune" : "déjà en place"));
            WizardSummaryLock.Text = "Verrouillage automatique : " + _uiSettings.SessionTimeoutMinutes switch
            {
                0   => "jamais",
                10  => "après 10 minutes",
                60  => "après 1 heure",
                300 => "après 5 heures",
                var m => $"après {m} minutes",
            };
        }
    }

    private void WizardPrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_wizardStep > 0)
        {
            _wizardStep--;
            UpdateWizardStep();
        }
    }

    private void WizardNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_wizardStep < 7)
        {
            _wizardStep++;
            UpdateWizardStep();
        }
        else
        {
            FinishWizard();
        }
    }

    // Etape 5 (photo de profil) : reutilise le flux existant du panneau Compte
    // (_pendingAvatarSourcePath/_pendingAvatarRemoval, MainWindow.Avatar.cs),
    // juste avec sa propre previsualisation puisque le panneau Compte n'est
    // pas visible pendant l'assistant.
    private void RefreshWizardAvatarUi()
    {
        var pendingPath = !_pendingAvatarRemoval ? _pendingAvatarSourcePath : null;
        var shownPath = pendingPath ?? (_pendingAvatarRemoval ? null : FindAvatarFile());
        if (shownPath is null)
        {
            WizardAvatarPreviewBrush.ImageSource = null;
            WizardAvatarPlaceholder.Visibility = Visibility.Visible;
        }
        else
        {
            WizardAvatarPreviewBrush.ImageSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(shownPath, UriKind.Absolute));
            WizardAvatarPlaceholder.Visibility = Visibility.Collapsed;
        }
        WizardRemoveAvatarButton.Visibility = shownPath is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void WizardChangeAvatarButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        foreach (var ext in AvatarExtensions) picker.FileTypeFilter.Add(ext);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            _pendingAvatarSourcePath = file.Path;
            _pendingAvatarRemoval = false;
            RefreshWizardAvatarUi();
            WizardAvatarPendingText.Text = "Photo prête, elle sera enregistrée à la fin de l'assistant.";
            WizardAvatarPendingText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            WizardAvatarPendingText.Text = $"Erreur lors de l'aperçu de l'image : {ex.Message}";
            WizardAvatarPendingText.Visibility = Visibility.Visible;
        }
    }

    private void WizardRemoveAvatarButton_Click(object sender, RoutedEventArgs e)
    {
        _pendingAvatarSourcePath = null;
        _pendingAvatarRemoval = true;
        RefreshWizardAvatarUi();
        WizardAvatarPendingText.Text = "Aucune photo ne sera enregistrée.";
        WizardAvatarPendingText.Visibility = Visibility.Visible;
    }

    // Etape 6 (verrouillage automatique) : meme reglage que SessionTimeoutCombo
    // (Parametres > Compte), pose ici en plus pour ne pas rester a decouvrir.
    private void SelectWizardSessionTimeout()
    {
        foreach (var item in WizardSessionTimeoutCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag?.ToString() == _uiSettings.SessionTimeoutMinutes.ToString())
            {
                WizardSessionTimeoutCombo.SelectedItem = item;
                return;
            }
        }
        WizardSessionTimeoutCombo.SelectedIndex = 0;
    }

    private void WizardSessionTimeoutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (WizardSessionTimeoutCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var minutes))
        {
            _uiSettings.SessionTimeoutMinutes = minutes;
        }
    }

    // Import direct d'une sauvegarde depuis l'assistant (etape "Profil
    // existant ?") : reutilise le meme flux que le bouton Parametres >
    // Stockage. En cas de succes, le profil importe a deja son moteur de
    // recherche/mode d'usage/disposition d'onglets - inutile de continuer
    // a les demander, l'assistant se termine directement.
    private async void WizardImportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (await RunImportBackupFlowAsync())
        {
            await FinishWizardAfterImportAsync();
        }
    }

    // "J'ai deja un profil sur ce disque" (session "Compte et ouverture",
    // 2026-08-14) : a la difference de l'import ci-dessus (qui COPIE des
    // donnees dans le profil vierge tout juste cree via
    // ProfileLocationContinueButton_Click), ceci repointe simplement l'app
    // vers un dossier de profil deja complet ailleurs sur le disque - meme
    // mecanisme que ChangeFolderButton_Click/ProfileLocationContinueButton_Click
    // (config.CustomProfilePath). Rien n'est copie, deplace ni supprime : le
    // profil vierge cree a l'etape precedente reste intact sur le disque, et
    // reapparaitra simplement comme profil non-actif au prochain demarrage
    // (LumoraProfileRegistry.Discover scanne tout ProfilesRoot()) - c'est le
    // choix le plus sur trouve pour cette fonctionnalite : aucune suppression/
    // quarantaine automatique a faire reposer sur une hypothese ("ce profil
    // vient forcement d'etre cree") qui ne tient plus si l'assistant est
    // rouvert pour un profil existant n'ayant jamais fini le wizard.
    // Coeur partage avec CreateProfileFindExistingButton_Click (icone sur l'ecran
    // "Bienvenue", MainWindow.Profile.cs) - voir AdoptExistingProfileFolderAsync.
    private async void WizardExistingProfileButton_Click(object sender, RoutedEventArgs e) =>
        await AdoptExistingProfileFolderAsync(mentionBlankProfileLeftIntact: true);

    private async Task FinishWizardAfterImportAsync()
    {
        // ui-settings.lumora vient d'etre remplace par celui de la sauvegarde :
        // recharge en memoire avant de fermer l'assistant, sinon FinishWizard()
        // ecraserait les valeurs importees avec celles encore en memoire (issues
        // du profil neuf, jamais reconciliees avec le fichier tout juste importe).
        _uiSettings = UiSettings.Load(_profile.UiSettingsFile, _profile.LegacyUiSettingsFile);
        _uiSettings.SetupWizardCompleted = true;
        _uiSettings.Save(_profile.UiSettingsFile);
        SetupWizardOverlay.Visibility = Visibility.Collapsed;
        ApplyUiSettings();
        RefreshNovaHomePages();

        var dialog = new ContentDialog
        {
            Title = "Sauvegarde importée",
            Content = "Vos favoris, onglets, coffre et réglages ont été restaurés. Redémarrez Lumora pour que tout s'applique correctement.",
            PrimaryButtonText = "Fermer Lumora",
            CloseButtonText = "Plus tard",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            Application.Current.Exit();
    }

    private void WizardTabsOrientation_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (sender is RadioButton rb && rb.Tag is string position)
        {
            _uiSettings.TabStripPosition = position;
            _uiSettings.VerticalTabsEnabled = position is "left" or "right";
        }
    }

    private void SelectWizardTabsOrientation()
    {
        var position = string.IsNullOrWhiteSpace(_uiSettings.TabStripPosition)
            ? (_uiSettings.VerticalTabsEnabled ? "left" : "top")
            : _uiSettings.TabStripPosition;
        var vertical = position is "left" or "right";
        WizardTabsHorizontal.IsChecked = !vertical;
        WizardTabsVertical.IsChecked = vertical;
    }

    private void WizardSearch_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (sender is RadioButton rb && rb.Tag is string engine)
            _uiSettings.SearchEngine = engine;
    }

    private void WizardUsage_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (sender is RadioButton rb && rb.Tag is string usageMode)
        {
            _uiSettings.UsageMode = usageMode;
            if (string.Equals(usageMode, "neutral", StringComparison.OrdinalIgnoreCase))
            {
                _uiSettings.PinnedModuleIds.Clear();
                SelectWizardModules();
            }
        }
    }

    private void WizardModule_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.PinnedModuleIds = WizardModuleCheckboxes()
            .Where(box => box.IsChecked == true)
            .Select(box => box.Tag?.ToString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();
    }

    private void SelectWizardUsageMode(string? usageMode)
    {
        var mode = (usageMode ?? "neutral").ToLowerInvariant();
        WizardUsageNeutral.IsChecked = mode == "neutral";
        WizardUsageBalanced.IsChecked = mode == "balanced";
        WizardUsageFocus.IsChecked = mode == "focus";
        WizardUsageReading.IsChecked = mode == "reading";
        WizardUsageCreative.IsChecked = mode == "creative";
        WizardUsageResearch.IsChecked = mode == "research";
        WizardUsageNight.IsChecked = mode == "night";
    }

    private void SelectWizardModules()
    {
        var pinned = _uiSettings.PinnedModuleIds;
        foreach (var box in WizardModuleCheckboxes())
        {
            var moduleId = box.Tag?.ToString() ?? string.Empty;
            box.IsChecked = pinned.Contains(moduleId, StringComparer.OrdinalIgnoreCase);
        }
    }

    private IEnumerable<CheckBox> WizardModuleCheckboxes()
    {
        yield return WizardModuleReader;
        yield return WizardModuleNotes;
        yield return WizardModuleReadAloud;
        yield return WizardModuleVideoDownload;
        yield return WizardModuleSearchAssist;
        yield return WizardModuleTranslate;
        yield return WizardModuleWebApps;
        yield return WizardModuleDictation;
    }

    private void FinishWizard()
    {
        ApplyPendingAvatarChange();
        _uiSettings.SetupWizardCompleted = true;
        _uiSettings.Save(_profile.UiSettingsFile);
        SetupWizardOverlay.Visibility = Visibility.Collapsed;
        ApplyUiSettings();
        RefreshNovaHomePages();
    }

    private static string UsageModeDisplayName(string? usageMode) =>
        (usageMode ?? "neutral").ToLowerInvariant() switch
        {
            "neutral" => "Neutre",
            "focus" => "Focus",
            "reading" => "Lecture",
            "creative" => "Creation",
            "research" => "Recherche",
            "night" => "Nuit",
            _ => "Equilibre"
        };

    private static string ModuleDisplayName(string moduleId) =>
        moduleId switch
        {
            "reader" => "lecture",
            "notes" => "notes",
            "readAloud" => "lecture vocale",
            "videoDownload" => "telechargement video",
            "searchAssist" => "assistant recherche",
            "translate" => "traduction",
            "webApps" => "applications web",
            "dictation" => "dictee",
            "detachVideo" => "video detachee",
            "rss" => "flux RSS",
            "readingLens" => "loupe de lecture",
            _ => moduleId
        };
}
