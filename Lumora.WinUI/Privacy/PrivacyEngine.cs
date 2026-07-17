namespace Lumora.Privacy;

internal enum PrivacyBlockCategory
{
    Advertising,
    Tracker,
    Other
}

internal sealed record PrivacyBlockCounters(int Total, int Ads, int Trackers, int Other)
{
    public static PrivacyBlockCounters Empty { get; } = new(0, 0, 0, 0);
}

internal sealed record PrivacyBlockEvent(
    DateTimeOffset Timestamp,
    string ModuleId,
    string ModuleName,
    string RequestHost,
    string RequestPath,
    string PageHost,
    string PageRootDomain,
    PrivacyBlockCategory Category,
    string Reason);

internal sealed record PrivacySiteBlockStats(
    string SiteHost,
    string RootDomain,
    int Total,
    int Ads,
    int Trackers,
    int Other,
    IReadOnlyList<PrivacyBlockEvent> RecentBlocks);

internal sealed class PrivacyEngine
{
    private const int MaxRecentBlocks = 40;
    private const int MaxRecentBlocksPerSite = 30;

    private readonly List<IPrivacyModule> _modules = new();
    private readonly object _recentBlocksLock = new();
    private readonly Queue<PrivacyBlockEvent> _recentBlocks = new();
    private readonly Queue<PrivacyBlockEvent> _recentPageBlocks = new();
    private readonly Dictionary<string, MutableSiteBlockStats> _siteStats = new(StringComparer.OrdinalIgnoreCase);
    private int _blockedCount;
    private int _pageBlockedCount;
    private int _adBlockedCount;
    private int _trackerBlockedCount;
    private int _otherBlockedCount;
    private int _pageAdBlockedCount;
    private int _pageTrackerBlockedCount;
    private int _pageOtherBlockedCount;

    public int BlockedCount     => _blockedCount;
    public int PageBlockedCount => _pageBlockedCount;
    public PrivacyBlockCounters GlobalCounters => new(_blockedCount, _adBlockedCount, _trackerBlockedCount, _otherBlockedCount);
    public PrivacyBlockCounters PageCounters   => new(_pageBlockedCount, _pageAdBlockedCount, _pageTrackerBlockedCount, _pageOtherBlockedCount);
    public IReadOnlyList<IPrivacyModule> Modules => _modules;
    public IReadOnlyList<PrivacyBlockEvent> RecentBlocks
    {
        get
        {
            lock (_recentBlocksLock)
            {
                return _recentBlocks.ToList();
            }
        }
    }

    public IReadOnlyList<PrivacyBlockEvent> RecentPageBlocks
    {
        get
        {
            lock (_recentBlocksLock)
            {
                return _recentPageBlocks.ToList();
            }
        }
    }

    public void Register(IPrivacyModule module) => _modules.Add(module);

    public T? Get<T>() where T : class, IPrivacyModule
    {
        foreach (var m in _modules)
            if (m is T typed) return typed;
        return null;
    }

    public PrivacySiteBlockStats SiteStatsFor(string? hostOrUri)
    {
        var host = HostOf(hostOrUri ?? string.Empty);
        if (string.IsNullOrWhiteSpace(host))
        {
            host = NormalizeHost(hostOrUri ?? string.Empty);
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return new PrivacySiteBlockStats(string.Empty, string.Empty, 0, 0, 0, 0, Array.Empty<PrivacyBlockEvent>());
        }

        var rootDomain = RootDomainOf(host);
        lock (_recentBlocksLock)
        {
            return _siteStats.TryGetValue(rootDomain, out var stats)
                ? stats.Snapshot()
                : new PrivacySiteBlockStats(host, rootDomain, 0, 0, 0, 0, Array.Empty<PrivacyBlockEvent>());
        }
    }

    // Returns true if the request should be blocked
    public bool ShouldBlock(string requestUri, string pageUri)
    {
        foreach (var m in _modules)
        {
            if (m.IsEnabled && m.ShouldBlock(requestUri, pageUri))
            {
                RecordBlock(m, requestUri, pageUri);
                return true;
            }
        }
        return false;
    }

    // Comme ShouldBlock, mais sans incrémenter les compteurs — pour les vérifications
    // hors rendu de page (ex. téléchargement direct de favicon).
    public bool IsBlocked(string requestUri, string pageUri)
    {
        foreach (var m in _modules)
            if (m.IsEnabled && m.ShouldBlock(requestUri, pageUri))
                return true;
        return false;
    }

    // Returns cleaned URL if any module modified it, null if unchanged
    public string? CleanUrl(string uri)
    {
        string? result = null;
        foreach (var m in _modules)
        {
            if (!m.IsEnabled) continue;
            var cleaned = m.CleanUrl(result ?? uri);
            if (cleaned is not null)
                result = cleaned;
        }
        return result;
    }

    // Enregistre un blocage décidé hors du pipeline de requêtes (popup bloquée,
    // navigation publicitaire annulée) : mêmes compteurs et même journal que les
    // blocages de requêtes, pour que le bouclier reflète tout ce qui est bloqué.
    public void RecordManualBlock(string moduleId, string moduleName, string requestUri, string pageUri)
    {
        RecordBlock(moduleId, moduleName, requestUri, pageUri);
    }

    public void ResetBlockedCount()
    {
        Interlocked.Exchange(ref _blockedCount, 0);
        Interlocked.Exchange(ref _adBlockedCount, 0);
        Interlocked.Exchange(ref _trackerBlockedCount, 0);
        Interlocked.Exchange(ref _otherBlockedCount, 0);
        lock (_recentBlocksLock)
        {
            _recentBlocks.Clear();
            _siteStats.Clear();
        }
    }

    public void ResetPageBlockedCount()
    {
        Interlocked.Exchange(ref _pageBlockedCount, 0);
        Interlocked.Exchange(ref _pageAdBlockedCount, 0);
        Interlocked.Exchange(ref _pageTrackerBlockedCount, 0);
        Interlocked.Exchange(ref _pageOtherBlockedCount, 0);
        lock (_recentBlocksLock)
        {
            _recentPageBlocks.Clear();
        }
    }

    private void RecordBlock(IPrivacyModule module, string requestUri, string pageUri) =>
        RecordBlock(module.Id, module.DisplayName, requestUri, pageUri);

    private void RecordBlock(string moduleId, string moduleName, string requestUri, string pageUri)
    {
        var requestHost = HostOf(requestUri);
        var requestPath = PathOf(requestUri);
        var pageHost = HostOf(pageUri);
        var pageRootDomain = RootDomainOf(pageHost);
        var category = Classify(moduleId, requestHost, requestPath);
        var entry = new PrivacyBlockEvent(
            DateTimeOffset.Now,
            moduleId,
            moduleName,
            requestHost,
            requestPath,
            pageHost,
            pageRootDomain,
            category,
            ReasonFor(moduleId, moduleName, requestHost, requestPath, category));

        IncrementCounters(category);

        lock (_recentBlocksLock)
        {
            EnqueueCapped(_recentBlocks, entry);
            EnqueueCapped(_recentPageBlocks, entry);
            if (!string.IsNullOrWhiteSpace(pageRootDomain))
            {
                if (!_siteStats.TryGetValue(pageRootDomain, out var stats))
                {
                    stats = new MutableSiteBlockStats(pageHost, pageRootDomain);
                    _siteStats[pageRootDomain] = stats;
                }
                stats.Record(entry);
            }
        }
    }

    private void IncrementCounters(PrivacyBlockCategory category)
    {
        Interlocked.Increment(ref _blockedCount);
        Interlocked.Increment(ref _pageBlockedCount);
        switch (category)
        {
            case PrivacyBlockCategory.Advertising:
                Interlocked.Increment(ref _adBlockedCount);
                Interlocked.Increment(ref _pageAdBlockedCount);
                break;
            case PrivacyBlockCategory.Tracker:
                Interlocked.Increment(ref _trackerBlockedCount);
                Interlocked.Increment(ref _pageTrackerBlockedCount);
                break;
            default:
                Interlocked.Increment(ref _otherBlockedCount);
                Interlocked.Increment(ref _pageOtherBlockedCount);
                break;
        }
    }

    private static void EnqueueCapped(Queue<PrivacyBlockEvent> queue, PrivacyBlockEvent entry)
    {
        queue.Enqueue(entry);
        while (queue.Count > MaxRecentBlocks)
        {
            queue.Dequeue();
        }
    }

    private static string HostOf(string uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            ? parsed.Host.ToLowerInvariant()
            : string.Empty;
    }

    private static string PathOf(string uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            ? parsed.AbsolutePath
            : string.Empty;
    }

    private static string NormalizeHost(string value)
    {
        var host = value.Trim().ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }
        return host.Contains('.') && !host.Contains('/') ? host : string.Empty;
    }

    private static string RootDomainOf(string host)
    {
        host = NormalizeHost(host);
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 2)
        {
            return host;
        }

        var last2 = $"{parts[^2]}.{parts[^1]}";
        var isCompoundTld = last2 is
            "co.uk" or "com.au" or "co.jp" or "co.in" or "co.za" or
            "com.br" or "net.br" or "org.br" or "co.nz" or "com.mx" or
            "co.kr" or "com.ar" or "com.sg" or "com.hk" or "com.tw";

        return isCompoundTld && parts.Length >= 3
            ? $"{parts[^3]}.{last2}"
            : last2;
    }

    private static PrivacyBlockCategory Classify(string moduleId, string requestHost, string requestPath)
    {
        if (moduleId.Equals("telemetry-blocker", StringComparison.OrdinalIgnoreCase) ||
            moduleId.Equals("cname-uncloaker", StringComparison.OrdinalIgnoreCase))
        {
            return PrivacyBlockCategory.Tracker;
        }

        if (moduleId.Equals("popup-blocker", StringComparison.OrdinalIgnoreCase) ||
            moduleId.Equals("strict-ad-block", StringComparison.OrdinalIgnoreCase))
        {
            return PrivacyBlockCategory.Advertising;
        }

        var signal = $"{requestHost} {requestPath}";
        if (ContainsAny(signal,
                "doubleclick", "googlesyndication", "googleadservices", "adservice",
                "adserver", "advertising", "advertisement", "adsystem", "adnxs",
                "criteo", "taboola", "outbrain", "pubmatic", "rubiconproject",
                "openx", "mopub", "/ads", "/adserver", "/ad/"))
        {
            return PrivacyBlockCategory.Advertising;
        }

        if (ContainsAny(signal,
                "analytics", "telemetry", "metrics", "pixel", "tracker", "tracking",
                "collect", "beacon", "sentry", "mixpanel", "amplitude", "hotjar",
                "clarity", "segment", "session-replay", "replay", "/track",
                "/event", "/events", "/collect"))
        {
            return PrivacyBlockCategory.Tracker;
        }

        return PrivacyBlockCategory.Other;
    }

    private static string ReasonFor(
        string moduleId,
        string moduleName,
        string requestHost,
        string requestPath,
        PrivacyBlockCategory category)
    {
        if (moduleId.Equals("telemetry-blocker", StringComparison.OrdinalIgnoreCase))
        {
            return "télémétrie ou mesure d'audience";
        }

        if (moduleId.Equals("cname-uncloaker", StringComparison.OrdinalIgnoreCase))
        {
            return "tracker masqué derrière un alias CNAME";
        }

        if (moduleId.Equals("popup-blocker", StringComparison.OrdinalIgnoreCase))
        {
            return "popup publicitaire ou automatique";
        }

        if (moduleId.Equals("strict-ad-block", StringComparison.OrdinalIgnoreCase))
        {
            return "redirection vers un domaine publicitaire";
        }

        if (moduleId.Equals("parasite-block", StringComparison.OrdinalIgnoreCase))
        {
            return "redirection parasite depuis la page";
        }

        return category switch
        {
            PrivacyBlockCategory.Advertising => "ressource publicitaire détectée",
            PrivacyBlockCategory.Tracker => "tracker ou mesure d'audience détecté",
            _ when !string.IsNullOrWhiteSpace(moduleName) => moduleName.ToLowerInvariant(),
            _ => string.IsNullOrWhiteSpace(requestHost) && string.IsNullOrWhiteSpace(requestPath)
                ? "blocage de protection"
                : "ressource filtrée"
        };
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private sealed class MutableSiteBlockStats
    {
        private readonly Queue<PrivacyBlockEvent> _recentBlocks = new();

        public MutableSiteBlockStats(string siteHost, string rootDomain)
        {
            SiteHost = siteHost;
            RootDomain = rootDomain;
        }

        public string SiteHost { get; private set; }
        public string RootDomain { get; }
        public int Total { get; private set; }
        public int Ads { get; private set; }
        public int Trackers { get; private set; }
        public int Other { get; private set; }

        public void Record(PrivacyBlockEvent entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.PageHost))
            {
                SiteHost = entry.PageHost;
            }

            Total++;
            switch (entry.Category)
            {
                case PrivacyBlockCategory.Advertising:
                    Ads++;
                    break;
                case PrivacyBlockCategory.Tracker:
                    Trackers++;
                    break;
                default:
                    Other++;
                    break;
            }

            _recentBlocks.Enqueue(entry);
            while (_recentBlocks.Count > MaxRecentBlocksPerSite)
            {
                _recentBlocks.Dequeue();
            }
        }

        public PrivacySiteBlockStats Snapshot() =>
            new(SiteHost, RootDomain, Total, Ads, Trackers, Other, _recentBlocks.ToList());
    }
}
