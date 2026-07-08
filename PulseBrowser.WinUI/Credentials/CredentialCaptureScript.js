(function () {
    if (window.__pulseCredentialCaptureV3) return;
    window.__pulseCredentialCaptureV3 = true;

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
        try { sessionStorage.setItem("__pulse_last_user", value); } catch (_) { }
        post({
            t: "pulse.credential.username",
            username: value,
            origin: location.origin,
            loginUrl: location.href
        });
    }

    function loadRememberedUser() {
        try {
            lastUser = sessionStorage.getItem("__pulse_last_user") || "";
        } catch (_) { lastUser = ""; }
    }

    function publishState(source) {
        try {
            loadRememberedUser();
            var users = usernameFields(document);
            var passwords = passwordFields(document, false);
            var username = bestUsername(document) || lastUser || "";
            var key = [
                location.origin,
                location.href,
                users.length ? "u1" : "u0",
                passwords.length ? "p1" : "p0",
                username,
                source || ""
            ].join("|");

            if (key === sentStateKey) return;
            sentStateKey = key;

            post({
                t: "pulse.credential.page-state",
                origin: location.origin,
                loginUrl: location.href,
                hasUsernameField: users.length > 0,
                hasPasswordField: passwords.length > 0,
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
                t: "pulse.credential.candidate",
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
        if (isUsernameCandidate(target) && target.value) {
            rememberUsername(target.value);
            publishState("username-input");
        }
    }, true);

    document.addEventListener("change", function (event) {
        var target = event.target;
        if (target && isUsernameCandidate(target) && target.value) {
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
