using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;

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

        // Complement a la capture d'identifiants classique, voir
        // MaybeOfferSessionKeepFromRecentLoginAsync (MainWindow.Sessions.cs) :
        // memorise qu'un champ mot de passe a ete vu ici, meme si aucun POST
        // classique ne suit (flux JS multi-etapes, ex. Google).
        if (pageState.HasPasswordField)
            DispatcherQueue.TryEnqueue(() => RecordPasswordFieldSighting(pageState.Origin));

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
        // Cas "changement de domaine" : le meme compte existe deja sous un autre
        // domaine. On propose de rattacher le nouveau, pas juste d'"enregistrer".
        // L'identifiant n'est plus mis dans ce texte : il est affiche (et
        // modifiable) dans le champ juste en dessous, seule vraie source de
        // verite au moment d'enregistrer.
        CredentialSaveText.Text = offer.LinkedFromDomain is { } linkedFrom
            ? $"Ce compte est déjà enregistré pour {linkedFrom}. Ajouter aussi {offer.DisplayOrigin} ?"
            : offer.IsUpdate
                ? $"Mettre à jour le mot de passe pour {offer.DisplayOrigin} ?"
                : $"Enregistrer le mot de passe pour {offer.DisplayOrigin} ?";
        CredentialSaveUsernameBox.Text = draft.Username;
        CredentialSavePasswordBox.Password = draft.Password;
        CredentialSaveBar.Visibility = Visibility.Visible;
    }

    private void CredentialSaveAccept_Click(object sender, RoutedEventArgs e)
    {
        CredentialSaveBar.Visibility = Visibility.Collapsed;
        if (_pendingCredential is not { } cred) return;
        _pendingCredential = null;

        // La detection reste une heuristique (surtout pour l'identifiant, voir
        // CredentialCaptureUsernameModule.js) : on enregistre les valeurs telles
        // qu'affichees dans la barre, que l'utilisateur ait pu les corriger ou
        // non, jamais la capture brute directement.
        var username = CredentialSaveUsernameBox.Text?.Trim() ?? string.Empty;
        var password = CredentialSavePasswordBox.Password ?? string.Empty;

        if (!_isGuestMode)
            _passwordManager.Save(new PasswordManagerEntryDraft(
                cred.Origin,
                username,
                password,
                cred.LoginUrl,
                cred.Label));
        StatusText.Text = $"Identifiants enregistrés pour {cred.Origin}.";
        CredentialSaveUsernameBox.Text = string.Empty;
        CredentialSavePasswordBox.Password = string.Empty;
    }

    private void CredentialSaveDismiss_Click(object sender, RoutedEventArgs e)
    {
        CredentialSaveBar.Visibility = Visibility.Collapsed;
        _pendingCredential = null;
        CredentialSaveUsernameBox.Text = string.Empty;
        CredentialSavePasswordBox.Password = string.Empty;
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

        _pendingGeneratedPassword = GeneratePasswordFromSettings();
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
        _pendingGeneratedPassword = GeneratePasswordFromSettings();
        SuggestPasswordValueText.Text = _pendingGeneratedPassword;
    }

    private void SuggestPasswordDismiss_Click(object sender, RoutedEventArgs e)
    {
        SuggestPasswordBar.Visibility = Visibility.Collapsed;
        _pendingGeneratedPassword = null;
    }
}
