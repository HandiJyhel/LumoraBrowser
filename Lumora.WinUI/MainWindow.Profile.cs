using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Système de profil ────────────────────────────────────────────────────

    private async Task InitializeLoginOverlayAsync()
    {
        await Task.Yield(); // retour sur le thread UI après la construction
        _profileEntries = LumoraProfileRegistry.Discover(LumoraConfig.Load());
        _userProfile = UserProfile.Load(_profile.ProfileFile, _profile.LegacyProfileFile);

        if (_userProfile is null && _profileEntries.Count > 0)
        {
            ShowProfilePicker();
        }
        else if (_profileEntries.Count > 1)
        {
            ShowProfilePicker();
        }
        else if (_userProfile is null)
        {
            ShowLoginPanel("create");
        }
        else
        {
            LoginWelcomeText.Text = $"Bonjour, {_userProfile.Name}";
            PinWelcomeText.Text = $"Bonjour, {_userProfile.Name}";
            ShowPinButton.Visibility = _userProfile.HasPinLogin ? Visibility.Visible : Visibility.Collapsed;

            if (_userProfile.HasPinLogin)
                ShowLoginPanel("pin");
            else
                ShowLoginPanel("password");
        }

        BrowserHost.IsHitTestVisible = false;
        LoginOverlay.Visibility = Visibility.Visible;
        LoginOverlay.Focus(FocusState.Programmatic);
        RefreshProfileSettings();
    }

    private void ShowLoginPanel(string mode)
    {
        ProfilePickerPanel.Visibility = Visibility.Collapsed;
        CreateProfilePanel.Visibility = Visibility.Collapsed;
        ProfileLocationPanel.Visibility = Visibility.Collapsed;
        LoginPasswordPanel.Visibility = Visibility.Collapsed;
        LoginPinPanel.Visibility = Visibility.Collapsed;
        MigrationPanel.Visibility = Visibility.Collapsed;
        LoginStatusText.Text = string.Empty;

        switch (mode)
        {
            case "create":    CreateProfilePanel.Visibility = Visibility.Visible; break;
            case "location":  ProfileLocationPanel.Visibility = Visibility.Visible; break;
            case "password":  LoginPasswordPanel.Visibility = Visibility.Visible; break;
            case "pin":       LoginPinPanel.Visibility = Visibility.Visible; break;
            case "migration": MigrationPanel.Visibility = Visibility.Visible; break;
        }
    }

    private void ShowProfilePicker()
    {
        _profileEntries = LumoraProfileRegistry.Discover(LumoraConfig.Load());
        ProfilePickerList.ItemsSource = _profileEntries.Select(entry => entry.Label).ToList();
        var activeIndex = Math.Max(0, _profileEntries.FindIndex(entry => entry.IsActive));
        ProfilePickerList.SelectedIndex = _profileEntries.Count == 0 ? -1 : activeIndex;
        ProfilePickerContinueButton.IsEnabled = _profileEntries.Count > 0;
        ShowLoginPanel("profiles");
        ProfilePickerPanel.Visibility = Visibility.Visible;
        LoginStatusText.Text = _profileEntries.Count == 0
            ? "Aucun profil local trouve."
            : "Choisissez le profil a ouvrir.";
    }

    private void ProfilePickerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ProfilePickerContinueButton.IsEnabled = ProfilePickerList.SelectedIndex >= 0;
    }

    private void ProfilePickerContinueButton_Click(object sender, RoutedEventArgs e)
    {
        var index = ProfilePickerList.SelectedIndex;
        if (index < 0 || index >= _profileEntries.Count) return;

        var entry = _profileEntries[index];
        var entryPaths = LumoraProfilePaths.FromDirectory(entry.ProfileDir);
        if (!entry.IsActive || !SameProfileDirectory(entryPaths.ProfileDir, _profile.ProfileDir))
        {
            SaveSelectedProfileAndRestart(entry);
            return;
        }

        _userProfile = UserProfile.Load(entryPaths.ProfileFile, entryPaths.LegacyProfileFile);
        if (_userProfile is null)
        {
            LoginStatusText.Text = "Profil actif illisible.";
            return;
        }

        LoginWelcomeText.Text = $"Bonjour, {_userProfile.Name}";
        PinWelcomeText.Text = $"Bonjour, {_userProfile.Name}";
        ShowPinButton.Visibility = _userProfile.HasPinLogin ? Visibility.Visible : Visibility.Collapsed;
        ShowLoginPanel(_userProfile.HasPinLogin ? "pin" : "password");
    }

    private void SaveSelectedProfileAndRestart(LumoraProfileEntry entry)
    {
        var config = LumoraConfig.Load();
        if (entry.IsCustom)
        {
            config.CustomProfilePath = entry.ProfileDir;
        }
        else
        {
            config.CustomProfilePath = null;
            config.ActiveProfileId = entry.Id;
        }

        config.Save();
        RestartApp();
    }

    private static bool SameProfileDirectory(string first, string second)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void CreateAnotherProfileButton_Click(object sender, RoutedEventArgs e)
    {
        _pendingUserProfile = null;
        _pendingProfilePassword = null;
        _pendingProfilePin = null;
        _pendingRecoveryKey = null;
        _pendingProfileDir = null;
        _pendingProfileId = null;
        _profileCreationTarget = null;
        ProfileNameBox.Text = string.Empty;
        CreatePasswordBox.Password = string.Empty;
        ConfirmPasswordBox.Password = string.Empty;
        EnablePinSwitch.IsOn = false;
        PinSetupBox.Password = string.Empty;
        ConfirmPinBox.Password = string.Empty;
        ShowLoginPanel("create");
    }

    private void ShowProfilePickerButton_Click(object sender, RoutedEventArgs e) =>
        ShowProfilePicker();

    private void ShowProfileOverlay()
    {
        BrowserHost.IsHitTestVisible = false;
        LoginOverlay.Visibility = Visibility.Visible;
        LoginOverlay.Focus(FocusState.Programmatic);
    }

    private void ShowProfilePickerOverlay()
    {
        ShowProfileOverlay();
        ShowProfilePicker();
    }

    private void ShowCreateProfileOverlay()
    {
        ShowProfileOverlay();
        CreateAnotherProfileButton_Click(this, new RoutedEventArgs());
    }

    private void OpenProfilePickerFromSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowProfilePickerOverlay();
    }

    private void CreateAnotherProfileFromSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCreateProfileOverlay();
    }

    private void UpdateProfileFlyoutUi()
    {
        if (_isGuestMode)
        {
            ProfileFlyoutTitleText.Text = "Mode invite";
            ProfileFlyoutSubtitleText.Text = "Aucune donnee persistante n'est gardee a la fermeture tant qu'aucun profil n'est choisi.";
            ProfileFlyoutCurrentUserText.Text = "Session sans profil";
            ProfileFlyoutCurrentStateText.Text = "Creer un utilisateur ou choisir un profil local pour retrouver vos donnees ensuite.";
            return;
        }

        var currentName = _userProfile?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(currentName))
        {
            ProfileFlyoutTitleText.Text = currentName;
            ProfileFlyoutSubtitleText.Text = "Ce menu regroupe les actions de profil, de compte local et de gestion d'utilisateurs.";
            ProfileFlyoutCurrentUserText.Text = $"Connecte : {currentName}";
            ProfileFlyoutCurrentStateText.Text = "Changer d'utilisateur, ouvrir les parametres du profil ou en creer un nouveau.";
            return;
        }

        ProfileFlyoutTitleText.Text = "Profil Lumora";
        ProfileFlyoutSubtitleText.Text = "Choisissez un profil local pour separer vos donnees, sessions et reglages.";
        ProfileFlyoutCurrentUserText.Text = "Aucun profil actif";
        ProfileFlyoutCurrentStateText.Text = "Creer ou choisir un profil local pour garder vos donnees sur cet appareil.";
    }

    private void ProfileFlyoutSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ProfileStatusFlyout.Hide();
        OpenProfileSettings();
    }

    private void ProfileFlyoutSwitchButton_Click(object sender, RoutedEventArgs e)
    {
        ProfileStatusFlyout.Hide();
        ShowProfilePickerOverlay();
        StatusText.Text = "Choisissez un autre utilisateur Lumora.";
    }

    private void ProfileFlyoutCreateButton_Click(object sender, RoutedEventArgs e)
    {
        ProfileStatusFlyout.Hide();
        ShowCreateProfileOverlay();
        StatusText.Text = "Creation d'un nouvel utilisateur Lumora.";
    }

    private void DismissLoginOverlay()
    {
        LoginOverlay.Visibility = Visibility.Collapsed;
        BrowserHost.IsHitTestVisible = true;
        _pinBuffer = string.Empty;
        _pinFailCount = 0;
        RefreshProfileSettings();
        UpdateProfileStatus();
        InitSessionTimer();
        _ = ClearHomeSearchFieldAfterDelayAsync(blur: true);
        // À la connexion : rapatrier ce que Chromium avait encore, puis vider son coffre
        // (stockage 100% maison → vault.lumora est le seul magasin).
        _ = MigrateAndClearBrowserPasswordsAsync();
        if (!_isGuestMode && !_uiSettings.SetupWizardCompleted)
            ShowSetupWizard();
    }

    private void InitSessionTimer()
    {
        _sessionTimer?.Stop();
        _sessionTimer = null;
        UnhookSystemLockEvents();
        if (_isGuestMode || _uiSettings.SessionTimeoutMinutes <= 0) return;
        _sessionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(_uiSettings.SessionTimeoutMinutes)
        };
        _sessionTimer.Tick += SessionTimer_Tick;
        _sessionTimer.Start();
        // Meme interrupteur "Jamais" (0 min) : quand l'utilisateur desactive le
        // verrouillage auto, on ne verrouille pas non plus a la veille/au lock.
        HookSystemLockEvents();
    }

    private void ResetSessionTimer()
    {
        if (_sessionTimer is null) return;
        _sessionTimer.Stop();
        _sessionTimer.Start();
    }

    private void SessionTimer_Tick(object? sender, object e)
    {
        _sessionTimer?.Stop();
        if (_isGuestMode || _userProfile is null) return;
        if (LoginOverlay.Visibility == Visibility.Visible) return;

        // Regarder une video/ecouter de l'audio dans un onglet est un usage actif,
        // meme sans toucher au clavier/a la souris : ne pas verrouiller tant qu'un
        // onglet (actif ou en arriere-plan) joue du son. Meme principe que "empecher
        // la mise en veille" pendant une lecture, cote OS/lecteurs video classiques.
        if (IsAnyTabPlayingAudio())
        {
            _sessionTimer?.Start();
            return;
        }

        LockSessionNow("Session verrouillee automatiquement.");
    }

    // Verrouillage effectif du coffre + ecran de re-login. Point d'entree commun au
    // timer d'inactivite ET aux evenements systeme (veille, verrouillage Windows).
    // Contrairement au tick d'inactivite, ceci ne tient PAS compte de l'audio : si
    // Windows se verrouille ou s'endort, l'utilisateur a quitte son poste, une video
    // qui continue de jouer derriere l'ecran de verrouillage ne doit rien empecher.
    private void LockSessionNow(string statusMessage)
    {
        if (_isGuestMode || _userProfile is null) return;
        if (LoginOverlay.Visibility == Visibility.Visible) return; // deja verrouille

        // Verrouillage réel : on purge la clé du coffre de la mémoire, pas seulement
        // l'écran. Le ré-login (mot de passe ou PIN) la reconstruit.
        _vault.Lock();
        ShowLoginPanel(_userProfile.HasPinLogin ? "pin" : "password");
        LoginStatusText.Text = statusMessage;
        BrowserHost.IsHitTestVisible = false;
        LoginOverlay.Visibility = Visibility.Visible;
        LoginOverlay.Focus(FocusState.Programmatic);
    }

    private bool IsAnyTabPlayingAudio() =>
        _tabs.Any(tab => tab.View?.CoreWebView2?.IsDocumentPlayingAudio == true);

    // ── Verrouillage immediat a la veille et au verrouillage de session Windows ──
    //
    // SystemEvents leve ses evenements sur un thread hors UI et conserve une
    // reference forte statique vers ses abonnes : on marshale vers le thread UI et
    // on se desabonne imperativement a la fermeture (sinon fuite de la fenetre et
    // crash au prochain evenement).

    private void HookSystemLockEvents()
    {
        if (_systemLockHooked || _isGuestMode) return;
        Microsoft.Win32.SystemEvents.SessionSwitch += OnSystemSessionSwitch;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnSystemPowerModeChanged;
        _systemLockHooked = true;
    }

    private void UnhookSystemLockEvents()
    {
        if (!_systemLockHooked) return;
        Microsoft.Win32.SystemEvents.SessionSwitch -= OnSystemSessionSwitch;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnSystemPowerModeChanged;
        _systemLockHooked = false;
    }

    private void OnSystemSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason != Microsoft.Win32.SessionSwitchReason.SessionLock) return;
        DispatcherQueue.TryEnqueue(() =>
            LockSessionNow("Coffre verrouille : session Windows verrouillee."));
    }

    private void OnSystemPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode != Microsoft.Win32.PowerModes.Suspend) return;
        DispatcherQueue.TryEnqueue(() =>
            LockSessionNow("Coffre verrouille : mise en veille."));
    }

    private void SessionTimeoutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (SessionTimeoutCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var minutes))
        {
            _uiSettings.SessionTimeoutMinutes = minutes;
            _uiSettings.Save(_profile.UiSettingsFile);
            InitSessionTimer();
        }
    }

    private void UpdateProfileStatus()
    {
        if (_isGuestMode)
        {
            ProfileStatusText.Text = "Mode invité";
            ProfileStatusText.Foreground = new SolidColorBrush(
                new Windows.UI.Color { A = 255, R = 200, G = 130, B = 30 });
        }
        else if (_userProfile is not null)
        {
            ProfileStatusText.Text = $"Connecté : {_userProfile.Name}";
            ProfileStatusText.ClearValue(TextBlock.ForegroundProperty);
        }
        else
        {
            ProfileStatusText.Text = "Non connecté";
            ProfileStatusText.ClearValue(TextBlock.ForegroundProperty);
        }
        RefreshAvatarUi();
    }

    private void RefreshProfileSettings()
    {
        if (_userProfile is null)
        {
            ProfileNameDisplay.Text = "Aucun profil configure.";
            ProfilePinSwitch.IsEnabled = false;
        }
        else
        {
            ProfileNameDisplay.Text = $"Connecte en tant que : {_userProfile.Name}\nDossier : {_profile.ProfileDir}";
            ProfilePinSwitch.IsEnabled = true;
            _suppressUiSettingsSave = true;
            ProfilePinSwitch.IsOn = _userProfile.HasPinLogin;
            _suppressUiSettingsSave = false;
        }
        RefreshAvatarUi();

        RefreshProfileManagementPanel();
    }

    private void RefreshProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProfileManagementPanel();
        StatusText.Text = "Liste des profils actualisee.";
    }

    private void RefreshProfileManagementPanel()
    {
        ProfileManagementPanel.Children.Clear();
        _profileEntries = LumoraProfileRegistry.Discover(LumoraConfig.Load());

        if (_profileEntries.Count == 0)
        {
            ProfileManagementPanel.Children.Add(new TextBlock
            {
                Text = "Aucun profil local detecte.",
                Opacity = 0.65
            });
            return;
        }

        foreach (var entry in _profileEntries)
        {
            ProfileManagementPanel.Children.Add(BuildProfileManagementCard(entry));
        }
    }

    private UIElement BuildProfileManagementCard(LumoraProfileEntry entry)
    {
        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = entry.IsActive ? $"{entry.Name} (actif)" : entry.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 0);
        header.Children.Add(title);

        var type = new TextBlock
        {
            Text = entry.IsCustom ? "Emplacement personnalise" : "Profil local",
            Opacity = 0.62,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(type, 1);
        header.Children.Add(type);

        var path = new TextBlock
        {
            Text = entry.ProfileDir,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.62,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var switchButton = new Button
        {
            Content = "Basculer",
            IsEnabled = !entry.IsActive
        };
        switchButton.Click += (_, _) => SwitchToProfile(entry);
        actions.Children.Add(switchButton);

        var openButton = new Button { Content = "Ouvrir le dossier" };
        openButton.Click += (_, _) => OpenProfileDirectory(entry);
        actions.Children.Add(openButton);

        var quarantineButton = new Button
        {
            Content = "Mettre en quarantaine",
            IsEnabled = !entry.IsActive,
            Foreground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 220, G = 70, B = 70 })
        };
        quarantineButton.Click += async (_, _) => await QuarantineProfileAsync(entry);
        actions.Children.Add(quarantineButton);

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(path);
        body.Children.Add(actions);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = body
        };
    }

    private void SwitchToProfile(LumoraProfileEntry entry)
    {
        if (entry.IsActive) return;
        SaveSelectedProfileAndRestart(entry);
    }

    private void OpenProfileDirectory(LumoraProfileEntry entry)
    {
        try
        {
            if (!Directory.Exists(entry.ProfileDir))
            {
                StatusText.Text = "Dossier de profil introuvable.";
                RefreshProfileManagementPanel();
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = entry.ProfileDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Ouverture du dossier impossible : {ex.Message}";
        }
    }

    private async Task QuarantineProfileAsync(LumoraProfileEntry entry)
    {
        if (entry.IsActive)
        {
            StatusText.Text = "Le profil actif ne peut pas etre mis en quarantaine.";
            return;
        }

        var nameBox = new TextBox
        {
            PlaceholderText = entry.Name,
            MinWidth = 320
        };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = "Le profil sera deplace dans un dossier de quarantaine. Il ne sera pas detruit immediatement.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = entry.ProfileDir,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.62,
            FontSize = 12
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Tapez le nom du profil pour confirmer : {entry.Name}",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(nameBox);

        var dialog = new ContentDialog
        {
            Title = "Mettre ce profil en quarantaine ?",
            Content = panel,
            PrimaryButtonText = "Mettre en quarantaine",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (!string.Equals(nameBox.Text.Trim(), entry.Name, StringComparison.CurrentCultureIgnoreCase))
        {
            StatusText.Text = "Confirmation incorrecte : profil conserve.";
            return;
        }

        try
        {
            var target = LumoraProfileRegistry.QuarantineProfile(entry);
            RefreshProfileManagementPanel();
            StatusText.Text = $"Profil mis en quarantaine : {target}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Mise en quarantaine impossible : {ex.Message}";
        }
    }

    private void EnablePinSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        var show = EnablePinSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        PinSetupBox.Visibility = show;
        ConfirmPinBox.Visibility = show;
    }

    private void SkipProfileButton_Click(object sender, RoutedEventArgs e)
    {
        _isGuestMode = true;
        _bookmarks.SetGuestMode(true);
        _notes.SetGuestMode(true);
        _annotations.SetGuestMode(true);
        _historyPanel.Store.SetGuestMode(true);
        _historyPanel.Downloads.SetGuestMode(true);
        _semanticIndex.SetGuestMode(true);
        _webApps.SetGuestMode(true);
        _savedTabGroups.SetGuestMode(true);
        _siteRelocations.SetGuestMode(true);
        _savedGroupIds.Clear();
        DismissLoginOverlay();
        // Reconstruire l'UI avec les stores vides
        ReloadBookmarks();
        _historyPanel.Items.Clear();
        _suppressTabSave = true;
        foreach (var item in BrowserTabs.TabItems.OfType<TabViewItem>().ToList())
            BrowserTabs.TabItems.Remove(item);
        _tabs.Clear();
        // Les onglets du profil précédent ne doivent pas être restaurables en mode invité.
        _closedTabs.Clear();
        AddTab("Nouvel onglet", "lumora://accueil", select: true);
        _suppressTabSave = false;
        Title = $"Lumora {Version} — Mode invité";
    }

    private async void CreateProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var name = ProfileNameBox.Text.Trim();
        var pw   = CreatePasswordBox.Password;
        var pw2  = ConfirmPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(name))
        { LoginStatusText.Text = "Veuillez entrer un prenom ou pseudo."; return; }

        if (pw.Length < 8)
        { LoginStatusText.Text = "Le mot de passe doit faire au moins 8 caracteres."; return; }

        if (pw != pw2)
        { LoginStatusText.Text = "Les mots de passe ne correspondent pas."; return; }

        string? pin = null;
        if (EnablePinSwitch.IsOn)
        {
            var pinVal  = PinSetupBox.Password;
            var pinConf = ConfirmPinBox.Password;
            if (pinVal.Length != 6 || !pinVal.All(char.IsDigit))
            { LoginStatusText.Text = "Le code PIN doit contenir exactement 6 chiffres."; return; }
            if (pinVal != pinConf)
            { LoginStatusText.Text = "Les codes PIN ne correspondent pas."; return; }
            pin = pinVal;
        }

        var recoveryKey = UserProfile.GenerateRecoveryKey();
        _pendingUserProfile = await Task.Run(() => UserProfile.Create(name, pw, pin).WithRecoveryKey(recoveryKey));
        _pendingProfilePassword = pw;
        _pendingProfilePin = pin;
        _pendingRecoveryKey = recoveryKey;
        _pendingProfileId = _profileEntries.Count == 0 && UserProfile.Load(_profile.ProfileFile, _profile.LegacyProfileFile) is null
            ? "default"
            : LumoraProfileRegistry.CreateProfileId(name);
        _profileCreationTarget = null;
        ProfileLocationPathText.Text = LumoraProfilePaths.ForProfileId(_pendingProfileId).ProfileDir;
        ShowLoginPanel("location");
    }

    private async void ProfileLocationContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUserProfile is null) return;

        if (_pendingProfileDir is not null)
        {
            var cfg = LumoraConfig.Load();
            cfg.CustomProfilePath = _pendingProfileDir;
            cfg.Save();
        }
        else
        {
            var cfg = LumoraConfig.Load();
            cfg.CustomProfilePath = null;
            cfg.ActiveProfileId = LumoraProfilePaths.NormalizeProfileId(_pendingProfileId);
            cfg.Save();
        }

        var targetProfile = _pendingProfileDir is not null
            ? LumoraProfilePaths.FromDirectory(_pendingProfileDir)
            : LumoraProfilePaths.ForProfileId(_pendingProfileId);
        _profileCreationTarget = targetProfile;
        _userProfile = _pendingUserProfile;
        _pendingUserProfile = null;
        _userProfile.Save(targetProfile.ProfileFile);

        if (Directory.Exists(targetProfile.NavigationDir))
            foreach (var f in Directory.GetFiles(targetProfile.NavigationDir, "*.nova"))
                File.Delete(f);

        _restartRequired = !string.Equals(targetProfile.ProfileDir, _profile.ProfileDir, StringComparison.OrdinalIgnoreCase);

        // Couplage coffre : si on ne redemarre pas (dossier par defaut), _vault pointe
        // deja sur le bon fichier et on le cle avec le mot de passe du profil. En cas de
        // redemarrage (dossier custom), le couplage se fera au login suivant, quand
        // _vault sera reconstruit sur le nouveau chemin.
        if (!_restartRequired && _pendingProfilePassword is not null)
        {
            _vault.EnsureUnlockedWith(_pendingProfilePassword);
            // Activer le déverrouillage par PIN si un PIN a été défini à la création.
            if (_pendingProfilePin is not null)
                _vault.EnablePinUnlock(_pendingProfilePin);
        }

        if (_pendingRecoveryKey is not null && _pendingProfilePassword is not null)
        {
            var targetVault = _restartRequired ? new VaultStore(targetProfile.VaultFile) : _vault;
            targetVault.EnsureUnlockedWith(_pendingProfilePassword);
            targetVault.SetRecoveryKey(_pendingRecoveryKey);
            if (_pendingProfilePin is not null)
                targetVault.EnablePinUnlock(_pendingProfilePin);
            await ShowRecoveryKeyDialogAsync(_pendingRecoveryKey);
        }
        _pendingProfilePassword = null;
        _pendingProfilePin = null;
        _pendingRecoveryKey = null;
        _pendingProfileId = null;
        _pendingProfileDir = null;

        ReloadBookmarks();
        _historyPanel.Items.Clear();
        ShowMigrationOrDismiss();
    }

    private void ShowMigrationOrDismiss()
    {
        var discovered = BrowserImportSource.Discover()
            .GroupBy(s => s.Browser, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Count).First(),
                          StringComparer.OrdinalIgnoreCase);

        _migrationEntries =
        [
            new("Google Chrome",  discovered.GetValueOrDefault("Google Chrome")),
            new("Microsoft Edge", discovered.GetValueOrDefault("Microsoft Edge")),
            new("Brave",          discovered.GetValueOrDefault("Brave")),
            new("Opera",          discovered.GetValueOrDefault("Opera")),
            new("Opera GX",       discovered.GetValueOrDefault("Opera GX")),
            new("Vivaldi",        discovered.GetValueOrDefault("Vivaldi")),
            new("Firefox",        discovered.GetValueOrDefault("Firefox")),
        ];

        MigrationSourcesList.ItemsSource = _migrationEntries.Select(e => e.Label).ToList();
        var firstAvailable = _migrationEntries.FindIndex(e => e.Source is not null);
        MigrationSourcesList.SelectedIndex = firstAvailable >= 0 ? firstAvailable : 0;
        ShowLoginPanel("migration");
    }

    private void MigrationImportButton_Click(object sender, RoutedEventArgs e)
    {
        var idx = MigrationSourcesList.SelectedIndex;
        if (idx < 0 || idx >= _migrationEntries.Count)
        {
            if (_restartRequired) RestartApp(); else DismissLoginOverlay();
            return;
        }

        var entry = _migrationEntries[idx];
        if (entry.Source is null)
        {
            LoginStatusText.Text = $"{entry.Name} n'est pas detecte sur ce PC.";
            return;
        }

        ImportAndFinish(entry.Source.ReadTree(), entry.Source.Browser);
    }

    private async void MigrationImportHtmlButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add(".html");
        picker.FileTypeFilter.Add(".htm");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            var content = await FileIO.ReadTextAsync(file);
            var tree = BookmarkImportTree.FromHtml(content, Path.GetFileNameWithoutExtension(file.Name));
            ImportAndFinish(tree, "HTML");
        }
        catch (Exception ex)
        {
            LoginStatusText.Text = $"Erreur lors de l'import : {ex.Message}";
        }
    }

    private void ImportAndFinish(BookmarkImportTree tree, string browserName)
    {
        if (_restartRequired)
        {
            // _bookmarks pointe vers l'ancien chemin → écrire dans le nouveau dossier cible
            var targetPaths = _profileCreationTarget ?? LumoraProfilePaths.Default();
            var tempStore = new BookmarkStore(targetPaths.BookmarksFile, null, null);
            tempStore.MergeImport(tree);
            RestartApp();
        }
        else
        {
            var imported = _bookmarks.MergeImport(tree);
            ReloadBookmarks();
            StatusText.Text = $"Import {browserName} : {imported} favoris importes.";
            DismissLoginOverlay();
        }
    }

    private void MigrationSkipButton_Click(object sender, RoutedEventArgs e)
    {
        if (_restartRequired) RestartApp();
        else DismissLoginOverlay();
    }

    private static void RestartApp()
    {
        var exe = Environment.ProcessPath;
        if (exe is not null)
            System.Diagnostics.Process.Start(exe);
        Application.Current.Exit();
    }

    private async void ChooseProfileLocationButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;
        _pendingProfileDir = folder.Path;
        ProfileLocationPathText.Text = folder.Path;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var pw = LoginPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(pw))
        { LoginStatusText.Text = "Entrez votre mot de passe."; return; }

        var ok = await Task.Run(() => _userProfile!.VerifyPassword(pw));
        if (ok)
        {
            // Le mot de passe du profil déverrouille (ou active la première fois) le coffre.
            _vault.EnsureUnlockedWith(pw);
            LoginPasswordBox.Password = string.Empty;
            DismissLoginOverlay();
        }
        else
        {
            LoginStatusText.Text = "Mot de passe incorrect.";
        }
    }

    private void LoginPasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) LoginButton_Click(sender, e);
    }

    private async void ForgotPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_userProfile is null) return;
        if (!_userProfile.HasRecoveryKey)
        {
            LoginStatusText.Text = "Aucune cle de recuperation n'est configuree pour ce profil.";
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Recuperer le profil",
            PrimaryButtonText = "Recuperer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var panel = new StackPanel { Spacing = 10 };
        var recoveryBox = new TextBox
        {
            Header = "Cle de recuperation",
            PlaceholderText = "NOVA-......-......-......",
            MinWidth = 320
        };
        var newBox = new PasswordBox
        {
            Header = "Nouveau mot de passe",
            PlaceholderText = "Minimum 8 caracteres",
            MinWidth = 320
        };
        var confirmBox = new PasswordBox
        {
            Header = "Confirmer le nouveau mot de passe",
            MinWidth = 320
        };
        panel.Children.Add(recoveryBox);
        panel.Children.Add(newBox);
        panel.Children.Add(confirmBox);
        panel.Children.Add(new TextBlock
        {
            Text = "La cle de recuperation reste locale. Sans cette cle, Lumora ne peut pas contourner le chiffrement.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.68,
            FontSize = 12
        });
        dialog.Content = panel;

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var recoveryKey = recoveryBox.Text;
        if (!_userProfile.VerifyRecoveryKey(recoveryKey))
        {
            LoginStatusText.Text = "Cle de recuperation incorrecte.";
            return;
        }
        if (newBox.Password.Length < 8)
        {
            LoginStatusText.Text = "Le nouveau mot de passe doit faire au moins 8 caracteres.";
            return;
        }
        if (newBox.Password != confirmBox.Password)
        {
            LoginStatusText.Text = "Les mots de passe ne correspondent pas.";
            return;
        }
        if (!_vault.UnlockWithRecoveryKey(recoveryKey))
        {
            LoginStatusText.Text = "La cle est valide pour le profil, mais le coffre n'a pas de recuperation active.";
            return;
        }

        _vault.SetMasterPassword(newBox.Password);
        _userProfile = _userProfile.WithNewPassword(newBox.Password).WithoutPin();
        _userProfile.Save(_profile.ProfileFile);
        LoginPasswordBox.Password = string.Empty;
        LoginStatusText.Text = "Mot de passe reinitialise avec la cle de recuperation.";
        DismissLoginOverlay();
    }

    private void ShowPinButton_Click(object sender, RoutedEventArgs e) =>
        ShowLoginPanel("pin");

    private void ShowPasswordButton_Click(object sender, RoutedEventArgs e) =>
        ShowLoginPanel("password");

    private void PinDigit_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag?.ToString() ?? string.Empty;
        _ = ProcessPinInputAsync(tag);
    }

    private void RootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        ResetSessionTimer();
        if (e.Key == Windows.System.VirtualKey.K && IsControlKeyDown() &&
            LoginOverlay.Visibility != Visibility.Visible &&
            SetupWizardOverlay.Visibility != Visibility.Visible)
        {
            e.Handled = true;
            ToggleCommandPalette();
            return;
        }

        if (CommandPaletteOverlay.Visibility == Visibility.Visible &&
            e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            HideCommandPalette();
            return;
        }

        if (IsImmersiveFullScreenActive() && e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            ExitImmersiveFullScreenFromKeyboard();
            return;
        }

        if (LoginPinPanel.Visibility != Visibility.Visible) return;

        string? tag = e.Key switch
        {
            var k when k >= Windows.System.VirtualKey.Number0 &&
                       k <= Windows.System.VirtualKey.Number9
                => ((int)(k - Windows.System.VirtualKey.Number0)).ToString(),
            var k when k >= Windows.System.VirtualKey.NumberPad0 &&
                       k <= Windows.System.VirtualKey.NumberPad9
                => ((int)(k - Windows.System.VirtualKey.NumberPad0)).ToString(),
            Windows.System.VirtualKey.Back   => "back",
            Windows.System.VirtualKey.Escape => "clear",
            _ => null
        };

        if (tag is null) return;
        e.Handled = true;
        _ = ProcessPinInputAsync(tag);
    }

    private async Task ProcessPinInputAsync(string tag)
    {
        if (_pinFailCount >= 5)
        {
            LoginStatusText.Text = "Trop de tentatives. Utilisez votre mot de passe.";
            ShowLoginPanel("password");
            return;
        }

        if (tag == "back")
        {
            if (_pinBuffer.Length > 0) _pinBuffer = _pinBuffer[..^1];
        }
        else if (tag == "clear")
        {
            _pinBuffer = string.Empty;
        }
        else if (tag.Length == 1 && char.IsDigit(tag[0]) && _pinBuffer.Length < 6)
        {
            _pinBuffer += tag;
        }

        PinDotsDisplay.Text = string.Concat(Enumerable.Repeat("● ", _pinBuffer.Length)) +
                              string.Concat(Enumerable.Repeat("○ ", 6 - _pinBuffer.Length));
        _ = ClearHomeSearchFieldAsync(blur: true);

        if (_pinBuffer.Length == 6)
        {
            var pin = _pinBuffer;
            _pinBuffer = string.Empty;
            PinDotsDisplay.Text = "○ ○ ○ ○ ○ ○";

            var ok = await Task.Run(() => _userProfile!.VerifyPin(pin));
            if (ok)
            {
                // Le PIN déverrouille aussi le coffre (si le déverrouillage PIN est activé).
                _vault.UnlockWithPin(pin);
                DismissLoginOverlay();
            }
            else
            {
                _pinFailCount++;
                LoginStatusText.Text = _pinFailCount >= 5
                    ? "Trop de tentatives. Utilisez votre mot de passe."
                    : $"Code PIN incorrect. ({_pinFailCount}/5)";
            }
        }
    }

    private async void ChangeProfileNameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_userProfile is null) return;
        var dialog = new ContentDialog
        {
            Title = "Nouveau prenom ou pseudo",
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            XamlRoot = Content.XamlRoot
        };
        var box = new TextBox { Text = _userProfile.Name, MinWidth = 260 };
        dialog.Content = box;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var newName = box.Text.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return;
        _userProfile = _userProfile with { Name = newName };
        _userProfile.Save(_profile.ProfileFile);
        RefreshProfileSettings();
        UpdateProfileStatus();
        StatusText.Text = "Nom de profil mis a jour.";
    }

    private async void ChangeProfilePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_userProfile is null) return;

        var dialog = new ContentDialog
        {
            Title = "Changer le mot de passe",
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            XamlRoot = Content.XamlRoot
        };
        var panel = new StackPanel { Spacing = 10 };
        var oldBox  = new PasswordBox { PlaceholderText = "Mot de passe actuel", MinWidth = 260 };
        var newBox  = new PasswordBox { PlaceholderText = "Nouveau mot de passe (min. 8 car.)", MinWidth = 260 };
        var confBox = new PasswordBox { PlaceholderText = "Confirmer", MinWidth = 260 };
        panel.Children.Add(oldBox);
        panel.Children.Add(newBox);
        panel.Children.Add(confBox);
        dialog.Content = panel;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        if (!await Task.Run(() => _userProfile.VerifyPassword(oldBox.Password)))
        { StatusText.Text = "Mot de passe actuel incorrect."; return; }
        if (newBox.Password.Length < 8)
        { StatusText.Text = "Le nouveau mot de passe doit faire au moins 8 caracteres."; return; }
        if (newBox.Password != confBox.Password)
        { StatusText.Text = "Les mots de passe ne correspondent pas."; return; }

        _userProfile = await Task.Run(() => _userProfile.WithNewPassword(newBox.Password));
        _userProfile.Save(_profile.ProfileFile);

        // Re-clé du coffre avec le nouveau mot de passe : on déverrouille d'abord avec
        // l'ancien (pour ne pas perdre les identifiants), puis on re-chiffre.
        if (_vault.HasMasterPassword)
        {
            if (_vault.EnsureUnlockedWith(oldBox.Password))
                _vault.SetMasterPassword(newBox.Password);
        }
        else
        {
            _vault.EnsureUnlockedWith(newBox.Password);
        }

        // Le re-chiffrement du coffre a invalidé l'emballage PIN. Si un PIN existe, on le
        // ré-emballe immédiatement en le redemandant, pour garder l'ouverture du coffre par PIN.
        if (_userProfile.HasPinLogin && !_vault.IsLocked)
        {
            var pin = await PromptMasterPasswordAsync(
                "Confirmez votre code PIN pour garder l'ouverture du coffre par PIN", confirm: false);
            if (!string.IsNullOrWhiteSpace(pin) && _userProfile.VerifyPin(pin))
                _vault.EnablePinUnlock(pin);
        }

        StatusText.Text = "Mot de passe du profil mis a jour.";
    }

    private async void CreateRecoveryKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_userProfile is null) return;
        if (!await RequireVaultAccessAsync())
        {
            StatusText.Text = "Cle de recuperation non creee : acces au coffre refuse.";
            return;
        }

        var recoveryKey = UserProfile.GenerateRecoveryKey();
        _userProfile = _userProfile.WithRecoveryKey(recoveryKey);
        _userProfile.Save(_profile.ProfileFile);
        if (!_vault.SetRecoveryKey(recoveryKey))
        {
            StatusText.Text = "Impossible de rattacher la cle de recuperation au coffre.";
            return;
        }

        await ShowRecoveryKeyDialogAsync(recoveryKey);
        StatusText.Text = "Nouvelle cle de recuperation activee.";
    }

    private async Task ShowRecoveryKeyDialogAsync(string recoveryKey)
    {
        var keyBox = new TextBox
        {
            Text = recoveryKey,
            IsReadOnly = true,
            MinWidth = 360,
            FontFamily = new FontFamily("Consolas")
        };
        var copyButton = new Button
        {
            Content = "Copier la cle",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        copyButton.Click += (_, _) =>
            CopySecretToClipboard(recoveryKey, "Cle de recuperation copiee.", clearAfterSeconds: 0);

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Notez cette cle maintenant. Elle permet de recuperer le profil et le coffre si le mot de passe est oublie. Lumora ne peut pas la retrouver a votre place.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(keyBox);
        panel.Children.Add(copyButton);

        var dialog = new ContentDialog
        {
            Title = "Cle de recuperation Lumora",
            Content = panel,
            PrimaryButtonText = "J'ai note la cle",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async void ProfilePinSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave || _userProfile is null) return;

        if (ProfilePinSwitch.IsOn)
        {
            var dialog = new ContentDialog
            {
                Title = "Definir le code PIN",
                PrimaryButtonText = "Enregistrer",
                CloseButtonText = "Annuler",
                XamlRoot = Content.XamlRoot
            };
            var panel   = new StackPanel { Spacing = 10 };
            var pinBox  = new PasswordBox { PlaceholderText = "Code PIN (6 chiffres)", MaxLength = 6, MinWidth = 220 };
            var confBox = new PasswordBox { PlaceholderText = "Confirmer", MaxLength = 6, MinWidth = 220 };
            panel.Children.Add(pinBox);
            panel.Children.Add(confBox);
            dialog.Content = panel;
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                _suppressUiSettingsSave = true;
                ProfilePinSwitch.IsOn = false;
                _suppressUiSettingsSave = false;
                return;
            }
            if (pinBox.Password.Length != 6 || !pinBox.Password.All(char.IsDigit))
            { StatusText.Text = "Le code PIN doit contenir exactement 6 chiffres."; _suppressUiSettingsSave = true; ProfilePinSwitch.IsOn = false; _suppressUiSettingsSave = false; return; }
            if (pinBox.Password != confBox.Password)
            { StatusText.Text = "Les codes PIN ne correspondent pas."; _suppressUiSettingsSave = true; ProfilePinSwitch.IsOn = false; _suppressUiSettingsSave = false; return; }

            _userProfile = await Task.Run(() => _userProfile.WithPin(pinBox.Password));
            // Le PIN peut désormais ouvrir le coffre (si celui-ci est déverrouillé maintenant).
            if (!_vault.IsLocked) _vault.EnablePinUnlock(pinBox.Password);
        }
        else
        {
            _userProfile = _userProfile.WithoutPin();
            _vault.DisablePinUnlock();
        }

        _userProfile.Save(_profile.ProfileFile);
        StatusText.Text = ProfilePinSwitch.IsOn ? "Code PIN active." : "Code PIN desactive.";
    }

    private async void ResetProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            Title = "Reinitialiser le profil ?",
            Content = "Cette action supprime definitivement toutes vos donnees : favoris, historique, onglets, parametres, coffre et identifiants de connexion. Cette operation est irreversible.",
            PrimaryButtonText = "Reinitialiser",
            CloseButtonText = "Annuler",
            XamlRoot = Content.XamlRoot
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        // Supprimer tout le dossier de profil (navigation, vault, favicons, profile.nova)
        if (Directory.Exists(_profile.ProfileDir))
            Directory.Delete(_profile.ProfileDir, recursive: true);

        // Remettre la configuration de profil dans un état local coherent.
        var cfg = LumoraConfig.Load();
        if (!string.IsNullOrWhiteSpace(cfg.CustomProfilePath)
            && string.Equals(Path.GetFullPath(cfg.CustomProfilePath), Path.GetFullPath(_profile.ProfileDir), StringComparison.OrdinalIgnoreCase))
        {
            cfg.ActiveProfileId = "default";
        }

        cfg.CustomProfilePath = null;
        cfg.Save();

        var exe = Environment.ProcessPath;
        if (exe is not null)
            System.Diagnostics.Process.Start(exe);
        Application.Current.Exit();
    }
}
