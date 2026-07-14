using System.Text.Json;
using Lumora.WinUI.Credentials;

namespace Lumora.WinUI;

// ── Mémoire des déménagements de sites ───────────────────────────────────────
// Certains sites changent régulièrement de nom de domaine (l'ancien domaine
// redirige un temps vers le nouveau, puis meurt). Chrome refait la redirection
// à chaque visite sans jamais rien retenir ; Lumora, lui, APPREND localement les
// redirections permanentes (301/308) observées sur le document principal :
//  - quand l'ancien domaine finit par mourir, la barre « site introuvable »
//    peut proposer directement la derniere adresse connue ;
//  - les favoris et raccourcis qui pointent encore vers l'ancien domaine
//    peuvent être mis à jour en un clic.
// Store pur (aucune dépendance UI), persistance chiffrée injectée par délégués,
// compilé aussi dans Lumora.Tests. Aucune donnée ne sort de la machine.

public sealed record SiteRelocation(
    string FromRootDomain,
    string ToOrigin,
    DateTimeOffset RecordedAt,
    bool UpdateOffered = false);

public sealed class SiteRelocationStore
{
    private const int MaxEntries = 200;
    private const int MaxChainHops = 10;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Func<string, string?> _read;
    private readonly Action<string, string> _write;
    private readonly List<SiteRelocation> _relocations = new();
    private bool _isGuest;

    public SiteRelocationStore(
        string path,
        Func<string, string?> read,
        Action<string, string> write)
    {
        _path = path;
        _read = read;
        _write = write;
        Load();
    }

    // Mode invité : rien ne doit toucher le disque du profil, et rien de la
    // session invitée ne doit être retenu.
    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (isGuest) _relocations.Clear();
    }

    public IReadOnlyList<SiteRelocation> All => _relocations;

    // Apprend une redirection permanente observée sur le document principal.
    // Ne retient que ce qui ressemble à un vrai déménagement de site :
    //  - domaines racine différents (http→https ou www→apex du même site sont
    //    des normalisations, pas des déménagements) ;
    //  - page d'accueil vers page d'accueil, ou chemin conservé — un
    //    raccourcisseur d'URL (bit.ly/abc → article profond) est exclu.
    // Retourne l'entrée enregistrée, null si la redirection n'est pas retenue.
    public SiteRelocation? RecordPermanentRedirect(string? fromUrl, string? toUrl, DateTimeOffset now)
    {
        if (!TryParseWebUrl(fromUrl, out var from) || !TryParseWebUrl(toUrl, out var to))
            return null;

        var fromRoot = RootOf(from.Host);
        var toRoot = RootOf(to.Host);
        if (fromRoot.Length == 0 || toRoot.Length == 0 ||
            fromRoot.Equals(toRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fromPath = from.AbsolutePath;
        var isHomePage = fromPath is "" or "/";
        var samePath = fromPath.Equals(to.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        if (!isHomePage && !samePath)
            return null;

        var entry = new SiteRelocation(fromRoot, to.GetLeftPart(UriPartial.Authority), now);
        _relocations.RemoveAll(r => r.FromRootDomain.Equals(fromRoot, StringComparison.OrdinalIgnoreCase));

        // Suivi de chaîne : si A→B était connu et que B déménage vers C, les
        // anciennes entrées pointant vers B sont re-pointées vers C directement.
        for (var i = 0; i < _relocations.Count; i++)
        {
            if (RootOf(HostOfOrigin(_relocations[i].ToOrigin)).Equals(fromRoot, StringComparison.OrdinalIgnoreCase))
            {
                _relocations[i] = _relocations[i] with { ToOrigin = entry.ToOrigin, RecordedAt = now };
            }
        }

        _relocations.Add(entry);

        // Retour au bercail (C→A alors que A→C était connu) : le re-pointage
        // ci-dessus peut créer des entrées « A déménage vers A » — on les purge.
        _relocations.RemoveAll(r =>
            r.FromRootDomain.Equals(RootOf(HostOfOrigin(r.ToOrigin)), StringComparison.OrdinalIgnoreCase));

        if (_relocations.Count > MaxEntries)
        {
            var excess = _relocations.OrderBy(r => r.RecordedAt).Take(_relocations.Count - MaxEntries).ToList();
            foreach (var old in excess) _relocations.Remove(old);
        }

        Persist();
        return Find(fromRoot);
    }

    // Nouvelle adresse pour une URL en échec : suit la chaîne de déménagements
    // (A→B→C ⇒ C) et applique le chemin de l'URL en échec à la nouvelle origine.
    // Null si le domaine en échec n'est pas connu comme déménagé.
    public string? TargetFor(string? failedUrl)
    {
        if (!TryParseWebUrl(failedUrl, out var failed))
            return null;

        var current = RootOf(failed.Host);
        string? origin = null;
        for (var hop = 0; hop < MaxChainHops; hop++)
        {
            var entry = Find(current);
            if (entry is null) break;
            origin = entry.ToOrigin;
            current = RootOf(HostOfOrigin(origin));
        }

        return origin is null ? null : RewriteToOrigin(failedUrl, origin);
    }

    public SiteRelocation? Find(string rootDomain) =>
        _relocations.FirstOrDefault(r =>
            r.FromRootDomain.Equals(rootDomain, StringComparison.OrdinalIgnoreCase));

    // La proposition de mise à jour des favoris ne doit être faite qu'une fois
    // par déménagement, pas à chaque fois que la redirection est re-observée.
    public void MarkUpdateOffered(string rootDomain)
    {
        var index = _relocations.FindIndex(r =>
            r.FromRootDomain.Equals(rootDomain, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || _relocations[index].UpdateOffered) return;

        _relocations[index] = _relocations[index] with { UpdateOffered = true };
        Persist();
    }

    // Reporte une URL sur une nouvelle origine en conservant chemin et requête :
    // https://old.win/film/x?y=1 + https://new.poker → https://new.poker/film/x?y=1
    public static string? RewriteToOrigin(string? url, string origin)
    {
        if (!TryParseWebUrl(url, out var parsed) ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            return null;
        }

        return new Uri(originUri, parsed.PathAndQuery).ToString();
    }

    public static string RootOf(string host) =>
        PublicSuffixService.RootDomainOf("https://" + host);

    private static string HostOfOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;

    private static bool TryParseWebUrl(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            parsed.Host.Length == 0 ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private void Load()
    {
        try
        {
            var json = _read(_path);
            if (string.IsNullOrWhiteSpace(json)) return;
            var loaded = JsonSerializer.Deserialize<List<SiteRelocation>>(json, JsonOpts);
            if (loaded is not null) _relocations.AddRange(loaded);
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
            _write(_path, JsonSerializer.Serialize(_relocations, JsonOpts));
        }
        catch
        {
            // L'échec d'écriture ne doit pas casser l'expérience navigateur.
        }
    }
}

// Calcule, hors UI, ce qu'un déménagement change dans les favoris et les
// raccourcis de la page d'accueil : l'appelant applique ensuite le plan.
public static class SiteRelocationUpdatePlanner
{
    public sealed record Plan(
        IReadOnlyDictionary<string, string> BookmarkUrlsById,
        IReadOnlyList<(string Title, string Url)> Shortcuts,
        int ShortcutChanges)
    {
        public int TotalChanges => BookmarkUrlsById.Count + ShortcutChanges;
    }

    public static Plan Compute(
        SiteRelocation relocation,
        IEnumerable<(string Id, string Url)> bookmarks,
        IEnumerable<(string Title, string Url)> shortcuts)
    {
        var bookmarkUpdates = new Dictionary<string, string>();
        foreach (var (id, url) in bookmarks)
        {
            var rewritten = RewriteIfMoved(url, relocation);
            if (rewritten is not null) bookmarkUpdates[id] = rewritten;
        }

        var newShortcuts = new List<(string Title, string Url)>();
        var shortcutChanges = 0;
        foreach (var (title, url) in shortcuts)
        {
            var rewritten = RewriteIfMoved(url, relocation);
            if (rewritten is not null) shortcutChanges++;
            newShortcuts.Add((title, rewritten ?? url));
        }

        return new Plan(bookmarkUpdates, newShortcuts, shortcutChanges);
    }

    private static string? RewriteIfMoved(string? url, SiteRelocation relocation)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return SiteRelocationStore.RootOf(parsed.Host)
                .Equals(relocation.FromRootDomain, StringComparison.OrdinalIgnoreCase)
            ? SiteRelocationStore.RewriteToOrigin(url, relocation.ToOrigin)
            : null;
    }
}
