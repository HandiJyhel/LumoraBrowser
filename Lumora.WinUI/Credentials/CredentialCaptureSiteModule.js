// ── Module Site/Page ────────────────────────────────────────────────────
//
// La 3e information (avec identifiant et mot de passe) : sur quel site et
// quelle page se passe la connexion. Sans ambiguite possible (c'est juste
// l'adresse de la page), donc ce module n'a besoin de rien detecter — sa
// seule responsabilite est la liste noire des pages ou la capture doit
// rester totalement silencieuse (ecrans techniques Google GSI/OAuth, pas de
// vrai formulaire de connexion du site visite).
(function () {
    window.__novaCredSite = window.__novaCredSite || {};
    var Site = window.__novaCredSite;

    Site.isAutomationSuppressed = function () {
        try {
            var host = location.hostname || "";
            var path = location.pathname || "";
            return host === "accounts.google.com" &&
                (path.indexOf("/gsi/") === 0 ||
                 path.indexOf("/o/oauth2/") === 0 ||
                 path.indexOf("/signin/oauth") === 0);
        } catch (_) {
            return false;
        }
    };
})();
