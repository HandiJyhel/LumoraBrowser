namespace Lumora.WinUI;

// Un flux RSS/Atom suivi par l'utilisateur. ReadArticleIds retient les
// identifiants (guid/id/lien) des articles deja lus ; KnownArticleIds retient
// tous les articles deja rencontres (lus ou non), pour ne compter/signaler
// comme "non lu" que ce qui a deja ete vu au moins une fois (pas les articles
// pas encore recuperes). Les deux sont plafonnes pour ne pas grossir
// indefiniment (voir RssFeedStore.MaxTrackedArticleIds).
internal sealed class RssFeed
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastFetchedAt { get; set; }
    public string? LastError { get; set; }
    public List<string> ReadArticleIds { get; set; } = new();
    public List<string> KnownArticleIds { get; set; } = new();
}

// Un article tel que recupere en direct depuis le flux (jamais persiste tel
// quel - seul l'identifiant rejoint ReadArticleIds une fois lu).
internal sealed record RssArticle(string Id, string Title, string Link, string Summary, DateTimeOffset? Published);
