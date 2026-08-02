namespace Lumora.WinUI.Sessions;

// Domaines racines qui partagent la meme session d'authentification qu'un
// autre domaine racine, bien que WebView2/Chromium les traite comme des
// domaines de cookies distincts pour la purge au demarrage. Sans ca, faire
// confiance a google.com ne suffit pas a garder YouTube connecte : ses
// cookies de session vivent sous youtube.com, purges au demarrage suivant
// meme si le compte Google, lui, reste connecte (cas reel diagnostique le
// 2026-08-02 - google.com trouve seul dans TrustedSessionSites, youtube.com
// deconnecte a chaque relance).
internal static class SessionDomainFamilies
{
    private static readonly IReadOnlyDictionary<string, string[]> Families =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["google.com"] = new[] { "youtube.com" },
            ["youtube.com"] = new[] { "google.com" },
        };

    public static IEnumerable<string> SiblingsOf(string rootDomain) =>
        Families.TryGetValue(rootDomain, out var siblings) ? siblings : Array.Empty<string>();
}
