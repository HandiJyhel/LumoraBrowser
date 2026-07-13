using Lumora.WinUI.Credentials;

namespace Lumora.WinUI.PasswordManager;

internal sealed class PasswordManagerInteractionService
{
    private readonly PasswordManagerService _passwordManager;
    private readonly Dictionary<string, CredentialPageState> _pageStatesByRoot = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _recentUsersByRoot = new(StringComparer.OrdinalIgnoreCase);

    public PasswordManagerInteractionService(PasswordManagerService passwordManager)
    {
        _passwordManager = passwordManager;
    }

    public PasswordManagerPageDecision EvaluatePage(string address, CredentialPageState? pageState = null)
    {
        var normalizedAddress = PasswordManagerService.NormalizeLoginUrl(address, string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedAddress))
        {
            normalizedAddress = address?.Trim() ?? string.Empty;
        }

        if (pageState is not null)
        {
            RememberPageState(pageState);
            normalizedAddress = string.IsNullOrWhiteSpace(pageState.LoginUrl)
                ? normalizedAddress
                : pageState.LoginUrl;
        }
        else
        {
            // Pas d'état live fourni (cas de l'appel depuis NavigationCompleted, avant
            // que le script de la page ait pu publier son état) : on retombe sur le
            // dernier état connu pour ce domaine plutot que de traiter la page comme
            // vierge, sans quoi cet appel écraserait systématiquement la barre déjà
            // affichée par un état réel reçu juste avant.
            var knownRoot = PublicSuffixService.RootDomainOf(normalizedAddress);
            if (!string.IsNullOrWhiteSpace(knownRoot) && _pageStatesByRoot.TryGetValue(knownRoot, out var knownState))
            {
                pageState = knownState;
            }
        }

        // Un champ "nouveau mot de passe" (inscription, changement de mot de passe)
        // prend le pas sur toute proposition de remplissage d'un identifiant existant :
        // remplir l'ancien mot de passe dans ce champ n'a jamais de sens, meme si
        // l'utilisateur a deja un compte enregistre pour ce site.
        if (pageState?.HasEmptyNewPasswordField == true)
        {
            return new PasswordManagerPageDecision(
                PasswordManagerPromptKind.SuggestNewPassword,
                Array.Empty<VaultCredential>(),
                "Utiliser un mot de passe fort genere ?",
                normalizedAddress,
                pageState);
        }

        var matches = _passwordManager.FindAllForAddress(normalizedAddress);
        Lumora.WinUI.WinUiRuntimeTrace.Write(
            $"EvaluatePage: address={normalizedAddress} matches={matches.Count} hasUser={pageState?.HasUsernameField} hasPass={pageState?.HasPasswordField}");
        if (matches.Count == 0)
        {
            return new PasswordManagerPageDecision(
                PasswordManagerPromptKind.None,
                Array.Empty<VaultCredential>(),
                string.Empty,
                normalizedAddress,
                pageState);
        }

        var who = matches.Count == 1
            ? $"le compte {matches[0].Username}"
            : $"un compte ({matches.Count} enregistres pour ce site)";

        var hasPasswordField = pageState?.HasPasswordField;
        if (hasPasswordField == true)
        {
            return new PasswordManagerPageDecision(
                PasswordManagerPromptKind.FillAvailable,
                matches,
                $"Remplir avec {who} ?",
                normalizedAddress,
                pageState);
        }

        if (pageState?.HasUsernameField == true)
        {
            return new PasswordManagerPageDecision(
                PasswordManagerPromptKind.UsernameFillAvailable,
                matches,
                $"Remplir l'identifiant ({who}) ?",
                normalizedAddress,
                pageState);
        }

        var root = PublicSuffixService.RootDomainOf(normalizedAddress);
        var display = string.IsNullOrWhiteSpace(root)
            ? PublicSuffixService.HostOf(normalizedAddress)
            : root;

        return new PasswordManagerPageDecision(
            PasswordManagerPromptKind.WaitingForPasswordField,
            matches,
            $"Identifiant trouve pour {display}. En attente du champ mot de passe.",
            normalizedAddress,
            pageState);
    }

    public PasswordManagerSaveOffer? BuildSaveOffer(CredentialCapture capture)
    {
        if (string.IsNullOrWhiteSpace(capture.Password) || capture.Password.Length < 3)
        {
            return null;
        }

        var origin = PasswordManagerService.NormalizeOrigin(capture.Origin);
        if (string.IsNullOrWhiteSpace(origin))
        {
            return null;
        }

        var username = capture.Username.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            username = RecentUsernameFor(origin);
        }

        // On propose l'enregistrement même sans identifiant capturé (formulaires
        // inhabituels, saisie en plusieurs étapes…) : l'utilisateur pourra compléter
        // le nom depuis le coffre. Un mot de passe soumis suffit à justifier l'offre.
        username = username?.Trim() ?? string.Empty;

        var loginUrl = PasswordManagerService.NormalizeLoginUrl(capture.LoginUrl, origin);
        RememberUsername(origin, username);

        var existing = _passwordManager.FindExistingLogin(origin, username, loginUrl);
        if (existing is not null &&
            existing.Password.Equals(capture.Password, StringComparison.Ordinal))
        {
            return null;
        }

        // Aucun compte pour CE domaine, mais peut-etre le meme compte (identifiant +
        // mot de passe) sous un autre domaine : le site a change de nom de domaine.
        // On reprend son nom personnalise pour garder les deux entrees coherentes.
        string? linkedFromDomain = null;
        var label = string.Empty;
        if (existing is null && !string.IsNullOrWhiteSpace(username))
        {
            var crossDomain = _passwordManager.FindSameLoginOnOtherDomain(origin, username, capture.Password);
            if (crossDomain is not null)
            {
                var otherRoot = PublicSuffixService.RootDomainOf(crossDomain.Origin);
                linkedFromDomain = string.IsNullOrWhiteSpace(otherRoot)
                    ? PublicSuffixService.HostOf(crossDomain.Origin)
                    : otherRoot;
                label = crossDomain.Label;
            }
        }

        var draft = new PasswordManagerEntryDraft(
            origin,
            username,
            capture.Password,
            loginUrl,
            label);

        return new PasswordManagerSaveOffer(
            draft,
            PublicSuffixService.OriginOf(origin),
            username,
            existing is not null,
            linkedFromDomain);
    }

    private void RememberPageState(CredentialPageState pageState)
    {
        var origin = PasswordManagerService.NormalizeOrigin(pageState.Origin);
        var root = PublicSuffixService.RootDomainOf(origin);
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        _pageStatesByRoot[root] = pageState;
        if (!string.IsNullOrWhiteSpace(pageState.Username))
        {
            _recentUsersByRoot[root] = pageState.Username.Trim();
        }
    }

    private void RememberUsername(string origin, string username)
    {
        var root = PublicSuffixService.RootDomainOf(origin);
        if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(username))
        {
            _recentUsersByRoot[root] = username.Trim();
        }
    }

    private string RecentUsernameFor(string origin)
    {
        var root = PublicSuffixService.RootDomainOf(origin);
        return !string.IsNullOrWhiteSpace(root) && _recentUsersByRoot.TryGetValue(root, out var username)
            ? username
            : string.Empty;
    }
}
