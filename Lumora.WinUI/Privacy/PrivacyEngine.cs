namespace Lumora.Privacy;

internal sealed record PrivacyBlockEvent(
    DateTimeOffset Timestamp,
    string ModuleId,
    string ModuleName,
    string RequestHost,
    string RequestPath,
    string PageHost);

internal sealed class PrivacyEngine
{
    private const int MaxRecentBlocks = 40;

    private readonly List<IPrivacyModule> _modules = new();
    private readonly object _recentBlocksLock = new();
    private readonly Queue<PrivacyBlockEvent> _recentBlocks = new();
    private readonly Queue<PrivacyBlockEvent> _recentPageBlocks = new();
    private int _blockedCount;
    private int _pageBlockedCount;

    public int BlockedCount     => _blockedCount;
    public int PageBlockedCount => _pageBlockedCount;
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

    // Returns true if the request should be blocked
    public bool ShouldBlock(string requestUri, string pageUri)
    {
        foreach (var m in _modules)
        {
            if (m.IsEnabled && m.ShouldBlock(requestUri, pageUri))
            {
                Interlocked.Increment(ref _blockedCount);
                Interlocked.Increment(ref _pageBlockedCount);
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
        Interlocked.Increment(ref _blockedCount);
        Interlocked.Increment(ref _pageBlockedCount);
        RecordBlock(moduleId, moduleName, requestUri, pageUri);
    }

    public void ResetBlockedCount()
    {
        Interlocked.Exchange(ref _blockedCount, 0);
        lock (_recentBlocksLock)
        {
            _recentBlocks.Clear();
        }
    }

    public void ResetPageBlockedCount()
    {
        Interlocked.Exchange(ref _pageBlockedCount, 0);
        lock (_recentBlocksLock)
        {
            _recentPageBlocks.Clear();
        }
    }

    private void RecordBlock(IPrivacyModule module, string requestUri, string pageUri) =>
        RecordBlock(module.Id, module.DisplayName, requestUri, pageUri);

    private void RecordBlock(string moduleId, string moduleName, string requestUri, string pageUri)
    {
        var entry = new PrivacyBlockEvent(
            DateTimeOffset.Now,
            moduleId,
            moduleName,
            HostOf(requestUri),
            PathOf(requestUri),
            HostOf(pageUri));

        lock (_recentBlocksLock)
        {
            EnqueueCapped(_recentBlocks, entry);
            EnqueueCapped(_recentPageBlocks, entry);
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
}
