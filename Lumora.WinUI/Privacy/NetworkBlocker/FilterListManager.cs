using System.Text.Json;

namespace Lumora.Privacy.NetworkBlocker;

// Télécharge et met en cache les listes de filtrage sur disque local.
// Aucune donnée utilisateur n'est envoyée — GET pur vers des sources publiques open source.
internal sealed class FilterListManager
{
    private static readonly string ListsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lumora", "privacy", "lists");

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromDays(7);

    private static readonly (string Id, string Url)[] Sources =
    [
        ("easylist",        "https://easylist.to/easylist/easylist.txt"),
        ("easyprivacy",     "https://easylist.to/easylist/easyprivacy.txt"),
        ("ublock-filters",  "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/filters.txt"),
        ("adguard-base",    "https://filters.adtidy.org/extension/ublock/filters/2.txt"),
        // Pubs des sites francophones (0.77) : indispensable pour un usage FR.
        ("liste-fr",        "https://easylist-downloads.adblockplus.org/liste_fr.txt"),
        // Overlays et pop-ins insistants (0.77) : la pub « impossible a enlever ».
        ("ublock-annoyances", "https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/annoyances.txt"),
    ];

    private static readonly System.Net.Http.HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "Lumora/1.0 (privacy filter updater)" } }
    };

    private Dictionary<string, DateTime> _meta = new();

    private string MetaPath => Path.Combine(ListsDir, "meta.json");

    // Charge les listes depuis le disque et déclenche une mise à jour en arrière-plan si nécessaire.
    public async Task<IEnumerable<string>> LoadAsync(Action<string>? onStatus = null)
    {
        Directory.CreateDirectory(ListsDir);
        LoadMeta();

        var lines = new List<string>(capacity: 500_000);

        foreach (var (id, _) in Sources)
        {
            var path = Path.Combine(ListsDir, $"{id}.txt");
            if (File.Exists(path))
            {
                try { lines.AddRange(await File.ReadAllLinesAsync(path)); }
                catch { }
            }
        }

        // Mise à jour en arrière-plan si les listes sont absentes ou périmées
        if (NeedsUpdate())
            _ = Task.Run(() => DownloadAllAsync(onStatus));

        return lines;
    }

    // Téléchargement forcé (depuis les paramètres)
    public Task ForceUpdateAsync(Action<string>? onStatus = null) =>
        Task.Run(() => DownloadAllAsync(onStatus));

    public DateTime? LastUpdate()
    {
        LoadMeta();
        if (_meta.Count == 0) return null;
        return _meta.Values.Min();
    }

    private bool NeedsUpdate()
    {
        foreach (var (id, _) in Sources)
        {
            var path = Path.Combine(ListsDir, $"{id}.txt");
            if (!File.Exists(path)) return true;
            if (!_meta.TryGetValue(id, out var ts) || DateTime.UtcNow - ts > UpdateInterval) return true;
        }
        return false;
    }

    private async Task DownloadAllAsync(Action<string>? onStatus)
    {
        Directory.CreateDirectory(ListsDir);

        foreach (var (id, url) in Sources)
        {
            try
            {
                onStatus?.Invoke($"Téléchargement : {id}...");
                var content = await Http.GetStringAsync(url);
                var path = Path.Combine(ListsDir, $"{id}.txt");
                await File.WriteAllTextAsync(path, content);
                _meta[id] = DateTime.UtcNow;
            }
            catch
            {
                // On garde la liste précédente si le téléchargement échoue
            }
        }

        SaveMeta();
        onStatus?.Invoke("Listes mises à jour.");
    }

    private void LoadMeta()
    {
        try
        {
            if (!File.Exists(MetaPath)) return;
            var json = File.ReadAllText(MetaPath);
            _meta = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json) ?? new();
        }
        catch { _meta = new(); }
    }

    private void SaveMeta()
    {
        try
        {
            var json = JsonSerializer.Serialize(_meta, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(MetaPath, json);
        }
        catch { }
    }
}
