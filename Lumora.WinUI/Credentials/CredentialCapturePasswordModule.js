// ── Module mot de passe ─────────────────────────────────────────────────
//
// Seul module autorise a savoir ce qu'est un champ mot de passe. C'est une
// frontiere delibérée, pas juste une organisation de fichiers : le module
// identifiant (CredentialCaptureUsernameModule.js) ne peut JAMAIS lire l'etat
// prive de ce module (le WeakSet ci-dessous) directement — seulement via
// wasEverPassword(), la seule question qu'il a le droit de poser. Impossible
// donc qu'une future modification du code d'identifiant se remette a piocher
// dans un champ mot de passe par erreur, comme c'est arrive une fois (voir
// commentaire de wasEverPassword ci-dessous).
(function () {
    window.__novaCredPassword = window.__novaCredPassword || {};
    var Password = window.__novaCredPassword;
    var Dom = window.__novaCredDom;

    var lastPassword = "";
    var lastPasswordAt = 0;

    // Champs deja vus avec type="password" : certains sites (dont la page de
    // connexion Google) proposent un oeil "Afficher le mot de passe" qui
    // bascule l'attribut type en "text" une fois le mot de passe saisi. Sans
    // memoire, ce meme champ redevient alors indiscernable d'un champ
    // identifiant au moment de la capture (submit/clic), et le mot de passe
    // se retrouve enregistre comme identifiant (bug reel signale par
    // l'utilisateur le 2026-08-13). On retient chaque champ mot de passe vu
    // au moins une fois pour l'exclure DEFINITIVEMENT de la detection
    // d'identifiant, meme apres bascule de son type.
    var passwordEverElements = new WeakSet();

    function markElements(elements) {
        for (var i = 0; i < elements.length; i++) passwordEverElements.add(elements[i]);
    }
    // Utilisee quand on a deja la liste d'elements en main (recherche profonde
    // via Dom.queryAllDeep, qui couvre aussi frames/shadow roots).
    Password.markElements = markElements;

    function markPasswordFields(root) {
        try {
            markElements((root || document).querySelectorAll("input[type='password']"));
        } catch (_) { }
    }
    Password.markPasswordFields = markPasswordFields;

    // La seule fenetre que le module identifiant a sur ce module : "ce champ
    // a-t-il deja ete un mot de passe ?". Rien d'autre n'est expose.
    Password.wasEverPassword = function (el) {
        return passwordEverElements.has(el);
    };

    // Appele par l'orchestrateur a chaque frappe dans un champ actuellement
    // type=password : marque le champ (avant toute bascule visuelle
    // eventuelle) et retient la valeur comme filet de secours pour capture().
    Password.notePasswordInput = function (el) {
        passwordEverElements.add(el);
        lastPassword = el.value || "";
        lastPasswordAt = Date.now();
    };

    Password.getLastPassword = function () { return lastPassword; };
    Password.getLastPasswordAt = function () { return lastPasswordAt; };

    // Champs mot de passe ACTUELLEMENT presents et au premier plan (utilise
    // pour la capture/l'etat de page — a distinguer de wasEverPassword, qui
    // porte sur tout l'historique du champ).
    Password.currentPasswordFields = function (scope, requireValue) {
        var roots = [];
        if (scope) roots.push(scope);
        roots.push(document);
        var out = [];
        var seen = new Set();

        for (var r = 0; r < roots.length; r++) {
            var fields = roots[r].querySelectorAll("input[type='password']");
            for (var i = 0; i < fields.length; i++) {
                var el = fields[i];
                if (seen.has(el) || !Dom.topmost(el)) continue;
                if (requireValue && !el.value) continue;
                seen.add(el);
                out.push(el);
            }
        }

        return out;
    };

    // Detection volontairement prudente d'un champ "nouveau mot de passe"
    // (ecran d'inscription ou de changement de mot de passe), pour proposer
    // un mot de passe genere — jamais sur un simple formulaire de connexion.
    // En cas de doute, ne pas proposer : mieux vaut rater une inscription
    // qu'gener une connexion normale.
    function newPasswordScore(el, siblingCount) {
        var text = Dom.fieldText(el);
        var context = Dom.contextText(el);
        var all = text + " " + context;

        if (/\b(current-password|current|ancien|actuel|old)\b/.test(all)) return -999;

        // Score "propre" au champ : uniquement des signaux portes par le
        // champ lui-meme (son propre autocomplete/nom/id/placeholder),
        // jamais par le texte ou les classes des ancetres. Beaucoup de sites
        // regroupent connexion ET inscription dans la meme modale, avec un
        // conteneur commun nomme "register"/"signup" (ex. Instant Gaming,
        // bug signale par l'utilisateur le 2026-08-13, un cran apres la
        // 0.93.32.0-dev) - un simple champ de connexion, sans aucun signal a
        // lui, ne doit jamais basculer en "nouveau mot de passe" juste parce
        // qu'un ancetre s'appelle ainsi.
        var ownScore = 0;
        if ((el.autocomplete || "").toLowerCase() === "new-password") ownScore += 100;
        if (siblingCount >= 2) ownScore += 60;
        if (/\b(new|confirm|register|signup|creer|inscri|nouveau)\b/.test(text)) ownScore += 40;

        if (ownScore <= 0) return ownScore;

        // Le contexte (texte/attributs des ancetres) ne peut que RENFORCER
        // un signal deja pose par le champ lui-meme, jamais en fabriquer un
        // a partir de rien - meme principe que scoreUsername dans
        // CredentialCaptureUsernameModule.js (correctif Gmail du meme jour).
        var contextBonus = /\b(creer un compte|creation de compte|inscription|sign up|s'inscrire|register|nouveau mot de passe|choisir un mot de passe)\b/.test(context) ? 70 : 0;
        return ownScore + contextBonus;
    }

    Password.newPasswordFields = function (scope) {
        var pwFields = Password.currentPasswordFields(scope, false).filter(function (el) { return !el.value; });
        if (!pwFields.length) return [];
        var siblingCount = pwFields.length;
        return pwFields.filter(function (el) { return newPasswordScore(el, siblingCount) > 50; });
    };

    Password.pickPasswordFieldForFill = function () {
        var fields = Dom.queryAllDeep("input[type='password']").filter(Dom.fillable);
        if (!fields.length) return null;
        // Preferer un champ pleinement interactif ; sinon repli sur les
        // champs opacity:0 / readonly (astuces anti-autofill courantes).
        var preferred = fields.filter(function (el) {
            var view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
            return Number(view.getComputedStyle(el).opacity) !== 0 && !el.readOnly;
        });
        var pool = preferred.length ? preferred : fields;
        return pool.find(function (el) { return !el.value; }) || pool[0];
    };

    window.__novaFillNewPassword = function (value) {
        try {
            var fields = Password.newPasswordFields(document);
            if (!fields.length) return { success: false, message: "Aucun champ nouveau mot de passe detecte." };
            var ok = fields.map(function (el) { return Dom.setValue(el, value); }).some(function (x) { return x; });
            return ok
                ? { success: true, message: "Mot de passe genere rempli." }
                : { success: false, message: "Champ detecte, mais le site a refuse l'ecriture." };
        } catch (error) {
            return { success: false, message: "Erreur: " + error.message };
        }
    };
})();
