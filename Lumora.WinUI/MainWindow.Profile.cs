using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
// Alias plutot qu'un "using Microsoft.UI.Xaml.Shapes;" complet : ce namespace
// contient aussi une classe Path, qui entrerait en collision avec
// System.IO.Path deja utilise partout dans ce fichier (Path.Combine,
// Path.GetFullPath...).
using Ellipse = Microsoft.UI.Xaml.Shapes.Ellipse;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // Regle renforcee (2026-08-13, remplace "8 caracteres, aucune complexite") :
    // le mot de passe de compte sert desormais aussi de cle de chiffrement pour
    // les sauvegardes .lumorabackup (reutilise tel quel, voir
    // MainWindow.SettingsStorage.cs) - 8 caracteres sans contrainte etait
    // insuffisant pour proteger un fichier qui peut contenir TOUS les mots de
    // passe/cartes de l'utilisateur. Utilisee aux 3 points de creation/
    // changement de mot de passe (creation de compte, changement volontaire,
    // reinitialisation par cle de recuperation).
    private static bool IsAccountPasswordStrongEnough(string password, out string error)
    {
        if (password.Length < 12)
        {
            error = "Le mot de passe doit faire au moins 12 caractères.";
            return false;
        }

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
        {
            error = "Le mot de passe doit contenir au moins un caractère spécial.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    // ── Système de profil ────────────────────────────────────────────────────

    private async Task InitializeLoginOverlayAsync()
    {
        await Task.Yield(); // retour sur le thread UI après la construction
        var config = LumoraConfig.Load();
        _profileEntries = LumoraProfileRegistry.Discover(config);
        _userProfile = UserProfile.Load(_profile.ProfileFile, _profile.LegacyProfileFile);

        // Lance via GuestProcessLauncher (--guest) : _profile pointe deja vers le
        // dossier ephemere dedie a cette session, entrer en invite tout de suite
        // sans jamais montrer le picker/l'ecran de connexion.
        if (_pendingGuestLaunch)
        {
            EnterGuestMode();
            return;
        }

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
            // Aucun profil n'a jamais existe sur cette machine (le seul cas ou
            // cette branche est atteinte, cf. les deux conditions precedentes) :
            // premier lancement reel. Montre les slides de bienvenue une seule
            // fois (LumoraConfig.WelcomeSlidesShown, global - aucun profil
            // n'existe encore pour porter ce flag) avant de proposer la
            // creation du premier profil. La suite (ShowLoginPanel("create") +
            // ShowLoginOverlayChrome()) est differee a CompleteWelcomeSlides()
            // (MainWindow.WelcomeSlides.cs) pour ne jamais superposer les deux
            // overlays.
            if (!config.WelcomeSlidesShown)
            {
                ShowWelcomeSlides(isReplay: false);
                RefreshProfileSettings();
                return;
            }
            ShowLoginPanel("create");
        }
        else if (!_userProfile.HasAccountPassword)
        {
            // Profil sans mot de passe (2026-08-13) : aucun ecran de connexion a
            // l'ouverture, entree directe - voir UserProfile.HasAccountPassword.
            DismissLoginOverlay();
            return;
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

        ShowLoginOverlayChrome();
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
            case "create":
                CreateProfilePanel.Visibility = Visibility.Visible;
                // Titre contextuel : "Bienvenue" uniquement au tout premier lancement
                // (aucun profil n'a jamais existe sur cette machine - meme condition que
                // la branche "premier lancement reel" d'InitializeLoginOverlayAsync).
                // Sinon ("Créer un autre profil" depuis le sélecteur ou les Réglages, un
                // profil existe deja) : "Nouveau profil", pour ne pas prétendre que c'est
                // un premier lancement alors qu'il y a déjà un utilisateur sur ce poste.
                var isFirstEverProfile = _userProfile is null && _profileEntries.Count == 0;
                CreateProfileTitleText.Text = isFirstEverProfile ? "Bienvenue" : "Nouveau profil";
                CreateProfileSubtitleText.Text = isFirstEverProfile
                    ? "Créez votre profil pour protéger vos données."
                    : "Ce profil aura ses propres favoris, historique et coffre, séparés des autres.";
                // Annuler n'a de sens que s'il existe reellement une session ou un profil
                // vers lequel revenir ; le lien invite n'a de sens que si aucun profil
                // n'est deja choisi cette session (sinon "continuer sans profil" swappe
                // silencieusement une session active en cours vers l'invite). _vault.IsLocked
                // exclu explicitement (trouve le 2026-07-29 en implementant ProfilePickerCancelButton) :
                // une session verrouillee (LockSessionNow) garde _userProfile non-null, mais
                // Annuler ne doit jamais rouvrir la navigation sans repasser par le mot de passe/PIN.
                CreateProfileCancelButton.Visibility =
                    (_userProfile is not null && !_vault.IsLocked) || _profileEntries.Count > 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                CreateProfileGuestLink.Visibility =
                    _userProfile is null ? Visibility.Visible : Visibility.Collapsed;
                break;
            case "location":  ProfileLocationPanel.Visibility = Visibility.Visible; break;
            case "password":  LoginPasswordPanel.Visibility = Visibility.Visible; break;
            case "pin":       LoginPinPanel.Visibility = Visibility.Visible; break;
            case "migration": MigrationPanel.Visibility = Visibility.Visible; break;
        }

        FocusActiveLoginPanel(mode);
    }

    // Trouve en usage reel le 2026-07-22 : il fallait cliquer une premiere fois
    // dans la fenetre avant de pouvoir taper son code PIN au lancement.
    // LoginOverlay.Focus(FocusState.Programmatic) (appele a l'affichage de
    // l'overlay) ne fait rien : LoginOverlay est un Grid, pas un Control, et
    // seuls les Control peuvent reellement recevoir le focus clavier dans ce
    // projet (voir CanReceiveProgrammaticFocus). Sans focus reel sur un
    // Control, aucun evenement KeyDown ne remonte jusqu'a RootKeyDown - d'ou
    // le PIN "muet" tant qu'on n'a pas clique sur quelque chose (un clic donne
    // le focus a un Control reel, apres quoi le clavier fonctionne).
    // DispatcherQueue.TryEnqueue : le focus doit etre pose apres que la mise
    // en page ait rendu le panneau visible, pas dans la meme passe que le
    // changement de Visibility.
    private void FocusActiveLoginPanel(string mode)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (mode)
            {
                case "create":    ProfileNameBox.Focus(FocusState.Programmatic); break;
                case "location":  ChooseProfileLocationButton.Focus(FocusState.Programmatic); break;
                case "password":  LoginPasswordBox.Focus(FocusState.Programmatic); break;
                case "pin":       PinDigit1Button.Focus(FocusState.Programmatic); break;
                case "migration": MigrationSourcesList.Focus(FocusState.Programmatic); break;
                case "profiles":  ProfilePickerList.Focus(FocusState.Programmatic); break;
            }
        });
    }

    // Retourne a la session en cours si elle existe (creation d'un profil
    // supplementaire depuis les Parametres), sinon au selecteur de profil s'il y a
    // au moins un profil detecte sur la machine. Le bouton n'est visible que dans
    // l'un de ces deux cas (voir ShowLoginPanel) : au tout premier lancement, sans
    // aucun profil existant, il n'y a rien vers quoi annuler.
    private void CreateProfileCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_userProfile is not null && !_vault.IsLocked)
        {
            DismissLoginOverlay();
        }
        else if (_profileEntries.Count > 0)
        {
            ShowProfilePickerOverlay();
        }
    }

    private void ShowProfilePicker()
    {
        _profileEntries = LumoraProfileRegistry.Discover(LumoraConfig.Load());
        ProfilePickerList.ItemsSource = _profileEntries
            .Select(entry => new ProfilePickerItem(
                entry.Name,
                entry.IsCustom ? "Emplacement personnalisé" : "Profil local",
                entry.AvatarPath,
                entry.IsActive))
            .ToList();
        var activeIndex = Math.Max(0, _profileEntries.FindIndex(entry => entry.IsActive));
        ProfilePickerList.SelectedIndex = _profileEntries.Count == 0 ? -1 : activeIndex;
        ProfilePickerContinueButton.IsEnabled = _profileEntries.Count > 0;
        ShowLoginPanel("profiles");
        ProfilePickerPanel.Visibility = Visibility.Visible;
        // _vault.IsLocked exclu ici : si la session est verrouillee (LockSessionNow),
        // _userProfile reste non-null mais Annuler ne doit surtout pas permettre de
        // rouvrir la navigation sans repasser par le mot de passe/PIN.
        ProfilePickerCancelButton.Visibility =
            (_userProfile is not null && !_vault.IsLocked) || _isGuestMode
                ? Visibility.Visible
                : Visibility.Collapsed;
        LoginStatusText.Text = _profileEntries.Count == 0
            ? "Aucun profil local trouvé."
            : "Choisissez le profil à ouvrir.";
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

        // Profil sans mot de passe (2026-08-13) : aucun ecran de connexion,
        // entree directe - voir UserProfile.HasAccountPassword.
        if (!_userProfile.HasAccountPassword)
        {
            DismissLoginOverlay();
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
        NoPasswordSwitch.IsOn = false; // remet aussi les champs mot de passe/PIN visibles (Toggled)
        CreatePasswordBox.Password = string.Empty;
        ConfirmPasswordBox.Password = string.Empty;
        EnablePinSwitch.IsOn = false;
        PinSetupBox.Password = string.Empty;
        ConfirmPinBox.Password = string.Empty;
        ShowLoginPanel("create");
    }

    private void ShowProfilePickerButton_Click(object sender, RoutedEventArgs e) =>
        ShowProfilePicker();

    // Meme raison que CreateProfileCancelButton_Click : la visibilite du bouton
    // (calculee dans ShowProfilePicker) garantit deja qu'on ne l'atteint que
    // s'il existe une session active (profil ou invite) vers laquelle revenir.
    private void ProfilePickerCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if ((_userProfile is not null && !_vault.IsLocked) || _isGuestMode)
        {
            DismissLoginOverlay();
        }
    }

    private void ShowProfileOverlay()
    {
        ShowLoginOverlayChrome();
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
            ProfileFlyoutTitleText.Text = "Mode invité";
            ProfileFlyoutSubtitleText.Text = "Aucune donnée persistante n'est gardée à la fermeture tant qu'aucun profil n'est choisi.";
            ProfileFlyoutCurrentUserText.Text = "Session sans profil";
            ProfileFlyoutCurrentStateText.Text = "Créer un utilisateur ou choisir un profil local pour retrouver vos données ensuite.";
            return;
        }

        var currentName = _userProfile?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(currentName))
        {
            ProfileFlyoutTitleText.Text = currentName;
            ProfileFlyoutSubtitleText.Text = "Ce menu regroupe les actions de profil, de compte local et de gestion d'utilisateurs.";
            ProfileFlyoutCurrentUserText.Text = $"Connecté : {currentName}";
            ProfileFlyoutCurrentStateText.Text = "Changer d'utilisateur, ouvrir les paramètres du profil ou en créer un nouveau.";
            return;
        }

        ProfileFlyoutTitleText.Text = "Profil Lumora";
        ProfileFlyoutSubtitleText.Text = "Choisissez un profil local pour séparer vos données, sessions et réglages.";
        ProfileFlyoutCurrentUserText.Text = "Aucun profil actif";
        ProfileFlyoutCurrentStateText.Text = "Créer ou choisir un profil local pour garder vos données sur cet appareil.";
    }

    private void ProfileFlyoutSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ProfileStatusFlyout.Hide();
        // Le bouton profil vit depuis 0.93.5.0-dev en en-tete du menu Demarrer
        // (ModulesFlyout) : fermer uniquement ProfileStatusFlyout ne suffit
        // plus, le menu Demarrer qui l'englobe reste ouvert derriere l'ecran
        // qu'on vient d'ouvrir (signale par l'utilisateur, capture a l'appui).
        // Hide() sur un flyout deja ferme est un no-op sans risque.
        ModulesFlyout.Hide();
        OpenProfileSettings();
    }

    private void ProfileFlyoutSwitchButton_Click(object sender, RoutedEventArgs e)
    {
        ProfileStatusFlyout.Hide();
        ModulesFlyout.Hide();
        ShowProfilePickerOverlay();
        StatusText.Text = "Choisissez un autre utilisateur Lumora.";
    }

    private void ProfileFlyoutCreateButton_Click(object sender, RoutedEventArgs e)
    {
        ProfileStatusFlyout.Hide();
        ModulesFlyout.Hide();
        ShowCreateProfileOverlay();
        StatusText.Text = "Création d'un nouvel utilisateur Lumora.";
    }

    private void DismissLoginOverlay()
    {
        LoginOverlay.Visibility = Visibility.Collapsed;
        BrowserHost.IsHitTestVisible = true;
        _pinBuffer = string.Empty;
        _pinFailCount = 0;
        // Restauration des onglets de la derniere session (donc navigation reseau
        // reelle) differee jusqu'ici expres : voir _pendingStartupPageApply. Une
        // seule fois par process - un reverrouillage/deverrouillage en cours de
        // session (LockSessionNow -> DismissLoginOverlay) ne doit pas dupliquer les
        // onglets deja ouverts. _suppressTabSave suspendu le temps de la
        // restauration, comme au tout premier demarrage (evite de reecrire
        // TabsFile a chaque onglet recree alors qu'on vient tout juste de le lire).
        if (_pendingStartupPageApply)
        {
            _pendingStartupPageApply = false;
            _suppressTabSave = true;
            ApplyStartupPage();
            _suppressTabSave = false;
        }
        RefreshProfileSettings();
        UpdateProfileStatus();
        InitSessionTimer();
        InitRssTimer();
        UpdateRssBadge();
        // Meme raison que ActivateTab/EnsureTabViewReadyAsync (voir leurs commentaires) :
        // sans focus explicite sur le WebView2, la molette reste muette tant qu'on n'a
        // pas clique dans la page. Ecran de connexion desormais focusable (champ mot de
        // passe, pave PIN...), donc le focus y reste bel et bien apres la connexion s'il
        // n'est pas explicitement rendu a l'onglet actif ici. FocusState.Pointer et non
        // Programmatic (2026-08-02, meme correctif) : Programmatic ne se propage pas
        // jusqu'a Chromium, la molette restait donc muette juste apres deverrouillage.
        CurrentTab()?.View?.Focus(FocusState.Pointer);
        _ = ClearHomeSearchFieldAfterDelayAsync(blur: true);
        // À la connexion : rapatrier ce que Chromium avait encore, puis vider son coffre
        // (stockage 100% maison → vault.lumora est le seul magasin).
        _ = MigrateAndClearBrowserPasswordsAsync();
        if (!_isGuestMode && !_uiSettings.SetupWizardCompleted)
            ShowSetupWizard();

        // Signal "cette fenetre a ses onglets prets" (restauration de session
        // comprise) - utilise par MainWindow.TabDetach.cs pour savoir quand
        // ajouter l'onglet detache sans risquer un doublon avec
        // ApplyStartupPage. Voir DetachTabToNewWindow. IsBrowserReady est la
        // version "etat" (utile quand ce point est deja passe au moment ou
        // l'appelant regarde, pas seulement pour un abonnement futur).
        IsBrowserReady = true;
        BrowserReady?.Invoke();
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

        LockSessionNow("Session verrouillée automatiquement.");
    }

    // Verrouillage effectif du coffre + ecran de re-login. Point d'entree commun au
    // timer d'inactivite ET aux evenements systeme (veille, verrouillage Windows).
    // Contrairement au tick d'inactivite, ceci ne tient PAS compte de l'audio : si
    // Windows se verrouille ou s'endort, l'utilisateur a quitte son poste, une video
    // qui continue de jouer derriere l'ecran de verrouillage ne doit rien empecher.
    private void LockSessionNow(string statusMessage)
    {
        if (_isGuestMode || _userProfile is null) return;
        // Profil sans mot de passe (2026-08-13) : rien a verrouiller, il n'y a
        // aucun moyen de re-deverrouiller sans mot de passe - "sans mot de
        // passe, le navigateur devient un navigateur presque banal" (mots de
        // l'utilisateur). L'inactivite/la veille/le verrouillage Windows ne
        // font donc rien pour ce profil, voir UserProfile.HasAccountPassword.
        if (!_userProfile.HasAccountPassword) return;
        if (LoginOverlay.Visibility == Visibility.Visible) return; // deja verrouille

        // Verrouillage réel : on purge la clé du coffre de la mémoire, pas seulement
        // l'écran. Le ré-login (mot de passe ou PIN) la reconstruit.
        _vault.Lock();
        ShowLoginPanel(_userProfile.HasPinLogin ? "pin" : "password");
        LoginStatusText.Text = statusMessage;
        ShowLoginOverlayChrome();
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
            LockSessionNow("Coffre verrouillé : session Windows verrouillée."));
    }

    private void OnSystemPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode != Microsoft.Win32.PowerModes.Suspend) return;
        DispatcherQueue.TryEnqueue(() =>
            LockSessionNow("Coffre verrouillé : mise en veille."));
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
            ProfileNameDisplay.Text = "Aucun profil configuré.";
            ProfilePinSwitch.IsEnabled = false;
            ProfileNoPasswordNotice.Visibility = Visibility.Collapsed;
            ChangeProfilePasswordButton.Visibility = Visibility.Visible;
            CreateRecoveryKeyButton.Visibility = Visibility.Visible;
        }
        else
        {
            ProfileNameDisplay.Text = $"Connecté en tant que : {_userProfile.Name}\nDossier : {_profile.ProfileDir}";
            ProfilePinSwitch.IsEnabled = _userProfile.HasAccountPassword;
            _suppressUiSettingsSave = true;
            ProfilePinSwitch.IsOn = _userProfile.HasPinLogin;
            _suppressUiSettingsSave = false;

            // Profil sans mot de passe (2026-08-13) : choix permanent, voir
            // UserProfile.HasAccountPassword - "Changer le mot de passe" et
            // "Nouvelle clé de récupération" n'ont plus de sens (rien à
            // changer/récupérer), masqués plutôt que menant à une impasse.
            var hasPassword = _userProfile.HasAccountPassword;
            ProfileNoPasswordNotice.IsOpen = !hasPassword;
            ProfileNoPasswordNotice.Visibility = hasPassword ? Visibility.Collapsed : Visibility.Visible;
            ChangeProfilePasswordButton.Visibility = hasPassword ? Visibility.Visible : Visibility.Collapsed;
            CreateRecoveryKeyButton.Visibility = hasPassword ? Visibility.Visible : Visibility.Collapsed;
        }
        RefreshAvatarUi();
        RefreshAccountDashboard();

        // La gestion des utilisateurs locaux (chemins reels, ouverture de dossier,
        // quarantaine) et la reinitialisation de profil n'ont pas de sens pour une
        // session invite et exposent des donnees d'un vrai profil sans
        // authentification (trouve en usage reel le 2026-07-22, voir commentaire
        // XAML de ProfileManagementRestrictedPanel) : bloc entier cache en mode
        // invite plutot que des gardes au cas par cas, plus sur et plus simple a
        // verifier. ProfileDangerZonePanel (0.93.45.0-dev, deplace hors des onglets
        // pour rester visible peu importe l'onglet ouvert) suit exactement la meme
        // regle, pour la meme raison exacte.
        ProfileManagementRestrictedPanel.Visibility = _isGuestMode ? Visibility.Collapsed : Visibility.Visible;
        ProfileDangerZonePanel.Visibility = _isGuestMode ? Visibility.Collapsed : Visibility.Visible;
        ProfileGuestRestrictedNotice.IsOpen = _isGuestMode;
        if (_isGuestMode)
        {
            ProfileManagementPanel.Children.Clear();
            return;
        }

        RefreshProfileManagementPanel();
    }

    private void RefreshProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProfileManagementPanel();
        StatusText.Text = "Liste des profils actualisée.";
    }

    private void RefreshProfileManagementPanel()
    {
        if (_isGuestMode) return;

        ProfileManagementPanel.Children.Clear();
        _profileEntries = LumoraProfileRegistry.Discover(LumoraConfig.Load());

        // Le profil actif a deja sa propre carte "Mon profil" plus haut (avec ses
        // actions dediees) : ne plus le lister ici a cote des autres avec des boutons
        // desactives - trouve confus en usage reel des qu'il y a plusieurs profils
        // (rien ne distinguait "le mien, inactif ici" d'"un autre, vraiment inactif").
        var others = _profileEntries.Where(entry => !entry.IsActive).ToList();

        if (others.Count == 0)
        {
            ProfileManagementPanel.Children.Add(new TextBlock
            {
                Text = "Aucun autre profil sur cet appareil.",
                Opacity = 0.65
            });
            return;
        }

        foreach (var entry in others)
        {
            ProfileManagementPanel.Children.Add(BuildProfileManagementCard(entry));
        }
    }

    private UIElement BuildProfileManagementCard(LumoraProfileEntry entry)
    {
        var avatarHost = new Grid { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center };
        var avatarPath = entry.AvatarPath;
        var avatarCircle = new Ellipse { Width = 40, Height = 40 };
        avatarCircle.Fill = avatarPath is null
            ? (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"]
            : new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(avatarPath, UriKind.Absolute)),
                Stretch = Stretch.UniformToFill
            };
        avatarHost.Children.Add(avatarCircle);
        if (avatarPath is null)
        {
            avatarHost.Children.Add(new FontIcon
            {
                Glyph = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        Grid.SetColumn(avatarHost, 0);

        var title = new TextBlock
        {
            Text = entry.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        var type = new TextBlock
        {
            Text = entry.IsCustom ? "Emplacement personnalisé" : "Profil local",
            Opacity = 0.62,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        var identity = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(title);
        identity.Children.Add(type);
        Grid.SetColumn(identity, 1);

        var switchButton = new Button
        {
            Content = "Basculer",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"]
        };
        switchButton.Click += (_, _) => SwitchToProfile(entry);

        // Modifier/Supprimer/Ouvrir le dossier derriere un menu "..." plutot qu'en
        // boutons visibles en permanence (point 2, session "Ecran de connexion",
        // valide par l'utilisateur apres une demo interactive) : ce sont des actions
        // rares, deja protegees par le mot de passe du profil cible
        // (RequireTargetProfilePasswordAsync) - les regrouper evite d'avoir
        // "Supprimer" colle a cote de "Basculer" sur une liste qu'on scanne vite.
        var menu = new MenuFlyout();
        var openItem = new MenuFlyoutItem { Text = "Ouvrir le dossier" };
        openItem.Click += async (_, _) => await OpenProfileDirectory(entry);
        var modifyItem = new MenuFlyoutItem { Text = "Modifier" };
        modifyItem.Click += async (_, _) => await ModifyProfileAsync(entry);
        var deleteItem = new MenuFlyoutItem
        {
            Text = "Supprimer",
            Foreground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 220, G = 70, B = 70 })
        };
        deleteItem.Click += async (_, _) => await DeleteProfileAsync(entry);
        menu.Items.Add(openItem);
        menu.Items.Add(modifyItem);
        menu.Items.Add(deleteItem);

        var moreButton = new Button { Content = "···", Flyout = menu };
        ToolTipService.SetToolTip(moreButton, "Ouvrir le dossier, modifier ou supprimer");
        AutomationProperties.SetName(moreButton, $"Autres actions pour {entry.Name}");

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(switchButton);
        actions.Children.Add(moreButton);
        Grid.SetColumn(actions, 2);

        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(avatarHost);
        header.Children.Add(identity);
        header.Children.Add(actions);

        var path = new TextBlock
        {
            Text = entry.ProfileDir,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.55,
            FontSize = 11,
            Margin = new Thickness(50, 6, 0, 0)
        };

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(path);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10, 12, 10),
            Child = body
        };
    }

    private void SwitchToProfile(LumoraProfileEntry entry)
    {
        if (entry.IsActive) return;
        SaveSelectedProfileAndRestart(entry);
    }

    private async Task OpenProfileDirectory(LumoraProfileEntry entry)
    {
        // Garde directe en plus du panneau cache (ProfileManagementRestrictedPanel) :
        // ne jamais reveler/ouvrir le dossier d'un profil depuis une session invite.
        if (_isGuestMode) return;

        // Ouvrir le dossier d'un AUTRE profil expose son vault.lumora (localisation
        // + acces filesystem complet) : meme regle que Modifier/Supprimer, aucun
        // profil n'a de pouvoir sur un autre sans preuve de son mot de passe.
        if (!entry.IsActive && await RequireTargetProfilePasswordAsync(entry) is null)
        {
            return;
        }

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

    // Aucun profil n'a de pouvoir sur un autre dans Lumora (pas de notion
    // d'admin) : la seule preuve acceptable pour modifier ou supprimer le
    // compte d'un AUTRE utilisateur est son propre mot de passe, jamais une
    // simple confirmation de nom deja visible a l'ecran. Charge le profil
    // cible depuis son dossier (il n'est pas forcement le profil actif en
    // memoire) et retourne l'instance verifiee, prete a etre modifiee et
    // resauvegardee par l'appelant.
    private async Task<UserProfile?> RequireTargetProfilePasswordAsync(LumoraProfileEntry entry)
    {
        var paths = LumoraProfilePaths.FromDirectory(entry.ProfileDir);
        var targetProfile = UserProfile.Load(paths.ProfileFile, paths.LegacyProfileFile);
        if (targetProfile is null)
        {
            StatusText.Text = "Profil illisible.";
            return null;
        }

        // Profil cible sans mot de passe (2026-08-13) : aucune preuve possible
        // a demander, ce profil a deja assume qu'il reste ouvert a quiconque
        // utilise cette session Windows - voir UserProfile.HasAccountPassword.
        if (!targetProfile.HasAccountPassword)
        {
            return targetProfile;
        }

        var passwordBox = new PasswordBox { PlaceholderText = "Mot de passe", MinWidth = 300 };
        var dialog = new ContentDialog
        {
            Title = $"Mot de passe de {entry.Name}",
            Content = passwordBox,
            PrimaryButtonText = "Continuer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

        // Lire .Password sur le thread UI AVANT Task.Run : un PasswordBox (comme tout
        // objet XAML) leve un COMException 0x8001010E (RPC_E_WRONG_THREAD) des qu'on
        // accede a sa propriete depuis un thread de pool - trouve en conditions
        // reelles le 2026-08-14 (session "Compte et ouverture"), voir MEMORY.md.
        var enteredPassword = passwordBox.Password;
        var verified = await Task.Run(() => targetProfile.VerifyPassword(enteredPassword));
        if (!verified)
        {
            StatusText.Text = "Mot de passe incorrect : profil inchangé.";
            return null;
        }

        return targetProfile;
    }

    // Modifier se limite volontairement au nom : contrairement a un
    // changement de mot de passe (qui exigerait de deverrouiller ET
    // re-chiffrer le vault.lumora du profil cible, une operation risquee a
    // faire sans pouvoir la tester en conditions reelles), renommer ne touche
    // a aucun secret et ne peut pas corrompre le coffre d'un autre profil.
    private async Task ModifyProfileAsync(LumoraProfileEntry entry)
    {
        if (_isGuestMode || entry.IsActive) return;

        var targetProfile = await RequireTargetProfilePasswordAsync(entry);
        if (targetProfile is null) return;

        var nameBox = new TextBox { Text = targetProfile.Name, MinWidth = 300 };
        var dialog = new ContentDialog
        {
            Title = $"Renommer {entry.Name}",
            Content = nameBox,
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var newName = nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            StatusText.Text = "Le nom ne peut pas être vide.";
            return;
        }

        var paths = LumoraProfilePaths.FromDirectory(entry.ProfileDir);
        (targetProfile with { Name = newName }).Save(paths.ProfileFile);
        RefreshProfileManagementPanel();
        StatusText.Text = $"Profil renommé : {newName}.";
    }

    private async Task DeleteProfileAsync(LumoraProfileEntry entry)
    {
        // Garde directe en plus du panneau cache (ProfileManagementRestrictedPanel) :
        // ne jamais deplacer/desactiver le profil d'un tiers depuis une session invite.
        if (_isGuestMode) return;

        if (entry.IsActive)
        {
            StatusText.Text = "Le profil actif ne peut pas être supprimé.";
            return;
        }

        var targetProfile = await RequireTargetProfilePasswordAsync(entry);
        if (targetProfile is null) return;

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = "Le profil sera déplacé dans un dossier de quarantaine. Il ne sera pas détruit immédiatement.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = entry.ProfileDir,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.62,
            FontSize = 12
        });

        var dialog = new ContentDialog
        {
            Title = $"Supprimer le profil {entry.Name} ?",
            Content = panel,
            PrimaryButtonText = "Supprimer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            var target = LumoraProfileRegistry.QuarantineProfile(entry);
            RefreshProfileManagementPanel();
            StatusText.Text = $"Profil supprimé (récupérable) : {target}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Suppression impossible : {ex.Message}";
        }
    }

    private void EnablePinSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        var show = EnablePinSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        PinSetupBox.Visibility = show;
        ConfirmPinBox.Visibility = show;
    }

    // Profil sans mot de passe (2026-08-13) : masque le mot de passe ET le PIN
    // (rien a raccourcir sans mot de passe de base) des que le choix est fait,
    // affiche l'avertissement. Choix permanent - voir CreateProfileButton_Click
    // et MEMORY.md pour le raisonnement complet.
    private void NoPasswordSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        var noPassword = NoPasswordSwitch.IsOn;
        var passwordFieldsVisibility = noPassword ? Visibility.Collapsed : Visibility.Visible;

        CreatePasswordBox.Visibility = passwordFieldsVisibility;
        CreatePasswordBox.Password = string.Empty;
        ConfirmPasswordBox.Visibility = passwordFieldsVisibility;
        ConfirmPasswordBox.Password = string.Empty;

        EnablePinSwitch.Visibility = passwordFieldsVisibility;
        EnablePinSwitch.IsOn = false;
        PinSetupBox.Visibility = Visibility.Collapsed;
        PinSetupBox.Password = string.Empty;
        ConfirmPinBox.Visibility = Visibility.Collapsed;
        ConfirmPinBox.Password = string.Empty;

        NoPasswordWarningBar.IsOpen = noPassword;
        NoPasswordWarningBar.Visibility = noPassword ? Visibility.Visible : Visibility.Collapsed;
    }

    // "Continuer sans profil" ne peut plus se faire a chaud dans ce process :
    // WEBVIEW2_USER_DATA_FOLDER est deja fige sur le dossier du profil "par
    // defaut" depuis le tout debut du process (voir WebView2Bootstrap.ConfigureOnce
    // dans le constructeur), avant meme ce clic. Rester dans ce process voudrait
    // dire que les cookies/cache/IndexedDB WebView2 de la session invite
    // survivraient reellement dans ce dossier "par defaut" a la fermeture, malgre
    // le message "Aucune donnee persistante n'est gardee" (voir
    // UpdateProfileFlyoutUi) - c'etait le bug reel avant ce correctif. Meme
    // contrainte, meme solution que MainWindow.Incognito.cs/OpenIncognitoWindow :
    // relancer un nouveau process avec le dossier ephemere deja pose, et fermer
    // celui-ci.
    private void SkipProfileButton_Click(object sender, RoutedEventArgs e)
    {
        GuestProcessLauncher.Launch();
        Close();
    }

    // Corps de l'ancien SkipProfileButton_Click : appele uniquement depuis le
    // process invite dedie (voir InitializeLoginOverlayAsync/_pendingGuestLaunch),
    // dont _profile pointe deja vers le dossier ephemere de la session.
    private void EnterGuestMode()
    {
        _isGuestMode = true;
        _bookmarks.SetGuestMode(true);
        _notes.SetGuestMode(true);
        _annotations.SetGuestMode(true);
        _historyPanel.Store.SetGuestMode(true);
        _historyPanel.Downloads.SetGuestMode(true);
        _semanticIndex.SetGuestMode(true);
        _webApps.SetGuestMode(true);
        _rssFeeds.SetGuestMode(true);
        _savedTabGroups.SetGuestMode(true);
        _siteRelocations.SetGuestMode(true);
        _savedGroupIds.Clear();
        // Politique "Live Linux" : reglages limites au strict necessaire en mode
        // invite. Personnalisation (fond d'ecran compris) et Coffre/Portefeuille
        // touchent a une identite persistante ou a des identifiants reels - ces
        // entrees de navigation disparaissent entierement plutot que de rester
        // visibles pour aboutir a un message d'indisponibilite (voir aussi
        // SettingsNav_Click pour la defense en profondeur si jamais atteintes
        // autrement). Vie privee/securite et Confort restent utiles meme pour
        // une session ephemere : ils restent visibles.
        SettingsNavAppearance.Visibility = Visibility.Collapsed;
        SettingsNavVault.Visibility = Visibility.Collapsed;
        DismissLoginOverlay();
        // Reconstruire l'UI avec les stores vides
        ReloadBookmarks();
        _historyPanel.Items.Clear();
        _suppressTabSave = true;
        // CloseTabView (pas seulement retirer de _tabs/BrowserTabs.TabItems) :
        // sinon le WebView2 de l'onglet de demarrage (ouvert avant meme le choix
        // du mode invite) reste vivant sans etre suivi nulle part, et le process
        // moteur Chromium qu'il a lance garde ses fichiers verrouilles - constate
        // en conditions reelles, ca empechait la suppression du dossier ephemere
        // a la fermeture de la fenetre invite (voir le Closed du constructeur).
        foreach (var tab in _tabs.ToList())
            CloseTabView(tab);
        foreach (var item in BrowserTabs.TabItems.OfType<TabViewItem>().ToList())
            BrowserTabs.TabItems.Remove(item);
        _tabs.Clear();
        // Les onglets du profil précédent ne doivent pas être restaurables en mode invité.
        _closedTabs.Clear();
        AddTab("Nouvel onglet", "lumora://accueil", select: true);
        _suppressTabSave = false;
        Title = $"Lumora {Version} — Mode invité";
    }

    // Icones disquette/dossier a cote du bandeau Lumora sur l'ecran "Bienvenue"
    // (session "Compte et ouverture", 2026-08-14 - maquette validee par
    // l'utilisateur en Artifact) : memes actions que l'etape "Profil existant ?"
    // de l'assistant (MainWindow.SetupWizard.cs), mais accessibles DES ce tout
    // premier ecran plutot que 2 etapes plus loin - l'utilisateur avait signale
    // que devoir remplir un formulaire de creation avant qu'on lui propose de
    // recuperer son profil etait a l'envers.
    private async void CreateProfileImportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await RunImportBackupFlowAsync()) return;

        await ShowSimpleDialogAsync(
            "Sauvegarde importée",
            "Vos favoris, onglets, coffre et réglages ont été restaurés. Lumora va redémarrer pour appliquer la sauvegarde.",
            closeButtonText: "OK");
        RestartApp();
    }

    private async void CreateProfileFindExistingButton_Click(object sender, RoutedEventArgs e) =>
        await AdoptExistingProfileFolderAsync(mentionBlankProfileLeftIntact: false);

    // Selecteur de dossier natif Windows, meme reglages (Bureau, aucun filtre
    // d'extension) partout ou Lumora demande "choisis un dossier" - factorise
    // pour ne pas repeter ces 4 lignes de cablage a chaque appelant.
    private async Task<StorageFolder?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        return await picker.PickSingleFolderAsync();
    }

    // Coeur partage entre ce bouton et WizardExistingProfileButton_Click
    // (etape "Profil existant ?" de l'assistant, MainWindow.SetupWizard.cs) -
    // memes verifications, meme mecanisme (config.CustomProfilePath + RestartApp,
    // identique a ChangeFolderButton_Click/ProfileLocationContinueButton_Click) :
    // rien n'est copie, deplace ni supprime.
    private async Task AdoptExistingProfileFolderAsync(bool mentionBlankProfileLeftIntact)
    {
        if (_isGuestMode) { LoginStatusText.Text = "Indisponible en mode invité."; return; }

        var folder = await PickFolderAsync();
        if (folder is null) return;

        if (SameProfileDirectory(folder.Path, _profile.ProfileDir))
        {
            LoginStatusText.Text = "C'est déjà le profil actuel.";
            return;
        }

        var candidatePaths = LumoraProfilePaths.FromDirectory(folder.Path);
        var candidate = UserProfile.Load(candidatePaths.ProfileFile, candidatePaths.LegacyProfileFile);
        if (candidate is null)
        {
            LoginStatusText.Text = "Ce dossier ne contient pas de profil Lumora reconnaissable.";
            return;
        }

        var dialog = new ContentDialog
        {
            Title = $"Profil trouvé : {candidate.Name}",
            Content = mentionBlankProfileLeftIntact
                ? "Lumora va redémarrer sur ce profil. Le profil vierge que vous venez de créer reste inchangé sur le disque et restera accessible depuis le sélecteur de profils si besoin."
                : "Lumora va redémarrer sur ce profil.",
            PrimaryButtonText = "Utiliser ce profil",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var config = LumoraConfig.Load();
        config.CustomProfilePath = folder.Path;
        config.Save();
        RestartApp();
    }

    private async void CreateProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var name = ProfileNameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        { LoginStatusText.Text = "Veuillez entrer un prénom ou pseudo."; return; }

        // Profil sans mot de passe (2026-08-13) : choix permanent, voir
        // MEMORY.md - aucune verification de force, aucun PIN (rien a
        // raccourcir sans mot de passe de base), aucune cle de recuperation
        // (rien a recuperer). NoPasswordSwitch_Toggled garantit deja que les
        // champs mot de passe/PIN sont vides et masques dans ce cas.
        if (NoPasswordSwitch.IsOn)
        {
            _pendingUserProfile = UserProfile.Create(name, null, null);
            _pendingProfilePassword = null;
            _pendingProfilePin = null;
            _pendingRecoveryKey = null;
        }
        else
        {
            var pw  = CreatePasswordBox.Password;
            var pw2 = ConfirmPasswordBox.Password;

            if (!IsAccountPasswordStrongEnough(pw, out var pwError))
            { LoginStatusText.Text = pwError; return; }

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
        }
        // Trouve en usage reel le 2026-07-22 : le tout premier profil crevait la
        // convention en gardant l'identifiant sentinelle "default" quel que soit
        // le prenom saisi, alors que tout profil suivant recevait deja un
        // dossier nomme d'apres son nom (LumoraProfileRegistry.CreateProfileId).
        // Avec plusieurs utilisateurs, avoir un "default" a cote de "jean"/"marie"
        // est illisible - chaque profil, premier inclus, doit avoir un dossier
        // nomme d'apres son utilisateur.
        _pendingProfileId = LumoraProfileRegistry.CreateProfileId(name);
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
            LoginStatusText.Text = $"{entry.Name} n'est pas détecté sur ce PC.";
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
            StatusText.Text = $"Import {browserName} : {imported} favoris importés.";
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
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo { FileName = exe, UseShellExecute = false };
            // Un process invite (voir GuestProcessLauncher) ou une verification
            // pilotee (skill verify) peuvent avoir LUMORA_PROFILE_DIR positionne
            // pour CE process. RestartApp ne sert qu'a relancer sur le profil
            // reel choisi (config.ActiveProfileId/CustomProfilePath) : sans ce
            // retrait, Process.Start heriterait silencieusement cette variable
            // et LumoraProfilePaths.Default() l'aurait toujours priorisee,
            // rouvrant l'ancien dossier ephemere/force au lieu du profil
            // reellement selectionne.
            startInfo.EnvironmentVariables.Remove(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable);
            System.Diagnostics.Process.Start(startInfo);
        }
        Application.Current.Exit();
    }

    private async void ChooseProfileLocationButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
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
            LoginStatusText.Text = "Aucune clé de récupération n'est configurée pour ce profil.";
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Récupérer le profil",
            PrimaryButtonText = "Récupérer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var panel = new StackPanel { Spacing = 10 };
        var recoveryBox = new TextBox
        {
            Header = "Clé de récupération",
            PlaceholderText = "NOVA-......-......-......",
            MinWidth = 320
        };
        var newBox = new PasswordBox
        {
            Header = "Nouveau mot de passe",
            PlaceholderText = "Minimum 12 caractères + 1 caractère spécial",
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
            Text = "La clé de récupération reste locale. Sans cette clé, Lumora ne peut pas contourner le chiffrement.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.68,
            FontSize = 12
        });
        dialog.Content = panel;

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var recoveryKey = recoveryBox.Text;
        if (!_userProfile.VerifyRecoveryKey(recoveryKey))
        {
            LoginStatusText.Text = "Clé de récupération incorrecte.";
            return;
        }
        if (!IsAccountPasswordStrongEnough(newBox.Password, out var recoveryPwError))
        {
            LoginStatusText.Text = recoveryPwError;
            return;
        }
        if (newBox.Password != confirmBox.Password)
        {
            LoginStatusText.Text = "Les mots de passe ne correspondent pas.";
            return;
        }
        if (!_vault.UnlockWithRecoveryKey(recoveryKey))
        {
            LoginStatusText.Text = "La clé est valide pour le profil, mais le coffre n'a pas de récupération active.";
            return;
        }

        _vault.SetMasterPassword(newBox.Password);
        _userProfile = _userProfile.WithNewPassword(newBox.Password).WithoutPin();
        _userProfile.Save(_profile.ProfileFile);
        LoginPasswordBox.Password = string.Empty;
        LoginStatusText.Text = "Mot de passe réinitialisé avec la clé de récupération.";
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

        UpdatePinDots(_pinBuffer.Length);
        _ = ClearHomeSearchFieldAsync(blur: true);

        if (_pinBuffer.Length == 6)
        {
            var pin = _pinBuffer;
            _pinBuffer = string.Empty;
            UpdatePinDots(0);

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

    // Pastilles PIN (2026-08-12) : bascule seulement l'Opacity de chaque
    // Ellipse deja posee en XAML (PinDotsPanel) - la couleur reste
    // NovaAccentBrush partout, deja tenue a jour par ApplyUsageModeChrome
    // pour le Mode d'usage actif, aucun lookup de resource ici.
    private void UpdatePinDots(int filledCount)
    {
        var i = 0;
        foreach (var dot in PinDotsPanel.Children.OfType<Ellipse>())
        {
            dot.Opacity = i < filledCount ? 1.0 : 0.28;
            i++;
        }
    }

    private async void ChangeProfileNameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_userProfile is null) return;
        var dialog = new ContentDialog
        {
            Title = "Nouveau prénom ou pseudo",
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
        StatusText.Text = "Nom de profil mis à jour.";
    }

    private async void ChangeProfilePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_userProfile is null) return;
        // Defense en profondeur : le bouton est deja masque pour un profil sans
        // mot de passe (RefreshProfileSettings), rien a changer ici de toute facon.
        if (!_userProfile.HasAccountPassword) return;

        var dialog = new ContentDialog
        {
            Title = "Changer le mot de passe",
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            XamlRoot = Content.XamlRoot
        };
        var panel = new StackPanel { Spacing = 10 };
        var oldBox  = new PasswordBox { PlaceholderText = "Mot de passe actuel", MinWidth = 260 };
        var newBox  = new PasswordBox { PlaceholderText = "Nouveau mot de passe (min. 12 car. + spécial)", MinWidth = 260 };
        var confBox = new PasswordBox { PlaceholderText = "Confirmer", MinWidth = 260 };
        panel.Children.Add(oldBox);
        panel.Children.Add(newBox);
        panel.Children.Add(confBox);
        dialog.Content = panel;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        // Lire .Password sur le thread UI AVANT tout Task.Run (meme raison qu'ailleurs
        // dans ce fichier - COMException 0x8001010E sur un thread de pool, trouve en
        // conditions reelles le 2026-08-14).
        var oldPassword = oldBox.Password;
        var newPassword = newBox.Password;

        if (!await Task.Run(() => _userProfile.VerifyPassword(oldPassword)))
        { StatusText.Text = "Mot de passe actuel incorrect."; return; }
        if (!IsAccountPasswordStrongEnough(newPassword, out var changePwError))
        { StatusText.Text = changePwError; return; }
        if (newPassword != confBox.Password)
        { StatusText.Text = "Les mots de passe ne correspondent pas."; return; }

        _userProfile = await Task.Run(() => _userProfile.WithNewPassword(newPassword));
        _userProfile.Save(_profile.ProfileFile);

        // Re-clé du coffre avec le nouveau mot de passe : on déverrouille d'abord avec
        // l'ancien (pour ne pas perdre les identifiants), puis on re-chiffre.
        if (_vault.HasMasterPassword)
        {
            if (_vault.EnsureUnlockedWith(oldPassword))
                _vault.SetMasterPassword(newPassword);
        }
        else
        {
            _vault.EnsureUnlockedWith(newPassword);
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

        StatusText.Text = "Mot de passe du profil mis à jour.";
    }

    private async void CreateRecoveryKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_userProfile is null) return;
        // Defense en profondeur : le bouton est deja masque pour un profil sans
        // mot de passe (RefreshProfileSettings) - une cle de recuperation sert
        // a reinitialiser un mot de passe, rien a recuperer sans mot de passe.
        if (!_userProfile.HasAccountPassword) return;
        if (!await RequireVaultAccessAsync())
        {
            StatusText.Text = "Clé de récupération non créée : accès au coffre refusé.";
            return;
        }

        var recoveryKey = UserProfile.GenerateRecoveryKey();
        _userProfile = _userProfile.WithRecoveryKey(recoveryKey);
        _userProfile.Save(_profile.ProfileFile);
        if (!_vault.SetRecoveryKey(recoveryKey))
        {
            StatusText.Text = "Impossible de rattacher la clé de récupération au coffre.";
            return;
        }

        await ShowRecoveryKeyDialogAsync(recoveryKey);
        StatusText.Text = "Nouvelle clé de récupération activée.";
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
            Content = "Copier la clé",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        copyButton.Click += (_, _) =>
            CopySecretToClipboard(recoveryKey, "Clé de récupération copiée.", clearAfterSeconds: 0);

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Notez cette clé maintenant. Elle permet de récupérer le profil et le coffre si le mot de passe est oublié. Lumora ne peut pas la retrouver à votre place.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(keyBox);
        panel.Children.Add(copyButton);

        var dialog = new ContentDialog
        {
            Title = "Clé de récupération Lumora",
            Content = panel,
            PrimaryButtonText = "J'ai noté la clé",
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
                Title = "Définir le code PIN",
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

            // Lire .Password sur le thread UI AVANT Task.Run (meme raison qu'ailleurs
            // dans ce fichier - COMException 0x8001010E sur un thread de pool).
            var pin = pinBox.Password;
            _userProfile = await Task.Run(() => _userProfile.WithPin(pin));
            // Le PIN peut désormais ouvrir le coffre (si celui-ci est déverrouillé maintenant).
            if (!_vault.IsLocked) _vault.EnablePinUnlock(pin);
        }
        else
        {
            _userProfile = _userProfile.WithoutPin();
            _vault.DisablePinUnlock();
        }

        _userProfile.Save(_profile.ProfileFile);
        StatusText.Text = ProfilePinSwitch.IsOn ? "Code PIN activé." : "Code PIN désactivé.";
    }

    private async void ResetProfileButton_Click(object sender, RoutedEventArgs e)
    {
        // Garde directe en plus du panneau cache (ProfileManagementRestrictedPanel) :
        // trouve en usage reel le 2026-07-22, ce bouton supprimait tout _profile.ProfileDir
        // (vault, mots de passe, historique) sans la moindre verification de mode
        // invite - or _profile pointe encore sur le vrai profil par defaut en mode
        // invite (seul _userProfile devient null). Sans cette garde, une session
        // invite pouvait detruire definitivement le vrai profil actif d'un simple clic.
        if (_isGuestMode) return;
        if (_userProfile is null) return;

        // Meme regle que Modifier/Supprimer sur un AUTRE profil
        // (RequireTargetProfilePasswordAsync) : reappliquee ici a SON PROPRE profil
        // (point 4, session "Ecran de connexion") - Reinitialiser est plus
        // destructeur que Supprimer un autre profil (suppression immediate et
        // definitive du dossier, pas une mise en quarantaine recuperable) et n'avait
        // pourtant aucune verification autre qu'une simple boite de confirmation.
        var passwordBox = new PasswordBox { PlaceholderText = "Mot de passe", MinWidth = 300 };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = "Cette action supprime définitivement toutes vos données : favoris, historique, onglets, paramètres, coffre et identifiants de connexion. Cette opération est irréversible.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Confirmez avec le mot de passe de {_userProfile.Name}.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
            FontSize = 12
        });
        panel.Children.Add(passwordBox);

        var dlg = new ContentDialog
        {
            Title = "Réinitialiser le profil ?",
            Content = panel,
            PrimaryButtonText = "Réinitialiser",
            CloseButtonText = "Annuler",
            // LA vraie cause du symptome "le dialogue se ferme, rien ne se passe,
            // aucun message" (2026-08-14, confirme par winui-runtime-trace.log :
            // resultat=None a chaque tentative) : DefaultButton=Close faisait que
            // taper le mot de passe puis appuyer sur Entree (reflexe naturel apres
            // un champ de mot de passe) annulait SILENCIEUSEMENT le dialogue, exactement
            // comme un clic sur "Annuler" - aucune suppression tentee, aucune
            // verification de mot de passe meme faite, et rien ne le signalait.
            // DefaultButton=None : Entree ne declenche plus aucun bouton (le
            // dialogue reste ouvert, l'utilisateur voit qu'il doit cliquer) - reste
            // aussi sur, puisqu'aucune touche seule ne peut plus declencher la
            // suppression.
            DefaultButton = ContentDialogButton.None,
            XamlRoot = Content.XamlRoot
        };
        WinUiRuntimeTrace.Write("ResetProfileButton: dialogue de confirmation ouvert");
        var confirmResult = await dlg.ShowAsync();
        WinUiRuntimeTrace.Write($"ResetProfileButton: dialogue de confirmation ferme, resultat={confirmResult}");
        if (confirmResult != ContentDialogResult.Primary)
        {
            // Meme principe que le mot de passe incorrect plus bas : une annulation
            // (bouton Annuler, Echap, ou clic hors du dialogue) doit rester visible,
            // pas silencieuse.
            if (confirmResult == ContentDialogResult.None)
                StatusText.Text = "Réinitialisation annulée : profil inchangé.";
            return;
        }

        // LA vraie cause du bug signale par l'utilisateur (2026-08-14) : lire
        // passwordBox.Password DANS le lambda Task.Run levait un COMException
        // 0x8001010E (RPC_E_WRONG_THREAD, confirme par winui-runtime-trace.log) -
        // un objet XAML comme PasswordBox ne peut etre lu que sur le thread UI.
        // L'exception partait donc en silence AVANT meme la comparaison du mot de
        // passe, expliquant "rien ne se passe" quel que soit le mot de passe tape -
        // le correctif WebView2/RetryDelete plus bas n'etait jamais atteint. Meme
        // anti-motif trouve et corrige aux 4 autres endroits de ce fichier qui
        // accedaient a .Password a l'interieur d'un Task.Run.
        var enteredPassword = passwordBox.Password;
        WinUiRuntimeTrace.Write($"ResetProfileButton: verification du mot de passe, longueur saisie={enteredPassword.Length}");
        var passwordOk = await Task.Run(() => _userProfile.VerifyPassword(enteredPassword));
        WinUiRuntimeTrace.Write($"ResetProfileButton: resultat VerifyPassword={passwordOk}");
        if (!passwordOk)
        {
            // Retour utilisateur reel (2026-08-14, session "Compte et ouverture") :
            // StatusText seul est trop facile a manquer sur une action aussi
            // destructrice - un mot de passe refuse doit etre impossible a rater.
            await ShowSimpleDialogAsync("Mot de passe incorrect", "Le profil n'a pas été réinitialisé.");
            return;
        }

        // Fermer chaque WebView2 explicitement AVANT de supprimer : sans ca, le
        // process moteur Chromium garde ses fichiers (Cache, LevelDB...) verrouilles
        // et Directory.Delete echoue silencieusement - meme piege deja rencontre et
        // corrige pour le dossier ephemere invite (voir le Closed du constructeur,
        // DeleteGuestSessionDirectoryWithRetry). C'etait le bug reel derriere ce
        // bouton : aucun message d'erreur, le dossier survivait, sans qu'on sache
        // pourquoi - trouve en le testant en conditions reelles avec l'utilisateur.
        WinUiRuntimeTrace.Write($"ResetProfileButton: mot de passe correct, fermeture de {_tabs.Count} onglet(s) puis suppression de {_profile.ProfileDir}");
        foreach (var tab in _tabs)
        {
            try { tab.View?.Close(); } catch { }
        }

        var deleted = RetryDelete.TryDeleteDirectory(_profile.ProfileDir, maxAttempts: 15, delayMs: 200, out var deleteError);
        WinUiRuntimeTrace.Write($"ResetProfileButton: RetryDelete.TryDeleteDirectory -> {deleted}" + (deleteError is null ? "" : $" (derniere erreur : {deleteError.GetType().Name}: {deleteError.Message})"));
        if (!deleted)
        {
            await ShowSimpleDialogAsync(
                "Réinitialisation impossible",
                "Le dossier du profil est toujours utilisé par Lumora (mot de passe correct, mais la suppression a échoué). " +
                "Fermez tous les onglets ouverts puis réessayez." +
                (deleteError is null ? "" : $"\n\nDétail : {deleteError.Message}"));
            return;
        }

        // Remettre la configuration de profil dans un état local coherent.
        var cfg = LumoraConfig.Load();
        if (!string.IsNullOrWhiteSpace(cfg.CustomProfilePath)
            && string.Equals(Path.GetFullPath(cfg.CustomProfilePath), Path.GetFullPath(_profile.ProfileDir), StringComparison.OrdinalIgnoreCase))
        {
            cfg.ActiveProfileId = "default";
        }

        cfg.CustomProfilePath = null;
        cfg.Save();
        WinUiRuntimeTrace.Write("ResetProfileButton: suppression reussie, redemarrage de l'application");

        // Retour utilisateur reel (2026-08-14, session "Compte et ouverture") : la
        // reussite redemarrait l'app sans un mot, alors que les deux echecs (mot de
        // passe incorrect, suppression impossible) avaient deja leur propre message -
        // seul le cas qui compte le plus (ca a marche) restait muet. Bouton "OK" a
        // valider explicitement, comme demande, avant le redemarrage.
        await ShowSimpleDialogAsync(
            "Profil réinitialisé",
            "Votre profil a été supprimé. Lumora va redémarrer pour repartir de zéro.",
            closeButtonText: "OK");

        var exe = Environment.ProcessPath;
        if (exe is not null)
            System.Diagnostics.Process.Start(exe);
        Application.Current.Exit();
    }

    // Meme logique de nouvelles tentatives que DeleteGuestSessionDirectoryWithRetry
    // (MainWindow.xaml.cs), mais qui rapporte l'echec au lieu de l'avaler en
    // silence : ce bouton a besoin de dire a l'utilisateur si la suppression a
    // reellement eu lieu, contrairement au nettoyage best-effort d'un dossier
    // invite ephemere.
    private async Task ShowSimpleDialogAsync(string title, string message, string closeButtonText = "Fermer")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = closeButtonText,
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
