using System.Text.Json;

namespace Lumora.WinUI;

// Registre local des applications web Lumora, chiffré (DPAPI) comme les
// favoris/historique/paramètres. Pas de données sensibles ici (juste des URL
// et des titres), même tier de protection que bookmarks.nova.
internal sealed class WebAppStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private readonly string _file;
    private readonly List<LumoraWebApp> _apps = new();
    private bool _isGuest;

    public WebAppStore(string file)
    {
        _file = file;
        Load();
    }

    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (isGuest) _apps.Clear();
    }

    private void Load()
    {
        try
        {
            var json = LumoraFile.TryReadAllText(_file);
            if (json is null) return;
            var loaded = JsonSerializer.Deserialize<List<LumoraWebApp>>(json, JsonOpts);
            if (loaded is not null) _apps.AddRange(loaded);
        }
        catch { }
    }

    private void Save()
    {
        if (_isGuest) return;
        try
        {
            LumoraFile.WriteAllText(_file, JsonSerializer.Serialize(_apps, JsonOpts));
        }
        catch { }
    }

    public IReadOnlyList<LumoraWebApp> All() => _apps.OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase).ToList();

    public LumoraWebApp? Find(string id) =>
        _apps.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

    public LumoraWebApp? FindByRootDomain(string rootDomain) =>
        _apps.FirstOrDefault(a => string.Equals(a.RootDomain, rootDomain, StringComparison.OrdinalIgnoreCase));

    public void Upsert(LumoraWebApp app)
    {
        var index = _apps.FindIndex(a => string.Equals(a.Id, app.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _apps[index] = app;
        else _apps.Add(app);
        Save();
    }

    public void Remove(string id)
    {
        _apps.RemoveAll(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        Save();
    }
}
