namespace Lumora.WinUI;

// ── Politique anti-parasite des navigations d'onglet ─────────────────────────
// Classe PURE (aucune dépendance UI), compilée aussi dans Lumora.Tests.
// Décide du sort d'une navigation du document principal (l'onglet part de
// fromUri vers toUri). Cible : les sites qui détournent les premiers clics —
// chaque clic téléporte l'onglet (ou un onglet neuf) vers un site douteux,
// et l'utilisateur doit revenir et recliquer plusieurs fois pour naviguer.
// Ces parasites sont éliminés SILENCIEUSEMENT : pas d'onglet, pas de barre,
// pas de question — l'utilisateur reste sur sa page (0.78.3.1).
//
// Il n'y a AUCUN comptage de clics : chaque tentative parasite est bloquée,
// une par une, aussi longtemps que le site essaie. Le signal « site agressif »
// vient du bouclier réseau (pageUnderAdPressure : des requêtes publicitaires
// ont déjà été bloquées sur la page) ou d'une popup toute fraîche (tab-under).
//
// Garde-fous, dans l'ordre :
//  - une adresse demandée explicitement (barre d'adresse, favori, suggestion)
//    n'est JAMAIS bloquée : l'utilisateur sait où il va ;
//  - un site whitelisté (origine ou destination) garde tout ;
//  - les destinations d'authentification restent libres (retours OAuth) ;
//  - un domaine répertorié publicitaire est toujours bloqué (même hors
//    pression : le clic capturé vers une régie connue n'a pas d'excuse) ;
//  - rester sur le même site racine est toujours permis (les sites normaux
//    naviguent chez eux) ;
//  - au-delà : cross-domaine + site sous pression publicitaire (ou popup
//    ouverte à l'instant) = parasite → bloqué.
public enum NavigationVerdict
{
    Allow,
    BlockAdDomain,
    BlockParasite
}

public static class NavigationHijackPolicy
{
    public static NavigationVerdict Decide(
        string? fromUri,
        string toUri,
        bool wasExplicitlyRequested,
        bool strictBlockEnabled,
        Func<string, bool> isAdHost,
        Func<string, bool> isWhitelistedHost,
        bool pageUnderAdPressure = false,
        bool openedPopupRecently = false)
    {
        if (wasExplicitlyRequested)
            return NavigationVerdict.Allow;

        if (!strictBlockEnabled || !IsWebUrl(toUri))
            return NavigationVerdict.Allow;

        var toHost = HostOf(toUri);
        var fromHost = HostOf(fromUri);
        if ((toHost.Length > 0 && isWhitelistedHost(toHost)) ||
            (fromHost.Length > 0 && isWhitelistedHost(fromHost)))
        {
            return NavigationVerdict.Allow;
        }

        if (PopupPolicy.IsKnownIdentityProviderHost(toUri) ||
            PopupPolicy.IsLikelyAuthenticationPopup(toUri, isUserInitiated: true))
        {
            return NavigationVerdict.Allow;
        }

        // Domaine entier répertorié publicitaire uniquement : les règles de
        // sous-chaîne (chemins « /ads/ »...) produiraient des faux positifs.
        if (toHost.Length > 0 && isAdHost(toHost))
            return NavigationVerdict.BlockAdDomain;

        // Une première navigation (onglet neuf, pas d'origine web) ou une
        // navigation qui reste sur le même site racine n'est jamais un
        // détournement.
        if (fromHost.Length == 0 || IsSameRootSite(fromHost, toHost))
            return NavigationVerdict.Allow;

        return pageUnderAdPressure || openedPopupRecently
            ? NavigationVerdict.BlockParasite
            : NavigationVerdict.Allow;
    }

    private static bool IsSameRootSite(string fromHost, string toHost) =>
        toHost.Length > 0 &&
        SiteRelocationStore.RootOf(fromHost).Equals(SiteRelocationStore.RootOf(toHost), StringComparison.OrdinalIgnoreCase);

    // Même règle que BookmarkStore.IsWebUrl, dupliquée pour rester compilable
    // seule dans Lumora.Tests.
    private static bool IsWebUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static string HostOf(string? uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.Host : string.Empty;
}
