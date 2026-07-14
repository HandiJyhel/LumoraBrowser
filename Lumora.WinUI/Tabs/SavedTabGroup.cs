using System.Text.Json;

namespace Lumora.WinUI;

// ── Groupes d'onglets enregistrés ────────────────────────────────────────────
// Un groupe « rangé » que l'utilisateur peut rouvrir plus tard, même après avoir
// fermé ses onglets. Contrairement aux groupes vivants (liés à des onglets
// ouverts, sauvegardés dans la session), un groupe enregistré est une archive :
// nom, couleur, date, et la liste des pages (titre + URL). Aucune donnée ne sort
// de la machine ; le store est chiffré comme le reste du profil.

public sealed record SavedTabGroupTab(string Title, string Url);

public sealed record SavedTabGroup(
    string Id,
    string Name,
    int ColorIndex,
    DateTimeOffset SavedAt,
    IReadOnlyList<SavedTabGroupTab> Tabs)
{
    public int TabCount => Tabs.Count;
}

// Store pur (aucune dépendance UI) : la persistance chiffrée est injectée via des
// délégués lecture/écriture pour rester testable hors du contexte WinUI.
public sealed class SavedTabGroupStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Func<string, string?> _read;
    private readonly Action<string, string> _write;
    private readonly List<SavedTabGroup> _groups = new();
    private bool _isGuest;

    // Mode invité : rien ne doit toucher le disque du profil. On vide la mémoire et
    // on coupe la persistance ; la bibliothèque redevient vide jusqu'à la sortie
    // du mode invité.
    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (isGuest) _groups.Clear();
    }

    public SavedTabGroupStore(
        string path,
        Func<string, string?> read,
        Action<string, string> write)
    {
        _path = path;
        _read = read;
        _write = write;
        Load();
    }

    // Les plus récemment enregistrés d'abord : c'est l'ordre utile pour retrouver
    // ce qu'on vient de ranger.
    public IReadOnlyList<SavedTabGroup> Groups =>
        _groups.OrderByDescending(group => group.SavedAt).ToList();

    public int Count => _groups.Count;

    // Enregistre un groupe vivant. Les pages internes (accueil Lumora) et les
    // entrées sans URL web sont ignorées : rouvrir « accueil » n'a aucun intérêt.
    // Retourne null si rien d'enregistrable ne reste après filtrage.
    public SavedTabGroup? Save(string name, int colorIndex, IEnumerable<SavedTabGroupTab> tabs, DateTimeOffset now)
    {
        var kept = tabs
            .Where(tab => IsSavableUrl(tab.Url))
            .Select(tab => new SavedTabGroupTab(
                string.IsNullOrWhiteSpace(tab.Title) ? tab.Url.Trim() : tab.Title.Trim(),
                tab.Url.Trim()))
            .ToList();
        if (kept.Count == 0) return null;

        var group = new SavedTabGroup(
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(name) ? "Groupe sans nom" : name.Trim(),
            colorIndex,
            now,
            kept);

        _groups.Add(group);
        Persist();
        return group;
    }

    public bool Remove(string id)
    {
        var removed = _groups.RemoveAll(group => group.Id == id) > 0;
        if (removed) Persist();
        return removed;
    }

    public SavedTabGroup? Find(string id) => _groups.FirstOrDefault(group => group.Id == id);

    public static bool IsSavableUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var value = url.Trim();
        if (value.StartsWith("lumora://", StringComparison.OrdinalIgnoreCase)) return false;
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
    }

    private void Load()
    {
        try
        {
            var json = _read(_path);
            if (string.IsNullOrWhiteSpace(json)) return;
            var loaded = JsonSerializer.Deserialize<List<SavedTabGroup>>(json, JsonOpts);
            if (loaded is not null) _groups.AddRange(loaded);
        }
        catch
        {
            // Un store illisible ne doit pas empêcher le démarrage.
        }
    }

    private void Persist()
    {
        if (_isGuest) return;
        try
        {
            _write(_path, JsonSerializer.Serialize(_groups, JsonOpts));
        }
        catch
        {
            // L'échec d'écriture ne doit pas casser l'expérience navigateur.
        }
    }
}
