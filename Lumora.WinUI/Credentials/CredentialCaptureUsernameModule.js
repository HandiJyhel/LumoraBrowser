// ── Module identifiant ──────────────────────────────────────────────────
//
// Le seul des 3 elements (site / identifiant / mot de passe) qui reste
// intrinsequement flou : contrairement au mot de passe (marque sans
// ambiguite par type="password") ou au site (juste une adresse), le HTML
// n'a aucun marqueur universel obligatoire pour dire "ce champ est
// l'identifiant". Ce module devine via des indices (nom du champ, texte
// autour, attribut autocomplete...). Sa seule regle stricte : il ne peut
// JAMAIS considerer un champ deja vu comme mot de passe, quel que soit son
// type actuel — verifie via CredentialPasswordModule.wasEverPassword(), la
// seule fonction exposee par ce module-la. Aucun autre acces a son etat.
(function () {
    window.__novaCredUsername = window.__novaCredUsername || {};
    var Username = window.__novaCredUsername;
    var Dom = window.__novaCredDom;
    var Password = window.__novaCredPassword;

    var lastUser = "";

    Username.loadRememberedUser = function () {
        try {
            lastUser = sessionStorage.getItem("__nova_last_user") || "";
        } catch (_) { lastUser = ""; }
    };

    Username.getLastUser = function () { return lastUser; };

    Username.rememberUsername = function (value, post) {
        if (!value) return;
        lastUser = value;
        try { sessionStorage.setItem("__nova_last_user", value); } catch (_) { }
        post({
            t: "nova.credential.username",
            username: value,
            origin: location.origin,
            loginUrl: location.href
        });
    };

    Username.isUsernameCandidate = function (el) {
        if (!el || el.tagName !== "INPUT") return false;
        // La frontiere : jamais un champ deja vu comme mot de passe, meme si
        // son type actuel dit autre chose (oeil "afficher le mot de passe").
        if (Password.wasEverPassword(el)) return false;
        var type = (el.type || "text").toLowerCase();
        if (["password", "hidden", "checkbox", "radio", "file", "submit", "button", "reset"].indexOf(type) >= 0) return false;
        if (["email", "text", "tel", "search", "url"].indexOf(type) < 0 && type !== "") return false;
        return Dom.topmost(el);
    };

    Username.scoreUsername = function (el) {
        var text = Dom.fieldText(el);
        var context = Dom.contextText(el);
        var all = text + " " + context;
        var type = (el.type || "").toLowerCase();

        // Score "propre" au champ : ce que le champ dit de lui-meme (type,
        // valeur, name/id/placeholder/aria-label). Independant de tout ce
        // qui l'entoure dans la page.
        var ownScore = 0;
        if (el.value) ownScore += 20;
        if (type === "email") ownScore += 30;
        if (/\b(username|user|login|email|mail|account|identifier|identifiant)\b/.test(text)) ownScore += 35;

        var score = ownScore;

        // Le contexte (texte des ancetres, jusqu'a 5 niveaux - voir
        // Dom.contextText) ne peut RENFORCER un champ que s'il a deja un
        // signal propre positif - jamais en fabriquer un a partir de rien.
        // Sans cette regle, un simple bouton "Compte Google" ou un lien
        // "Connexion" affiche ailleurs dans la meme barre d'outils (ex.
        // Gmail deja connecte) suffit a faire remonter un champ totalement
        // anodin (barre de recherche...) comme "champ identifiant" - bug
        // reel constate le 2026-08-13 : la barre "Remplir l'identifiant ?"
        // s'affichait sur l'INBOX Gmail, sans aucun formulaire de connexion
        // visible, le champ recherche captant le mot "compte" du bouton de
        // profil voisin.
        if (ownScore > 0 && /\b(connexion|connecter|connectez|compte|auth|login|identifier|identifiez|continuer|mot de passe|password)\b/.test(context)) {
            score += 70;
        }

        if (/\b(newsletter|inscrivez|actualit|offres|commercial|marketing|désabonnement|desabonnement|footer|presse|recrutement|paypal|visa|mastercard)\b/.test(all)) score -= 160;
        if (/\b(current-password|new-password|otp|code|search|recherche|coupon|promo|quantity|qty|quantite|quantité)\b/.test(all)) score -= 80;

        // Signal le plus fiable qui existe : un site qui pose explicitement
        // autocomplete="username" (attribut standard W3C, pense pour
        // l'accessibilite et les gestionnaires de mots de passe) ne se trompe
        // quasiment jamais. On le laisse ecraser tous les autres indices
        // plutot que de le ponderer comme un simple indice parmi d'autres.
        if ((el.autocomplete || "").toLowerCase() === "username") {
            score = Math.max(score + 45, 400);
        }
        return score;
    };

    Username.usernameFields = function (scope) {
        var roots = [];
        if (scope) roots.push(scope);
        roots.push(document);
        for (var m = 0; m < roots.length; m++) Password.markPasswordFields(roots[m]);
        var out = [];
        var seen = new Set();

        for (var r = 0; r < roots.length; r++) {
            var fields = roots[r].querySelectorAll("input");
            for (var i = 0; i < fields.length; i++) {
                var el = fields[i];
                if (seen.has(el) || !Username.isUsernameCandidate(el)) continue;
                if (Username.scoreUsername(el) <= -20) continue;
                seen.add(el);
                out.push(el);
            }
        }

        return out;
    };

    // Seuil releve, utilise UNIQUEMENT pour la suggestion proactive "Remplir
    // l'identifiant ?" affichee sans aucune action de l'utilisateur (voir
    // CredentialCaptureOrchestrator.publishState -> hasUsernameField).
    // usernameFields() reste volontairement permissif (score > -20, "rien
    // d'excluant") car il sert aussi a la capture/au remplissage a la
    // demande, ou rater un champ au signal faible est pire qu'un faux
    // candidat de plus dans une liste triee. Mais pour DECLENCHER une barre
    // sur une page ou l'utilisateur n'a rien demande, "rien d'excluant" ne
    // suffit pas : il faut un vrai signal (email/texte propre au champ,
    // autocomplete=username, ou contexte confirme par un signal propre -
    // voir la regle ownScore>0 dans scoreUsername). Sans ce filtre plus
    // strict, n'importe quel champ neutre (score 0, ex. barre de recherche)
    // suffisait a afficher la barre des qu'un identifiant existait en coffre
    // pour le domaine - constate le 2026-08-13 sur l'inbox Gmail.
    Username.confidentUsernameFields = function (scope) {
        return Username.usernameFields(scope).filter(function (el) {
            return Username.scoreUsername(el) >= 30;
        });
    };

    Username.bestUsername = function (scope) {
        var fields = Username.usernameFields(scope);
        var best = null;
        var bestScore = 0;

        for (var i = 0; i < fields.length; i++) {
            var el = fields[i];
            if (!el.value) continue;
            var score = Username.scoreUsername(el);
            if (score > bestScore) {
                best = el;
                bestScore = score;
            }
        }

        return best ? best.value : lastUser;
    };

    // ── Remplissage a la demande (voir CredentialCaptureDom.js pour la note
    // sur les regles assouplies specifiques au remplissage) ─────────────────

    Username.isUsernameCandidateForFill = function (el) {
        if (!el || el.tagName !== "INPUT") return false;
        if (Password.wasEverPassword(el)) return false;
        var type = (el.type || "text").toLowerCase();
        if (["password", "hidden", "checkbox", "radio", "file", "submit", "button", "reset"].indexOf(type) >= 0) return false;
        if (["email", "text", "tel", "search", "url"].indexOf(type) < 0 && type !== "") return false;
        return Dom.fillable(el);
    };

    Username.pickUsernameFieldForFill = function (passwordField) {
        // Balayage de securite : queryAllDeep couvre aussi les frames/shadow
        // roots que markPasswordFields(document) seul ne voit pas.
        Password.markElements(Dom.queryAllDeep("input[type='password']"));
        var candidates = Dom.queryAllDeep("input").filter(Username.isUsernameCandidateForFill);
        var best = null;
        var bestScore = -999;
        for (var i = 0; i < candidates.length; i++) {
            var el = candidates[i];
            var score = Username.scoreUsername(el);
            if (passwordField) {
                if (el.form && passwordField.form && el.form === passwordField.form) score += 35;
                if (el.ownerDocument === passwordField.ownerDocument) score += 10;
                try {
                    if (el.compareDocumentPosition(passwordField) & Node.DOCUMENT_POSITION_FOLLOWING) score += 10;
                } catch (_) { }
            }
            if (el.ownerDocument && el.ownerDocument.activeElement === el) score += 15;
            if (score > bestScore) {
                best = el;
                bestScore = score;
            }
        }
        return bestScore > -20 ? best : null;
    };
})();
