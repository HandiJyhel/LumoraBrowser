namespace Lumora.WinUI;

// ── Politique d'ouverture des popups ─────────────────────────────────────────
// Classe PURE (aucune dépendance UI), compilée aussi dans Lumora.Tests.
// Décide si un window.open / target=_blank mérite un onglet Lumora :
//  - une popup NON déclenchée par un geste utilisateur est un popunder
//    publicitaire dans l'immense majorité des cas → bloquée ;
//  - une popup déclenchée par un clic mais visant un domaine répertorié
//    publicitaire est un clic détourné → bloquée ;
//  - une DEUXIÈME popup issue du même geste utilisateur est une rafale
//    publicitaire (aucun site légitime n'ouvre deux fenêtres par clic) → bloquée ;
//  - les fenêtres d'authentification restent ouvrables : les fournisseurs
//    d'identité connus et les hôtes conventionnels (login./auth./sso.…) passent
//    toujours ; les URL qui ne font que RESSEMBLER à un login (mot-clé dans le
//    chemin) n'ouvrent que sur geste utilisateur — durcissement 0.78.2 : une
//    régie servait « pub.example/login/... » pour traverser le bouclier ;
//  - sur un site déjà pris en flagrant délit publicitaire (requêtes bloquées en
//    nombre sur la page), toute popup vers un domaine TIERS est bloquée net
//    (0.78.4, durci sur demande : plus d'onglet du tout, barre « Continuer
//    quand même » en recours) ; seules les popups vers le même site racine
//    restent ouvrables ;
//  - un site whitelisté par l'utilisateur garde toutes ses popups ;
//  - au-delà (0.84.0.6) : un vrai clic vers un domaine cross-site inconnu, sans
//    aucun des motifs de confiance ci-dessus, n'est techniquement PAS
//    distinguable d'un détournement — aucun signal ne permet de deviner juste
//    à tous les coups. Au lieu de laisser passer par défaut (l'ancien
//    comportement, contourné par les sites qui rouvrent un onglet à chaque
//    clic), la popup est retenue en attente : l'utilisateur tranche via une
//    action de récupération explicite (icône dédiée), comme le font Chrome,
//    Firefox et Edge nativement — pas une supposition automatique.
public enum PopupVerdict
{
    Allow,
    BlockAutomatic,
    BlockAdDomain,
    BlockGestureFlood,
    BlockUnderAdPressure,
    BlockPendingUserChoice
}

public static class PopupPolicy
{
    public static PopupVerdict Decide(
        string? popupUri,
        string? openerUri,
        bool isUserInitiated,
        bool blockerEnabled,
        Func<string, bool> isAdHost,
        Func<string, bool> isWhitelistedHost,
        int popupsAlreadyOpenedForGesture = 0,
        bool openerUnderAdPressure = false)
    {
        if (!blockerEnabled)
            return PopupVerdict.Allow;

        var openerHost = HostOf(openerUri);
        if (openerHost.Length > 0 && isWhitelistedHost(openerHost))
            return PopupVerdict.Allow;

        // Fournisseurs d'identité connus : toujours ouvrables, même hors geste
        // (certains flux OAuth ouvrent leur fenêtre après un aller-retour réseau).
        if (IsKnownIdentityProviderHost(popupUri))
            return PopupVerdict.Allow;

        // Le domaine répertorié publicitaire est éliminatoire AVANT l'heuristique
        // de login par mot-clé : « pub.example/login/redirect » ne passe plus.
        var popupHost = HostOf(popupUri);
        if (popupHost.Length > 0 && isAdHost(popupHost))
            return PopupVerdict.BlockAdDomain;

        if (IsLikelyAuthenticationPopup(popupUri, isUserInitiated))
            return PopupVerdict.Allow;

        if (!isUserInitiated)
            return PopupVerdict.BlockAutomatic;

        if (popupsAlreadyOpenedForGesture >= 1)
            return PopupVerdict.BlockGestureFlood;

        // Le site garde le droit d'ouvrir SES propres pages (lecteur vidéo,
        // page de détail...) sans jamais passer par l'attente de choix
        // ci-dessous : seul le cross-domaine est ambigu.
        if (IsSameRootSite(popupHost, openerHost))
            return PopupVerdict.Allow;

        // Site sous pression publicitaire : le clic est très probablement
        // détourné. Bloqué net — l'utilisateur garde la barre « Continuer
        // quand même » en recours.
        if (openerUnderAdPressure)
            return PopupVerdict.BlockUnderAdPressure;

        // Ni domaine répertorié, ni site sous pression : un vrai clic vers un
        // domaine cross-site totalement inconnu. Indécidable techniquement
        // entre popup légitime (partage, paiement) et détournement — retenu
        // en attente d'un choix explicite plutôt qu'autorisé par défaut
        // (0.84.0.6, cf. commentaire d'en-tête).
        return PopupVerdict.BlockPendingUserChoice;
    }

    private static bool IsSameRootSite(string popupHost, string openerHost) =>
        popupHost.Length > 0 && openerHost.Length > 0 &&
        SiteRelocationStore.RootOf(popupHost).Equals(SiteRelocationStore.RootOf(openerHost), StringComparison.OrdinalIgnoreCase);

    // Hôtes des grands fournisseurs d'identité (match exact ou sous-domaine).
    // Les régies publicitaires ne servent pas d'OAuth depuis ces domaines : le
    // risque de faux négatif est nul, et la liste évite de dépendre de mots-clés.
    private static readonly string[] KnownIdentityProviderHosts =
    [
        "accounts.google.com",
        "accounts.youtube.com",
        "login.microsoftonline.com",
        "login.live.com",
        "login.microsoft.com",
        "appleid.apple.com",
        "account.apple.com",
        "id.atlassian.com",
        "login.yahoo.com",
        "auth.openai.com"
    ];

    // Préfixes d'hôte conventionnels des SSO d'entreprise : convention forte,
    // jamais observée chez les régies publicitaires → toujours ouvrables.
    private static readonly string[] AuthenticationHostPrefixes =
    [
        "login.", "auth.", "sso.", "signin.", "id.", "idp.", "accounts.", "account.", "federation."
    ];

    public static bool IsKnownIdentityProviderHost(string? uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Host.Length == 0)
            return false;

        foreach (var known in KnownIdentityProviderHosts)
        {
            if (parsed.Host.Equals(known, StringComparison.OrdinalIgnoreCase) ||
                parsed.Host.EndsWith("." + known, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Fenêtres de connexion / fédération d'identité. Les conventions d'hôte
    // (login., sso.…) passent toujours ; les simples mots-clés de chemin
    // (oauth, /login, /signin) n'ouvrent que sur geste utilisateur, car une URL
    // publicitaire se fabrique trivialement avec « /login » dans le chemin.
    public static bool IsLikelyAuthenticationPopup(string? uri, bool isUserInitiated)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Host.Length == 0)
            return false;

        var host = parsed.Host;
        foreach (var prefix in AuthenticationHostPrefixes)
        {
            if (host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!isUserInitiated)
            return false;

        var path = parsed.AbsolutePath;
        return path.Contains("oauth", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/signin", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/login", StringComparison.OrdinalIgnoreCase);
    }

    private static string HostOf(string? uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.Host : string.Empty;
}
