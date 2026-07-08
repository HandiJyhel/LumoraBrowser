namespace PulseBrowser.Privacy.ConsentManager;

// Scripts JS injectés dans chaque page pour gérer automatiquement les bandeaux cookies.
// Deux couches complémentaires :
//   1. Stubs IAB TCF v1/v2 — intercepte les CMPs standards avant qu'ils ne lisent le consentement
//   2. Sélecteurs + textes — clique sur "Refuser tout" via MutationObserver pour les CMPs non-TCF
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
            '[role="dialog"][aria-modal="true"]', '#sp-cc', '.sp_choice_type_REJECT_ALL'
        ]
        """;

    // Script principal — injecté via AddScriptToExecuteOnDocumentCreatedAsync
    // Actif sur toutes les pages dès la création du document (avant le JS du site)
    internal static string InjectionScript => $$$"""
        (function(){
            'use strict';

            // ── IAB TCF v1 (window.__cmp) ─────────────────────────────────────
            try {
                if (!window.__cmp) {
                    window.__cmp = function(cmd, arg, cb) {
                        if (typeof cb === 'function') {
                            if (cmd === 'ping') cb({gdprAppliesGlobally: true, cmpLoaded: true}, true);
                            else cb({purposeConsents: {}, vendorConsents: {}}, true);
                        }
                    };
                }
            } catch(e) {}

            // ── IAB TCF v2 (window.__tcfapi) ──────────────────────────────────
            try {
                if (!window.__tcfapi) {
                    var _mc = {
                        tcString: '', gdprApplies: true, cmpStatus: 'loaded',
                        eventStatus: 'tcloaded', isServiceSpecific: true,
                        useNonStandardStacks: false, purposeOneTreatment: false, publisherCC: 'FR',
                        purpose: {consents: {}, legitimateInterests: {}},
                        vendor: {consents: {}, legitimateInterests: {}},
                        specialFeatureOptins: {},
                        publisher: {consents: {}, legitimateInterests: {},
                            customPurpose: {consents: {}, legitimateInterests: {}}, restrictions: {}}
                    };
                    window.__tcfapi = function(cmd, version, cb) {
                        if (!cb) return;
                        if (cmd === 'ping') cb({cmpLoaded: true, cmpStatus: 'loaded',
                            displayStatus: 'hidden', apiVersion: '2.0',
                            gdprApplies: true, eventStatus: 'tcloaded'}, true);
                        else cb(_mc, true);
                    };
                }
            } catch(e) {}

            // ── Boutons "Refuser tout" ─────────────────────────────────────────
            var SEL  = {{{SelectorList}}};
            var TXT  = {{{TextList}}};
            var CONT = {{{ContainerList}}};

            function vis(el) { return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length); }

            function tryReject() {
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
                            var btns = nodes[n].querySelectorAll('button, a[role="button"], a[href="#"], a[href=""]');
                            for (var b = 0; b < btns.length; b++) {
                                var txt = (btns[b].textContent || '').trim().toLowerCase();
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

    // Script de rattrapage — exécuté via ExecuteScriptAsync après NavigationCompleted
    // Pour les CMPs qui finalisent leur affichage après le chargement complet de la page
    internal static string RetryScript => $$$"""
        (function(){
            var SEL  = {{{SelectorList}}};
            var TXT  = {{{TextList}}};
            var CONT = {{{ContainerList}}};
            function vis(el) { return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length); }
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
                        var btns = nodes[n].querySelectorAll('button, a[role="button"], a[href="#"], a[href=""]');
                        for (var b = 0; b < btns.length; b++) {
                            var txt = (btns[b].textContent || '').trim().toLowerCase();
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
        })();
        """;
}
