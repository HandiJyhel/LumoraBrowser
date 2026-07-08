namespace PulseBrowser.Privacy;

internal sealed class PrivacyEngine
{
    private readonly List<IPrivacyModule> _modules = new();
    private int _blockedCount;
    private int _pageBlockedCount;

    public int BlockedCount     => _blockedCount;
    public int PageBlockedCount => _pageBlockedCount;
    public IReadOnlyList<IPrivacyModule> Modules => _modules;

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
                return true;
            }
        }
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

    public void ResetBlockedCount()     => Interlocked.Exchange(ref _blockedCount, 0);
    public void ResetPageBlockedCount() => Interlocked.Exchange(ref _pageBlockedCount, 0);
}
