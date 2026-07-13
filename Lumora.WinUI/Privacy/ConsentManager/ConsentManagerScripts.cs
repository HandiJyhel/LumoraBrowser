namespace Lumora.Privacy.ConsentManager;

// Scripts JS injectés dans chaque page pour gérer automatiquement les bandeaux cookies.
// Le module reste volontairement non destructif : il n'émule plus __cmp/__tcfapi
// avant l'initialisation du site. Certains sites conditionnent le login/session a
// leur CMP ; un faux "tout refuse" précoce peut rendre un bouton Connexion muet.
// On se limite donc aux bandeaux visibles, avec des garde-fous de compatibilité.
internal static class ConsentManagerScripts
{
    // Sélecteurs CSS des boutons "Refuser tout" par CMP (partagé entre les deux scripts)
    private const string SelectorList = """
        [
            '#CybotCookiebotDialogBodyButtonDecline',
            '#CybotCookiebotDialogBodyLevelButtonLevelOptinDeclineAll',
            '#onetrust-reject-all-handler',
            '.ot-pc-refuse-all-handler',
            '#didomi-notice-disagree-button',
            'button.didomi-components-button--type-refuse',
            '.axeptio_btn_dismiss',
            '.axeptio_btn_deny',
            '.qc-cmp2-summary-buttons .qc-cmp2-button:not(.qc-cmp2-button--primary)',
            '#truste-consent-required',
            'a.cmpboxbtnno',
            'button[data-testid="uc-deny-all-button"]',
            '#tarteaucitronAllDenied2',
            '#tarteaucitronDeny',
            '.tarteaucitronDeny',
            '#BorlabsCookieBtn--decline',
            '.cm-btn-decline',
            'button[data-cookiefirst-action="reject"]',
            '.iubenda-cs-reject-btn',
            '.orejime-Button--decline',
            '#gdpr-cookie-tool-decline',
            '.gdpr-reject-btn',
            '[class*="reject-all"]',
            '[data-action="reject-all"]'
        ]
        """;

    private const string TextList = """
        [
            'tout refuser', 'refuser tout', 'tout rejeter', 'je refuse tout',
            'refuser', 'refuser et fermer', 'continuer sans accepter',
            'continuer sans consentir', 'passer',
            'reject all', 'decline all', 'deny all', 'refuse all',
            'reject cookies', 'i decline', 'no thanks', 'no, thank you'
        ]
        """;

    private const string ContainerList = """
        [
            '[class*="cookie"]', '[class*="consent"]', '[class*="gdpr"]',
            '[class*="rgpd"]', '[id*="cookie"]', '[id*="consent"]',
            '[role="dialog"][aria-modal="true"]', '#sp-cc', '.sp_choice_type_REJECT_ALL',
            '[id^="sp_message"]', '[class*="sp_message"]', '[id*="sp_message"]'
        ]
        """;

    private const string RiskyConsentTextList = """
        [
            'rejeter les cookies peut limiter',
            'refuser les cookies peut limiter',
            'rejet des cookies peut limiter',
            'limiter certaines fonctionnal',
            'limit certain features',
            'may limit certain features',
            'rejecting cookies may limit',
            'declining cookies may limit'
        ]
        """;

    internal static string BuildInjectionScript(IEnumerable<string> loginCompatibilitySites)
    {
        var compatibilityList = CompatibilityList(loginCompatibilitySites);

        // Script principal — injecté via AddScriptToExecuteOnDocumentCreatedAsync
        // Actif sur toutes les pages, mais uniquement sur les bandeaux visibles.
        return $$$"""
        (function(){
            'use strict';

            // ── Boutons "Refuser tout" ─────────────────────────────────────────
            var SEL  = {{{SelectorList}}};
            var TXT  = {{{TextList}}};
            var CONT = {{{ContainerList}}};
            var RISK = {{{RiskyConsentTextList}}};
            var COMPAT = {{{compatibilityList}}};
            // Beaucoup de sites (Amazon compris) construisent leurs boutons avec des
            // <span>/<div role="button"> ou des <input type="submit"> plutôt que des
            // <button> natifs — le texte visible peut même être dans "value" ou
            // "aria-label" plutôt que dans le contenu de l'élément.
            var CLICKABLE = 'button, [role="button"], input[type="button"], input[type="submit"], a[href="#"], a[href=""]';

            function vis(el) { return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length); }
            function label(el) {
                return (el.getAttribute('aria-label') || el.value || el.textContent || '').trim().toLowerCase();
            }
            function textOf(el) {
                return ((el && (el.innerText || el.textContent)) || '').trim().toLowerCase();
            }
            function hostMatchesDomain(host, domain) {
                return host === domain || host.endsWith('.' + domain);
            }
            function compatibilityEnabledForPage() {
                try {
                    var host = (location.hostname || '').toLowerCase();
                    for (var i = 0; i < COMPAT.length; i++) {
                        if (hostMatchesDomain(host, COMPAT[i])) return true;
                    }
                } catch(e) {}
                return false;
            }
            function shouldAvoidAutoReject() {
                if (compatibilityEnabledForPage()) return true;
                try {
                    var candidates = [];
                    for (var c = 0; c < CONT.length; c++) {
                        var nodes = document.querySelectorAll(CONT[c]);
                        for (var n = 0; n < nodes.length; n++) candidates.push(nodes[n]);
                    }
                    if (document.body) candidates.push(document.body);
                    for (var i = 0; i < candidates.length; i++) {
                        var txt = textOf(candidates[i]);
                        if (!txt) continue;
                        for (var r = 0; r < RISK.length; r++) {
                            if (txt.indexOf(RISK[r]) >= 0) return true;
                        }
                    }
                } catch(e) {}
                return false;
            }

            function tryReject() {
                // Certains sites préviennent explicitement qu'un refus peut casser des
                // fonctions comme la connexion. Dans ce cas Lumora laisse l'utilisateur
                // décider au lieu de rendre le bouton Connexion inerte.
                if (shouldAvoidAutoReject()) return false;

                // 1. Sélecteurs connus par CMP
                for (var i = 0; i < SEL.length; i++) {
                    try {
                        var el = document.querySelector(SEL[i]);
                        if (el && vis(el)) { el.click(); return true; }
                    } catch(e) {}
                }
                // 2. Correspondance par texte dans des conteneurs de bandeau cookie
                for (var c = 0; c < CONT.length; c++) {
                    try {
                        var nodes = document.querySelectorAll(CONT[c]);
                        for (var n = 0; n < nodes.length; n++) {
                            var btns = nodes[n].querySelectorAll(CLICKABLE);
                            for (var b = 0; b < btns.length; b++) {
                                var txt = label(btns[b]);
                                for (var t = 0; t < TXT.length; t++) {
                                    if (txt === TXT[t] || txt.indexOf(TXT[t]) === 0) {
                                        btns[b].click();
                                        return true;
                                    }
                                }
                            }
                        }
                    } catch(e) {}
                }
                // 3. Repli : bandeaux maison sans classe/id reconnaissable (ex. conteneurs
                // Sourcepoint à id généré dynamiquement, comme sur amazon.fr). Recherche sur
                // TOUTE la page, mais texte EXACT uniquement (pas de préfixe) pour limiter
                // les faux positifs sur un bouton ordinaire qui contiendrait ces mots.
                try {
                    var allBtns = document.querySelectorAll(CLICKABLE);
                    for (var i2 = 0; i2 < allBtns.length; i2++) {
                        var t2 = label(allBtns[i2]);
                        if (TXT.indexOf(t2) !== -1 && vis(allBtns[i2])) { allBtns[i2].click(); return true; }
                    }
                } catch(e) {}
                return false;
            }

            // Tentative immédiate si le DOM est déjà prêt
            if (document.readyState !== 'loading') tryReject();
            document.addEventListener('DOMContentLoaded', function() { tryReject(); }, {once: true});

            // MutationObserver pour les CMPs qui s'affichent après le chargement initial
            try {
                var obs = new MutationObserver(function() { tryReject(); });
                obs.observe(document.documentElement, {childList: true, subtree: true});
                // Désactiver après 12s — tous les CMPs se chargent dans cette fenêtre
                setTimeout(function() { obs.disconnect(); }, 12000);
            } catch(e) {}

        })();
        """;
    }

    internal static string BuildRetryScript(IEnumerable<string> loginCompatibilitySites)
    {
        var compatibilityList = CompatibilityList(loginCompatibilitySites);

        // Script de rattrapage — exécuté via ExecuteScriptAsync après NavigationCompleted
        // Pour les CMPs qui finalisent leur affichage après le chargement complet de la page
        return $$$"""
        (function(){
            var SEL  = {{{SelectorList}}};
            var TXT  = {{{TextList}}};
            var CONT = {{{ContainerList}}};
            var RISK = {{{RiskyConsentTextList}}};
            var COMPAT = {{{compatibilityList}}};
            var CLICKABLE = 'button, [role="button"], input[type="button"], input[type="submit"], a[href="#"], a[href=""]';
            function vis(el) { return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length); }
            function label(el) {
                return (el.getAttribute('aria-label') || el.value || el.textContent || '').trim().toLowerCase();
            }
            function textOf(el) {
                return ((el && (el.innerText || el.textContent)) || '').trim().toLowerCase();
            }
            function hostMatchesDomain(host, domain) {
                return host === domain || host.endsWith('.' + domain);
            }
            function compatibilityEnabledForPage() {
                try {
                    var host = (location.hostname || '').toLowerCase();
                    for (var i = 0; i < COMPAT.length; i++) {
                        if (hostMatchesDomain(host, COMPAT[i])) return true;
                    }
                } catch(e) {}
                return false;
            }
            function shouldAvoidAutoReject() {
                if (compatibilityEnabledForPage()) return true;
                try {
                    var candidates = [];
                    for (var c = 0; c < CONT.length; c++) {
                        var nodes = document.querySelectorAll(CONT[c]);
                        for (var n = 0; n < nodes.length; n++) candidates.push(nodes[n]);
                    }
                    if (document.body) candidates.push(document.body);
                    for (var i = 0; i < candidates.length; i++) {
                        var txt = textOf(candidates[i]);
                        if (!txt) continue;
                        for (var r = 0; r < RISK.length; r++) {
                            if (txt.indexOf(RISK[r]) >= 0) return true;
                        }
                    }
                } catch(e) {}
                return false;
            }
            if (shouldAvoidAutoReject()) return;
            for (var i = 0; i < SEL.length; i++) {
                try {
                    var el = document.querySelector(SEL[i]);
                    if (el && vis(el)) { el.click(); return; }
                } catch(e) {}
            }
            for (var c = 0; c < CONT.length; c++) {
                try {
                    var nodes = document.querySelectorAll(CONT[c]);
                    for (var n = 0; n < nodes.length; n++) {
                        var btns = nodes[n].querySelectorAll(CLICKABLE);
                        for (var b = 0; b < btns.length; b++) {
                            var txt = label(btns[b]);
                            for (var t = 0; t < TXT.length; t++) {
                                if (txt === TXT[t] || txt.indexOf(TXT[t]) === 0) {
                                    btns[b].click();
                                    return;
                                }
                            }
                        }
                    }
                } catch(e) {}
            }
            try {
                var allBtns = document.querySelectorAll(CLICKABLE);
                for (var i2 = 0; i2 < allBtns.length; i2++) {
                    var t2 = label(allBtns[i2]);
                    if (TXT.indexOf(t2) !== -1 && vis(allBtns[i2])) { allBtns[i2].click(); return; }
                }
            } catch(e) {}
        })();
        """;
    }

    private static string CompatibilityList(IEnumerable<string> rootDomains)
    {
        var domains = rootDomains
            .Select(domain => domain.Trim().ToLowerInvariant())
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(domain => "'" + domain.Replace("\\", "\\\\").Replace("'", "\\'") + "'");
        return "[" + string.Join(",", domains) + "]";
    }
}
