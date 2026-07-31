using System.Text.Json;

namespace Lumora.WinUI;

// Registre local des flux RSS/Atom suivis, chiffre (DPAPI) comme les
// favoris/historique/parametres - meme tier de protection que webapps.lumora
// (pas de donnees sensibles ici, juste des URL de flux publics et des titres).
internal sealed class RssFeedStore
{
    // Borne les listes d'identifiants d'articles (lus/connus) par flux : un
    // flux actif depuis longtemps ne doit pas faire grossir indefiniment le
    // fichier local.
    private const int MaxTrackedArticleIds = 500;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private readonly string _file;
    private readonly List<RssFeed> _feeds = new();
    private bool _isGuest;

    public RssFeedStore(string file)
    {
        _file = file;
        Load();
    }

    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (isGuest) _feeds.Clear();
    }

    private void Load()
    {
        try
        {
            var json = LumoraFile.TryReadAllText(_file);
            if (json is null) return;
            var loaded = JsonSerializer.Deserialize<List<RssFeed>>(json, JsonOpts);
            if (loaded is not null) _feeds.AddRange(loaded);
        }
        catch { }
    }

    private void Save()
    {
        if (_isGuest) return;
        try
        {
            LumoraFile.WriteAllText(_file, JsonSerializer.Serialize(_feeds, JsonOpts));
        }
        catch { }
    }

    public IReadOnlyList<RssFeed> All() =>
        _feeds.OrderBy(f => f.Title, StringComparer.CurrentCultureIgnoreCase).ToList();

    public RssFeed? Find(string id) =>
        _feeds.FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));

    public bool ContainsUrl(string url) =>
        _feeds.Any(f => string.Equals(f.Url, url, StringComparison.OrdinalIgnoreCase));

    public RssFeed Add(string url, string title)
    {
        var feed = new RssFeed { Url = url, Title = title };
        _feeds.Add(feed);
        Save();
        return feed;
    }

    public void Remove(string id)
    {
        _feeds.RemoveAll(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    public void MarkFetched(string id, string? title, string? error)
    {
        var feed = Find(id);
        if (feed is null) return;
        feed.LastFetchedAt = DateTimeOffset.Now;
        feed.LastError = error;
        if (error is null && !string.IsNullOrWhiteSpace(title)) feed.Title = title;
        Save();
    }

    public void MarkArticleRead(string feedId, string articleId)
    {
        var feed = Find(feedId);
        if (feed is null) return;
        if (feed.ReadArticleIds.Contains(articleId)) return;
        feed.ReadArticleIds.Add(articleId);
        // Purge les plus anciens si la borne est depassee (FIFO simple).
        var overflow = feed.ReadArticleIds.Count - MaxTrackedArticleIds;
        if (overflow > 0) feed.ReadArticleIds.RemoveRange(0, overflow);
        Save();
    }

    // Enregistre les articles rencontres (panneau ouvert ou verification
    // periodique) : sert uniquement a calculer le compteur "non lus" (un
    // article jamais recupere n'est pas compte). N'affecte pas ReadArticleIds.
    public void MarkArticlesKnown(string feedId, IEnumerable<string> articleIds)
    {
        var feed = Find(feedId);
        if (feed is null) return;

        var added = false;
        foreach (var id in articleIds)
        {
            if (feed.KnownArticleIds.Contains(id)) continue;
            feed.KnownArticleIds.Add(id);
            added = true;
        }
        if (!added) return;

        var overflow = feed.KnownArticleIds.Count - MaxTrackedArticleIds;
        if (overflow > 0) feed.KnownArticleIds.RemoveRange(0, overflow);
        Save();
    }

    public static int UnreadCount(RssFeed feed) =>
        feed.KnownArticleIds.Count(id => !feed.ReadArticleIds.Contains(id));

    public int TotalUnreadCount() => _feeds.Sum(UnreadCount);
}
