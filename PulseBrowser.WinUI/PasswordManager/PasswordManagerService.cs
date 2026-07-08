using PulseBrowser.WinUI.Credentials;

namespace PulseBrowser.WinUI.PasswordManager;

internal sealed class PasswordManagerService
{
    private readonly VaultStore _vault;

    public PasswordManagerService(VaultStore vault)
    {
        _vault = vault;
    }

    public bool IsLocked => _vault.IsLocked;

    public IReadOnlyList<VaultCredential> List(PasswordManagerSearchOptions? options = null)
    {
        var credentials = _vault.ListCredentials();
        var query = options?.Query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return credentials
                .OrderBy(c => DisplayName(c), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(c => c.Username, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        return credentials
            .Where(c => Matches(c, query))
            .OrderByDescending(c => Score(c, query))
            .ThenBy(c => DisplayName(c), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public VaultCredential? FindBestForAddress(string address)
    {
        if (!IsWebAddress(address))
        {
            return null;
        }

        var origin = PublicSuffixService.OriginOf(address);
        var root = PublicSuffixService.RootDomainOf(address);

        return _vault.ListCredentials()
            .Where(c => !string.IsNullOrWhiteSpace(c.Password))
            .Select(c => new { Credential = c, Score = ScoreForAddress(c, address, origin, root) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Credential.UpdatedAt)
            .Select(x => x.Credential)
            .FirstOrDefault();
    }

    public VaultCredential? FindExistingLogin(string origin, string username, string loginUrl = "")
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var normalizedOrigin = NormalizeOrigin(origin);
        var normalizedLoginUrl = NormalizeLoginUrl(loginUrl, normalizedOrigin);
        var address = !string.IsNullOrWhiteSpace(normalizedLoginUrl)
            ? normalizedLoginUrl
            : normalizedOrigin;

        if (!IsWebAddress(address))
        {
            return null;
        }

        var pageOrigin = PublicSuffixService.OriginOf(address);
        var pageRoot = PublicSuffixService.RootDomainOf(address);

        return _vault.ListCredentials()
            .Where(c => c.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(c => new { Credential = c, Score = ScoreForAddress(c, address, pageOrigin, pageRoot) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Credential.UpdatedAt)
            .Select(x => x.Credential)
            .FirstOrDefault();
    }

    public bool Save(PasswordManagerEntryDraft draft)
    {
        var normalized = NormalizeDraft(draft);
        if (string.IsNullOrWhiteSpace(normalized.Origin) ||
            string.IsNullOrWhiteSpace(normalized.Password))
        {
            return false;
        }

        _vault.Upsert(
            normalized.Origin,
            normalized.Username.Trim(),
            normalized.Password,
            normalized.LoginUrl);

        if (!string.IsNullOrWhiteSpace(normalized.Label))
        {
            _vault.SetLabel(normalized.Origin, normalized.Username.Trim(), normalized.Label);
        }

        return true;
    }

    public void Rename(string origin, string username, string label) =>
        _vault.SetLabel(origin, username, label);

    public void Delete(string origin, string username) =>
        _vault.Delete(origin, username);

    public IReadOnlyList<VaultCredential> ExportClear() =>
        _vault.ExportClear();

    public int ImportClear(IEnumerable<(string origin, string username, string password)> items) =>
        _vault.ImportClear(items.Select(item => (
            NormalizeOrigin(item.origin),
            item.username,
            item.password)));

    public static string NormalizeOrigin(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = $"https://{trimmed}";
        }

        return PublicSuffixService.OriginOf(trimmed);
    }

    public static string NormalizeLoginUrl(string value, string fallbackOrigin)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return fallbackOrigin;
        }

        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = $"https://{trimmed}";
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.ToString()
            : fallbackOrigin;
    }

    public static string DisplayName(VaultCredential credential)
    {
        if (!string.IsNullOrWhiteSpace(credential.Label))
        {
            return credential.Label;
        }

        var host = PublicSuffixService.HostOf(credential.Origin);
        return string.IsNullOrWhiteSpace(host) ? credential.Origin : host;
    }

    private static PasswordManagerEntryDraft NormalizeDraft(PasswordManagerEntryDraft draft)
    {
        var origin = NormalizeOrigin(draft.Origin);
        var loginUrl = NormalizeLoginUrl(draft.LoginUrl, origin);
        return draft with
        {
            Origin = origin,
            LoginUrl = loginUrl,
            Username = draft.Username.Trim(),
            Label = draft.Label.Trim()
        };
    }

    private static bool Matches(VaultCredential credential, string query)
    {
        var q = query.Trim();
        return DisplayName(credential).Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
               credential.Origin.Contains(q, StringComparison.OrdinalIgnoreCase) ||
               credential.LoginUrl.Contains(q, StringComparison.OrdinalIgnoreCase) ||
               credential.Username.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
               PublicSuffixService.RootDomainOf(credential.Origin).Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private static int Score(VaultCredential credential, string query)
    {
        var q = query.Trim();
        var score = 0;
        if (DisplayName(credential).Equals(q, StringComparison.CurrentCultureIgnoreCase)) score += 100;
        if (PublicSuffixService.RootDomainOf(credential.Origin).Equals(q, StringComparison.OrdinalIgnoreCase)) score += 90;
        if (credential.Username.Equals(q, StringComparison.CurrentCultureIgnoreCase)) score += 75;
        if (credential.Origin.Contains(q, StringComparison.OrdinalIgnoreCase)) score += 50;
        if (credential.LoginUrl.Contains(q, StringComparison.OrdinalIgnoreCase)) score += 40;
        if (DisplayName(credential).Contains(q, StringComparison.CurrentCultureIgnoreCase)) score += 30;
        if (credential.Username.Contains(q, StringComparison.CurrentCultureIgnoreCase)) score += 25;
        return score;
    }

    private static int ScoreForAddress(VaultCredential credential, string address, string origin, string root)
    {
        var score = 0;
        var credentialOrigin = PublicSuffixService.OriginOf(credential.Origin);
        var credentialRoot = PublicSuffixService.RootDomainOf(credential.Origin);

        if (credentialOrigin.Equals(origin, StringComparison.OrdinalIgnoreCase))
        {
            score += 120;
        }
        else if (!string.IsNullOrWhiteSpace(credentialRoot) &&
                 credentialRoot.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            score += 80;
        }

        if (!string.IsNullOrWhiteSpace(credential.LoginUrl))
        {
            var loginOrigin = PublicSuffixService.OriginOf(credential.LoginUrl);
            var loginRoot = PublicSuffixService.RootDomainOf(credential.LoginUrl);

            if (loginOrigin.Equals(origin, StringComparison.OrdinalIgnoreCase)) score += 45;
            else if (!string.IsNullOrWhiteSpace(loginRoot) &&
                     loginRoot.Equals(root, StringComparison.OrdinalIgnoreCase)) score += 25;

            if (address.StartsWith(credential.LoginUrl, StringComparison.OrdinalIgnoreCase)) score += 30;
        }

        return score;
    }

    private static bool IsWebAddress(string address) =>
        Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https";
}
