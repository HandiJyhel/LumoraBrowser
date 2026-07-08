(function (payload) {
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
        var points = [
            [rect.left + rect.width / 2, rect.top + rect.height / 2],
            [rect.left + Math.min(12, rect.width / 2), rect.top + rect.height / 2],
            [rect.right - Math.min(12, rect.width / 2), rect.top + rect.height / 2]
        ];

        for (var i = 0; i < points.length; i++) {
            var x = Math.max(0, Math.min(window.innerWidth - 1, points[i][0]));
            var y = Math.max(0, Math.min(window.innerHeight - 1, points[i][1]));
            var hit = document.elementFromPoint(x, y);
            if (hit && (hit === el || el.contains(hit) || hit.contains(el))) return true;
        }

        return false;
    }

    function textOf(el) {
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

    function scoreUsername(el, passwordField) {
        var score = 0;
        var text = textOf(el);
        var context = contextText(el);
        var all = text + " " + context;
        var type = (el.type || "").toLowerCase();

        if (type === "email") score += 45;
        if ((el.autocomplete || "").toLowerCase() === "username") score += 60;
        if (/\b(username|user|login|email|mail|account|identifier|identifiant)\b/.test(text)) score += 45;
        if (/\b(connexion|connecter|connectez|compte|auth|login|identifier|identifiez|continuer|mot de passe|password)\b/.test(context)) score += 85;
        if (/\b(newsletter|inscrivez|actualit|offres|commercial|marketing|désabonnement|desabonnement|footer|presse|recrutement|paypal|visa|mastercard|newsletter)\b/.test(all)) score -= 180;
        if (/\b(search|recherche|otp|code|coupon|promo|quantity|qty|quantite|quantité)\b/.test(all)) score -= 100;
        if (el.value) score += 5;

        if (passwordField) {
            if (el.form && passwordField.form && el.form === passwordField.form) score += 35;
            if (el.compareDocumentPosition(passwordField) & Node.DOCUMENT_POSITION_FOLLOWING) score += 10;
        }

        var rect = el.getBoundingClientRect();
        if (rect.right > window.innerWidth * 0.55) score += 18;
        if (rect.top >= 0 && rect.bottom <= window.innerHeight) score += 12;

        return score;
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

    function bestPassword() {
        var fields = Array.prototype.slice.call(document.querySelectorAll("input[type='password']"))
            .filter(topmost);
        if (!fields.length) return null;
        return fields.find(function (el) { return !el.value; }) || fields[0];
    }

    function bestUsername(passwordField) {
        var active = document.activeElement;
        if (isUsernameCandidate(active) && scoreUsername(active, passwordField) > -20) {
            return active;
        }

        var scope = passwordField
            ? (passwordField.form || passwordField.closest("form") || passwordField.closest("[role='form']") || document)
            : document;
        var fields = Array.prototype.slice.call(scope.querySelectorAll("input"))
            .concat(Array.prototype.slice.call(document.querySelectorAll("input")))
            .filter(isUsernameCandidate);
        var seen = new Set();
        var best = null;
        var bestScore = -999;
        fields.forEach(function (el) {
            if (seen.has(el)) return;
            seen.add(el);
            var score = scoreUsername(el, passwordField);
            if (score > bestScore) {
                best = el;
                bestScore = score;
            }
        });
        return bestScore > -20 ? best : null;
    }

    try {
        var passwordField = bestPassword();
        var usernameField = bestUsername(passwordField);
        var filledUsername = false;
        var filledPassword = false;

        if (usernameField && payload.username) {
            filledUsername = setValue(usernameField, payload.username);
        }

        if (passwordField && payload.password) {
            filledPassword = setValue(passwordField, payload.password || "");
        }

        if (filledUsername && filledPassword) {
            return { success: true, message: "Identifiants remplis." };
        }

        if (filledUsername) {
            return { success: true, message: "Identifiant rempli. Mot de passe attendu." };
        }

        if (filledPassword) {
            return { success: true, message: "Mot de passe rempli." };
        }

        if (usernameField || passwordField) {
            return { success: false, message: "Champ detecte, mais le site a refuse l'ecriture." };
        }

        return { success: false, message: "Aucun champ de connexion visible." };
    } catch (error) {
        return { success: false, message: "Erreur autofill: " + error.message };
    }
})(__PULSE_CREDENTIAL_PAYLOAD__);
