using Microsoft.Data.Sqlite;

namespace PulseBrowser.WinUI;

// Lecteur des favoris Firefox.
//
// Firefox range ses favoris dans une base SQLite "places.sqlite" (tables
// moz_bookmarks pour l'arborescence, moz_places pour les URLs). La base est
// verrouillée quand Firefox tourne → on travaille sur une copie temporaire,
// en embarquant le journal WAL s'il existe pour ne pas perdre les écritures
// récentes. Lecture seule, aucune donnée envoyée nulle part.
internal static class FirefoxBookmarkReader
{
    // GUIDs stables des racines Firefox (constants depuis Firefox 36).
    private const string ToolbarGuid = "toolbar_____";
    private const string MenuGuid    = "menu________";
    private const string UnfiledGuid = "unfiled_____";
    private const string MobileGuid  = "mobile______";

    private const int TypeBookmark = 1;
    private const int TypeFolder   = 2;

    private sealed record Row(long Id, long Parent, int Type, string Guid, string Title, string Url, long Position);

    // Détecte les profils Firefox locaux contenant des favoris.
    public static List<(string ProfileName, string PlacesPath)> DiscoverProfiles()
    {
        var results = new List<(string, string)>();
        try
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var profilesDir = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
            if (!Directory.Exists(profilesDir)) return results;

            foreach (var dir in Directory.EnumerateDirectories(profilesDir))
            {
                var places = Path.Combine(dir, "places.sqlite");
                if (File.Exists(places))
                    results.Add((Path.GetFileName(dir), places));
            }
        }
        catch { }
        return results;
    }

    public static int CountUrls(string placesPath)
    {
        try
        {
            var tree = ReadTree(placesPath);
            return CountItems(tree.Toolbar) + CountItems(tree.Other);
        }
        catch { return 0; }
    }

    public static BookmarkImportTree ReadTree(string placesPath)
    {
        var rows = ReadRows(placesPath);
        var byParent = rows
            .GroupBy(r => r.Parent)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Position).ToList());
        var byGuid = rows.ToDictionary(r => r.Guid, r => r);

        var toolbar = ChildrenOf(byGuid, byParent, ToolbarGuid);

        // Menu, "Autres favoris" et favoris mobiles vont ensemble dans root-other.
        var other = new List<BookmarkImportItem>();
        other.AddRange(ChildrenOf(byGuid, byParent, MenuGuid));
        other.AddRange(ChildrenOf(byGuid, byParent, UnfiledGuid));
        var mobile = ChildrenOf(byGuid, byParent, MobileGuid);
        if (mobile.Count > 0)
            other.Add(BookmarkImportItem.Folder("Favoris mobiles", mobile));

        return new BookmarkImportTree(toolbar, other);
    }

    private static List<BookmarkImportItem> ChildrenOf(
        Dictionary<string, Row> byGuid,
        Dictionary<long, List<Row>> byParent,
        string rootGuid)
    {
        return byGuid.TryGetValue(rootGuid, out var root)
            ? BuildItems(byParent, root.Id)
            : new List<BookmarkImportItem>();
    }

    private static List<BookmarkImportItem> BuildItems(Dictionary<long, List<Row>> byParent, long parentId)
    {
        var items = new List<BookmarkImportItem>();
        if (!byParent.TryGetValue(parentId, out var children)) return items;

        foreach (var child in children)
        {
            if (child.Type == TypeBookmark && BookmarkStore.IsWebUrl(child.Url))
                items.Add(BookmarkImportItem.UrlItem(child.Title, child.Url));
            else if (child.Type == TypeFolder)
                items.Add(BookmarkImportItem.Folder(child.Title, BuildItems(byParent, child.Id)));
            // Type 3 (séparateur) ignoré.
        }

        return items;
    }

    private static List<Row> ReadRows(string placesPath)
    {
        // Copie temporaire : la base est verrouillée quand Firefox tourne, et le
        // WAL doit suivre pour que les favoris récents soient visibles.
        var tmp = Path.Combine(Path.GetTempPath(), "pulse_ff_places_" + Guid.NewGuid().ToString("N") + ".sqlite");
        File.Copy(placesPath, tmp, true);
        var wal = placesPath + "-wal";
        if (File.Exists(wal))
        {
            try { File.Copy(wal, tmp + "-wal", true); } catch { }
        }

        try
        {
            var rows = new List<Row>();
            using var conn = new SqliteConnection($"Data Source={tmp};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT b.id, b.parent, b.type, b.guid,
                       IFNULL(b.title, ''), IFNULL(p.url, ''), b.position
                FROM moz_bookmarks b
                LEFT JOIN moz_places p ON b.fk = p.id
                """;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new Row(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetInt32(2),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetInt64(6)));
            }
            return rows;
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
            try { File.Delete(tmp + "-wal"); } catch { }
        }
    }

    private static int CountItems(IEnumerable<BookmarkImportItem> items) =>
        items.Sum(item => item.Url is not null ? 1 : CountItems(item.Children));
}
