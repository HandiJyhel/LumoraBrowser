// ── Orchestrateur ────────────────────────────────────────────────────────
//
// Cablage des evenements de page (frappe, soumission, clic...) et assemblage
// des 3 informations a envoyer a l'app hote : site (CredentialCaptureSite
// Module.js), identifiant (CredentialCaptureUsernameModule.js), mot de passe
// (CredentialCapturePasswordModule.js). Ce fichier ne contient AUCUNE
// heuristique de detection — seulement de la tuyauterie entre les modules.
(function () {
    if (window.__novaCredentialCaptureV3) return;
    window.__novaCredentialCaptureV3 = true;

    var Site = window.__novaCredSite;
    var Password = window.__novaCredPassword;
    var Username = window.__novaCredUsername;

    if (Site.isAutomationSuppressed()) return;

    var sentCredentialKey = "";
    var sentStateKey = "";
    var stateTimer = 0;

    function post(payload) {
        try {
            window.chrome.webview.postMessage(payload);
        } catch (_) { }
    }

    function publishState(source) {
        try {
            Username.loadRememberedUser();
            // confidentUsernameFields (pas usernameFields) : cet etat pilote
            // l'affichage PROACTIF de la barre "Remplir l'identifiant ?",
            // sans action de l'utilisateur - il exige donc un vrai signal,
            // pas seulement "rien d'excluant" (voir le commentaire de
            // confidentUsernameFields dans CredentialCaptureUsernameModule.js).
            var users = Username.confidentUsernameFields(document);
            var passwords = Password.currentPasswordFields(document, false);
            var newPasswords = Password.newPasswordFields(document);
            var username = Username.bestUsername(document) || Username.getLastUser() || "";
            var key = [
                location.origin,
                location.href,
                users.length ? "u1" : "u0",
                passwords.length ? "p1" : "p0",
                newPasswords.length ? "n1" : "n0",
                username,
                source || ""
            ].join("|");

            if (key === sentStateKey) return;
            sentStateKey = key;

            post({
                t: "nova.credential.page-state",
                origin: location.origin,
                loginUrl: location.href,
                hasUsernameField: users.length > 0,
                hasPasswordField: passwords.length > 0,
                hasEmptyNewPasswordField: newPasswords.length > 0,
                username: username,
                source: source || "scan",
                confidence: passwords.length ? 90 : (users.length ? 65 : 20)
            });
        } catch (_) { }
    }

    function scheduleState(source) {
        if (stateTimer) clearTimeout(stateTimer);
        stateTimer = setTimeout(function () {
            stateTimer = 0;
            publishState(source);
        }, 80);
    }

    function capture(scope, source) {
        try {
            Username.loadRememberedUser();
            var passwords = Password.currentPasswordFields(scope, true);
            if (!passwords.length && Password.getLastPassword()) {
                passwords = [{ value: Password.getLastPassword(), form: null }];
            }
            if (!passwords.length) {
                scheduleState(source || "capture-no-password");
                return;
            }

            var password = passwords[0].value || "";
            if (password.length < 3) return;

            var username = Username.bestUsername(scope || passwords[0].form) || Username.getLastUser() || "";
            var key = location.origin + "|" + username + "|" + password;
            if (key === sentCredentialKey) return;
            sentCredentialKey = key;

            post({
                t: "nova.credential.candidate",
                username: username,
                password: password,
                origin: location.origin,
                loginUrl: location.href,
                source: source || "unknown",
                confidence: username ? 90 : 55
            });
        } catch (_) { }
    }

    function nearestScope(target) {
        if (!target || !target.closest) return document;
        return target.form || target.closest("form") || target.closest("[role='form']") || document;
    }

    document.addEventListener("input", function (event) {
        var target = event.target;
        if (!target || target.tagName !== "INPUT") return;
        var type = (target.type || "text").toLowerCase();
        if (type === "password") {
            Password.notePasswordInput(target);
            publishState("password-input");
            return;
        }
        if (Username.isUsernameCandidate(target) && target.value && Username.scoreUsername(target) > -20) {
            Username.rememberUsername(target.value, post);
            publishState("username-input");
        }
    }, true);

    document.addEventListener("change", function (event) {
        var target = event.target;
        // Meme seuil que Username.usernameFields() : un champ code/OTP (ex.
        // verification Google par email) ne doit jamais ecraser l'identifiant
        // deja memorise.
        if (target && Username.isUsernameCandidate(target) && target.value && Username.scoreUsername(target) > -20) {
            Username.rememberUsername(target.value, post);
            publishState("username-change");
        }
    }, true);

    document.addEventListener("focusin", function () {
        scheduleState("focus");
    }, true);

    document.addEventListener("submit", function (event) {
        capture(event.target, "submit");
    }, true);

    document.addEventListener("click", function (event) {
        var target = event.target;
        if (!target || !target.closest) return;
        var button = target.closest("button,input[type='submit'],input[type='button'],a[role='button'],[role='button']");
        scheduleState("click");
        if (!button) return;
        capture(nearestScope(button), "click");
        setTimeout(function () {
            publishState("click-delay");
            capture(nearestScope(button), "click-delay");
        }, 120);
    }, true);

    document.addEventListener("keydown", function (event) {
        if (event.key === "Enter") {
            capture(nearestScope(event.target), "enter");
            setTimeout(function () { publishState("enter-delay"); }, 120);
        }
    }, true);

    window.addEventListener("pagehide", function () {
        capture(document, "pagehide");
    }, true);

    document.addEventListener("visibilitychange", function () {
        if (document.visibilityState === "hidden" && Date.now() - Password.getLastPasswordAt() < 30000) {
            capture(document, "visibility-hidden");
        }
    }, true);

    var observer = new MutationObserver(function () {
        Username.loadRememberedUser();
        scheduleState("mutation");
    });
    try {
        observer.observe(document.documentElement || document, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ["type", "style", "class", "hidden", "aria-hidden", "disabled"]
        });
    } catch (_) { }

    // ── Remplissage a la demande (declenche par l'app hote) ──────────────
    window.__novaFillCredential = function (payload) {
        try {
            payload = payload || {};
            var passwordField = payload.password ? Password.pickPasswordFieldForFill() : null;
            var usernameField = payload.username ? Username.pickUsernameFieldForFill(passwordField) : null;
            var report = {
                success: false,
                message: "",
                filledUsername: false,
                filledPassword: false,
                foundUsernameField: !!usernameField,
                foundPasswordField: !!passwordField
            };

            var Dom = window.__novaCredDom;
            if (usernameField) report.filledUsername = Dom.setValueForFill(usernameField, payload.username);
            if (passwordField) report.filledPassword = Dom.setValueForFill(passwordField, payload.password);

            report.success = report.filledUsername || report.filledPassword;
            if (report.filledUsername && report.filledPassword) report.message = "Identifiants remplis.";
            else if (report.filledPassword) report.message = "Mot de passe rempli.";
            else if (report.filledUsername) report.message = "Identifiant rempli. Mot de passe attendu.";
            else if (passwordField || usernameField) report.message = "Champ detecte, mais le site a refuse l'ecriture.";
            else report.message = "Aucun champ de connexion visible.";

            // Verification differee : certains frameworks (re-render
            // React/Vue...) reinitialisent la valeur juste apres. On re-tente
            // une fois, et on ne previent l'app que si le resultat final
            // contredit la reponse immediate.
            if (report.success) {
                var immediate = { u: report.filledUsername, p: report.filledPassword };
                setTimeout(function () {
                    try {
                        var uOk = !immediate.u || String(usernameField.value) === String(payload.username);
                        var pOk = !immediate.p || String(passwordField.value) === String(payload.password);
                        if (!uOk) uOk = Dom.setValueForFill(usernameField, payload.username);
                        if (!pOk) pOk = Dom.setValueForFill(passwordField, payload.password);
                        if (!uOk || !pOk) {
                            post({
                                t: "nova.credential.fill-report",
                                origin: location.origin,
                                success: false,
                                message: "Le site a efface la valeur remplie ; reessayez apres un clic dans le champ."
                            });
                        }
                    } catch (_) { }
                }, 300);
            }

            return report;
        } catch (error) {
            return {
                success: false,
                message: "Erreur autofill: " + error.message,
                filledUsername: false,
                filledPassword: false,
                foundUsernameField: false,
                foundPasswordField: false
            };
        }
    };

    Username.loadRememberedUser();
    scheduleState("document-created");
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () { publishState("dom-ready"); }, { once: true });
    } else {
        publishState("dom-ready");
    }
    window.addEventListener("load", function () { publishState("load"); }, { once: true });
})();
