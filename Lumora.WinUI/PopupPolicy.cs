namespace Lumora.WinUI;

// ── Politique d'ouverture des popups ─────────────────────────────────────────
// Classe PURE (aucune dépendance UI), compilée aussi dans Lumora.Tests.
// Décide si un window.open / target=_blank mérite un onglet Lumora :
//  - une popup NON déclenchée par un geste utilisateur est un popunder
//    publicitaire dans l'immense majorité des cas → bloquée ;
//  - une popup déclenchée par un clic mais visant un domaine répertorié
//    publicitaire est un clic détourné → bloquée ;
//  - les fenêtres d'authentification (OAuth, connexion) restent toujours
//    ouvrables, même hors geste : certains flux de connexion ouvrent leur
//    fenêtre après un aller-retour réseau, hors du handler de clic ;
//  - un site whitelisté par l'utilisateur garde toutes ses popups.
public enum PopupVerdict
{
    Allow,
    BlockAutomatic,
    BlockAdDomain
}

public static class PopupPolicy
{
    public static PopupVerdict Decide(
        string? popupUri,
        string? openerUri,
        bool isUserInitiated,
        bool blockerEnabled,
        Func<string, bool> isAdHost,
        Func<string, bool> isWhitelistedHost)
    {
        if (!blockerEnabled)
            return PopupVerdict.Allow;

        var openerHost = HostOf(openerUri);
        if (openerHost.Length > 0 && isWhitelistedHost(openerHost))
            return PopupVerdict.Allow;

        if (IsLikelyAuthenticationPopup(popupUri))
            return PopupVerdict.Allow;

        var popupHost = HostOf(popupUri);
        if (popupHost.Length > 0 && isAdHost(popupHost))
            return PopupVerdict.BlockAdDomain;

        return isUserInitiated ? PopupVerdict.Allow : PopupVerdict.BlockAutomatic;
    }

    // Fenêtres de connexion / fédération d'identité : jamais bloquées. Les
    // domaines publicitaires ne servent pas d'OAuth ; le risque de faux positif
    // est nul comparé au coût d'une connexion cassée.
    public static bool IsLikelyAuthenticationPopup(string? uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Host.Length == 0)
            return false;

        var host = parsed.Host;
        var path = parsed.AbsolutePath;
        return host.Contains("accounts.google", StringComparison.OrdinalIgnoreCase) ||
               host.StartsWith("login.", StringComparison.OrdinalIgnoreCase) ||
               host.StartsWith("auth.", StringComparison.OrdinalIgnoreCase) ||
               host.StartsWith("sso.", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("oauth", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/signin", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/login", StringComparison.OrdinalIgnoreCase);
    }

    private static string HostOf(string? uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.Host : string.Empty;
}
