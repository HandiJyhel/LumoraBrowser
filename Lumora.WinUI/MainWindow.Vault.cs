using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;
using System.Text.Json;
using System.Text.Json.Nodes;
using Windows.ApplicationModel.DataTransfer;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{

    // ── Capture automatique des identifiants ──────────────────────────────────

    private void CredentialService_CredentialCaptured(CredentialCapture capture)
    {
        if (_isGuestMode) return;
        if (IsFederatedIdentityIntermediary(capture.LoginUrl)) return;

        // Un login réussi est le bon moment pour proposer de garder la session,
        // indépendamment de l'offre d'enregistrement du mot de passe ci-dessous.
        DispatcherQueue.TryEnqueue(() => MaybeOfferSessionKeep(capture.Origin));

        var offer = _passwordManagerInteraction.BuildSaveOffer(capture);
        if (offer is null) return;

        DispatcherQueue.TryEnqueue(() => ShowCredentialSaveOffer(offer));
    }

    private void CredentialService_PageStateChanged(CredentialPageState pageState)
    {
        WinUiRuntimeTrace.Write(
            $"CredentialService_PageStateChanged: origin={pageState.Origin} guestMode={_isGuestMode} vaultLocked={_vault.IsLocked}");
        if (_isGuestMode || _vault.IsLocked) return;
        if (IsFederatedIdentityIntermediary(pageState.LoginUrl))
        {
            DispatcherQueue.TryEnqueue(HideCredentialAutomationBars);
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            var decision = _passwordManagerInteraction.EvaluatePage(pageState.LoginUrl, pageState);
            WinUiRuntimeTrace.Write(
                $"EvaluatePage (etat live) -> kind={decision.Kind} canOffer={decision.CanOfferFill} comptes={decision.Credentials.Count}");
            ApplyPasswordManagerDecision(decision);
        });
    }

    private void ShowCredentialSaveOffer(PasswordManagerSaveOffer offer)
    {
        var draft = offer.Draft;

        if (_pendingCredential is { } existing &&
            existing.Username == draft.Username &&
            OriginOf(existing.Origin) == OriginOf(draft.Origin))
            return;

        _pendingCredential = (draft.Origin, draft.Username, draft.Password, draft.LoginUrl, draft.Label);
        var forWhom = string.IsNullOrWhiteSpace(offer.Username)
            ? offer.DisplayOrigin
            : $"{offer.Username} sur {offer.DisplayOrigin}";
        // Cas "changement de domaine" : le meme compte existe deja sous un autre
        // domaine. On propose de rattacher le nouveau, pas juste d'"enregistrer".
        CredentialSaveText.Text = offer.LinkedFromDomain is { } linkedFrom
            ? $"Ce compte est deja enregistre pour {linkedFrom}. Ajouter aussi {offer.DisplayOrigin} ?"
            : offer.IsUpdate
                ? $"Mettre a jour le mot de passe pour {forWhom} ?"
                : $"Enregistrer le mot de passe pour {forWhom} ?";
        CredentialSaveBar.Visibility = Visibility.Visible;
    }

    private async void BrowserCore_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = JsonNode.Parse(e.WebMessageAsJson);
            if (json is JsonValue value && value.TryGetValue<string>(out var nested))
            {
                json = JsonNode.Parse(nested);
            }

            if (json is not JsonObject obj) return;
            var type = obj["t"]?.GetValue<string>();

            if (type == "nova.loginDiagnostic")
            {
                HandleLoginDiagnosticMessage(obj);
                return;
            }

            if (type == "passkey_created" || type == "passkey_used")
            {
                if (_isGuestMode) return;
                var pkOrigin = obj["o"]?.GetValue<string>() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(pkOrigin)) return;
                if (type == "passkey_created")
                    RecordPasskeyCreated(pkOrigin);
                else
                    RecordPasskeyUsed(pkOrigin);
                return;
            }

            if (type == "nova.payment.form")
            {
                HandlePaymentFormDetected(sender as CoreWebView2);
                return;
            }

            if (type == "nova.fullscreenExit")
            {
                HandleContentFullScreenExitSignal(sender as CoreWebView2);
                return;
            }

            if (type is "newtab_add_shortcut" or "newtab_edit_shortcut" or "newtab_delete_shortcut")
            {
                await HandleNewTabShortcutMessageAsync(type, obj);
                return;
            }
        }
        catch { }
    }

    private async Task HandleNewTabShortcutMessageAsync(string type, JsonObject obj)
    {
        if (_isGuestMode)
        {
            StatusText.Text = "Raccourcis indisponibles en mode invite.";
            return;
        }

        var index = obj["i"]?.GetValue<int>() ?? -1;
        if (type == "newtab_delete_shortcut")
        {
            if (index < 0 || index >= _uiSettings.NewTabShortcuts.Count) return;
            var title = _uiSettings.NewTabShortcuts[index].Title;
            _uiSettings.NewTabShortcuts.RemoveAt(index);
            SaveNewTabShortcutSettings();
            RefreshNovaHomePages();
            StatusText.Text = $"Raccourci supprime : {title}.";
            return;
        }

        var existingTitle = obj["title"]?.GetValue<string>() ?? string.Empty;
        var existingUrl = obj["url"]?.GetValue<string>() ?? string.Empty;
        var shortcut = await PromptNewTabShortcutAsync(
            type == "newtab_edit_shortcut" ? "Modifier le raccourci" : "Ajouter un raccourci",
            existingTitle,
            existingUrl);
        if (shortcut is null) return;

        if (type == "newtab_edit_shortcut" && index >= 0 && index < _uiSettings.NewTabShortcuts.Count)
        {
            _uiSettings.NewTabShortcuts[index] = shortcut;
            StatusText.Text = $"Raccourci modifie : {shortcut.Title}.";
        }
        else
        {
            if (_uiSettings.NewTabShortcuts.Count >= 12)
            {
                StatusText.Text = "Maximum de 12 raccourcis atteint.";
                return;
            }

            _uiSettings.NewTabShortcuts.Add(shortcut);
            // Le toggle "Afficher les raccourcis" (Parametres > Apparence) est
            // desactive par defaut sur un profil neuf. Sans cette ligne, un
            // raccourci ajoute depuis le bouton "+" de la page est enregistre
            // mais ne s'affiche jamais : l'utilisateur qui vient d'utiliser ce
            // bouton veut evidemment VOIR le raccourci qu'il vient de creer.
            _uiSettings.NewTabShortcutsVisible = true;
            StatusText.Text = $"Raccourci ajoute : {shortcut.Title}.";
        }

        SaveNewTabShortcutSettings();
        RefreshNovaHomePages();
    }

    private async Task<NewTabShortcut?> PromptNewTabShortcutAsync(string title, string currentTitle, string currentUrl)
    {
        var titleBox = new TextBox
        {
            Header = "Nom",
            Text = currentTitle,
            MaxLength = 32,
            MinWidth = 320
        };
        var urlBox = new TextBox
        {
            Header = "Adresse",
            Text = currentUrl,
            PlaceholderText = "https://exemple.com",
            MinWidth = 320
        };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(titleBox);
        panel.Children.Add(urlBox);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;

        var cleanTitle = titleBox.Text.Trim();
        var cleanUrl = NewTabMarkup.NormalizeShortcutUrl(urlBox.Text);
        if (string.IsNullOrWhiteSpace(cleanTitle) || string.IsNullOrWhiteSpace(cleanUrl))
        {
            StatusText.Text = "Nom ou adresse manquant.";
            return null;
        }

        return new NewTabShortcut(cleanTitle, cleanUrl);
    }

    private void SaveNewTabShortcutSettings()
    {
        _uiSettings.NewTabShortcuts = _uiSettings.NewTabShortcuts
            .Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.Title) && !string.IsNullOrWhiteSpace(shortcut.Url))
            .Take(12)
            .ToList();
        _uiSettings.Save(_profile.UiSettingsFile);

        _suppressUiSettingsSave = true;
        try
        {
            NewTabShortcutsBox.Text = NewTabShortcutsToText(_uiSettings.NewTabShortcuts);
            NewTabShortcutsSwitch.IsOn = _uiSettings.NewTabShortcutsVisible;
        }
        finally
        {
            _suppressUiSettingsSave = false;
        }
    }

    private void RefreshNovaHomePages()
    {
        foreach (var tab in _tabs.Where(tab => tab.Address.Equals("lumora://accueil", StringComparison.OrdinalIgnoreCase)))
        {
            if (tab.View?.CoreWebView2 is not null)
            {
                tab.View.CoreWebView2.NavigateToString(HomePageHtml());
            }
        }
    }

    private void CredentialSaveAccept_Click(object sender, RoutedEventArgs e)
    {
        CredentialSaveBar.Visibility = Visibility.Collapsed;
        if (_pendingCredential is not { } cred) return;
        _pendingCredential = null;
        if (!_isGuestMode)
            _passwordManager.Save(new PasswordManagerEntryDraft(
                cred.Origin,
                cred.Username,
                cred.Password,
                cred.LoginUrl,
                cred.Label));
        StatusText.Text = $"Identifiants enregistres pour {cred.Origin}.";
    }

    private void CredentialSaveDismiss_Click(object sender, RoutedEventArgs e)
    {
        CredentialSaveBar.Visibility = Visibility.Collapsed;
        _pendingCredential = null;
    }

    // ── Remplissage automatique des identifiants ──────────────────────────────

    // Rapport differe du script de remplissage : le site a efface la valeur
    // apres coup (re-render SPA) et le nouvel essai a echoue aussi.
    private void CredentialService_FillReported(CredentialFillResult result)
    {
        DispatcherQueue.TryEnqueue(() => StatusText.Text = result.Message);
    }

    private void OfferAutoFill(string address)
    {
        if (!BookmarkStore.IsWebUrl(address)) return;
        if (IsFederatedIdentityIntermediary(address))
        {
            HideCredentialAutomationBars();
            return;
        }
        if (_isGuestMode || _vault.IsLocked)
        {
            WinUiRuntimeTrace.Write(
                $"OfferAutoFill: bloque avant evaluation (guestMode={_isGuestMode} vaultLocked={_vault.IsLocked}) address={address}");
            AutoFillBar.Visibility = Visibility.Collapsed;
            return;
        }
        var decision = _passwordManagerInteraction.EvaluatePage(address);
        WinUiRuntimeTrace.Write(
            $"OfferAutoFill (sans etat live) address={address} kind={decision.Kind} canOffer={decision.CanOfferFill}");
        ApplyPasswordManagerDecision(decision);
    }

    private void ApplyPasswordManagerDecision(PasswordManagerPageDecision decision)
    {
        if (decision.Kind == PasswordManagerPromptKind.SuggestNewPassword)
        {
            ShowSuggestPasswordBar();
            return;
        }

        // Un champ "nouveau mot de passe" n'est plus en contexte (rempli, page
        // changee...) : refermer la barre de suggestion si elle etait ouverte.
        SuggestPasswordBar.Visibility = Visibility.Collapsed;
        _pendingGeneratedPassword = null;

        if (!decision.CanOfferFill || decision.Credentials.Count == 0)
        {
            WinUiRuntimeTrace.Write($"ApplyPasswordManagerDecision: barre masquee (kind={decision.Kind})");
            AutoFillBar.Visibility = Visibility.Collapsed;
            _pendingAutoFillCandidates = Array.Empty<VaultCredential>();
            if (decision.Kind == PasswordManagerPromptKind.WaitingForPasswordField &&
                decision.PageState is not null &&
                !string.IsNullOrWhiteSpace(decision.Message))
            {
                StatusText.Text = decision.Message;
            }
            return;
        }

        WinUiRuntimeTrace.Write(
            $"ApplyPasswordManagerDecision: barre affichee (kind={decision.Kind} comptes={decision.Credentials.Count})");
        _pendingAutoFillCandidates = decision.Credentials;
        AutoFillText.Text = decision.Message;

        // Un seul compte trouvé : bouton direct "Remplir" (comportement historique).
        // Plusieurs comptes (ex. 2 comptes Google) : bouton "Choisir un compte" avec la liste.
        if (decision.Credentials.Count == 1)
        {
            AutoFillAccept.Visibility = Visibility.Visible;
            AutoFillChooseButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            AutoFillAccept.Visibility = Visibility.Collapsed;
            AutoFillChooseButton.Visibility = Visibility.Visible;
            AutoFillAccountsFlyout.Items.Clear();
            foreach (var candidate in decision.Credentials)
            {
                var item = new MenuFlyoutItem
                {
                    Text = string.IsNullOrWhiteSpace(candidate.Username) ? "(sans identifiant)" : candidate.Username,
                    Icon = new SymbolIcon(Symbol.Contact),
                    Tag = candidate
                };
                item.Click += AutoFillAccountItem_Click;
                AutoFillAccountsFlyout.Items.Add(item);
            }
        }

        AutoFillBar.Visibility = Visibility.Visible;
    }

    private async void AutoFillAccept_Click(object sender, RoutedEventArgs e)
    {
        var cred = _pendingAutoFillCandidates.Count > 0 ? _pendingAutoFillCandidates[0] : null;
        AutoFillBar.Visibility = Visibility.Collapsed;
        _pendingAutoFillCandidates = Array.Empty<VaultCredential>();
        if (cred is null) return;

        var result = await _credentialService.FillAsync(cred);
        StatusText.Text = result.Message;
    }

    private async void AutoFillAccountItem_Click(object sender, RoutedEventArgs e)
    {
        AutoFillBar.Visibility = Visibility.Collapsed;
        _pendingAutoFillCandidates = Array.Empty<VaultCredential>();
        if (sender is not MenuFlyoutItem { Tag: VaultCredential cred }) return;

        var result = await _credentialService.FillAsync(cred);
        StatusText.Text = result.Message;
    }

    private void AutoFillDismiss_Click(object sender, RoutedEventArgs e)
    {
        AutoFillBar.Visibility = Visibility.Collapsed;
        _pendingAutoFillCandidates = Array.Empty<VaultCredential>();
    }

    // Ne regenere que si la barre n'etait pas deja affichee, pour ne pas changer
    // le mot de passe propose a chaque re-evaluation de l'etat de page (frappe
    // dans un champ voisin, mutation du DOM...) tant que l'utilisateur ne le
    // demande pas explicitement (bouton "Regenerer").
    private void ShowSuggestPasswordBar()
    {
        if (SuggestPasswordBar.Visibility == Visibility.Visible && _pendingGeneratedPassword is not null)
        {
            return;
        }

        _pendingGeneratedPassword = PasswordGenerator.Generate(20);
        SuggestPasswordValueText.Text = _pendingGeneratedPassword;
        SuggestPasswordBar.Visibility = Visibility.Visible;
    }

    private async void SuggestPasswordAccept_Click(object sender, RoutedEventArgs e)
    {
        var generated = _pendingGeneratedPassword;
        SuggestPasswordBar.Visibility = Visibility.Collapsed;
        _pendingGeneratedPassword = null;
        if (string.IsNullOrEmpty(generated)) return;

        var result = await _credentialService.FillGeneratedPasswordAsync(generated);
        StatusText.Text = result.Message;
    }

    private void SuggestPasswordRegenerate_Click(object sender, RoutedEventArgs e)
    {
        _pendingGeneratedPassword = PasswordGenerator.Generate(20);
        SuggestPasswordValueText.Text = _pendingGeneratedPassword;
    }

    private void SuggestPasswordDismiss_Click(object sender, RoutedEventArgs e)
    {
        SuggestPasswordBar.Visibility = Visibility.Collapsed;
        _pendingGeneratedPassword = null;
    }

    // ── Mot de passe du coffre ────────────────────────────────────────────────
    //
    // Le coffre est protégé par le mot de passe du profil (couplage automatique à
    // la création / au login). Il n'y a donc plus de « mot de passe maître »
    // séparé à activer : le fichier vault.lumora est portable et ne dépend que de
    // ce mot de passe. Le changement de mot de passe re-clé le coffre
    // (voir ChangeProfilePasswordButton_Click).

    private async Task<string?> PromptMasterPasswordAsync(string title, bool confirm)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = "Confirmer",
            CloseButtonText = "Annuler",
            XamlRoot = Content.XamlRoot
        };

        var panel = new StackPanel { Spacing = 10 };
        var pwBox = new PasswordBox { PlaceholderText = "Mot de passe", MinWidth = 260 };
        panel.Children.Add(pwBox);

        PasswordBox? pwBox2 = null;
        if (confirm)
        {
            pwBox2 = new PasswordBox { PlaceholderText = "Confirmer le mot de passe", MinWidth = 260 };
            panel.Children.Add(pwBox2);
        }

        dialog.Content = panel;
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;
        if (string.IsNullOrWhiteSpace(pwBox.Password)) return null;
        if (pwBox2 is not null && pwBox.Password != pwBox2.Password)
        {
            StatusText.Text = "Les mots de passe ne correspondent pas.";
            return null;
        }

        return pwBox.Password;
    }

    private async Task<bool> UnlockVaultIfNeededAsync()
    {
        if (!_vault.HasMasterPassword) return true;
        if (!_vault.IsLocked) return true;

        var pw = await PromptMasterPasswordAsync("Coffre verrouille - entrez le mot de passe du profil", confirm: false);
        if (string.IsNullOrWhiteSpace(pw)) return false;

        return _vault.Unlock(pw);
    }

    // Barrière d'accès au coffre : re-demande le PIN (si activé) sinon le mot de passe,
    // À CHAQUE ouverture — même déjà connecté. Le coffre est la zone la plus sensible.
    private async Task<bool> RequireVaultAccessAsync()
    {
        if (_userProfile is null) return false;

        if (_userProfile.HasPinLogin)
        {
            var pin = await PromptMasterPasswordAsync("Entrez votre code PIN pour ouvrir le coffre", confirm: false);
            if (string.IsNullOrWhiteSpace(pin) || !_userProfile.VerifyPin(pin)) return false;
            if (_vault.IsLocked) _vault.UnlockWithPin(pin);
            return true;
        }

        var pw = await PromptMasterPasswordAsync("Entrez votre mot de passe pour ouvrir le coffre", confirm: false);
        if (string.IsNullOrWhiteSpace(pw) || !_userProfile.VerifyPassword(pw)) return false;
        if (_vault.IsLocked) _vault.EnsureUnlockedWith(pw);
        return true;
    }

    private async void VaultMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode)
        {
            StatusText.Text = "Coffre indisponible en mode invité.";
            return;
        }

        if (!await RequireVaultAccessAsync())
        {
            StatusText.Text = "Acces au coffre refuse : code incorrect ou annule.";
            return;
        }

        ShowPanel(VaultPanel, "Gestionnaire de mots de passe");
        SyncFromBrowserStore();
        RefreshVaultPanel();
    }

    private async void ImportPasswordsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode)
        {
            StatusText.Text = "Import des mots de passe indisponible en mode invité.";
            return;
        }

        if (!await RequireVaultAccessAsync())
        {
            StatusText.Text = "Acces au coffre refuse : import annule.";
            return;
        }

        ShowPanel(VaultPanel, "Importer des mots de passe");
        SyncFromBrowserStore();
        RefreshVaultPanel();
        await ImportPasswordsAsync();
    }

    // Point d'entree unique pour l'import de mots de passe : propose une source
    // CSV ou, si des navigateurs Chromium tiers (Chrome, Edge, Brave...) sont
    // detectes sur la machine avec des identifiants enregistres, propose de les
    // importer directement (dechiffrement local DPAPI, aucun reseau, aucune extension).
    private async Task ImportPasswordsAsync()
    {
        var sources = PasswordImportSource.Discover();
        if (sources.Count == 0)
        {
            await ImportPasswordsCsvAsync();
            return;
        }

        var choice = await PromptPasswordImportSourceAsync(sources);
        if (choice < 0)
        {
            StatusText.Text = "Import annule.";
            return;
        }

        if (choice == 0)
        {
            await ImportPasswordsCsvAsync();
            return;
        }

        await ImportPasswordsFromBrowserAsync(sources[choice - 1]);
    }

    // Retourne -1 (annule), 0 (fichier CSV) ou l'index+1 dans `sources`.
    private async Task<int> PromptPasswordImportSourceAsync(IReadOnlyList<PasswordImportSource> sources)
    {
        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 260 };
        list.Items.Add("Fichier CSV (Proton Pass, Bitwarden, 1Password, export...)");
        foreach (var source in sources) list.Items.Add(source.Label);
        list.SelectedIndex = 1;

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = "Depuis quelle source importer les mots de passe ?",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(list);
        panel.Children.Add(new TextBlock
        {
            Text = "L'import depuis un navigateur lit son magasin local et le dechiffre sur cette machine (DPAPI), sans extension ni reseau.",
            Opacity = 0.6,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            Title = "Importer des mots de passe",
            Content = panel,
            PrimaryButtonText = "Continuer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return -1;
        return list.SelectedIndex < 0 ? -1 : list.SelectedIndex;
    }

    private async Task ImportPasswordsFromBrowserAsync(PasswordImportSource source)
    {
        try
        {
            var creds = source.ReadCredentials();
            if (creds.Count == 0)
            {
                StatusText.Text = $"Aucun identifiant lisible depuis {source.Browser}.";
                return;
            }

            var confirm = new ContentDialog
            {
                Title = "Importer ces identifiants ?",
                Content = $"{creds.Count} identifiant(s) detecte(s) dans {source.Browser} ({source.Profile}).\n\n" +
                          "Ils seront dechiffres localement puis stockes dans vault.lumora.",
                PrimaryButtonText = "Importer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                StatusText.Text = "Import navigateur annule.";
                return;
            }

            var n = _passwordManager.ImportClear(creds);
            RefreshVaultPanel();
            StatusText.Text = $"Import termine : {n} identifiant(s) ajoute(s) ou mis a jour depuis {source.Browser}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur d'import navigateur : {ex.Message}";
        }
    }

    // Importe dans vault.lumora les mots de passe éventuellement présents dans le magasin
    // Chromium (legacy / migration). En mode 100% maison, Chromium ne stocke plus rien de
    // neuf, mais cette lecture récupère ce qui aurait été enregistré avant la bascule.
    private int SyncFromBrowserStore()
    {
        if (_isGuestMode || _vault.IsLocked) return 0;
        try
        {
            var creds = ChromiumCredentialReader.Read(_profile.BrowserDataDir);
            return creds.Count > 0 ? _vault.ImportClear(creds) : 0;
        }
        catch { return 0; }
    }

    // Migration 100% maison : rapatrie ce que Chromium a déjà enregistré dans vault.lumora,
    // puis EFFACE définitivement le coffre de mots de passe de Chromium, pour que le seul
    // stockage soit vault.lumora.
    private async Task MigrateAndClearBrowserPasswordsAsync(CoreWebView2? core = null)
    {
        // Garde essentielle : ne jamais vider Chromium si le coffre est verrouillé
        // (on ne pourrait pas rapatrier d'abord → perte de données).
        if (_isGuestMode || _vault.IsLocked) return;
        // Une fois par lancement suffit : le profil Chromium est partagé par tous
        // les moteurs (un WebView2 par onglet).
        if (_browserPasswordsMigrated) return;
        try
        {
            SyncFromBrowserStore();
            core ??= _browserView?.CoreWebView2;
            if (core is not null)
            {
                await core.Profile.ClearBrowsingDataAsync(
                    Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.PasswordAutosave
                    | Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.GeneralAutofill);
                _browserPasswordsMigrated = true;
            }
        }
        catch { }
    }

    private void VaultRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var n = SyncFromBrowserStore();
        RefreshVaultPanel();
        StatusText.Text = n > 0
            ? $"Coffre synchronise : {n} identifiant(s) depuis le navigateur."
            : "Coffre a jour.";
    }

    // Fusion des doublons d'import (meme site racine, meme identifiant, meme mot
    // de passe) : detection d'abord, puis confirmation explicite avant suppression.
    private async void VaultMergeButton_Click(object sender, RoutedEventArgs e)
    {
        var duplicates = _passwordManager.FindDuplicates();
        if (duplicates.Count == 0)
        {
            StatusText.Text = "Aucun doublon dans le coffre.";
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Fusionner les doublons ?",
            Content = $"{duplicates.Count} entree(s) en double detectee(s) : meme site, meme identifiant et meme " +
                      "mot de passe enregistres plusieurs fois (souvent des variantes www. ou sous-domaines " +
                      "issues d'un import).\n\nChaque groupe sera reduit a une seule entree. Deux comptes dont " +
                      "le mot de passe differe ne sont jamais fusionnes.",
            PrimaryButtonText = "Fusionner",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            StatusText.Text = "Fusion annulee.";
            return;
        }

        var n = _passwordManager.MergeDuplicates();
        RefreshVaultPanel();
        StatusText.Text = $"Fusion terminee : {n} doublon(s) supprime(s).";
    }

    private async void VaultAddButton_Click(object sender, RoutedEventArgs e)
    {
        var (draft, cancelled) = await PromptNewCredentialAsync();
        if (cancelled) { StatusText.Text = "Ajout annule."; return; }

        if (!_passwordManager.Save(draft))
        {
            StatusText.Text = "Identifiant incomplet : site et mot de passe requis.";
            return;
        }

        RefreshVaultPanel();
        StatusText.Text = $"Identifiant enregistre : {draft.Username}";
    }

    // ── Export / Import (maîtrise du fichier par l'utilisateur) ────────────────

    private async void VaultExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vault.IsLocked && !await UnlockVaultIfNeededAsync()) return;

        var items = _passwordManager.ExportClear();
        if (items.Count == 0) { StatusText.Text = "Coffre vide : rien a exporter."; return; }

        // Avertissement explicite : l'export produit un fichier EN CLAIR.
        var warn = new ContentDialog
        {
            Title = "Exporter en clair ?",
            Content = "Le fichier CSV genere contiendra vos mots de passe EN CLAIR, non chiffres. " +
                      "Rangez-le en lieu sur et supprimez-le apres usage. Continuer ?",
            PrimaryButtonText = "Exporter",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        if (await warn.ShowAsync() != ContentDialogResult.Primary) return;

        var picker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop,
            SuggestedFileName = "lumora-coffre-export"
        };
        picker.FileTypeChoices.Add("Fichier CSV", new List<string> { ".csv" });
        WinRT.Interop.InitializeWithWindow.Initialize(
            picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("name,url,username,password");
        foreach (var c in items)
            sb.AppendLine(string.Join(',',
                CredentialCsv.Escape(PasswordManagerService.DisplayName(c)),
                CredentialCsv.Escape(string.IsNullOrWhiteSpace(c.LoginUrl) ? c.Origin : c.LoginUrl),
                CredentialCsv.Escape(c.Username), CredentialCsv.Escape(c.Password)));

        await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString());
        StatusText.Text = $"Export termine : {items.Count} identifiant(s) vers {file.Name}.";
    }

    private async void VaultImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vault.IsLocked && !await UnlockVaultIfNeededAsync()) return;
        await ImportPasswordsAsync();
    }

    private async Task ImportPasswordsCsvAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop
        };
        picker.FileTypeFilter.Add(".csv");
        WinRT.Interop.InitializeWithWindow.Initialize(
            picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            var text = await Windows.Storage.FileIO.ReadTextAsync(file);
            var items = CredentialCsv.Parse(text);
            if (items.Count == 0) { StatusText.Text = "Aucun identifiant reconnu dans ce fichier."; return; }

            var confirm = new ContentDialog
            {
                Title = "Importer ce fichier CSV ?",
                Content = $"{items.Count} identifiant(s) reconnu(s) dans {file.Name}.\n\n" +
                          "Le CSV contient des mots de passe en clair. Apres import, ils seront stockes dans vault.lumora.",
                PrimaryButtonText = "Importer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                StatusText.Text = "Import CSV annule.";
                return;
            }

            var n = _passwordManager.ImportClear(items);
            RefreshVaultPanel();
            StatusText.Text = $"Import termine : {n} identifiant(s) ajoute(s) ou mis a jour.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur d'import : {ex.Message}";
        }
    }

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
                    : "Aucun identifiant ne correspond a cette recherche.",
                Opacity = 0.65,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var cred in credentials)
        {
            VaultPanelItems.Children.Add(BuildVaultCard(cred));
        }
    }

    private UIElement BuildVaultCard(VaultCredential cred)
    {
        var hasLabel = !string.IsNullOrWhiteSpace(cred.Label);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = PasswordManagerService.DisplayName(cred),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleBlock, 0);
        headerGrid.Children.Add(titleBlock);

        var openBtn = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new FontIcon
            {
                Glyph = "", // Globe
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14
            }
        };
        ToolTipService.SetToolTip(openBtn, "Ouvrir la page de connexion");
        openBtn.Click += (_, _) => OpenVaultLoginPage(cred);
        Grid.SetColumn(openBtn, 1);
        headerGrid.Children.Add(openBtn);

        var editBtn = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        editBtn.Content = new FontIcon
        {
            Glyph = "",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14
        };
        ToolTipService.SetToolTip(editBtn, "Renommer cet identifiant");
        editBtn.Click += async (_, _) =>
        {
            var box = new TextBox
            {
                Text = cred.Label,
                PlaceholderText = "Nom personnalise (ex. Amazon perso)",
                MinWidth = 320
            };
            var dlg = new ContentDialog
            {
                Title = "Nom personnalise",
                Content = box,
                PrimaryButtonText = "Enregistrer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            _passwordManager.RenameById(cred.Id, box.Text);
            RefreshVaultPanel();
            StatusText.Text = "Nom mis a jour.";
        };
        Grid.SetColumn(editBtn, 2);
        headerGrid.Children.Add(editBtn);

        var deleteBtn = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        deleteBtn.Content = new FontIcon
        {
            Glyph = "",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14
        };
        ToolTipService.SetToolTip(deleteBtn, "Supprimer cet identifiant");
        deleteBtn.Click += async (_, _) =>
        {
            var confirm = new ContentDialog
            {
                Title = "Supprimer cet identifiant ?",
                Content = $"{(hasLabel ? cred.Label + "\n" : "")}{cred.Origin}\n\nCette action est definitive.",
                PrimaryButtonText = "Supprimer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            _passwordManager.DeleteById(cred.Id);
            StatusText.Text = "Identifiant supprime.";
            RefreshVaultPanel();
        };
        Grid.SetColumn(deleteBtn, 3);
        headerGrid.Children.Add(deleteBtn);

        var body = new StackPanel();
        body.Children.Add(headerGrid);

        // Si un nom perso est défini, rappeler le site en dessous.
        if (hasLabel)
            body.Children.Add(new TextBlock
            {
                Text = cred.Origin,
                Opacity = 0.55,
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0)
            });

        // Identifiant, ou libellé propre si vide.
        var hasUser = !string.IsNullOrWhiteSpace(cred.Username);
        body.Children.Add(new TextBlock
        {
            Text = hasUser ? cred.Username : "(aucun identifiant enregistre)",
            Opacity = hasUser ? 0.75 : 0.45,
            FontStyle = hasUser ? Windows.UI.Text.FontStyle.Normal : Windows.UI.Text.FontStyle.Italic,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var copyUser = new Button
        {
            Content = "Copier identifiant",
            IsEnabled = hasUser
        };
        copyUser.Click += (_, _) => CopyPasswordManagerText(cred.Username, "Identifiant copie.");
        actions.Children.Add(copyUser);

        var copyPassword = new Button { Content = "Copier mot de passe" };
        copyPassword.Click += (_, _) => CopyPasswordManagerText(cred.Password, "Mot de passe copie.");
        actions.Children.Add(copyPassword);

        body.Children.Add(actions);

        body.Children.Add(new TextBlock
        {
            Text = $"Mis a jour : {DateTimeOffset.FromUnixTimeSeconds(cred.UpdatedAt).LocalDateTime:dd/MM/yyyy HH:mm}",
            Opacity = 0.5,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0)
        });

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = body
        };
    }

    // Ouvre la page de connexion mémorisée (repli sur l'origine pour les anciennes
    // entrées sans login_url), puis referme le panneau au profit de la page web.
    private void OpenVaultLoginPage(VaultCredential cred)
    {
        var target = !string.IsNullOrWhiteSpace(cred.LoginUrl) ? cred.LoginUrl : cred.Origin;
        if (!BookmarkStore.IsWebUrl(target))
        {
            StatusText.Text = "Aucune page de connexion enregistree pour cet identifiant.";
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

    private async Task<(PasswordManagerEntryDraft draft, bool cancelled)> PromptNewCredentialAsync()
    {
        var labelBox    = new TextBox { PlaceholderText = "Nom optionnel (ex. Micromania perso)", MinWidth = 340 };
        var originBox   = new TextBox { PlaceholderText = "micromania.fr ou https://auth.micromania.fr", MinWidth = 340 };
        var loginUrlBox = new TextBox { PlaceholderText = "URL de connexion optionnelle", MinWidth = 340 };
        var usernameBox = new TextBox { PlaceholderText = "utilisateur@email.com" };
        var passwordBox = new PasswordBox { PlaceholderText = "Mot de passe" };

        // Générateur intégré : longueur réglable + tirage crypto-sûr, révélé après génération.
        var lengthBox = new NumberBox
        {
            Value = 20,
            Minimum = PasswordGenerator.MinLength,
            Maximum = 64,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 120
        };
        var generateButton = new Button { Content = "Générer" };
        generateButton.Click += (_, _) =>
        {
            passwordBox.Password = PasswordGenerator.Generate((int)lengthBox.Value);
            passwordBox.PasswordRevealMode = PasswordRevealMode.Visible;
        };
        var generatorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        generatorRow.Children.Add(lengthBox);
        generatorRow.Children.Add(generateButton);

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

    // ── Clés d'accès (Passkeys) ───────────────────────────────────────────────

    private void LoadPasskeys()
    {
        _passkeys.Clear();
        if (_isGuestMode) return;
        try
        {
            var text = LumoraFile.TryReadAllText(_profile.PasskeysFile);
            if (text is null) return;
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var entry = PasskeyEntry.TryParse(trimmed);
                if (entry is not null) _passkeys.Add(entry);
            }
        }
        catch { }
    }

    private void SavePasskeys()
    {
        if (_isGuestMode) return;
        try
        {
            var text = string.Join('\n', _passkeys.Select(p => p.Serialize()));
            LumoraFile.WriteAllText(_profile.PasskeysFile, text);
        }
        catch { }
    }

    private void RecordPasskeyCreated(string origin)
    {
        var now      = DateTimeOffset.UtcNow;
        var existing = _passkeys.FirstOrDefault(p =>
            p.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            _passkeys[_passkeys.IndexOf(existing)] = existing with { LastUsedAt = now };
        else
            _passkeys.Add(new PasskeyEntry(origin, now, now));
        SavePasskeys();
    }

    private void RecordPasskeyUsed(string origin)
    {
        var existing = _passkeys.FirstOrDefault(p =>
            p.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _passkeys[_passkeys.IndexOf(existing)] = existing with { LastUsedAt = DateTimeOffset.UtcNow };
            SavePasskeys();
        }
    }

    private void PasskeysMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode)
        {
            StatusText.Text = "Clés d'accès indisponibles en mode invité.";
            return;
        }
        ShowPanel(PasskeysPanel, "Clés d'accès (Passkeys)");
        RenderPasskeysPanel();
    }

    private async void PasskeysWindowsSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-passkeys"));
        }
        catch
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:privacy-passkeys",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private void RenderPasskeysPanel()
    {
        PasskeysPanelItems.Children.Clear();

        if (_passkeys.Count == 0)
        {
            PasskeysPanelItems.Children.Add(new TextBlock
            {
                Text = "Aucune clé d'accès enregistrée pour l'instant.",
                Opacity = 0.65,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var entry in _passkeys.OrderByDescending(p => p.LastUsedAt))
        {
            PasskeysPanelItems.Children.Add(BuildPasskeyCard(entry));
        }
    }

    private UIElement BuildPasskeyCard(PasskeyEntry entry)
    {
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var originBlock = new TextBlock
        {
            Text = entry.Origin,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(originBlock, 0);
        headerGrid.Children.Add(originBlock);

        var deleteBtn = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        deleteBtn.Content = new FontIcon
        {
            // Corbeille (U+E74D). Le caractere avait ete perdu (chaine vide) :
            // le bouton s'affichait sans icone.
            Glyph = "", // Corbeille (Delete)
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14
        };
        ToolTipService.SetToolTip(deleteBtn, "Supprimer cette clé d'accès");
        var captured = entry;
        deleteBtn.Click += (_, _) => PasskeyDeleteButton_Click(captured);
        Grid.SetColumn(deleteBtn, 1);
        headerGrid.Children.Add(deleteBtn);

        var createdBlock = new TextBlock
        {
            Text = $"Créée le {entry.CreatedAt.LocalDateTime:dd/MM/yyyy}",
            Opacity = 0.65,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var usedBlock = new TextBlock
        {
            Text = $"Dernière utilisation : {entry.LastUsedAt.LocalDateTime:dd/MM/yyyy HH:mm}",
            Opacity = 0.5,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0)
        };

        var body = new StackPanel();
        body.Children.Add(headerGrid);
        body.Children.Add(createdBlock);
        body.Children.Add(usedBlock);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = body
        };
    }

    private void PasskeyDeleteButton_Click(PasskeyEntry entry)
    {
        _passkeys.Remove(entry);
        SavePasskeys();
        RenderPasskeysPanel();
        StatusText.Text = $"Clé d'accès supprimée : {entry.Origin}";
    }
}
