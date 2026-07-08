using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using PulseBrowser.WinUI.Credentials;

namespace PulseBrowser.WinUI;

public sealed record BookmarkListItem(
    string IconGlyph,
    string IconImageUri,
    Visibility IconImageVisibility,
    Visibility IconGlyphVisibility,
    string Title,
    string Detail,
    BookmarkNode Node);

public enum BookmarkKind
{
    Folder,
    Url
}

public sealed record BookmarkNode(
    string Id,
    string ParentId,
    BookmarkKind Kind,
    uint Position,
    string Title,
    string Url,
    string IconPath = "")
{
    public bool IsRoot => Id is BookmarkStore.ToolbarRootId or BookmarkStore.OtherRootId;
    public string IconUri => string.IsNullOrWhiteSpace(IconPath) ? string.Empty : new Uri(IconPath).AbsoluteUri;
}

public static class BookmarkGlyphs
{
    public const string Folder = "";
    public const string Link = "";

    public static string For(BookmarkNode node) => node.Kind == BookmarkKind.Folder ? Folder : Link;
}


public sealed class BookmarkStore
{
    public const string ToolbarRootId = "root-toolbar";
    public const string OtherRootId = "root-other";

    private readonly string _bookmarksFile;
    private readonly string? _legacyBookmarksFile;
    private readonly string? _legacyFavoritesFile;
    private bool _isGuest;

    public BookmarkStore(string bookmarksFile, string? legacyBookmarksFile, string? legacyFavoritesFile)
    {
        _bookmarksFile = bookmarksFile;
        _legacyBookmarksFile = legacyBookmarksFile;
        _legacyFavoritesFile = legacyFavoritesFile;
    }

    public void SetGuestMode(bool isGuest) => _isGuest = isGuest;

    public List<BookmarkNode> AllNodes()
    {
        if (_isGuest) return Sort(RootNodes());

        EnsureFile();
        var content = PulseFile.TryReadAllText(_bookmarksFile) ?? string.Empty;
        var nodes = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseLine)
            .Where(node => node is not null)
            .Cast<BookmarkNode>()
            .ToList();
        EnsureRoots(nodes);
        return Sort(nodes);
    }

    public void AddUrl(string parentId, string url, string title, string iconPath = "")
    {
        if (!IsWebUrl(url))
        {
            return;
        }

        var nodes = AllNodes();
        nodes.Add(new BookmarkNode(NextNodeId(nodes, "bookmark"), parentId, BookmarkKind.Url, NextPosition(nodes, parentId), CleanTitle(title, url), url, CleanLocalPath(iconPath)));
        WriteNodes(nodes);
    }

    public void AddOrUpdateUrl(string? id, string parentId, string url, string title, string iconPath = "")
    {
        if (!IsWebUrl(url))
        {
            return;
        }

        var nodes = AllNodes();
        if (nodes.All(node => node.Id != parentId || node.Kind != BookmarkKind.Folder))
        {
            parentId = ToolbarRootId;
        }

        var index = string.IsNullOrWhiteSpace(id)
            ? -1
            : nodes.FindIndex(node => node.Id == id && node.Kind == BookmarkKind.Url);

        if (index >= 0)
        {
            var node = nodes[index];
            var moved = !node.ParentId.Equals(parentId, StringComparison.Ordinal);
            nodes[index] = node with
            {
                ParentId = parentId,
                Position = moved ? NextPosition(nodes, parentId) : node.Position,
                Title = CleanTitle(title, url),
                Url = url,
                IconPath = CleanLocalPath(iconPath)
            };
            WriteNodes(nodes);
            return;
        }

        nodes.Add(new BookmarkNode(
            NextNodeId(nodes, "bookmark"),
            parentId,
            BookmarkKind.Url,
            NextPosition(nodes, parentId),
            CleanTitle(title, url),
            url,
            CleanLocalPath(iconPath)));
        WriteNodes(nodes);
    }

    public bool SetIconForUrl(string url, string iconPath)
    {
        if (!IsWebUrl(url) || string.IsNullOrWhiteSpace(iconPath))
        {
            return false;
        }

        var nodes = AllNodes();
        var changed = false;
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node.Kind == BookmarkKind.Url && node.Url.Equals(url, StringComparison.OrdinalIgnoreCase) && node.IconPath != iconPath)
            {
                nodes[index] = node with { IconPath = CleanLocalPath(iconPath) };
                changed = true;
            }
        }

        if (changed)
        {
            WriteNodes(nodes);
        }

        return changed;
    }

    public bool SetIconForOrigin(string url, string iconPath)
    {
        if (!IsWebUrl(url) || string.IsNullOrWhiteSpace(iconPath))
        {
            return false;
        }

        var origin = UrlOrigin(url);
        var nodes = AllNodes();
        var changed = false;
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node.Kind == BookmarkKind.Url && node.IconPath != iconPath &&
                UrlOrigin(node.Url).Equals(origin, StringComparison.OrdinalIgnoreCase))
            {
                nodes[index] = node with { IconPath = CleanLocalPath(iconPath) };
                changed = true;
            }
        }

        if (changed)
        {
            WriteNodes(nodes);
        }

        return changed;
    }

    // Normalisation d'origine centralisée sur PublicSuffixService (source unique).
    private static string UrlOrigin(string url) => PublicSuffixService.OriginOf(url);

    public void AddFolder(string parentId, string title)
    {
        var nodes = AllNodes();
        if (nodes.All(node => node.Id != parentId || node.Kind != BookmarkKind.Folder))
        {
            parentId = ToolbarRootId;
        }

        nodes.Add(new BookmarkNode(NextNodeId(nodes, "folder"), parentId, BookmarkKind.Folder, NextPosition(nodes, parentId), CleanTitle(title, string.Empty), string.Empty));
        WriteNodes(nodes);
    }

    public void RenameNode(string id, string title)
    {
        if (id is ToolbarRootId or OtherRootId)
        {
            return;
        }

        var nodes = AllNodes();
        var index = nodes.FindIndex(node => node.Id == id);
        if (index < 0)
        {
            return;
        }

        var node = nodes[index];
        nodes[index] = node with { Title = CleanTitle(title, node.Url) };
        WriteNodes(nodes);
    }

    public void RemoveNode(string id)
    {
        if (id is ToolbarRootId or OtherRootId)
        {
            return;
        }

        var nodes = AllNodes();
        var removeIds = new HashSet<string> { id };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in nodes)
            {
                if (removeIds.Contains(node.ParentId) && removeIds.Add(node.Id))
                {
                    changed = true;
                }
            }
        }

        WriteNodes(nodes.Where(node => !removeIds.Contains(node.Id)).ToList());
    }

    public int MergeImport(BookmarkImportTree tree)
    {
        EnsureFile();
        Backup("before-winui-import");
        var nodes = AllNodes();
        var imported = 0;
        imported += AddImportItems(nodes, ToolbarRootId, tree.Toolbar);
        imported += AddImportItems(nodes, OtherRootId, tree.Other);
        WriteNodes(nodes);
        return imported;
    }

    public int ReplaceWithImport(BookmarkImportTree tree)
    {
        EnsureFile();
        Backup("before-winui-replace");
        var nodes = RootNodes();
        var imported = 0;
        imported += AddImportItems(nodes, ToolbarRootId, tree.Toolbar);
        imported += AddImportItems(nodes, OtherRootId, tree.Other);
        WriteNodes(nodes);
        return imported;
    }

    private void EnsureFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_bookmarksFile)!);
        if (File.Exists(_bookmarksFile)) return;

        var nodes = RootNodes();

        // Migration depuis bookmarks.tsv (format précédent non chiffré)
        if (_legacyBookmarksFile is not null && File.Exists(_legacyBookmarksFile))
        {
            var legacyContent = File.ReadAllText(_legacyBookmarksFile, Encoding.UTF8);
            PulseFile.WriteAllText(_bookmarksFile, legacyContent);
            try { File.Delete(_legacyBookmarksFile); } catch { }
            return;
        }

        // Migration depuis favorites.tsv (très ancien format plat)
        if (_legacyFavoritesFile is not null && File.Exists(_legacyFavoritesFile))
        {
            var folderId = "legacy-flat-favorites";
            nodes.Add(new BookmarkNode(folderId, OtherRootId, BookmarkKind.Folder, 0, "Anciens favoris importes", string.Empty));
            var index = 0u;
            foreach (var line in File.ReadLines(_legacyFavoritesFile))
            {
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;
                var url = DecodeField(parts[1]);
                if (!IsWebUrl(url)) continue;
                nodes.Add(new BookmarkNode($"legacy-{index + 1}", folderId, BookmarkKind.Url, index++, CleanTitle(DecodeField(parts[2]), url), url));
            }
        }

        WriteNodes(nodes);
    }

    private int AddImportItems(List<BookmarkNode> nodes, string parentId, IReadOnlyList<BookmarkImportItem> items)
    {
        var imported = 0;
        foreach (var item in items)
        {
            if (item.Url is not null)
            {
                if (!IsWebUrl(item.Url) || nodes.Any(node => node.Kind == BookmarkKind.Url && node.Url == item.Url))
                {
                    continue;
                }

                nodes.Add(new BookmarkNode(NextNodeId(nodes, "bookmark"), parentId, BookmarkKind.Url, NextPosition(nodes, parentId), CleanTitle(item.Title, item.Url), item.Url));
                imported++;
                continue;
            }

            var folderId = NextNodeId(nodes, "folder");
            nodes.Add(new BookmarkNode(folderId, parentId, BookmarkKind.Folder, NextPosition(nodes, parentId), CleanTitle(item.Title, string.Empty), string.Empty));
            imported += AddImportItems(nodes, folderId, item.Children);
        }

        return imported;
    }

    private void WriteNodes(IReadOnlyList<BookmarkNode> nodes)
    {
        if (_isGuest) return;
        var builder = new StringBuilder();
        foreach (var node in Sort(nodes))
        {
            builder.Append(EncodeField(node.Id)).Append('\t')
                .Append(EncodeField(node.ParentId)).Append('\t')
                .Append(node.Kind == BookmarkKind.Folder ? "folder" : "url").Append('\t')
                .Append(node.Position).Append('\t')
                .Append(EncodeField(node.Title)).Append('\t')
                .Append(EncodeField(node.Url)).Append('\t')
                .Append(EncodeField(node.IconPath)).Append('\n');
        }

        PulseFile.WriteAllText(_bookmarksFile, builder.ToString());
    }

    private void Backup(string label)
    {
        if (!File.Exists(_bookmarksFile))
        {
            return;
        }

        var backupPath = Path.Combine(Path.GetDirectoryName(_bookmarksFile)!, $"bookmarks.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{label}.bak.tsv");
        File.Copy(_bookmarksFile, backupPath, overwrite: true);
    }

    private static List<BookmarkNode> RootNodes() =>
        new()
        {
            new BookmarkNode(ToolbarRootId, string.Empty, BookmarkKind.Folder, 0, "Barre des favoris", string.Empty),
            new BookmarkNode(OtherRootId, string.Empty, BookmarkKind.Folder, 1, "Autres favoris", string.Empty)
        };

    private static void EnsureRoots(List<BookmarkNode> nodes)
    {
        foreach (var root in RootNodes())
        {
            if (nodes.All(node => node.Id != root.Id))
            {
                nodes.Add(root);
            }
        }
    }

    private static BookmarkNode? ParseLine(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 6 || !uint.TryParse(parts[3], out var position))
        {
            return null;
        }

        var kind = parts[2] switch
        {
            "folder" => BookmarkKind.Folder,
            "url" => BookmarkKind.Url,
            _ => (BookmarkKind?)null
        };

        var iconPath = parts.Length >= 7 ? CleanLocalPath(DecodeField(parts[6])) : string.Empty;
        return kind is null
            ? null
            : new BookmarkNode(DecodeField(parts[0]), DecodeField(parts[1]), kind.Value, position, DecodeField(parts[4]), DecodeField(parts[5]), iconPath);
    }

    private static List<BookmarkNode> Sort(IEnumerable<BookmarkNode> nodes) =>
        nodes.OrderBy(node => node.ParentId).ThenBy(node => node.Position).ThenBy(node => node.Title).ToList();

    private static uint NextPosition(IEnumerable<BookmarkNode> nodes, string parentId)
    {
        var positions = nodes.Where(node => node.ParentId == parentId).Select(node => node.Position).ToList();
        return positions.Count == 0 ? 0 : positions.Max() + 1;
    }

    private static string NextNodeId(IEnumerable<BookmarkNode> nodes, string prefix)
    {
        var existing = nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var index = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        while (true)
        {
            var id = $"{prefix}-{index++}";
            if (!existing.Contains(id))
            {
                return id;
            }
        }
    }

    private static string CleanTitle(string title, string url)
    {
        title = title.Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Length > 160 ? title[..160] : title;
        }

        return IsWebUrl(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "Dossier sans nom";
    }

    private static string CleanLocalPath(string path)
    {
        path = path.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static bool IsWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static string EncodeField(string value)
    {
        var builder = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if ((b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9') || b is (byte)'-' or (byte)'_' or (byte)'.' or (byte)'~')
            {
                builder.Append((char)b);
            }
            else
            {
                builder.Append('%').Append(b.ToString("X2"));
            }
        }

        return builder.ToString();
    }

    private static string DecodeField(string value)
    {
        var bytes = new List<byte>();
        for (var index = 0; index < value.Length;)
        {
            if (value[index] == '%' && index + 2 < value.Length && byte.TryParse(value.Substring(index + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var decoded))
            {
                bytes.Add(decoded);
                index += 3;
            }
            else
            {
                bytes.Add((byte)value[index]);
                index++;
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}

public sealed record BookmarkImportItem(string Title, string? Url, List<BookmarkImportItem> Children)
{
    public static BookmarkImportItem Folder(string title, IEnumerable<BookmarkImportItem> children) =>
        new(title, null, children.ToList());

    public static BookmarkImportItem UrlItem(string title, string url) =>
        new(title, url, new List<BookmarkImportItem>());
}

public sealed record BookmarkImportTree(List<BookmarkImportItem> Toolbar, List<BookmarkImportItem> Other)
{
    private static readonly Regex AnchorRegex = new(
        "<A\\s+[^>]*HREF\\s*=\\s*[\"'](?<url>[^\"']+)[\"'][^>]*>(?<title>.*?)</A>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static BookmarkImportTree FromHtml(string html, string sourceName)
    {
        var items = AnchorRegex.Matches(html)
            .Select(match => BookmarkImportItem.UrlItem(
                WebUtility.HtmlDecode(Regex.Replace(match.Groups["title"].Value, "<.*?>", string.Empty)).Trim(),
                WebUtility.HtmlDecode(match.Groups["url"].Value).Trim()))
            .Where(item => BookmarkStore.IsWebUrl(item.Url ?? string.Empty))
            .ToList();

        return new BookmarkImportTree(new List<BookmarkImportItem>(), new List<BookmarkImportItem>
        {
            BookmarkImportItem.Folder($"Import HTML {sourceName}", items)
        });
    }
}

public sealed record MigrationBrowserEntry(string Name, BrowserImportSource? Source)
{
    public string Label => Source is not null
        ? $"{Name}  —  {Source.Count} favori(s) détecté(s)"
        : $"{Name}  —  non détecté";
}

public sealed record BrowserImportSource(string Browser, string Profile, string Path, int Count)
{
    public string Label => $"{Browser} {Profile} - {Count} favoris";

    public static List<BrowserImportSource> Discover()
    {
        var local   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var roots = new (string Browser, string UserData)[]
        {
            ("Google Chrome", System.IO.Path.Combine(local,   "Google", "Chrome", "User Data")),
            ("Microsoft Edge", System.IO.Path.Combine(local,   "Microsoft", "Edge", "User Data")),
            ("Brave",          System.IO.Path.Combine(local,   "BraveSoftware", "Brave-Browser", "User Data")),
            ("Chromium",       System.IO.Path.Combine(local,   "Chromium", "User Data")),
            ("Vivaldi",        System.IO.Path.Combine(local,   "Vivaldi", "User Data")),
            ("Opera",          System.IO.Path.Combine(roaming, "Opera Software", "Opera Stable", "User Data")),
            ("Opera GX",       System.IO.Path.Combine(roaming, "Opera Software", "Opera GX Stable", "User Data")),
        };

        var sources = new List<BrowserImportSource>();
        foreach (var (browser, userData) in roots)
        {
            if (!Directory.Exists(userData))
            {
                continue;
            }

            foreach (var profileDir in Directory.EnumerateDirectories(userData).Where(IsChromiumProfileDir))
            {
                var bookmarks = System.IO.Path.Combine(profileDir, "Bookmarks");
                if (!File.Exists(bookmarks))
                {
                    continue;
                }

                var count = CountUrls(bookmarks);
                if (count > 0)
                {
                    sources.Add(new BrowserImportSource(browser, System.IO.Path.GetFileName(profileDir), bookmarks, count));
                }
            }
        }

        // Firefox : format SQLite (places.sqlite), lu par FirefoxBookmarkReader.
        foreach (var (profileName, placesPath) in FirefoxBookmarkReader.DiscoverProfiles())
        {
            var count = FirefoxBookmarkReader.CountUrls(placesPath);
            if (count > 0)
            {
                sources.Add(new BrowserImportSource("Firefox", profileName, placesPath, count));
            }
        }

        return sources;
    }

    private bool IsFirefoxSource =>
        System.IO.Path.GetFileName(Path).Equals("places.sqlite", StringComparison.OrdinalIgnoreCase);

    public BookmarkImportTree ReadTree()
    {
        if (IsFirefoxSource)
        {
            return FirefoxBookmarkReader.ReadTree(Path);
        }

        var json = JsonNode.Parse(File.ReadAllText(Path));
        var roots = json?["roots"];
        return new BookmarkImportTree(
            ReadChildren(roots?["bookmark_bar"]?["children"]),
            ReadChildren(roots?["other"]?["children"]));
    }

    private static bool IsChromiumProfileDir(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        return name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountUrls(string bookmarksFile)
    {
        try
        {
            var source = new BrowserImportSource(string.Empty, string.Empty, bookmarksFile, 0);
            var tree = source.ReadTree();
            return CountItems(tree.Toolbar) + CountItems(tree.Other);
        }
        catch
        {
            return 0;
        }
    }

    private static int CountItems(IEnumerable<BookmarkImportItem> items) =>
        items.Sum(item => item.Url is not null ? 1 : CountItems(item.Children));

    private static List<BookmarkImportItem> ReadChildren(JsonNode? children)
    {
        var items = new List<BookmarkImportItem>();
        if (children is not JsonArray array)
        {
            return items;
        }

        foreach (var child in array)
        {
            var type = child?["type"]?.GetValue<string>() ?? string.Empty;
            var name = child?["name"]?.GetValue<string>() ?? string.Empty;
            if (type == "url")
            {
                var url = child?["url"]?.GetValue<string>() ?? string.Empty;
                if (BookmarkStore.IsWebUrl(url))
                {
                    items.Add(BookmarkImportItem.UrlItem(name, url));
                }
            }
            else if (type == "folder")
            {
                items.Add(BookmarkImportItem.Folder(name, ReadChildren(child?["children"])));
            }
        }

        return items;
    }
}

public static class BookmarkTreePresenter
{
    public static IEnumerable<BookmarkListItem> FlattenFolders(IReadOnlyList<BookmarkNode> nodes)
    {
        foreach (var rootId in new[] { BookmarkStore.ToolbarRootId, BookmarkStore.OtherRootId })
        {
            var root = nodes.First(node => node.Id == rootId);
            yield return ItemForNode(nodes, root, 0);
            foreach (var item in FlattenFolderChildren(nodes, root.Id, 1))
            {
                yield return item;
            }
        }
    }

    public static BookmarkListItem ItemForNode(IReadOnlyList<BookmarkNode> nodes, BookmarkNode node, int depth)
    {
        var prefix = new string(' ', depth * 3);
        var icon = BookmarkGlyphs.For(node);
        var title = string.IsNullOrWhiteSpace(node.Title) ? "(sans nom)" : node.Title;
        var detail = node.Kind == BookmarkKind.Folder
            ? $"{nodes.Count(candidate => candidate.ParentId == node.Id)} element(s)"
            : node.Url;
        var hasIcon = node.Kind == BookmarkKind.Url && !string.IsNullOrWhiteSpace(node.IconPath) && File.Exists(node.IconPath);
        return new BookmarkListItem(
            icon,
            hasIcon ? node.IconUri : string.Empty,
            hasIcon ? Visibility.Visible : Visibility.Collapsed,
            hasIcon ? Visibility.Collapsed : Visibility.Visible,
            prefix + title,
            detail,
            node);
    }

    public static string Breadcrumb(IReadOnlyList<BookmarkNode> nodes, string folderId)
    {
        var parts = new List<string>();
        var currentId = folderId;
        while (!string.IsNullOrWhiteSpace(currentId))
        {
            var current = nodes.FirstOrDefault(node => node.Id == currentId);
            if (current is null)
            {
                break;
            }

            parts.Insert(0, current.Title);
            currentId = current.ParentId;
        }

        return string.Join(" > ", parts);
    }

    public static IEnumerable<BookmarkListItem> Search(IReadOnlyList<BookmarkNode> nodes, string query)
    {
        var normalized = query.Trim().ToLowerInvariant();
        return nodes
            .Where(node => !node.IsRoot)
            .Where(node =>
                node.Title.ToLowerInvariant().Contains(normalized) ||
                node.Url.ToLowerInvariant().Contains(normalized))
            .OrderBy(node => node.Kind == BookmarkKind.Url ? 1 : 0)
            .ThenBy(node => node.Title)
            .Select(node => ItemForNode(nodes, node, 0));
    }

    private static IEnumerable<BookmarkListItem> FlattenFolderChildren(IReadOnlyList<BookmarkNode> nodes, string parentId, int depth)
    {
        foreach (var node in nodes
                     .Where(candidate => candidate.ParentId == parentId && candidate.Kind == BookmarkKind.Folder)
                     .OrderBy(candidate => candidate.Position))
        {
            yield return ItemForNode(nodes, node, depth);
            foreach (var child in FlattenFolderChildren(nodes, node.Id, depth + 1))
            {
                yield return child;
            }
        }
    }
}

public static class BookmarkHtmlExporter
{
    public static string Export(IReadOnlyList<BookmarkNode> nodes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        builder.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        builder.AppendLine("<TITLE>Bookmarks</TITLE>");
        builder.AppendLine("<H1>Bookmarks</H1>");
        builder.AppendLine("<DL><p>");
        WriteChildren(builder, nodes, BookmarkStore.ToolbarRootId, 1, "Barre des favoris");
        WriteChildren(builder, nodes, BookmarkStore.OtherRootId, 1, "Autres favoris");
        builder.AppendLine("</DL><p>");
        return builder.ToString();
    }

    private static void WriteChildren(StringBuilder builder, IReadOnlyList<BookmarkNode> nodes, string parentId, int depth, string title)
    {
        var indent = new string(' ', depth * 4);
        builder.Append(indent).Append("<DT><H3>").Append(WebUtility.HtmlEncode(title)).AppendLine("</H3>");
        builder.Append(indent).AppendLine("<DL><p>");
        foreach (var node in nodes.Where(candidate => candidate.ParentId == parentId).OrderBy(candidate => candidate.Position))
        {
            if (node.Kind == BookmarkKind.Url)
            {
                builder.Append(indent).Append("    <DT><A HREF=\"")
                    .Append(WebUtility.HtmlEncode(node.Url))
                    .Append("\">")
                    .Append(WebUtility.HtmlEncode(node.Title))
                    .AppendLine("</A>");
            }
            else
            {
                WriteChildren(builder, nodes, node.Id, depth + 1, node.Title);
            }
        }

        builder.Append(indent).AppendLine("</DL><p>");
    }
}

