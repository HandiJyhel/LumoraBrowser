// ── Module DOM ──────────────────────────────────────────────────────────
//
// Utilitaires bas niveau, sans aucune connaissance de ce qu'est un
// "identifiant" ou un "mot de passe" : juste des operations generiques sur
// un element ou un ensemble d'elements. Partage par CredentialCapture
// PasswordModule.js et CredentialCaptureUsernameModule.js, qui restent ainsi
// concentres sur LEUR propre logique de detection sans dupliquer ces
// operations.
(function () {
    window.__novaCredDom = window.__novaCredDom || {};
    var Dom = window.__novaCredDom;

    Dom.visible = function (el) {
        if (!el || el.disabled || el.readOnly) return false;
        var style = window.getComputedStyle(el);
        if (!style || style.visibility === "hidden" || style.display === "none" || Number(style.opacity) === 0) return false;
        var rect = el.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
    };

    Dom.topmost = function (el) {
        if (!Dom.visible(el)) return false;
        var rect = el.getBoundingClientRect();
        var x = Math.max(0, Math.min(window.innerWidth - 1, rect.left + rect.width / 2));
        var y = Math.max(0, Math.min(window.innerHeight - 1, rect.top + rect.height / 2));
        var hit = document.elementFromPoint(x, y);
        return !!hit && (hit === el || el.contains(hit) || hit.contains(el));
    };

    Dom.fieldText = function (el) {
        return [
            el.name || "",
            el.id || "",
            el.className || "",
            el.autocomplete || "",
            el.placeholder || "",
            el.getAttribute("aria-label") || ""
        ].join(" ").toLowerCase();
    };

    Dom.contextText = function (el) {
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
    };

    // Visibilite assouplie au remplissage : pas d'exigence de premier plan
    // (bandeau cookies, overlay...) ni de presence dans le viewport, et un
    // champ readOnly reste eligible (deverrouille dans setValueForFill).
    Dom.fillable = function (el) {
        if (!el || el.disabled) return false;
        var view = (el.ownerDocument && el.ownerDocument.defaultView) || window;
        var style = view.getComputedStyle(el);
        if (style && (style.visibility === "hidden" || style.display === "none")) return false;
        var rect = el.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
    };

    // Recherche profonde (shadow roots ouverts + iframes same-origin), utilisee
    // uniquement pour le REMPLISSAGE a la demande (geste explicite), jamais pour
    // la detection passive.
    Dom.collectFillRoots = function () {
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
    };

    Dom.queryAllDeep = function (selector) {
        var roots = Dom.collectFillRoots();
        var out = [];
        for (var r = 0; r < roots.length; r++) {
            try {
                var found = roots[r].querySelectorAll(selector);
                for (var i = 0; i < found.length; i++) out.push(found[i]);
            } catch (_) { }
        }
        return out;
    };

    Dom.setValue = function (el, value) {
        el.focus();
        var proto = Object.getPrototypeOf(el);
        var desc = Object.getOwnPropertyDescriptor(proto, "value");
        if (desc && desc.set) desc.set.call(el, value);
        else el.value = value;
        el.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: value }));
        el.dispatchEvent(new Event("change", { bubbles: true }));
        return String(el.value || "") === String(value || "");
    };

    Dom.setValueForFill = function (el, value) {
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
    };
})();
