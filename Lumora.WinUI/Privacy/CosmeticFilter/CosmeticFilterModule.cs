using System.Text;

namespace Lumora.Privacy.CosmeticFilter;

// Masque visuellement les emplacements publicitaires en injectant du CSS dans chaque page.
// Complète le NetworkBlocker : quand une requête est bloquée, la page laisse un espace vide.
// Le CosmeticFilter applique display:none sur les conteneurs pub connus, éliminant ces trous.
//
// Deux niveaux :
//   1. Seed intégrée — actif immédiatement, sans liste téléchargée
//   2. Règles génériques des listes (EasyList, uBlock…) — chargées depuis le cache disque
//
// Les règles spécifiques à un domaine (domain##selector) sont stockées séparément
// et injectées dynamiquement après chaque navigation (via ExecuteScriptAsync).
internal sealed class CosmeticFilterModule : IPrivacyModule
{
    public string Id => "cosmetic-filter";
    public string DisplayName => "Masquage visuel des emplacements pub";
    public bool IsEnabled { get; set; } = true;

    private static readonly string ListsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lumora", "privacy", "lists");

    private static readonly string[] ListFiles =
        ["easylist.txt", "easyprivacy.txt", "ublock-filters.txt", "adguard-base.txt"];

    // Sélecteurs génériques (toutes pages)
    private readonly HashSet<string> _genericSelectors =
        new(StringComparer.OrdinalIgnoreCase);

    // Sélecteurs par domaine — clé = domaine (ex: "youtube.com"), valeur = sélecteurs CSS
    private readonly Dictionary<string, HashSet<string>> _siteSelectors =
        new(StringComparer.OrdinalIgnoreCase);

    // Script générique mis en cache après chaque rechargement
    private string? _cachedGenericScript;

    public int GenericRuleCount  => _genericSelectors.Count;
    public int SiteRuleCount     => _siteSelectors.Values.Sum(s => s.Count);

    public CosmeticFilterModule()
    {
        // Seed active immédiatement
        foreach (var sel in CosmeticSeedSelectors.Generic)
            _genericSelectors.Add(sel);
    }

    // Chargement depuis les listes en cache (sans téléchargement — NetworkBlocker s'en charge)
    public async Task LoadAsync()
    {
        var lines = new List<string>(capacity: 200_000);
        foreach (var file in ListFiles)
        {
            var path = Path.Combine(ListsDir, file);
            if (File.Exists(path))
                try { lines.AddRange(await File.ReadAllLinesAsync(path)); } catch { }
        }

        foreach (var rule in CosmeticFilterParser.ParseLines(lines))
        {
            if (rule.IsException) continue; // exceptions non gérées en v1

            if (rule.Domain is null)
            {
                _genericSelectors.Add(rule.Selector);
            }
            else
            {
                if (!_siteSelectors.TryGetValue(rule.Domain, out var set))
                    _siteSelectors[rule.Domain] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(rule.Selector);
            }
        }

        _cachedGenericScript = null; // invalider le cache
    }

    // Script JS injecté via AddScriptToExecuteOnDocumentCreatedAsync (toutes pages, au démarrage du document)
    public string BuildGenericInjectionScript()
    {
        if (_cachedGenericScript is not null) return _cachedGenericScript;

        var css = BuildCss(_genericSelectors);
        _cachedGenericScript = BuildInjectionJs("__nova_cf_generic", css);
        return _cachedGenericScript;
    }

    // Script JS injecté via ExecuteScriptAsync après navigation (règles spécifiques au site)
    // Retourne null si aucune règle ne correspond à ce host.
    public string? BuildSiteInjectionScript(string pageUri)
    {
        var host = ExtractHost(pageUri);
        if (host is null) return null;

        var selectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectSiteSelectors(host, selectors);
        if (selectors.Count == 0) return null;

        var css = BuildCss(selectors);
        return BuildInjectionJs("__nova_cf_site", css);
    }

    // Script JS pour retirer le CSS injecté (quand le module est désactivé à la volée)
    public static string BuildRemovalScript() =>
        "['__nova_cf_generic','__nova_cf_site'].forEach(function(id){var e=document.getElementById(id);if(e)e.remove();});";

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void CollectSiteSelectors(string host, HashSet<string> target)
    {
        if (_siteSelectors.TryGetValue(host, out var exact))
            foreach (var s in exact) target.Add(s);

        // Remonter les niveaux : "a.b.c" → "b.c"
        var idx = host.IndexOf('.');
        while (idx >= 0 && idx < host.Length - 1)
        {
            var parent = host[(idx + 1)..];
            if (_siteSelectors.TryGetValue(parent, out var ps))
                foreach (var s in ps) target.Add(s);
            idx = host.IndexOf('.', idx + 1);
        }
    }

    private static string BuildCss(IEnumerable<string> selectors)
    {
        // On regroupe en blocs de 2 000 sélecteurs max pour éviter les limites Chromium
        var buf     = new StringBuilder();
        var batch   = new List<string>(2000);
        int total   = 0;

        foreach (var sel in selectors)
        {
            batch.Add(sel);
            if (batch.Count == 2000)
            {
                AppendBlock(buf, batch);
                batch.Clear();
            }
            total++;
        }
        if (batch.Count > 0) AppendBlock(buf, batch);
        return buf.ToString();
    }

    private static void AppendBlock(StringBuilder buf, List<string> batch)
    {
        buf.Append(string.Join(",", batch));
        buf.Append("{display:none!important}");
    }

    private static string BuildInjectionJs(string styleId, string css)
    {
        // CSS échappé pour insertion dans un string JS
        var escaped = css
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "")
            .Replace("\n", "");

        return $@"(function(){{
var e=document.getElementById('{styleId}');
if(!e){{e=document.createElement('style');e.id='{styleId}';(document.documentElement||document.head).appendChild(e);}}
e.textContent='{escaped}';
}})();";
    }

    private static string? ExtractHost(string uri)
    {
        try { return new Uri(uri).Host.ToLowerInvariant(); }
        catch { return null; }
    }
}
