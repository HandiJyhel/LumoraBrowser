namespace Lumora.Privacy.ConsentManager;

// Scripts JS injectés dans chaque page pour gérer automatiquement les bandeaux cookies.
// Le module reste volontairement non destructif : il n'émule plus __cmp/__tcfapi
// avant l'initialisation du site. Certains sites conditionnent le login/session a
// leur CMP ; un faux "tout refuse" précoce peut rendre un bouton Connexion muet.
// On se limite donc aux bandeaux visibles, avec des garde-fous de compatibilité.
//
// Comportement en cascade, du plus simple au plus lourd :
//   1. Bouton "Refuser tout" / "Cookies essentiels uniquement" connu par CMP (sélecteur CSS).
//   2. Bouton "Refuser tout" repéré par texte, dans un conteneur de bandeau reconnu.
//   3. Repli site large : texte EXACT sur tout bouton visible (limite les faux positifs).
//   4. Aucun refus direct disponible : ouverture du panneau "Personnaliser"/"Gérer mes
//      choix", décochage de tout ce qui n'est pas obligatoire, puis validation — c'est
//      l'équivalent du "sinon, cookies essentiels uniquement" demandé pour ce module.
internal static class ConsentManagerScripts
{
    // Sélecteurs CSS des boutons "Refuser tout" / "essentiels uniquement" par CMP.
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
            '#cmplz-deny',
            '.cmplz-btn.cmplz-deny',
            '.cmplz-deny',
            '#cky-btn-reject',
            '.cky-btn-reject',
            '.osano-cm-deny',
            '.osano-cm-denyAll',
            '.osano-cm-button--type_deny',
            '.t-declineAllButton',
            '#cookiescript_reject',
            '.cookiescript_reject',
            '#ccc-reject-settings',
            '.ccc-reject-settings',
            '#cn-reject-cookie',
            '.cn-decline-cookie',
            '#wt-cli-reject-btn',
            '.wt-cli-reject-btn',
            '#shopify-pc__banner__btn-decline',
            '.cc-deny',
            '.cc-decline',
            '.cc-dismiss',
            '#declineButton',
            '.coi-banner__decline',
            'button[data-cookie-consent="reject"]',
            '[class*="reject-all"]',
            '[id*="reject-all"]',
            '[class*="deny-all"]',
            '[id*="decline-all"]',
            '[data-action="reject-all"]'
        ]
        """;

    // Textes de bouton "refuser tout" ou "essentiels/nécessaires uniquement" — un clic
    // direct sur l'un de ces boutons produit déjà le résultat "essentiels uniquement",
    // les cookies strictement nécessaires restant déposés par construction du site.
    private const string TextList = """
        [
            'tout refuser', 'refuser tout', 'tout rejeter', 'je refuse tout',
            'refuser', 'refuser et fermer', 'non merci', 'continuer sans accepter',
            'continuer sans consentir', 'passer', 'tout décliner', 'décliner tout',
            'refuser tous les cookies', 'continuer sans cookies',
            'cookies essentiels uniquement', 'uniquement les cookies essentiels',
            'accepter uniquement les cookies essentiels',
            'autoriser uniquement les cookies nécessaires', 'cookies nécessaires uniquement',
            'refuser les cookies non essentiels',
            'je désactive les finalités non essentielles', 'désactiver les finalités non essentielles',
            'désactiver les cookies non essentiels',
            'reject all', 'decline all', 'deny all', 'refuse all',
            'reject cookies', 'i decline', 'no thanks', 'no, thank you',
            'necessary cookies only', 'only necessary cookies', 'use necessary cookies only',
            'strictly necessary only', 'essential cookies only', 'reject non-essential'
        ]
        """;

    // Conteneurs de bandeau reconnus — sert à la fois au clic par texte (passe 2) et au
    // cadrage du garde-fou anti-casse-connexion (le texte n'est lu que dans ces éléments,
    // jamais sur la page entière).
    private const string ContainerList = """
        [
            '[class*="cookie"]', '[class*="consent"]', '[class*="gdpr"]',
            '[class*="rgpd"]', '[id*="cookie"]', '[id*="consent"]',
            '[class*="cmp"]', '[id*="cmp"]',
            '[class*="cmplz"]', '[id*="cmplz"]',
            '[id*="usercentrics"]', '[class*="usercentrics"]',
            '[role="dialog"][aria-modal="true"]', '#sp-cc', '#sp-cc-wrapper', '.sp_choice_type_REJECT_ALL',
            '[id^="sp_message"]', '[class*="sp_message"]', '[id*="sp_message"]',
            '.cc-window', '#cookie-law-info-bar', '.cli-modal-backdrop',
            '#cookiescript_injected', '.osano-cm-window', '#ccc-module',
            '#cookie-notice', '.CybotCookiebotDialog', '#shopify-pc__banner'
        ]
        """;

    // Boilerplate quasi universel des bandeaux RGPD ("refuser peut limiter votre
    // expérience/certaines fonctionnalités") : n'est PAS un signal fiable de casse de
    // connexion, juste une formule légale standard. On la lit donc seulement pour info
    // dans le conteneur du bandeau — jamais comme motif de blocage à lui seul, et jamais
    // sur la page entière (voir historique : c'était le bug qui désactivait le refus
    // auto sur la plupart des sites).
    //
    // Le seul motif qui bloque réellement le refus auto est LoginRiskTextList ci-dessous.
    private const string LoginRiskTextList = """
        [
            'vous devez accepter les cookies pour vous connecter',
            'nécessaires pour vous connecter',
            'accepter les cookies pour vous identifier',
            'cookies necessary to log in',
            'cookies required to sign in',
            'without accepting cookies you cannot log in',
            'you must accept cookies to sign in'
        ]
        """;

    // Phrases qui ouvrent le panneau détaillé quand aucun refus direct n'existe.
    private const string ManageTextList = """
        [
            'personnaliser', 'personnaliser les cookies', 'paramétrer les cookies',
            'paramétrer mes choix', 'gérer mes choix', 'gérer les cookies',
            'gérer mes préférences', 'préférences cookies', 'préférences relatives aux cookies',
            'customize', 'customise', 'manage preferences', 'manage cookies',
            'cookie settings', 'more options', 'options avancées'
        ]
        """;

    // Phrases du bouton qui valide le panneau détaillé une fois les cases décochées.
    private const string ConfirmTextList = """
        [
            'enregistrer', 'enregistrer mes choix', 'enregistrer mes préférences',
            'confirmer', 'confirmer mes choix', 'valider', 'valider mes choix',
            'sauvegarder', 'sauvegarder mes choix',
            'save', 'save settings', 'save preferences', 'save choices',
            'confirm', 'confirm choices', 'submit preferences', 'apply', 'apply settings'
        ]
        """;

    internal static string BuildInjectionScript(IEnumerable<string> loginCompatibilitySites)
    {
        var compatibilityList = CompatibilityList(loginCompatibilitySites);

        return $$$"""
        (function(){
            'use strict';
            {{{EngineFunctions}}}
            var COMPAT = {{{compatibilityList}}};

            if (document.readyState !== 'loading') runConsentEngine();
            document.addEventListener('DOMContentLoaded', function() { runConsentEngine(); }, {once: true});

            // MutationObserver pour les CMPs qui s'affichent après le chargement initial,
            // et pour repasser sur le panneau détaillé une fois ouvert (passe 4).
            //
            // observe(document), PAS document.documentElement (bug réel trouvé le
            // 2026-08-22, confirmé en direct sur amazon.fr via CDP/Edge headless avec
            // le VRAI mecanisme d'injection de WebView2 - AddScriptToExecuteOnDocumentCreatedAsync
            // s'execute avant que document.documentElement existe : observe() levait donc
            // une exception silencieuse (avalee par le try/catch), l'observateur ne
            // s'attachait JAMAIS. Consequence : tout bandeau affiche apres le chargement
            // initial (Sourcepoint sur amazon.fr, entre autres) n'etait jamais detecte,
            // quels que soient les autres correctifs de ce moteur. document existe toujours
            // des la creation du document (contrairement a documentElement), et observer
            // document avec subtree:true capte exactement les memes mutations.
            try {
                var obs = new MutationObserver(function() { runConsentEngine(); });
                obs.observe(document, {childList: true, subtree: true});
                // 20s : couvre les CMP à affichage tardif (ex. Sourcepoint sur connexion lente).
                setTimeout(function() { obs.disconnect(); }, 20000);
            } catch(e) {}
        })();
        """;
    }

    internal static string BuildRetryScript(IEnumerable<string> loginCompatibilitySites)
    {
        var compatibilityList = CompatibilityList(loginCompatibilitySites);

        // Script de rattrapage — exécuté via ExecuteScriptAsync après NavigationCompleted,
        // pour les CMP qui finalisent leur affichage après le chargement complet de la page.
        return $$$"""
        (function(){
            var COMPAT = {{{compatibilityList}}};
            {{{EngineFunctions}}}
            runConsentEngine();
        })();
        """;
    }

    // Fonctions partagées entre le script d'injection (continu, avec observer) et le
    // script de rattrapage (un seul passage) — même moteur de décision dans les deux cas.
    private static readonly string EngineFunctions = $$$"""
        var SEL     = {{{SelectorList}}};
        var TXT     = {{{TextList}}};
        var CONT    = {{{ContainerList}}};
        var LOGRISK = {{{LoginRiskTextList}}};
        var MANAGE  = {{{ManageTextList}}};
        var CONFIRM = {{{ConfirmTextList}}};
        // 'a' sans restriction sur le href : beaucoup de bandeaux modernes (ex.
        // leboncoin, "Continuer sans accepter") utilisent un lien géré uniquement en
        // JS, sans href="#" ni href="" - le filtre précédent (a[href="#"], a[href=""])
        // les ratait completement. Bug réel constaté le 2026-08-19 (bandeau jamais
        // fermé automatiquement, alors même que le bon texte était dans TXT).
        var CLICKABLE = 'button, [role="button"], input[type="button"], input[type="submit"], a';

        // Traverse aussi les shadow roots OUVERTS (certains bandeaux modernes -
        // Cookiebot recent, certaines configs OneTrust - encapsulent leur bannière
        // dans un Web Component, invisible pour un simple document.querySelectorAll).
        // Même pattern que collectFillRoots/queryAllDeep du remplissage de mots de
        // passe (CredentialCaptureDom.js). Plafonné (60 racines) pour rester
        // dans le même ordre de coût que le repli "page entière" déjà existant
        // (passe 3 plus bas), pas une nouvelle classe de coût.
        function collectRoots() {
            var roots = [document];
            var queue = [document];
            while (queue.length && roots.length < 60) {
                var root = queue.shift();
                var all;
                try { all = root.querySelectorAll('*'); } catch(e) { continue; }
                for (var i = 0; i < all.length && roots.length < 60; i++) {
                    var sr = all[i].shadowRoot;
                    if (sr && roots.indexOf(sr) === -1) { roots.push(sr); queue.push(sr); }
                }
            }
            return roots;
        }
        function deepQueryFirst(selector) {
            var roots = collectRoots();
            for (var r = 0; r < roots.length; r++) {
                try { var el = roots[r].querySelector(selector); if (el) return el; } catch(e) {}
            }
            return null;
        }
        function deepQueryAll(selector) {
            var roots = collectRoots();
            var out = [];
            for (var r = 0; r < roots.length; r++) {
                try {
                    var found = roots[r].querySelectorAll(selector);
                    for (var i = 0; i < found.length; i++) out.push(found[i]);
                } catch(e) {}
            }
            return out;
        }
        // checkVisibility() (disponible dans le Chromium de ce WebView2) couvre
        // visibility:hidden, que offsetWidth/offsetHeight ne detectent pas
        // (l'element garde sa mise en page, juste invisible a l'oeil) - un
        // bouton "Refuser" cache de cette facon (variante desktop/mobile
        // dupliquee, onglet inactif d'un bandeau a plusieurs vues) etait
        // clique sans aucun effet reel, laissant le bandeau ouvert (2026-08-22,
        // trouve par relecture apres un signalement utilisateur). Repli sur
        // l'ancienne methode si l'API n'existe pas.
        //
        // PAS de checkOpacity ici (regression reelle trouvee le meme jour,
        // capture d'ecran a l'appui sur amazon.fr) : le vrai bouton "Refuser"
        // (#sp-cc-rejectall-link) est un <input> natif rendu a opacite 0,
        // technique d'accessibilite standard (le rendu visuel vient d'un
        // habillage voisin, le natif reste le vrai element cliquable/
        // focusable au clavier) - PAS un doublon cache. checkOpacity:true
        // excluait a tort ce genre de bouton parfaitement fonctionnel.
        function vis(el) {
            if (typeof el.checkVisibility === 'function') {
                try { return el.checkVisibility({checkVisibilityCSS: true}); } catch(e) {}
            }
            return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
        }
        // Espace insecable (U+00A0, tres courant dans le HTML des bandeaux
        // cookies), tabulations et retours a la ligne (texte reparti sur
        // plusieurs lignes dans le HTML source) reduits a un simple espace
        // avant comparaison - sans ca, un bouton pourtant au bon texte visible
        // ne correspondait jamais exactement a une phrase de la liste
        // (2026-08-22, trouve par relecture apres un signalement utilisateur).
        function normalizeText(s) {
            return s.replace(/\s+/g, ' ').trim().toLowerCase();
        }
        function label(el) {
            return normalizeText(el.getAttribute('aria-label') || el.value || el.textContent || '');
        }
        function textOf(el) {
            return normalizeText((el && (el.innerText || el.textContent)) || '');
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
        function bannerContainers() {
            var out = [];
            for (var c = 0; c < CONT.length; c++) {
                try {
                    var nodes = deepQueryAll(CONT[c]);
                    for (var n = 0; n < nodes.length; n++) out.push(nodes[n]);
                } catch(e) {}
            }
            return out;
        }
        // Seul un motif explicite de casse de connexion bloque le refus auto. Lu sur
        // toute la page (le message peut être hors du bandeau) mais avec des phrases
        // volontairement spécifiques, pas le boilerplate RGPD générique. Inclut les
        // shadow roots ouverts : sans ça, un avertissement de casse de connexion
        // écrit à l'intérieur d'un bandeau en Shadow DOM serait invisible à ce
        // garde-fou alors même que le clic sur "Refuser" (lui, deepQueryFirst) le
        // trouverait - un décalage qui affaiblirait justement la protection anti-
        // casse-connexion pour ces sites-là.
        function loginRiskDetected() {
            if (compatibilityEnabledForPage()) return true;
            try {
                var roots = collectRoots();
                var txt = '';
                for (var r = 0; r < roots.length; r++) {
                    txt += ' ' + textOf(roots[r].body || roots[r]);
                }
                txt = txt.toLowerCase();
                for (var i = 0; i < LOGRISK.length; i++) {
                    if (txt.indexOf(LOGRISK[i]) >= 0) return true;
                }
            } catch(e) {}
            return false;
        }
        function clickByTextIn(nodes, phrases, exactOnly) {
            for (var n = 0; n < nodes.length; n++) {
                var btns;
                try { btns = nodes[n].querySelectorAll(CLICKABLE); } catch(e) { continue; }
                for (var b = 0; b < btns.length; b++) {
                    if (!vis(btns[b])) continue;
                    var txt = label(btns[b]);
                    if (!txt) continue;
                    for (var t = 0; t < phrases.length; t++) {
                        if (txt === phrases[t] || (!exactOnly && txt.indexOf(phrases[t]) === 0)) {
                            btns[b].click();
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        function uncheckToggles(root) {
            try {
                var boxes = root.querySelectorAll('input[type="checkbox"]:checked:not([disabled])');
                for (var i = 0; i < boxes.length; i++) {
                    boxes[i].checked = false;
                    boxes[i].dispatchEvent(new Event('change', {bubbles: true}));
                    boxes[i].dispatchEvent(new Event('input', {bubbles: true}));
                }
                var switches = root.querySelectorAll('[role="switch"][aria-checked="true"]:not([aria-disabled="true"]):not([disabled])');
                for (var j = 0; j < switches.length; j++) {
                    switches[j].click();
                }
            } catch(e) {}
        }
        // Signale a l'app qu'un refus a bien eu lieu sur cette page (icone dans la
        // barre d'outils) - sans ca, rien ne distingue "le script a agi" de "il n'y
        // avait pas de bandeau". method: 'direct' (bouton refus/essentiels direct)
        // ou 'panel' (panneau detaille decoche puis valide).
        function notifyHost(method) {
            try {
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage({t: 'nova.consentHandled', method: method});
                }
            } catch(e) {}
        }

        function runConsentEngine() {
            if (window.__lumoraConsentDone) return false;
            if (loginRiskDetected()) return false;

            var containers = bannerContainers();

            // 1. Sélecteurs connus par CMP (refus direct ou essentiels uniquement)
            for (var i = 0; i < SEL.length; i++) {
                try {
                    var el = deepQueryFirst(SEL[i]);
                    if (el && vis(el)) { el.click(); window.__lumoraConsentDone = true; notifyHost('direct'); return true; }
                } catch(e) {}
            }

            // 2. Correspondance par texte, dans les conteneurs de bandeau repérés
            if (containers.length && clickByTextIn(containers, TXT, false)) {
                window.__lumoraConsentDone = true;
                notifyHost('direct');
                return true;
            }

            // 3. Repli site large : texte EXACT uniquement (bandeaux maison sans
            // classe/id reconnaissable, ex. conteneurs Sourcepoint à id généré).
            try {
                var allBtns = deepQueryAll(CLICKABLE);
                for (var i2 = 0; i2 < allBtns.length; i2++) {
                    if (!vis(allBtns[i2])) continue;
                    var t2 = label(allBtns[i2]);
                    if (TXT.indexOf(t2) !== -1) { allBtns[i2].click(); window.__lumoraConsentDone = true; notifyHost('direct'); return true; }
                }
            } catch(e) {}

            // 4. Panneau détaillé déjà ouvert (passe 5 d'un tour précédent) : décoche
            // tout ce qui n'est pas obligatoire (les cases désactivées restent —
            // cookies strictement nécessaires) puis valide.
            if (window.__lumoraConsentPanelOpened) {
                for (var c4 = 0; c4 < containers.length; c4++) uncheckToggles(containers[c4]);
                if (containers.length && clickByTextIn(containers, CONFIRM, false)) {
                    window.__lumoraConsentDone = true;
                    notifyHost('panel');
                    return true;
                }
                return false;
            }

            // 5. Aucun refus direct disponible : ouvrir "Personnaliser"/"Gérer mes
            // choix". La passe 4 termine le travail au prochain passage (observer ou
            // rattrapage planifié juste en dessous).
            if (containers.length && clickByTextIn(containers, MANAGE, true)) {
                window.__lumoraConsentPanelOpened = true;
                setTimeout(function() { try { runConsentEngine(); } catch(e) {} }, 400);
                return true;
            }

            return false;
        }
        """;

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
