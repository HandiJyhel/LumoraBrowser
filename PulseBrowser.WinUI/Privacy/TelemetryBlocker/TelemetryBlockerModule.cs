namespace PulseBrowser.Privacy.TelemetryBlocker;

// Bloque les balises de télémétrie applicative : mesure d'audience, session
// replay, rapports de crash et métriques produit. Complémentaire au bloqueur
// pubs/trackers : ici on vise la mesure d'usage, pas la publicité.
// Domaines DÉDIÉS uniquement — pas d'heuristique de chemin d'URL, pour ne
// jamais sur-bloquer un service mixte.
internal sealed class TelemetryBlockerModule : IPrivacyModule
{
    public string Id => "telemetry-blocker";
    public string DisplayName => "Anti-télémétrie";
    public bool IsEnabled { get; set; } = true;

    private readonly HashSet<string> _domains =
        new(StringComparer.OrdinalIgnoreCase);

    // Domaines whitelistés par l'utilisateur (partagés avec le bloqueur réseau)
    private readonly HashSet<string> _userWhitelist =
        new(StringComparer.OrdinalIgnoreCase);

    private int _blockedCount;
    private int _pageBlockedCount;

    public int BlockedCount     => _blockedCount;
    public int PageBlockedCount => _pageBlockedCount;
    public int RuleCount        => _domains.Count;

    public TelemetryBlockerModule()
    {
        foreach (var d in TelemetrySeedList.Domains)
            _domains.Add(d);
    }

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

        // Whitelist utilisateur — jamais bloqué (mêmes règles que le bloqueur réseau)
        if (IsInSet(_userWhitelist, reqHost)) return false;

        if (!IsInSet(_domains, reqHost)) return false;

        Interlocked.Increment(ref _blockedCount);
        Interlocked.Increment(ref _pageBlockedCount);
        return true;
    }

    public void ResetPageBlockedCount() => Interlocked.Exchange(ref _pageBlockedCount, 0);

    // Vérifie si le host ou l'un de ses parents est dans le set (support sous-domaines)
    private static bool IsInSet(HashSet<string> set, string host)
    {
        if (set.Contains(host)) return true;

        var idx = host.IndexOf('.');
        while (idx >= 0 && idx < host.Length - 1)
        {
            var parent = host[(idx + 1)..];
            if (set.Contains(parent)) return true;
            idx = host.IndexOf('.', idx + 1);
        }
        return false;
    }

    private static string? ExtractHost(string uri)
    {
        try
        {
            if (!uri.Contains("://")) return null;
            return new Uri(uri).Host.ToLowerInvariant();
        }
        catch { return null; }
    }
}
