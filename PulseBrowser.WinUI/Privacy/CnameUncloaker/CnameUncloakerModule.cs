using System.Collections.Concurrent;
using PulseBrowser.Privacy.NetworkBlocker;

namespace PulseBrowser.Privacy.CnameUncloaker;

// Détecte et bloque les trackers cachés derrière un alias CNAME (« CNAME cloaking »).
// Ex : analytics.monsite.com → (CNAME) → collect.tracker-tiers.net
//
// Stratégie :
//   1. Requête réseau entrante non bloquée par NetworkBlocker.
//   2. Si le host est un tiers inconnu, on lance une résolution CNAME en arrière-plan.
//   3. Si la chaîne CNAME aboutit à un domaine de la blocklist → le host est marqué « cloaké ».
//   4. Les requêtes suivantes vers ce host sont bloquées directement (O(1)).
//
// La première requête vers un host cloaké passe toujours — c'est inévitable avec la résolution asynchrone.
// Toutes les suivantes sont bloquées.
internal sealed class CnameUncloakerModule : IPrivacyModule
{
    public string Id => "cname-uncloaker";
    public string DisplayName => "Détection CNAME cloaking";
    public bool IsEnabled { get; set; } = true;

    // Hosts confirmés comme cloakés (écrits depuis thread DNS, lus depuis WebResourceRequested)
    private readonly ConcurrentDictionary<string, bool> _confirmed =
        new(StringComparer.OrdinalIgnoreCase);

    // Hosts dont la résolution est en cours (évite les requêtes DNS dupliquées)
    private readonly ConcurrentDictionary<string, bool> _pending =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly NetworkBlockerModule _blocker;

    public int DetectedCount => _confirmed.Count;

    public CnameUncloakerModule(NetworkBlockerModule blocker)
    {
        _blocker = blocker;
    }

    public bool ShouldBlock(string requestUri, string pageUri)
    {
        var host = ExtractHost(requestUri);
        if (host is null) return false;

        // Bloquer immédiatement les hosts déjà confirmés
        if (_confirmed.ContainsKey(host)) return true;

        // Lancer la résolution si pas déjà en cours et si c'est un tiers
        if (!_blocker.IsBlocked(host) &&
            !_pending.ContainsKey(host) &&
            IsThirdParty(host, pageUri) &&
            HasSubdomain(host))
        {
            _pending[host] = true;
            _ = ResolveAndCheckAsync(host);
        }

        return false;
    }

    private async Task ResolveAndCheckAsync(string host)
    {
        try
        {
            var chain = await CnameResolver.ResolveCnameChainAsync(host);
            foreach (var target in chain)
            {
                if (_blocker.IsBlocked(target))
                {
                    _confirmed[host] = true;
                    break;
                }
            }
        }
        catch { }
        finally
        {
            _pending.TryRemove(host, out _);
        }
    }

    private static bool HasSubdomain(string host) => host.Count(c => c == '.') >= 2;

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
            return new Uri(uri).Host.ToLowerInvariant();
        }
        catch { return null; }
    }

    private static string GetEtldPlusOne(string host)
    {
        var parts = host.Split('.');
        if (parts.Length <= 2) return host;

        var last2 = $"{parts[^2]}.{parts[^1]}";
        bool compound = last2 is
            "co.uk" or "com.au" or "co.jp" or "co.in" or "co.za" or
            "com.br" or "net.br" or "co.nz" or "com.mx" or "co.kr";

        return compound && parts.Length >= 3
            ? $"{parts[^3]}.{last2}"
            : last2;
    }
}
