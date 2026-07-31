using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Lumora.WinUI;

// Recupere et parse un flux RSS 2.0 ou Atom. Simple GET vers l'URL choisie par
// l'utilisateur (aucune donnee utilisateur envoyee) - meme principe que
// FilterListManager. Parsing fait main via System.Xml.Linq plutot qu'une
// dependance tierce (System.ServiceModel.Syndication) : les deux formats
// couverts ici sont bornes et deja bien connus, coherent avec le reste du
// projet qui prefere un parseur maison a une dependance quand ce n'est pas
// necessaire (voir Notes/Bookmarks).
internal static class RssFeedService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    internal sealed record FetchResult(string Title, IReadOnlyList<RssArticle> Articles);

    public static async Task<FetchResult> FetchAsync(string url)
    {
        var xml = await Http.GetStringAsync(url);
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidOperationException("Flux vide ou invalide.");

        return root.Name.LocalName.Equals("feed", StringComparison.OrdinalIgnoreCase)
            ? ParseAtom(root)
            : ParseRss(root);
    }

    private static FetchResult ParseRss(XElement root)
    {
        var channel = root.Element("channel") ?? root;
        var title = channel.Element("title")?.Value.Trim();
        var articles = new List<RssArticle>();

        foreach (var item in channel.Elements("item"))
        {
            var link = item.Element("link")?.Value.Trim() ?? string.Empty;
            var guid = item.Element("guid")?.Value.Trim();
            var id = !string.IsNullOrWhiteSpace(guid) ? guid : link;
            if (string.IsNullOrWhiteSpace(id)) continue;

            var itemTitle = item.Element("title")?.Value.Trim();
            var summary = StripHtml(item.Element("description")?.Value ?? string.Empty);
            var published = ParseDate(item.Element("pubDate")?.Value);
            articles.Add(new RssArticle(id, string.IsNullOrWhiteSpace(itemTitle) ? "(sans titre)" : itemTitle, link, summary, published));
        }

        return new FetchResult(string.IsNullOrWhiteSpace(title) ? "Flux sans titre" : title, articles);
    }

    private static FetchResult ParseAtom(XElement root)
    {
        var title = root.Element(Atom + "title")?.Value.Trim();
        var articles = new List<RssArticle>();

        foreach (var entry in root.Elements(Atom + "entry"))
        {
            var links = entry.Elements(Atom + "link").ToList();
            var link =
                links.FirstOrDefault(l => (string?)l.Attribute("rel") is null or "alternate")?.Attribute("href")?.Value.Trim()
                ?? links.FirstOrDefault()?.Attribute("href")?.Value.Trim()
                ?? string.Empty;

            var id = entry.Element(Atom + "id")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(id)) id = link;
            if (string.IsNullOrWhiteSpace(id)) continue;

            var entryTitle = entry.Element(Atom + "title")?.Value.Trim();
            var summary = StripHtml(
                entry.Element(Atom + "summary")?.Value
                ?? entry.Element(Atom + "content")?.Value
                ?? string.Empty);
            var published = ParseDate(
                entry.Element(Atom + "published")?.Value
                ?? entry.Element(Atom + "updated")?.Value);

            articles.Add(new RssArticle(id, string.IsNullOrWhiteSpace(entryTitle) ? "(sans titre)" : entryTitle, link, summary, published));
        }

        return new FetchResult(string.IsNullOrWhiteSpace(title) ? "Flux sans titre" : title, articles);
    }

    private static DateTimeOffset? ParseDate(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) &&
        DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    // Les resumes RSS/Atom contiennent souvent du HTML brut : jamais injecte
    // dans WebView2, seulement affiche en texte simple dans la liste.
    private static string StripHtml(string html)
    {
        var decoded = WebUtility.HtmlDecode(html);
        var stripped = Regex.Replace(decoded, "<[^>]+>", " ");
        stripped = Regex.Replace(stripped, @"\s+", " ").Trim();
        return stripped.Length > 400 ? stripped[..400] + "…" : stripped;
    }
}
