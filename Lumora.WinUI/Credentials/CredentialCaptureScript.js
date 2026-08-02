(function () {
    if (window.__novaCredentialCaptureV3) return;
    window.__novaCredentialCaptureV3 = true;

    function credentialAutomationSuppressed() {
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
    }

    if (credentialAutomationSuppressed()) return;

    var lastUser = "";
    var lastPassword = "";
    var lastPasswordAt = 0;
    var sentCredentialKey = "";
    var sentStateKey = "";
    var stateTimer = 0;

    function visible(el) {
        if (!el || el.disabled || el.readOnly) return false;
        var style = window.getComputedStyle(el);
        if (!style || style.visibility === "hidden" || style.display === "none" || Number(style.opacity) === 0) return false;
        var rect = el.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
    }

    function topmost(el) {
        if (!visible(el)) return false;
        var rect = el.getBoundingClientRect();
        var x = Math.max(0, Math.min(window.innerWidth - 1, rect.left + rect.width / 2));
        var y = Math.max(0, Math.min(window.innerHeight - 1, rect.top + rect.height / 2));
        var hit = document.elementFromPoint(x, y);
        return !!hit && (hit === el || el.contains(hit) || hit.contains(el));
    }

    function fieldText(el) {
        return [
            el.name || "",
            el.id || "",
            el.className || "",
            el.autocomplete || "",
            el.placeholder || "",
            el.getAttribute("aria-label") || ""
        ].join(" ").toLowerCase();
    }

    function contextText(el) {
        var out = [];
        var node = el;
        var depth = 0;
        while (node && node !== document.body && depth < 5) {
            out.push(node.id || "");
            out.push(node.className || "");
            out.push(node.getAttribute && (node.getAttribute("role") || ""));
            out.push(node.getAttribute && (node.getAttribute("aria-label") || ""));
            if (node.innerText) out.push(node.innerText.slice(0, 900));
            node = node.parentElement;
            depth++;
        }
        return out.join(" ").toLowerCase();
    }

    function isUsernameCandidate(el) {
        if (!el || el.tagName !== "INPUT") return false;
        var type = (el.type || "text").toLowerCase();
        if (["password", "hidden", "checkbox", "radio", "file", "submit", "button", "reset"].indexOf(type) >= 0) return false;
        if (["email", "text", "tel", "search", "url"].indexOf(type) < 0 && type !== "") return false;
        return topmost(el);
    }

    function scoreUsername(el) {
        var text = fieldText(el);
        var context = contextText(el);
        var all = text + " " + context;
        var type = (el.type || "").toLowerCase();
        var score = 0;
        if (el.value) score += 20;
        if (type === "email") score += 30;
        if (/\b(username|user|login|email|mail|account|identifier|identifiant)\b/.test(text)) score += 35;
        if (/\b(connexion|connecter|connectez|compte|auth|login|identifier|identifiez|continuer|mot de passe|password)\b/.test(context)) score += 70;
        if (/\b(newsletter|inscrivez|actualit|offres|commercial|marketing|désabonnement|desabonnement|footer|presse|recrutement|paypal|visa|mastercard)\b/.test(all)) score -= 160;
        if (/\b(current-password|new-password|otp|code|search|recherche|coupon|promo|quantity|qty|quantite|quantité)\b/.test(all)) score -= 80;
        if ((el.autocomplete || "").toLowerCase() === "username") score += 45;
        return score;
    }

    function usernameFields(scope) {
        var roots = [];
        if (scope) roots.push(scope);
        roots.push(document);
        var out = [];
        var seen = new Set();

        for (var r = 0; r < roots.length; r++) {
            var fields = roots[r].querySelectorAll("input");
            for (var i = 0; i < fields.length; i++) {
                var el = fields[i];
                if (seen.has(el) || !isUsernameCandidate(el)) continue;
                if (scoreUsername(el) <= -20) continue;
                seen.add(el);
                out.push(el);
            }
        }

        return out;
    }

    function bestUsername(scope) {
        var fields = usernameFields(scope);
        var best = null;
        var bestScore = 0;

        for (var i = 0; i < fields.length; i++) {
            var el = fields[i];
            if (!el.value) continue;
            var score = scoreUsername(el);
            if (score > bestScore) {
                best = el;
                bestScore = score;
            }
        }

        return best ? best.value : lastUser;
    }

    // Detection volontairement prudente d'un champ "nouveau mot de passe" (ecran
    // d'inscription ou de changement de mot de passe), pour proposer un mot de
    // passe genere — jamais sur un simple formulaire de connexion. En cas de
    // doute, ne pas proposer : mieux vaut rater une inscription qu'gener une
    // connexion normale.
    function newPasswordScore(el, siblingCount) {
        var text = fieldText(el);
        var context = contextText(el);
        var all = text + " " + context;

        if (/\b(current-password|current|ancien|actuel|old)\b/.test(all)) return -999;

        var score = 0;
        if ((el.autocomplete || "").toLowerCase() === "new-password") score += 100;
        if (siblingCount >= 2) score += 60;
        if (/\b(new|confirm|register|signup|creer|inscri|nouveau)\b/.test(text)) score += 40;
        if (/\b(creer un compte|creation de compte|inscription|sign up|s'inscrire|register|nouveau mot de passe|choisir un mot de passe)\b/.test(context)) score += 70;
        return score;
    }

    function newPasswordFields(scope) {
        var pwFields = passwordFields(scope, false).filter(function (el) { return !el.value; });
        if (!pwFields.length) return [];
        var siblingCount = pwFields.length;
        return pwFields.filter(function (el) { return newPasswordScore(el, siblingCount) > 50; });
    }

    function setValue(el, value) {
        el.focus();
        var proto = Object.getPrototypeOf(el);
        var desc = Object.getOwnPropertyDescriptor(proto, "value");
        if (desc && desc.set) desc.set.call(el, value);
        else el.value = value;
        el.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: value }));
        el.dispatchEvent(new Event("change", { bubbles: true }));
        return String(el.value || "") === String(value || "");
    }

    window.__novaFillNewPassword = function (value) {
        try {
            var fields = newPasswordFields(document);
            if (!fields.length) return { success: false, message: "Aucun champ nouveau mot de passe detecte." };
            var ok = fields.map(function (el) { return setValue(el, value); }).some(function (x) { return x; });
            return ok
                ? { success: true, message: "Mot de passe genere rempli." }
                : { success: false, message: "Champ detecte, mais le site a refuse l'ecriture." };
        } catch (error) {
            return { success: false, message: "Erreur: " + error.message };
        }
    };

    // ── Remplissage a la demande (declenche par l'app hote) ──────────────────
    //
    // Contrairement a la DETECTION (prudente : champ au premier plan exige via
    // elementFromPoint), le REMPLISSAGE est un geste explicite de l'utilisateur :
    // les regles sont assouplies. On cherche aussi dans les shadow roots ouverts
    // et les iframes same-origin ; les iframes cross-origin sont couvertes cote
    // hote (le meme script vit dans chaque frame, l'app appelle chaque frame).

    function collectFillRoots() {
        var roots = [];
        var queue = [document];
        while (queue.length && roots.length < 40) {
            var root = queue.shift();
            if (!root || roots.indexOf(root) >= 0) continue;
            roots.push(root);
            var all;
            try { all = root.querySelectorAll("*"); } catch (_) { continue; }
            for (var i = 0; i < all.length; i++) {
                var el = all[i];
                if (el.shadowRoot) queue.push(el.shadowRoot);
                if (el.tagName === "IFRAME" || el.tagName === "FRAME") {
                    // contentDocument est null pour une iframe cross-origin : elle
                    // sera servie par sa propre copie du script, via l'app hote.
                    try { if (el.contentDocument) queue.push(el.contentDocument); } catch (_) { }
                }
            }
        }
        return roots;
    }

    function queryAllDeep(selector) {
        var roots = collectFillRoots();
        var out = [];
        for (var r = 0; r < roots.length; r++) {
            try {
                var found = roots[r].querySelectorAll(selector);
                for (var i = 0; i < found.length; i++) out.push(found[i]);
            } catch (_) { }
        }
        return out;
    }

    // Visibilite assouplie au remplissage : pas d'exigence de premier plan
    // (bandeau cookies, overlay...) ni de presence dans le viewport, et un champ
    // readOnly reste eligible (deverrouille dans setValueForFill).
    function fillable(el) {
        if (!el || el.disabled) return false;
        var view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
        var style = view.getComputedStyle(el);
        if (style && (style.visibility === "hidden" || style.display === "none")) return false;
        var rect = el.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
    }

    function isUsernameCandidateForFill(el) {
        if (!el || el.tagName !== "INPUT") return false;
        var type = (el.type || "text").toLowerCase();
        if (["password", "hidden", "checkbox", "radio", "file", "submit", "button", "reset"].indexOf(type) >= 0) return false;
        if (["email", "text", "tel", "search", "url"].indexOf(type) < 0 && type !== "") return false;
        return fillable(el);
    }

    function pickPasswordFieldForFill() {
        var fields = queryAllDeep("input[type='password']").filter(fillable);
        if (!fields.length) return null;
        // Preferer un champ pleinement interactif ; sinon repli sur les champs
        // opacity:0 / readonly (astuces anti-autofill courantes).
        var preferred = fields.filter(function (el) {
            var view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
            return Number(view.getComputedStyle(el).opacity) !== 0 && !el.readOnly;
        });
        var pool = preferred.length ? preferred : fields;
        return pool.find(function (el) { return !el.value; }) || pool[0];
    }

    function pickUsernameFieldForFill(passwordField) {
        var candidates = queryAllDeep("input").filter(isUsernameCandidateForFill);
        var best = null;
        var bestScore = -999;
        for (var i = 0; i < candidates.length; i++) {
            var el = candidates[i];
            var score = scoreUsername(el);
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
    }

    function setValueForFill(el, value) {
        try { el.scrollIntoView({ block: "center", inline: "nearest" }); } catch (_) { }
        try { el.focus(); } catch (_) { }
        // Certains sites posent readonly et ne le retirent qu'au focus ; si le
        // site ne l'a pas fait, on force pour pouvoir ecrire.
        if (el.readOnly) {
            try { el.readOnly = false; el.removeAttribute("readonly"); } catch (_) { }
        }
        var proto = Object.getPrototypeOf(el);
        var desc = proto ? Object.getOwnPropertyDescriptor(proto, "value") : null;
        if (desc && desc.set) desc.set.call(el, value);
        else el.value = value;
        try {
            el.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: value }));
        } catch (_) {
            el.dispatchEvent(new Event("input", { bubbles: true }));
        }
        el.dispatchEvent(new Event("change", { bubbles: true }));
        return String(el.value || "") === String(value || "");
    }

    window.__novaFillCredential = function (payload) {
        try {
            payload = payload || {};
            var passwordField = payload.password ? pickPasswordFieldForFill() : null;
            var usernameField = payload.username ? pickUsernameFieldForFill(passwordField) : null;
            var report = {
                success: false,
                message: "",
                filledUsername: false,
                filledPassword: false,
                foundUsernameField: !!usernameField,
                foundPasswordField: !!passwordField
            };

            if (usernameField) report.filledUsername = setValueForFill(usernameField, payload.username);
            if (passwordField) report.filledPassword = setValueForFill(passwordField, payload.password);

            report.success = report.filledUsername || report.filledPassword;
            if (report.filledUsername && report.filledPassword) report.message = "Identifiants remplis.";
            else if (report.filledPassword) report.message = "Mot de passe rempli.";
            else if (report.filledUsername) report.message = "Identifiant rempli. Mot de passe attendu.";
            else if (passwordField || usernameField) report.message = "Champ detecte, mais le site a refuse l'ecriture.";
            else report.message = "Aucun champ de connexion visible.";

            // Verification differee : certains frameworks (re-render React/Vue...)
            // reinitialisent la valeur juste apres. On re-tente une fois, et on ne
            // previent l'app que si le resultat final contredit la reponse immediate.
            if (report.success) {
                var immediate = { u: report.filledUsername, p: report.filledPassword };
                setTimeout(function () {
                    try {
                        var uOk = !immediate.u || String(usernameField.value) === String(payload.username);
                        var pOk = !immediate.p || String(passwordField.value) === String(payload.password);
                        if (!uOk) uOk = setValueForFill(usernameField, payload.username);
                        if (!pOk) pOk = setValueForFill(passwordField, payload.password);
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

    function passwordFields(scope, requireValue) {
        var roots = [];
        if (scope) roots.push(scope);
        roots.push(document);
        var out = [];
        var seen = new Set();

        for (var r = 0; r < roots.length; r++) {
            var fields = roots[r].querySelectorAll("input[type='password']");
            for (var i = 0; i < fields.length; i++) {
                var el = fields[i];
                if (seen.has(el) || !topmost(el)) continue;
                if (requireValue && !el.value) continue;
                seen.add(el);
                out.push(el);
            }
        }

        return out;
    }

    function post(payload) {
        try {
            window.chrome.webview.postMessage(payload);
        } catch (_) { }
    }

    function rememberUsername(value) {
        if (!value) return;
        lastUser = value;
        try { sessionStorage.setItem("__nova_last_user", value); } catch (_) { }
        post({
            t: "nova.credential.username",
            username: value,
            origin: location.origin,
            loginUrl: location.href
        });
    }

    function loadRememberedUser() {
        try {
            lastUser = sessionStorage.getItem("__nova_last_user") || "";
        } catch (_) { lastUser = ""; }
    }

    function publishState(source) {
        try {
            loadRememberedUser();
            var users = usernameFields(document);
            var passwords = passwordFields(document, false);
            var newPasswords = newPasswordFields(document);
            var username = bestUsername(document) || lastUser || "";
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
            loadRememberedUser();
            var passwords = passwordFields(scope, true);
            if (!passwords.length && lastPassword) {
                passwords = [{ value: lastPassword, form: null }];
            }
            if (!passwords.length) {
                scheduleState(source || "capture-no-password");
                return;
            }

            var password = passwords[0].value || "";
            if (password.length < 3) return;

            var username = bestUsername(scope || passwords[0].form) || lastUser || "";
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
            lastPassword = target.value || "";
            lastPasswordAt = Date.now();
            publishState("password-input");
            return;
        }
        if (isUsernameCandidate(target) && target.value && scoreUsername(target) > -20) {
            rememberUsername(target.value);
            publishState("username-input");
        }
    }, true);

    document.addEventListener("change", function (event) {
        var target = event.target;
        // Meme seuil que usernameFields() : un champ code/OTP (ex. verification
        // Google par email) ne doit jamais ecraser l'identifiant deja memorise.
        if (target && isUsernameCandidate(target) && target.value && scoreUsername(target) > -20) {
            rememberUsername(target.value);
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
        if (document.visibilityState === "hidden" && Date.now() - lastPasswordAt < 30000) {
            capture(document, "visibility-hidden");
        }
    }, true);

    var observer = new MutationObserver(function () {
        loadRememberedUser();
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

    loadRememberedUser();
    scheduleState("document-created");
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () { publishState("dom-ready"); }, { once: true });
    } else {
        publishState("dom-ready");
    }
    window.addEventListener("load", function () { publishState("load"); }, { once: true });
})();
