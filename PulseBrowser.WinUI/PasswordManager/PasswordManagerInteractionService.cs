using PulseBrowser.WinUI.Credentials;

namespace PulseBrowser.WinUI.PasswordManager;

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

        var match = _passwordManager.FindBestForAddress(normalizedAddress);
        if (match is null)
        {
            return new PasswordManagerPageDecision(
                PasswordManagerPromptKind.None,
                null,
                string.Empty,
                normalizedAddress,
                pageState);
        }

        var hasPasswordField = pageState?.HasPasswordField;
        if (hasPasswordField == true)
        {
            return new PasswordManagerPageDecision(
                PasswordManagerPromptKind.FillAvailable,
                match,
                $"Remplir avec le compte {match.Username} ?",
                normalizedAddress,
                pageState);
        }

        if (pageState?.HasUsernameField == true)
        {
            return new PasswordManagerPageDecision(
                PasswordManagerPromptKind.UsernameFillAvailable,
                match,
                $"Remplir l'identifiant {match.Username} ?",
                normalizedAddress,
                pageState);
        }

        var root = PublicSuffixService.RootDomainOf(normalizedAddress);
        var display = string.IsNullOrWhiteSpace(root)
            ? PublicSuffixService.HostOf(normalizedAddress)
            : root;

        return new PasswordManagerPageDecision(
            PasswordManagerPromptKind.WaitingForPasswordField,
            match,
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

        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var loginUrl = PasswordManagerService.NormalizeLoginUrl(capture.LoginUrl, origin);
        RememberUsername(origin, username);

        var existing = _passwordManager.FindExistingLogin(origin, username, loginUrl);
        if (existing is not null &&
            existing.Password.Equals(capture.Password, StringComparison.Ordinal))
        {
            return null;
        }

        var draft = new PasswordManagerEntryDraft(
            origin,
            username,
            capture.Password,
            loginUrl);

        return new PasswordManagerSaveOffer(
            draft,
            PublicSuffixService.OriginOf(origin),
            username,
            existing is not null);
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
