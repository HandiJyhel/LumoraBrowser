using Lumora.WinUI.Credentials;

namespace Lumora.WinUI.PasswordManager;

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

    // Renvoie tous les comptes candidats pour une adresse, triés par pertinence puis
    // récence (le plus pertinent en premier). Un site comme Google peut avoir plusieurs
    // comptes enregistrés : c'est à l'appelant de proposer un choix si Count > 1,
    // comme le font Chrome/Firefox, plutôt que de n'en retenir qu'un silencieusement.
    public IReadOnlyList<VaultCredential> FindAllForAddress(string address)
    {
        if (!IsWebAddress(address))
        {
            return Array.Empty<VaultCredential>();
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
            .ToList();
    }

    // Detection "changement de domaine". Cherche un compte identique — meme
    // identifiant ET meme mot de passe — enregistre sous un AUTRE domaine racine que
    // celui de la page courante. On ne se fie JAMAIS a une ressemblance de nom : la
    // preuve que c'est le meme compte, c'est que l'utilisateur vient de se connecter
    // avec succes avec ces identifiants exacts. Aucun risque de phishing (on ne
    // propose rien sur un domaine inconnu tant qu'un login reussi ne l'a pas prouve),
    // aucune donnee ne sort de la machine (comparaison au coffre local uniquement).
    public VaultCredential? FindSameLoginOnOtherDomain(string origin, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var root = PublicSuffixService.RootDomainOf(origin);
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var trimmedUser = username.Trim();
        return _vault.ListCredentials()
            .Where(c =>
                c.Username.Equals(trimmedUser, StringComparison.OrdinalIgnoreCase) &&
                c.Password.Equals(password, StringComparison.Ordinal))
            .Where(c =>
            {
                var otherRoot = PublicSuffixService.RootDomainOf(c.Origin);
                return !string.IsNullOrWhiteSpace(otherRoot) &&
                       !otherRoot.Equals(root, StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(c => c.UpdatedAt)
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

    public void RenameById(string id, string label) =>
        _vault.SetLabelById(id, label);

    public void SetUsernameById(string id, string username) =>
        _vault.SetUsernameById(id, username);

    public void SetPasswordById(string id, string password) =>
        _vault.SetPasswordById(id, password);

    // secret vide/null = supprime le TOTP de cet identifiant.
    public void SetTotpById(string id, string? secret, int digits = 6, int period = 30, string algorithm = "SHA1") =>
        _vault.SetTotpById(id, secret, digits, period, algorithm);

    public void DeleteById(string id) =>
        _vault.DeleteById(id);

    public IReadOnlyList<VaultCredential> ExportClear() =>
        _vault.ExportClear();

    public int ImportClear(IEnumerable<(string origin, string username, string password)> items) =>
        _vault.ImportClear(items.Select(item => (
            NormalizeOrigin(item.origin),
            item.username,
            item.password)));

    public int ImportClear(IEnumerable<CredentialImportItem> items) =>
        _vault.ImportClear(items.Select(item =>
        {
            var origin = NormalizeOrigin(item.Origin);
            return (
                origin,
                item.Username.Trim(),
                item.Password,
                NormalizeLoginUrl(item.LoginUrl, origin),
                item.Label.Trim());
        }));

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
        if (string.IsNullOrWhiteSpace(host))
        {
            return credential.Origin;
        }

        // "www." est du bruit d'import : sans ce retrait, "amazon.fr" et
        // "www.amazon.fr" affichent deux noms differents pour le meme site et
        // se trient a deux endroits opposes de la liste.
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && host.Length > 4
            ? host[4..]
            : host;
    }

    // Doublons d'import : le meme compte enregistre sous plusieurs origines du
    // meme site (www./apex/sous-domaine — http est deja normalise en https).
    // Un doublon = meme domaine racine, meme identifiant ET meme mot de passe ;
    // deux entrees dont le mot de passe differe ne sont JAMAIS considerees en
    // double (multi-comptes, rotation de mot de passe...).
    // Retourne les entrees excedentaires, celles qu'une fusion supprimerait.
    public IReadOnlyList<VaultCredential> FindDuplicates()
    {
        return _vault.ListCredentials()
            .GroupBy(c => (
                Site: DuplicateSiteKey(c.Origin),
                User: c.Username.Trim().ToLowerInvariant(),
                c.Password))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key.Site) && g.Count() > 1)
            .SelectMany(g => g
                // L'entree conservee : la plus renseignee (nom perso, page de
                // connexion), puis la plus recente.
                .OrderByDescending(c => string.IsNullOrWhiteSpace(c.Label) ? 0 : 1)
                .ThenByDescending(c => string.IsNullOrWhiteSpace(c.LoginUrl) ? 0 : 1)
                .ThenByDescending(c => c.UpdatedAt)
                .Skip(1))
            .ToList();
    }

    public int MergeDuplicates()
    {
        var duplicates = FindDuplicates();
        foreach (var extra in duplicates)
        {
            _vault.DeleteById(extra.Id);
        }

        return duplicates.Count;
    }

    private static string DuplicateSiteKey(string origin)
    {
        var root = PublicSuffixService.RootDomainOf(origin);
        return string.IsNullOrWhiteSpace(root)
            ? PublicSuffixService.HostOf(origin).ToLowerInvariant()
            : root.ToLowerInvariant();
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
