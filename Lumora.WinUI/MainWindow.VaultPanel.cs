using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;
using Windows.ApplicationModel.DataTransfer;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private VaultSortMode _vaultSortMode = VaultSortMode.Recent;
    private VaultCredential? _selectedVaultCredential;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _totpTimer;

    private async void VaultAddButton_Click(object sender, RoutedEventArgs e)
    {
        var (draft, cancelled) = await PromptNewCredentialAsync();
        if (cancelled) { StatusText.Text = "Ajout annulé."; return; }

        if (!_passwordManager.Save(draft))
        {
            StatusText.Text = "Identifiant incomplet : site et mot de passe requis.";
            return;
        }

        RefreshVaultPanel();
        StatusText.Text = $"Identifiant enregistré : {draft.Username}";
    }

    private void VaultSort_Click(object sender, RoutedEventArgs e)
    {
        _vaultSortMode = VaultSortAlphabeticalRadio.IsChecked == true
            ? VaultSortMode.Alphabetical
            : VaultSortMode.Recent;
        RefreshVaultPanel();
    }

    // Regroupe par site (sous-domaines fusionnés) via VaultGroupingService, plutôt
    // que l'ancien empilement plat carte par carte. Le détail (actions) vit dans le
    // volet de droite, alimenté par la sélection courante.
    private void RefreshVaultPanel()
    {
        VaultPanelItems.Children.Clear();

        var query = PasswordManagerSearchBox?.Text ?? string.Empty;
        var credentials = _passwordManager.List(new PasswordManagerSearchOptions(query));
        if (credentials.Count == 0)
        {
            VaultPanelItems.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(query)
                    ? "Aucun identifiant dans le gestionnaire."
                    : "Aucun identifiant ne correspond à cette recherche.",
                Opacity = 0.65,
                Margin = new Thickness(0, 8, 0, 0)
            });
            ShowVaultDetail(null);
            return;
        }

        foreach (var group in VaultGroupingService.Group(credentials, _vaultSortMode))
        {
            VaultPanelItems.Children.Add(BuildVaultGroupHeader(group));
            foreach (var cred in group.Credentials)
            {
                VaultPanelItems.Children.Add(BuildVaultRow(cred));
            }
        }

        // Garde la sélection si l'identifiant existe toujours (ex. après renommage
        // ou fusion), sinon vide le volet de détail plutôt que de laisser un
        // identifiant fantôme affiché.
        var stillThere = _selectedVaultCredential is { } current
            ? credentials.FirstOrDefault(c => c.Id == current.Id)
            : null;
        ShowVaultDetail(stillThere);
    }

    private UIElement BuildVaultGroupHeader(VaultCredentialGroup group)
    {
        var row = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 6, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        row.Children.Add(BuildFaviconElement(group.Credentials[0].Origin, size: 16));

        var title = group.Credentials.Count > 1
            ? $"{group.DisplayName} ({group.Credentials.Count})"
            : group.DisplayName;
        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            Opacity = 0.85,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleBlock, 1);
        row.Children.Add(titleBlock);

        return row;
    }

    private UIElement BuildVaultRow(VaultCredential cred)
    {
        var isSelected = _selectedVaultCredential?.Id == cred.Id;
        var hasUser = !string.IsNullOrWhiteSpace(cred.Username);

        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 2),
            Background = isSelected
                ? RootShell.Resources["NovaAccentSoftBrush"] as Brush
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Tag = cred
        };

        button.Content = new TextBlock
        {
            Text = hasUser ? cred.Username : "(aucun identifiant enregistré)",
            Opacity = hasUser ? 0.85 : 0.5,
            FontStyle = hasUser ? Windows.UI.Text.FontStyle.Normal : Windows.UI.Text.FontStyle.Italic,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        button.Click += (_, _) =>
        {
            _selectedVaultCredential = cred;
            RefreshVaultPanel();
        };

        return button;
    }

    // Icône de site à partir du cache favicon déjà alimenté par la navigation
    // (MainWindow.Bookmarks.cs), en lecture seule : aucun téléchargement déclenché
    // depuis le coffre. Repli sur un glyphe générique si rien n'est en cache.
    private UIElement BuildFaviconElement(string origin, double size)
    {
        var path = CachedFaviconPathFor(origin);
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                return new Image
                {
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Uniform,
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path))
                };
            }
            catch { /* fichier corrompu ou illisible : repli glyphe */ }
        }

        return new FontIcon
        {
            Glyph = "", // Globe
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = size * 0.85,
            Opacity = 0.55
        };
    }

    // Alimente le volet de détail (colonne de droite) pour l'identifiant
    // sélectionné : actions (copier, ouvrir, renommer, supprimer) regroupées au
    // même endroit plutôt qu'empilées sur chaque carte de la liste.
    private void ShowVaultDetail(VaultCredential? cred)
    {
        _selectedVaultCredential = cred;
        _totpTimer?.Stop();
        _totpTimer = null;
        VaultDetailPanel.Children.Clear();

        if (cred is null)
        {
            VaultDetailPanel.Visibility = Visibility.Collapsed;
            VaultDetailEmptyText.Visibility = Visibility.Visible;
            return;
        }

        VaultDetailEmptyText.Visibility = Visibility.Collapsed;
        VaultDetailPanel.Visibility = Visibility.Visible;

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        headerRow.Children.Add(BuildFaviconElement(cred.Origin, size: 28));
        var titleStack = new StackPanel { Spacing = 2 };
        titleStack.Children.Add(new TextBlock
        {
            Text = PasswordManagerService.DisplayName(cred),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 18,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (!string.IsNullOrWhiteSpace(cred.Label))
        {
            titleStack.Children.Add(new TextBlock { Text = cred.Origin, Opacity = 0.55, FontSize = 12 });
        }
        headerRow.Children.Add(titleStack);
        VaultDetailPanel.Children.Add(headerRow);

        var hasUser = !string.IsNullOrWhiteSpace(cred.Username);
        VaultDetailPanel.Children.Add(new TextBlock
        {
            Text = hasUser ? cred.Username : "(aucun identifiant enregistré)",
            Opacity = hasUser ? 0.8 : 0.45,
            FontStyle = hasUser ? Windows.UI.Text.FontStyle.Normal : Windows.UI.Text.FontStyle.Italic
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var openBtn = new Button { Content = "Ouvrir la page de connexion" };
        openBtn.Click += (_, _) => OpenVaultLoginPage(cred);
        actions.Children.Add(openBtn);

        var copyUser = new Button { Content = "Copier identifiant", IsEnabled = hasUser };
        copyUser.Click += (_, _) => CopyPasswordManagerText(cred.Username, "Identifiant copié.");
        actions.Children.Add(copyUser);

        var copyPassword = new Button { Content = "Copier mot de passe" };
        copyPassword.Click += (_, _) => CopyPasswordManagerText(cred.Password, "Mot de passe copié.");
        actions.Children.Add(copyPassword);
        VaultDetailPanel.Children.Add(actions);

        var manageActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var renameBtn = new Button { Content = "Renommer" };
        renameBtn.Click += async (_, _) =>
        {
            var box = new TextBox
            {
                Text = cred.Label,
                PlaceholderText = "Nom personnalisé (ex. Amazon perso)",
                MinWidth = 320
            };
            var dlg = new ContentDialog
            {
                Title = "Nom personnalisé",
                Content = box,
                PrimaryButtonText = "Enregistrer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            _passwordManager.RenameById(cred.Id, box.Text);
            RefreshVaultPanel();
            StatusText.Text = "Nom mis à jour.";
        };
        manageActions.Children.Add(renameBtn);

        var deleteBtn = new Button { Content = "Supprimer" };
        deleteBtn.Click += async (_, _) =>
        {
            var confirm = new ContentDialog
            {
                Title = "Supprimer cet identifiant ?",
                Content = $"{(!string.IsNullOrWhiteSpace(cred.Label) ? cred.Label + "\n" : "")}{cred.Origin}\n\nCette action est définitive.",
                PrimaryButtonText = "Supprimer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            _passwordManager.DeleteById(cred.Id);
            _selectedVaultCredential = null;
            StatusText.Text = "Identifiant supprimé.";
            RefreshVaultPanel();
        };
        manageActions.Children.Add(deleteBtn);
        VaultDetailPanel.Children.Add(manageActions);

        VaultDetailPanel.Children.Add(BuildTotpSection(cred));

        VaultDetailPanel.Children.Add(new TextBlock
        {
            Text = $"Mis à jour : {DateTimeOffset.FromUnixTimeSeconds(cred.UpdatedAt).LocalDateTime:dd/MM/yyyy HH:mm}",
            Opacity = 0.5,
            FontSize = 12
        });
    }

    // Authentification a deux facteurs (TOTP) locale, type Google Authenticator :
    // secret chiffre dans vault.lumora, code calcule sur l'appareil, rien envoye
    // nulle part. Saisie manuelle (secret colle ou URI otpauth://) pour cette
    // version, sans scan de QR code.
    private UIElement BuildTotpSection(VaultCredential cred)
    {
        var container = new StackPanel { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        container.Children.Add(new TextBlock
        {
            Text = "Authentification à deux facteurs",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            Opacity = 0.8
        });

        if (string.IsNullOrWhiteSpace(cred.TotpSecret))
        {
            var addBtn = new Button { Content = "Ajouter un code TOTP" };
            addBtn.Click += async (_, _) => await PromptAddTotpAsync(cred);
            container.Children.Add(addBtn);
            return container;
        }

        var codeText = new TextBlock
        {
            FontSize = 26,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        var countdownText = new TextBlock { Opacity = 0.6, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

        void Tick()
        {
            var now = DateTimeOffset.UtcNow;
            codeText.Text = TotpService.GenerateCode(cred.TotpSecret, now, cred.TotpDigits, cred.TotpPeriod);
            countdownText.Text = $"Expire dans {TotpService.SecondsRemaining(now, cred.TotpPeriod)}s";
        }
        Tick();

        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += (_, _) => Tick();
        timer.Start();
        _totpTimer = timer;

        var codeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        codeRow.Children.Add(codeText);
        codeRow.Children.Add(countdownText);
        container.Children.Add(codeRow);

        var totpActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var copyCodeBtn = new Button { Content = "Copier le code" };
        copyCodeBtn.Click += (_, _) => CopyPasswordManagerText(codeText.Text, "Code TOTP copié.");
        totpActions.Children.Add(copyCodeBtn);

        var removeTotpBtn = new Button { Content = "Supprimer le TOTP" };
        removeTotpBtn.Click += (_, _) =>
        {
            _passwordManager.SetTotpById(cred.Id, null);
            StatusText.Text = "TOTP supprimé.";
            RefreshVaultPanel();
        };
        totpActions.Children.Add(removeTotpBtn);
        container.Children.Add(totpActions);

        return container;
    }

    private async Task PromptAddTotpAsync(VaultCredential cred)
    {
        var box = new TextBox
        {
            PlaceholderText = "Secret TOTP ou URI otpauth://...",
            MinWidth = 380
        };
        var errorText = new TextBlock
        {
            Text = "Secret invalide. Vérifiez qu'il s'agit bien d'un secret TOTP (base32) ou d'une URI otpauth://.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Collez le secret fourni par le site (affiché généralement sous le QR code lors de l'activation), ou l'URI otpauth:// complète si vous l'avez exportée.",
            Opacity = 0.7,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(box);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = "Ajouter un code TOTP",
            Content = panel,
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (TotpService.ParseSecretInput(box.Text) is not null) return;
            args.Cancel = true;
            errorText.Visibility = Visibility.Visible;
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var account = TotpService.ParseSecretInput(box.Text);
        if (account is null) return; // garde-fou : déjà validé par PrimaryButtonClick

        _passwordManager.SetTotpById(cred.Id, account.Secret, account.Digits, account.Period);
        StatusText.Text = "Code TOTP ajouté.";
        RefreshVaultPanel();
    }

    // Ouvre la page de connexion mémorisée (repli sur l'origine pour les anciennes
    // entrées sans login_url), puis referme le panneau au profit de la page web.
    private void OpenVaultLoginPage(VaultCredential cred)
    {
        var target = !string.IsNullOrWhiteSpace(cred.LoginUrl) ? cred.LoginUrl : cred.Origin;
        if (!BookmarkStore.IsWebUrl(target))
        {
            StatusText.Text = "Aucune page de connexion enregistrée pour cet identifiant.";
            return;
        }

        NavigateCurrentTab(target, DisplayTitle(target));
    }

    private void PasswordManagerSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        RefreshVaultPanel();

    private void CopyPasswordManagerText(string value, string status) =>
        CopySecretToClipboard(value, status, clearAfterSeconds: 30);

    // Copie d'un secret : exclu de l'historique du presse-papiers et de la synchro
    // cloud, puis effacé après clearAfterSeconds (<= 0 = pas d'effacement auto).
    private void CopySecretToClipboard(string value, string status, int clearAfterSeconds)
    {
        var text = value ?? string.Empty;
        var package = new DataPackage();
        package.SetText(text);
        try
        {
            Clipboard.SetContentWithOptions(package, new ClipboardContentOptions
            {
                IsAllowedInHistory = false,
                IsRoamable = false
            });
        }
        catch
        {
            Clipboard.SetContent(package); // repli si l'API d'options est indisponible
        }

        StatusText.Text = status;
        if (clearAfterSeconds > 0 && text.Length > 0)
        {
            ScheduleClipboardClear(text, clearAfterSeconds);
        }
    }

    private void ScheduleClipboardClear(string copied, int seconds)
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(seconds);
        timer.IsRepeating = false;
        timer.Tick += async (t, _) =>
        {
            t.Stop();
            try
            {
                // N'effacer que si le presse-papiers contient TOUJOURS notre secret,
                // pour ne pas jeter ce que l'utilisateur aurait copié entre-temps.
                var current = Clipboard.GetContent();
                if (current.Contains(StandardDataFormats.Text) &&
                    await current.GetTextAsync() == copied)
                {
                    Clipboard.Clear();
                }
            }
            catch { }
        };
        timer.Start();
    }

    // Génère un mot de passe selon les préférences persistées de l'utilisateur
    // (aléatoire configurable ou phrase de passe), partagées entre le dialogue
    // "Nouvel identifiant" et la barre de suggestion automatique.
    private string GeneratePasswordFromSettings() =>
        _uiSettings.VaultGeneratorMode == "passphrase"
            ? PasswordGenerator.GeneratePassphrase(_uiSettings.VaultGeneratorPassphraseWords)
            : PasswordGenerator.Generate(_uiSettings.VaultGeneratorLength, _uiSettings.VaultGeneratorUseSymbols);

    private async Task<(PasswordManagerEntryDraft draft, bool cancelled)> PromptNewCredentialAsync()
    {
        var labelBox    = new TextBox { PlaceholderText = "Nom optionnel (ex. Micromania perso)", MinWidth = 340 };
        var originBox   = new TextBox { PlaceholderText = "micromania.fr ou https://auth.micromania.fr", MinWidth = 340 };
        var loginUrlBox = new TextBox { PlaceholderText = "URL de connexion optionnelle", MinWidth = 340 };
        var usernameBox = new TextBox { PlaceholderText = "utilisateur@email.com" };
        var passwordBox = new PasswordBox { PlaceholderText = "Mot de passe" };

        // Générateur intégré : mode aléatoire (longueur/symboles) ou phrase de
        // passe (nombre de mots), tirage crypto-sûr. Les réglages choisis ici
        // sont mémorisés et réutilisés par la barre de suggestion automatique.
        var isPassphrase = _uiSettings.VaultGeneratorMode == "passphrase";
        var modeRandomRadio = new RadioButton { Content = "Aléatoire", GroupName = "PwGenMode", IsChecked = !isPassphrase };
        var modePassphraseRadio = new RadioButton { Content = "Phrase de passe", GroupName = "PwGenMode", IsChecked = isPassphrase };
        var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        modeRow.Children.Add(modeRandomRadio);
        modeRow.Children.Add(modePassphraseRadio);

        var lengthBox = new NumberBox
        {
            Value = _uiSettings.VaultGeneratorLength,
            Minimum = PasswordGenerator.MinLength,
            Maximum = 64,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 100,
            Visibility = isPassphrase ? Visibility.Collapsed : Visibility.Visible
        };
        var symbolsCheck = new CheckBox
        {
            Content = "Symboles",
            IsChecked = _uiSettings.VaultGeneratorUseSymbols,
            Visibility = isPassphrase ? Visibility.Collapsed : Visibility.Visible
        };
        var wordCountBox = new NumberBox
        {
            Value = _uiSettings.VaultGeneratorPassphraseWords,
            Minimum = PasswordGenerator.MinPassphraseWords,
            Maximum = PasswordGenerator.MaxPassphraseWords,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 100,
            Visibility = isPassphrase ? Visibility.Visible : Visibility.Collapsed
        };

        void UpdateGeneratorOptionsVisibility()
        {
            var passphraseMode = modePassphraseRadio.IsChecked == true;
            lengthBox.Visibility = passphraseMode ? Visibility.Collapsed : Visibility.Visible;
            symbolsCheck.Visibility = passphraseMode ? Visibility.Collapsed : Visibility.Visible;
            wordCountBox.Visibility = passphraseMode ? Visibility.Visible : Visibility.Collapsed;
        }
        modeRandomRadio.Checked += (_, _) => UpdateGeneratorOptionsVisibility();
        modePassphraseRadio.Checked += (_, _) => UpdateGeneratorOptionsVisibility();

        var generateButton = new Button { Content = "Générer" };
        generateButton.Click += (_, _) =>
        {
            _uiSettings.VaultGeneratorMode = modePassphraseRadio.IsChecked == true ? "passphrase" : "random";
            _uiSettings.VaultGeneratorLength = (int)lengthBox.Value;
            _uiSettings.VaultGeneratorUseSymbols = symbolsCheck.IsChecked == true;
            _uiSettings.VaultGeneratorPassphraseWords = (int)wordCountBox.Value;
            _uiSettings.Save(_profile.UiSettingsFile);

            passwordBox.Password = GeneratePasswordFromSettings();
            passwordBox.PasswordRevealMode = PasswordRevealMode.Visible;
        };

        var generatorOptionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        generatorOptionsRow.Children.Add(lengthBox);
        generatorOptionsRow.Children.Add(symbolsCheck);
        generatorOptionsRow.Children.Add(wordCountBox);
        generatorOptionsRow.Children.Add(generateButton);

        var generatorRow = new StackPanel { Spacing = 6 };
        generatorRow.Children.Add(modeRow);
        generatorRow.Children.Add(generatorOptionsRow);

        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = "Nom" });
        panel.Children.Add(labelBox);
        panel.Children.Add(new TextBlock { Text = "Origine (site web)" });
        panel.Children.Add(originBox);
        panel.Children.Add(new TextBlock { Text = "Page de connexion", Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(loginUrlBox);
        panel.Children.Add(new TextBlock { Text = "Identifiant", Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(usernameBox);
        panel.Children.Add(new TextBlock { Text = "Mot de passe", Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(passwordBox);
        panel.Children.Add(generatorRow);

        var dialog = new ContentDialog
        {
            Title = "Nouvel identifiant",
            Content = panel,
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return (new PasswordManagerEntryDraft("", "", ""), true);

        return (new PasswordManagerEntryDraft(
            originBox.Text.Trim(),
            usernameBox.Text.Trim(),
            passwordBox.Password,
            loginUrlBox.Text.Trim(),
            labelBox.Text.Trim()), false);
    }
}
