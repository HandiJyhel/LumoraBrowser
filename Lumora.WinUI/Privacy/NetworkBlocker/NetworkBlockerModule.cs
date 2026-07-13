namespace Lumora.Privacy.NetworkBlocker;

// Bloque les requêtes réseau correspondant aux règles EasyList / EasyPrivacy / uBlock / AdGuard.
// Quatre barrières : seed intégrée → listes locales → téléchargement auto → exceptions utilisateur.
internal sealed class NetworkBlockerModule : IPrivacyModule
{
    public string Id => "network-blocker";
    public string DisplayName => "Bloqueur de pubs et trackers";
    public bool IsEnabled { get; set; } = true;

    // Domaines bloqués (lookup O(1))
    private readonly HashSet<string> _blockedDomains =
        new(StringComparer.OrdinalIgnoreCase);

    // Exceptions @@||domain^
    private readonly HashSet<string> _allowedDomains =
        new(StringComparer.OrdinalIgnoreCase);

    // Règles nécessitant un match de sous-chaîne dans l'URL
    private readonly List<(string Pattern, bool ThirdPartyOnly)> _substringBlocks = new();

    // Règles d'exception sur sous-chaîne
    private readonly HashSet<string> _substringAllows =
        new(StringComparer.OrdinalIgnoreCase);

    // Domaines whitelistés par l'utilisateur
    private readonly HashSet<string> _userWhitelist =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly FilterListManager _listManager = new();

    public int RuleCount => _blockedDomains.Count + _substringBlocks.Count;

    // Utilisé par CnameUncloakerModule pour vérifier les cibles CNAME
    public bool IsBlocked(string host) =>
        !IsInSet(_userWhitelist, host) &&
        !IsInSet(_allowedDomains, host) &&
        IsInSet(_blockedDomains, host);

    public event Action<string>? StatusChanged;

    public NetworkBlockerModule()
    {
        // Seed immédiatement disponible — protection dès le premier lancement
        foreach (var d in SeedList.Domains)
            _blockedDomains.Add(d);
    }

    // Chargement des listes depuis le disque + déclenchement de la mise à jour si nécessaire
    public async Task LoadAsync()
    {
        var lines = await _listManager.LoadAsync(s => StatusChanged?.Invoke(s));
        ApplyRules(FilterParser.ParseLines(lines));
        StatusChanged?.Invoke($"Prêt — {RuleCount:N0} règles chargées.");
    }

    public Task ForceUpdateAsync() =>
        _listManager.ForceUpdateAsync(s => StatusChanged?.Invoke(s));

    public DateTime? LastListUpdate() => _listManager.LastUpdate();

    public void SetUserWhitelist(IEnumerable<string> domains)
    {
        _userWhitelist.Clear();
        foreach (var d in domains)
        {
            var norm = d.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(norm)) _userWhitelist.Add(norm);
        }
    }

    public bool ShouldBlock(string requestUri, string pageUri)
    {
        if (!IsEnabled) return false;

        var reqHost = ExtractHost(requestUri);
        if (reqHost is null) return false;

        // Whitelist utilisateur — jamais bloqué
        if (IsInSet(_userWhitelist, reqHost)) return false;

        // Exceptions @@
        if (IsInSet(_allowedDomains, reqHost)) return false;

        bool isThirdParty = IsThirdParty(reqHost, pageUri);

        // Match domaine + sous-domaines
        if (IsInSet(_blockedDomains, reqHost))
        {
            // Si la règle exige third-party, vérifier
            return true; // seed = toujours bloqué (all requests)
        }

        // Match sous-chaîne (règles complexes)
        foreach (var (pattern, thirdPartyOnly) in _substringBlocks)
        {
            if (thirdPartyOnly && !isThirdParty) continue;
            if (requestUri.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                if (!_substringAllows.Contains(pattern)) return true;
            }
        }

        return false;
    }

    private void ApplyRules(IEnumerable<ParsedRule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.Action == RuleAction.Allow)
            {
                if (!string.IsNullOrEmpty(rule.Domain))
                    _allowedDomains.Add(rule.Domain);
                else if (!string.IsNullOrEmpty(rule.Pattern))
                    _substringAllows.Add(rule.Pattern);
                continue;
            }

            if (!string.IsNullOrEmpty(rule.Domain))
            {
                _blockedDomains.Add(rule.Domain);
            }
            else if (!string.IsNullOrEmpty(rule.Pattern))
            {
                _substringBlocks.Add((rule.Pattern, rule.ThirdPartyOnly));
            }
        }
    }

    // Vérifie si le host ou l'un de ses parents est dans le set (support sous-domaines)
    private static bool IsInSet(HashSet<string> set, string host)
    {
        if (set.Contains(host)) return true;

        // Remonter les labels : "cdn.tracker.ads.com" → "tracker.ads.com" → "ads.com"
        var idx = host.IndexOf('.');
        while (idx >= 0 && idx < host.Length - 1)
        {
            var parent = host[(idx + 1)..];
            if (set.Contains(parent)) return true;
            idx = host.IndexOf('.', idx + 1);
        }
        return false;
    }

    private static bool IsThirdParty(string reqHost, string pageUri)
    {
        var pageHost = ExtractHost(pageUri);
        if (pageHost is null) return true;
        return !string.Equals(GetEtldPlusOne(reqHost), GetEtldPlusOne(pageHost),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractHost(string uri)
    {
        try
        {
            if (!uri.Contains("://")) return null;
            var u = new Uri(uri);
            return u.Host.ToLowerInvariant();
        }
        catch { return null; }
    }

    // Approximation eTLD+1 — couvre 99 % des domaines courants
    private static string GetEtldPlusOne(string host)
    {
        var parts = host.Split('.');
        if (parts.Length <= 2) return host;

        // TLDs composés connus
        var last2 = $"{parts[^2]}.{parts[^1]}";
        bool isCompoundTld = last2 is
            "co.uk" or "com.au" or "co.jp" or "co.in" or "co.za" or
            "com.br" or "net.br" or "org.br" or "co.nz" or "com.mx" or
            "co.kr" or "com.ar" or "com.sg" or "com.hk" or "com.tw";

        return isCompoundTld && parts.Length >= 3
            ? $"{parts[^3]}.{last2}"
            : last2;
    }
}
