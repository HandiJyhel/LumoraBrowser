using System.Net;
using System.Text.RegularExpressions;

namespace Lumora.WinUI;

// Arbre de favoris a importer + lecteur de l'export HTML (format Netscape).
// Classe pure (aucune dependance UI) : compilee aussi par Lumora.Tests.
public sealed record BookmarkImportItem(string Title, string? Url, List<BookmarkImportItem> Children, string IconPath = "")
{
    public static BookmarkImportItem Folder(string title, IEnumerable<BookmarkImportItem> children) =>
        new(title, null, children.ToList());

    public static BookmarkImportItem UrlItem(string title, string url, string iconPath = "") =>
        new(title, url, new List<BookmarkImportItem>(), iconPath);
}

public sealed record BookmarkImportTree(List<BookmarkImportItem> Toolbar, List<BookmarkImportItem> Other)
{
    // Jetons du format "Netscape Bookmark" (export HTML de Chrome, Edge, Firefox,
    // Brave...) : ouverture/fermeture d'une liste <DL>, titre de dossier <H3>,
    // lien <A>. Les dossiers sont portes par <H3> suivi de <DL> (leurs enfants).
    private static readonly Regex HtmlTokenRegex = new(
        "<DL(?:\\s[^>]*)?>|</DL\\s*>|<H3(?<h3attrs>\\s[^>]*)?>(?<h3title>.*?)</H3\\s*>|<A\\s+(?<aattrs>[^>]*)>(?<atitle>.*?)</A\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex HrefRegex = new(
        "HREF\\s*=\\s*[\"'](?<url>[^\"']+)[\"']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ToolbarFlagRegex = new(
        "PERSONAL_TOOLBAR_FOLDER\\s*=\\s*[\"']?true",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex UnfiledFlagRegex = new(
        "UNFILED_BOOKMARKS_FOLDER\\s*=\\s*[\"']?true",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Racines "Autres favoris" de Chrome/Edge, qui n'ont aucun attribut pour se
    // reconnaitre : leur contenu va directement dans "Autres favoris" de Lumora
    // plutot que dans un sous-dossier du meme nom.
    private static readonly string[] UnfiledRootTitles =
    {
        "Autres favoris", "Other bookmarks", "Other favorites", "Autres marque-pages", "Other Bookmarks"
    };

    // Import HTML avec l'arborescence d'origine (dossiers et sous-dossiers).
    // Avant, un simple balayage des <A> aplatissait tout dans un seul dossier :
    // l'objet meme de l'import (ne pas reclasser des centaines de favoris a la
    // main) etait perdu - signale par l'utilisateur, 2026-10-25.
    public static BookmarkImportTree FromHtml(string html, string sourceName)
    {
        var root = new List<BookmarkImportItem>();
        var frames = new Stack<List<BookmarkImportItem>>();
        BookmarkImportItem? pendingFolder = null;
        BookmarkImportItem? toolbarFolder = null;
        var unfiledFolders = new List<BookmarkImportItem>();

        List<BookmarkImportItem> Current() => frames.Count > 0 ? frames.Peek() : root;

        foreach (Match match in HtmlTokenRegex.Matches(html))
        {
            var token = match.Value;
            if (token.StartsWith("</", StringComparison.Ordinal))
            {
                if (frames.Count > 0)
                {
                    frames.Pop();
                }

                pendingFolder = null;
            }
            else if (match.Groups["h3title"].Success)
            {
                var title = WebUtility.HtmlDecode(Regex.Replace(match.Groups["h3title"].Value, "<.*?>", string.Empty)).Trim();
                var folder = new BookmarkImportItem(title, null, new List<BookmarkImportItem>());
                Current().Add(folder);
                pendingFolder = folder;

                var attributes = match.Groups["h3attrs"].Value;
                if (toolbarFolder is null && ToolbarFlagRegex.IsMatch(attributes))
                {
                    toolbarFolder = folder;
                }
                else if (UnfiledFlagRegex.IsMatch(attributes))
                {
                    unfiledFolders.Add(folder);
                }
            }
            else if (match.Groups["aattrs"].Success)
            {
                pendingFolder = null;
                var href = HrefRegex.Match(match.Groups["aattrs"].Value);
                if (!href.Success)
                {
                    continue;
                }

                var url = WebUtility.HtmlDecode(href.Groups["url"].Value).Trim();
                if (!IsWebUrl(url))
                {
                    continue;
                }

                var title = WebUtility.HtmlDecode(Regex.Replace(match.Groups["atitle"].Value, "<.*?>", string.Empty)).Trim();
                Current().Add(BookmarkImportItem.UrlItem(title, url));
            }
            else
            {
                // <DL> : les enfants du dossier qui vient d'etre annonce, ou le
                // niveau courant s'il s'agit de la liste racine du fichier.
                frames.Push(pendingFolder?.Children ?? Current());
                pendingFolder = null;
            }
        }

        // Fichier sans aucun dossier (liste de liens a plat) : meme comportement
        // qu'avant, regroupes dans un dossier nomme d'apres le fichier.
        if (toolbarFolder is null && root.All(item => item.Url is not null))
        {
            return new BookmarkImportTree(new List<BookmarkImportItem>(), new List<BookmarkImportItem>
            {
                BookmarkImportItem.Folder($"Import HTML {sourceName}", root)
            });
        }

        var toolbar = new List<BookmarkImportItem>();
        var other = new List<BookmarkImportItem>();
        foreach (var item in root)
        {
            if (ReferenceEquals(item, toolbarFolder))
            {
                toolbar.AddRange(item.Children);
            }
            else if (item.Url is null
                     && (unfiledFolders.Any(folder => ReferenceEquals(folder, item))
                         || UnfiledRootTitles.Contains(item.Title, StringComparer.OrdinalIgnoreCase)))
            {
                other.AddRange(item.Children);
            }
            else
            {
                other.Add(item);
            }
        }

        return new BookmarkImportTree(toolbar, other);
    }

    // Meme regle que BookmarkStore.IsWebUrl, dupliquee ici pour que cette classe
    // reste pure (BookmarkStore tire WinUI/WebView2).
    private static bool IsWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
