// ── Mode lecture Lumora ──────────────────────────────────────────────────────
// Injecté à la demande (jamais en tâche de fond). Extrait l'article de la page,
// l'affiche dans une surcouche épurée, et permet de surligner / commenter des
// passages. Chaque annotation est ancrée par « extrait exact + contexte
// avant/après » (le principe TextQuoteSelector du standard W3C Web Annotation)
// et envoyée au C# via chrome.webview.postMessage — rien ne quitte l'appareil.
// La surcouche vit PAR-DESSUS la page (aucune destruction du DOM d'origine) :
// quitter le mode lecture la retire simplement, sans rechargement.
(function () {
    'use strict';
    if (window.__lumoraReader) { return; }

    var OVERLAY_ID = 'lumora-reader-overlay';
    var pendingSeq = 0;
    var state = {
        overlay: null,
        content: null,     // conteneur du texte de l'article (zone annotable)
        annotations: [],   // annotations connues, appliquées ou non
        fontSize: 18,
        savedBodyOverflow: ''
    };

    function post(message) {
        try { window.chrome.webview.postMessage(message); } catch (e) { }
    }

    // ── Extraction de l'article ──────────────────────────────────────────────
    // Heuristique locale : parmi les conteneurs candidats, garder celui qui a
    // le plus de texte « utile » (longueur pondérée par la densité de liens —
    // un menu est plein de liens, un article n'en a presque pas).
    function scoreNode(node) {
        var text = (node.textContent || '').replace(/\s+/g, ' ').trim();
        if (text.length < 250) { return 0; }
        var linkText = 0;
        var links = node.querySelectorAll('a');
        for (var i = 0; i < links.length; i++) {
            linkText += (links[i].textContent || '').length;
        }
        var density = text.length > 0 ? linkText / text.length : 1;
        return text.length * (1 - Math.min(density, 0.95));
    }

    function pickArticleRoot() {
        var selectors = ['article', '[role="main"]', 'main',
            '.post-content', '.article-content', '.entry-content', '#content'];
        var best = null;
        var bestScore = 0;
        for (var s = 0; s < selectors.length; s++) {
            var found;
            try { found = document.querySelectorAll(selectors[s]); } catch (e) { continue; }
            for (var i = 0; i < found.length; i++) {
                var score = scoreNode(found[i]);
                if (score > bestScore) { best = found[i]; bestScore = score; }
            }
        }
        return best || document.body;
    }

    // Nettoie une COPIE de l'article : jamais la page d'origine. Retire tout ce
    // qui n'est pas du contenu de lecture, ainsi que les gestionnaires inline
    // (le clone vit dans le même document, un onclick y resterait actif).
    function cleanClone(root) {
        var clone = root.cloneNode(true);
        var junk = clone.querySelectorAll(
            'script, style, noscript, iframe, object, embed, form, button, input, ' +
            'select, textarea, nav, aside, footer, header, [role="navigation"], ' +
            '[role="banner"], [role="complementary"], [aria-hidden="true"]');
        for (var i = junk.length - 1; i >= 0; i--) {
            if (junk[i].parentNode) { junk[i].parentNode.removeChild(junk[i]); }
        }
        var all = clone.querySelectorAll('*');
        for (var j = 0; j < all.length; j++) {
            var el = all[j];
            for (var a = el.attributes.length - 1; a >= 0; a--) {
                var name = el.attributes[a].name;
                if (name.indexOf('on') === 0 || name === 'style' || name === 'id' ||
                    (name === 'href' && /^\s*javascript:/i.test(el.attributes[a].value))) {
                    el.removeAttribute(name);
                }
            }
        }
        return clone;
    }

    function articleTitle(root) {
        var h1 = root.querySelector('h1') || document.querySelector('h1');
        var fromH1 = h1 ? h1.textContent.replace(/\s+/g, ' ').trim() : '';
        return fromH1 || document.title || location.hostname;
    }

    // ── Construction de la surcouche ─────────────────────────────────────────
    function readerCss() {
        return [
            // Remise à zéro TOTALE : les feuilles de style de la page s'appliquent
            // aussi à la surcouche (même document). Sans ça, un simple
            // « div { width: 600px } » du site rétrécit le mode lecture entier.
            // Les règles Lumora ci-dessous repassent derrière (spécificité #id+).
            '#' + OVERLAY_ID + ', #' + OVERLAY_ID + ' * { all: revert; }',
            '#' + OVERLAY_ID + ' { position: fixed !important;',
            '  top: 0 !important; left: 0 !important; right: 0 !important; bottom: 0 !important;',
            '  width: auto !important; height: auto !important;',
            '  max-width: none !important; max-height: none !important;',
            '  margin: 0 !important; padding: 0 !important; border: none !important;',
            '  box-shadow: none !important; transform: none !important; float: none !important;',
            '  display: block !important; visibility: visible !important; opacity: 1 !important;',
            '  z-index: 2147483000 !important; overflow-y: auto !important;',
            '  background: #f5f1e8 !important; color: #262521 !important;',
            '  font-family: Georgia, "Times New Roman", serif !important;',
            '  font-size: 18px; line-height: 1.65; text-align: left !important; }',
            '@media (prefers-color-scheme: dark) {',
            '  #' + OVERLAY_ID + ' { background: #24251f !important; color: #e8e4da !important; } }',
            '#' + OVERLAY_ID + ' .lumora-reader-page { max-width: 720px; margin: 0 auto; padding: 28px 24px 90px; }',
            '#' + OVERLAY_ID + ' .lumora-reader-bar { position: sticky; top: 0; display: flex; gap: 8px;',
            '  align-items: center; justify-content: flex-end; padding: 10px 0; margin-bottom: 10px;',
            '  background: inherit; font-family: "Segoe UI", sans-serif; font-size: 13px; }',
            '#' + OVERLAY_ID + ' .lumora-reader-bar button { cursor: pointer; border: 1px solid rgba(128,120,100,.45);',
            '  border-radius: 6px; background: transparent; color: inherit; padding: 4px 12px; font-size: 13px; }',
            '#' + OVERLAY_ID + ' .lumora-reader-bar button:hover { background: rgba(128,120,100,.18); }',
            '#' + OVERLAY_ID + ' .lumora-reader-hint { margin-right: auto; opacity: .62; }',
            '#' + OVERLAY_ID + ' h1.lumora-reader-title { font-size: 1.7em; line-height: 1.25; margin: 0 0 4px; }',
            '#' + OVERLAY_ID + ' .lumora-reader-source { font-family: "Segoe UI", sans-serif; font-size: 12px;',
            '  opacity: .6; margin-bottom: 26px; word-break: break-all; }',
            '#' + OVERLAY_ID + ' .lumora-reader-content img, #' + OVERLAY_ID + ' .lumora-reader-content video',
            '  { max-width: 100%; height: auto; }',
            '#' + OVERLAY_ID + ' .lumora-reader-content a { color: inherit; text-decoration: underline; }',
            '#' + OVERLAY_ID + ' .lumora-reader-content pre { overflow-x: auto; }',
            '#' + OVERLAY_ID + ' mark.lumora-highlight { background: rgba(255, 216, 84, .55); color: inherit;',
            '  border-radius: 2px; padding: 0 1px; cursor: pointer; }',
            '#' + OVERLAY_ID + ' mark.lumora-highlight.lumora-has-comment { border-bottom: 2px dotted rgba(200,140,0,.9); }',
            '@media (prefers-color-scheme: dark) { #' + OVERLAY_ID + ' mark.lumora-highlight { background: rgba(255, 216, 84, .32); } }',
            '#' + OVERLAY_ID + ' .lumora-annot-pop { position: absolute; z-index: 2147483100; max-width: 340px;',
            '  background: #fffdf7; color: #262521; border: 1px solid rgba(128,120,100,.5);',
            '  border-radius: 8px; box-shadow: 0 6px 24px rgba(0,0,0,.25); padding: 10px;',
            '  font-family: "Segoe UI", sans-serif; font-size: 13px; }',
            '@media (prefers-color-scheme: dark) { #' + OVERLAY_ID + ' .lumora-annot-pop { background: #2e2f28; color: #e8e4da; } }',
            '#' + OVERLAY_ID + ' .lumora-annot-pop textarea { width: 100%; box-sizing: border-box; min-height: 56px;',
            '  margin: 8px 0; font: inherit; background: transparent; color: inherit;',
            '  border: 1px solid rgba(128,120,100,.5); border-radius: 6px; padding: 6px; }',
            '#' + OVERLAY_ID + ' .lumora-annot-pop .lumora-row { display: flex; gap: 8px; justify-content: flex-end; }',
            '#' + OVERLAY_ID + ' .lumora-annot-pop button { cursor: pointer; border: 1px solid rgba(128,120,100,.45);',
            '  border-radius: 6px; background: transparent; color: inherit; padding: 3px 10px; font-size: 12px; }',
            '#' + OVERLAY_ID + ' .lumora-annot-pop button.lumora-primary { background: #7a6af0; border-color: #7a6af0; color: #fff; }'
        ].join('\n');
    }

    function buildOverlay() {
        var root = pickArticleRoot();
        var overlay = document.createElement('div');
        overlay.id = OVERLAY_ID;
        overlay.setAttribute('role', 'document');
        overlay.setAttribute('aria-label', 'Mode lecture Lumora');

        var style = document.createElement('style');
        style.textContent = readerCss();
        overlay.appendChild(style);

        var page = document.createElement('div');
        page.className = 'lumora-reader-page';

        var bar = document.createElement('div');
        bar.className = 'lumora-reader-bar';
        var hint = document.createElement('span');
        hint.className = 'lumora-reader-hint';
        hint.textContent = 'Selectionnez du texte pour le surligner ou le commenter.';
        var smaller = barButton('A−', 'Reduire le texte', function () { setFontSize(state.fontSize - 1); });
        var larger = barButton('A+', 'Agrandir le texte', function () { setFontSize(state.fontSize + 1); });
        var close = barButton('Quitter le mode lecture', 'Quitter le mode lecture', exitReader);
        bar.appendChild(hint); bar.appendChild(smaller); bar.appendChild(larger); bar.appendChild(close);
        page.appendChild(bar);

        var title = document.createElement('h1');
        title.className = 'lumora-reader-title';
        title.textContent = articleTitle(root);
        page.appendChild(title);

        var source = document.createElement('div');
        source.className = 'lumora-reader-source';
        source.textContent = location.href;
        page.appendChild(source);

        var content = document.createElement('div');
        content.className = 'lumora-reader-content';
        content.appendChild(cleanClone(root));
        // Pas de titre en double : si le premier titre de l'article répète
        // celui déjà affiché en tête de la vue, on le retire.
        var duplicateHeading = content.querySelector('h1, h2');
        if (duplicateHeading &&
            duplicateHeading.textContent.replace(/\s+/g, ' ').trim() === title.textContent) {
            duplicateHeading.parentNode.removeChild(duplicateHeading);
        }
        page.appendChild(content);

        overlay.appendChild(page);
        document.documentElement.appendChild(overlay);

        state.overlay = overlay;
        state.content = content;
        state.savedBodyOverflow = document.body ? document.body.style.overflow : '';
        if (document.body) { document.body.style.overflow = 'hidden'; }
        setFontSize(state.fontSize);

        overlay.addEventListener('mouseup', function () { setTimeout(onSelectionSettled, 10); });
        // La sélection au clavier (Maj+flèches) ou par un lecteur d'écran ne
        // produit aucun mouseup : suivre aussi selectionchange, avec anti-rebond.
        document.addEventListener('selectionchange', onSelectionChangedDebounced);
        content.addEventListener('click', function (event) {
            var mark = event.target && event.target.closest ? event.target.closest('mark.lumora-highlight') : null;
            if (mark) { openAnnotationPopup(mark); event.preventDefault(); }
        });
    }

    function barButton(text, label, onClick) {
        var button = document.createElement('button');
        button.type = 'button';
        button.textContent = text;
        button.setAttribute('aria-label', label);
        button.addEventListener('click', onClick);
        return button;
    }

    function setFontSize(px) {
        state.fontSize = Math.max(14, Math.min(26, px));
        if (state.content) { state.content.style.fontSize = state.fontSize + 'px'; }
    }

    var selectionDebounce = null;
    function onSelectionChangedDebounced() {
        if (!state.overlay) { return; }
        // Ne pas fermer/rouvrir la barre pendant la saisie d'un commentaire.
        var active = document.activeElement;
        if (active && active.closest && active.closest('.lumora-annot-pop')) { return; }
        if (selectionDebounce) { clearTimeout(selectionDebounce); }
        selectionDebounce = setTimeout(onSelectionSettled, 250);
    }

    function exitReader() {
        closePopups();
        document.removeEventListener('selectionchange', onSelectionChangedDebounced);
        if (state.overlay && state.overlay.parentNode) {
            state.overlay.parentNode.removeChild(state.overlay);
        }
        if (document.body) { document.body.style.overflow = state.savedBodyOverflow; }
        state.overlay = null;
        state.content = null;
        post({ t: 'lumora.annotation', a: 'reader-closed' });
    }

    // ── Ancrage des annotations dans le texte ────────────────────────────────
    // Le texte de référence est la concaténation des nœuds texte de l'article
    // épuré : c'est la même chaîne côté création (sélection) et côté
    // réapplication, ce qui rend l'ancrage stable.
    function collectTextNodes() {
        var nodes = [];
        var offsets = [];
        var total = 0;
        var walker = document.createTreeWalker(state.content, NodeFilter.SHOW_TEXT, null);
        var node;
        while ((node = walker.nextNode())) {
            nodes.push(node);
            offsets.push(total);
            total += node.data.length;
        }
        return { nodes: nodes, offsets: offsets, length: total };
    }

    // Cherche l'extrait dans le texte ; s'il apparaît plusieurs fois, garde
    // l'occurrence dont le contexte avant/après correspond le mieux.
    function locateQuote(fullText, quote, prefix, suffix) {
        var starts = [];
        var from = 0;
        while (true) {
            var at = fullText.indexOf(quote, from);
            if (at < 0) { break; }
            starts.push(at);
            from = at + 1;
            if (starts.length > 200) { break; }
        }
        if (starts.length === 0) { return -1; }
        if (starts.length === 1) { return starts[0]; }

        var best = starts[0];
        var bestScore = -1;
        for (var i = 0; i < starts.length; i++) {
            var start = starts[i];
            var score = 0;
            var before = fullText.substring(Math.max(0, start - prefix.length), start);
            var after = fullText.substring(start + quote.length, start + quote.length + suffix.length);
            score += commonSuffixLength(before, prefix) + commonPrefixLength(after, suffix);
            if (score > bestScore) { bestScore = score; best = start; }
        }
        return best;
    }

    function commonPrefixLength(a, b) {
        var n = Math.min(a.length, b.length);
        for (var i = 0; i < n; i++) { if (a[i] !== b[i]) { return i; } }
        return n;
    }

    function commonSuffixLength(a, b) {
        var n = Math.min(a.length, b.length);
        for (var i = 0; i < n; i++) { if (a[a.length - 1 - i] !== b[b.length - 1 - i]) { return i; } }
        return n;
    }

    // Enveloppe la plage [start, start+length) dans des <mark>, un par nœud
    // texte traversé (une sélection peut couvrir plusieurs paragraphes).
    function wrapRange(start, length, id, comment) {
        var map = collectTextNodes();
        var end = start + length;
        var marks = [];
        for (var i = 0; i < map.nodes.length && length > 0; i++) {
            var nodeStart = map.offsets[i];
            var node = map.nodes[i];
            var nodeEnd = nodeStart + node.data.length;
            if (nodeEnd <= start || nodeStart >= end) { continue; }

            var sliceStart = Math.max(0, start - nodeStart);
            var sliceEnd = Math.min(node.data.length, end - nodeStart);
            if (sliceEnd <= sliceStart) { continue; }

            var target = node;
            if (sliceStart > 0) { target = target.splitText(sliceStart); }
            if (sliceEnd - sliceStart < target.data.length) { target.splitText(sliceEnd - sliceStart); }

            var mark = document.createElement('mark');
            mark.className = 'lumora-highlight';
            mark.setAttribute('data-lumora-id', id);
            target.parentNode.replaceChild(mark, target);
            mark.appendChild(target);
            marks.push(mark);
        }
        setCommentIndicator(id, comment);
        return marks.length > 0;
    }

    function marksFor(id) {
        return state.content
            ? state.content.querySelectorAll('mark.lumora-highlight[data-lumora-id="' + id + '"]')
            : [];
    }

    function setCommentIndicator(id, comment) {
        var marks = marksFor(id);
        for (var i = 0; i < marks.length; i++) {
            marks[i].classList.toggle('lumora-has-comment', !!(comment && comment.trim()));
            marks[i].title = comment || '';
        }
    }

    function unwrapMarks(id) {
        var marks = marksFor(id);
        for (var i = 0; i < marks.length; i++) {
            var mark = marks[i];
            while (mark.firstChild) { mark.parentNode.insertBefore(mark.firstChild, mark); }
            mark.parentNode.removeChild(mark);
        }
    }

    function applyAnnotations() {
        var map = collectTextNodes();
        var fullText = textOf(map);
        var missing = 0;
        for (var i = 0; i < state.annotations.length; i++) {
            var annotation = state.annotations[i];
            var start = locateQuote(fullText, annotation.quote, annotation.prefix || '', annotation.suffix || '');
            if (start < 0) { missing++; continue; }
            wrapRange(start, annotation.quote.length, annotation.id, annotation.comment || '');
            // Le texte de référence n'est pas recalculé : envelopper dans des
            // <mark> ne change pas la concaténation des nœuds texte.
        }
        if (missing > 0) {
            post({ t: 'lumora.annotation', a: 'apply-missing', n: missing });
        }
    }

    function textOf(map) {
        var parts = [];
        for (var i = 0; i < map.nodes.length; i++) { parts.push(map.nodes[i].data); }
        return parts.join('');
    }

    // ── Sélection → barre Surligner / Commenter ──────────────────────────────
    function onSelectionSettled() {
        closeSelectionBar();
        if (!state.content) { return; }
        var selection = window.getSelection();
        if (!selection || selection.isCollapsed || selection.rangeCount === 0) { return; }
        // Copie figée : le Range de getRangeAt est une référence vivante que le
        // clic sur la barre ferait s'effondrer avant createAnnotation.
        var range = selection.getRangeAt(0).cloneRange();
        if (!state.content.contains(range.commonAncestorContainer)) { return; }
        var quote = range.toString();
        if (!quote || !quote.trim()) { return; }

        var rect = range.getBoundingClientRect();
        var bar = document.createElement('div');
        bar.className = 'lumora-annot-pop lumora-selection-bar';
        // Sans ça, le mousedown sur un bouton désélectionne le texte AVANT le clic.
        bar.addEventListener('mousedown', function (event) { event.preventDefault(); });
        var row = document.createElement('div');
        row.className = 'lumora-row';
        row.appendChild(popButton('Surligner', true, function () {
            createAnnotation(range, '');
        }));
        row.appendChild(popButton('Commenter', false, function () {
            createAnnotation(range, null); // null = ouvrir la saisie du commentaire
        }));
        bar.appendChild(row);
        positionPopup(bar, rect);
    }

    function popButton(text, primary, onClick) {
        var button = document.createElement('button');
        button.type = 'button';
        button.textContent = text;
        if (primary) { button.className = 'lumora-primary'; }
        button.addEventListener('click', function (event) {
            event.stopPropagation();
            onClick();
        });
        return button;
    }

    function positionPopup(popup, rect) {
        state.overlay.appendChild(popup);
        var scrollTop = state.overlay.scrollTop;
        var top = rect.bottom + scrollTop + 8;
        var left = Math.max(12, Math.min(rect.left, state.overlay.clientWidth - 360));
        popup.style.top = top + 'px';
        popup.style.left = left + 'px';
    }

    function closeSelectionBar() {
        var bars = document.querySelectorAll('.lumora-selection-bar');
        for (var i = 0; i < bars.length; i++) { bars[i].parentNode.removeChild(bars[i]); }
    }

    function closePopups() {
        var pops = document.querySelectorAll('.lumora-annot-pop');
        for (var i = 0; i < pops.length; i++) { pops[i].parentNode.removeChild(pops[i]); }
    }

    // Crée l'annotation depuis la sélection : ancre calculée dans le texte de
    // référence, surlignage immédiat sous un id provisoire, envoi au C# qui
    // confirmera l'id définitif (confirmAdd).
    function createAnnotation(range, comment) {
        var map = collectTextNodes();
        var fullText = textOf(map);
        var pre = document.createRange();
        pre.selectNodeContents(state.content);
        pre.setEnd(range.startContainer, range.startOffset);
        var start = pre.toString().length;
        var quote = range.toString();
        if (!quote) { return; }

        var prefix = fullText.substring(Math.max(0, start - 40), start);
        var suffix = fullText.substring(start + quote.length, start + quote.length + 40);
        var tempId = 'pending-' + (++pendingSeq);

        window.getSelection().removeAllRanges();
        closeSelectionBar();
        wrapRange(start, quote.length, tempId, comment || '');

        var record = {
            id: tempId, quote: quote, prefix: prefix, suffix: suffix,
            comment: comment || ''
        };
        state.annotations.push(record);
        post({
            t: 'lumora.annotation', a: 'add', ref: tempId,
            u: location.href, ti: document.title,
            q: quote, p: prefix, s: suffix, c: comment || ''
        });

        if (comment === null) {
            var marks = marksFor(tempId);
            if (marks.length > 0) { openAnnotationPopup(marks[0]); }
        }
    }

    // ── Fiche d'une annotation (commentaire, suppression) ────────────────────
    function annotationById(id) {
        for (var i = 0; i < state.annotations.length; i++) {
            if (state.annotations[i].id === id) { return state.annotations[i]; }
        }
        return null;
    }

    function openAnnotationPopup(mark) {
        closePopups();
        var id = mark.getAttribute('data-lumora-id');
        var annotation = annotationById(id);
        if (!annotation) { return; }

        var pop = document.createElement('div');
        pop.className = 'lumora-annot-pop';
        var label = document.createElement('div');
        label.style.opacity = '.7';
        label.textContent = 'Commentaire :';
        var textarea = document.createElement('textarea');
        textarea.value = annotation.comment || '';
        textarea.setAttribute('aria-label', 'Commentaire de l’annotation');

        var row = document.createElement('div');
        row.className = 'lumora-row';
        row.appendChild(popButton('Supprimer', false, function () {
            unwrapMarks(id);
            closePopups();
            state.annotations = state.annotations.filter(function (item) { return item.id !== id; });
            post({ t: 'lumora.annotation', a: 'remove', id: id });
        }));
        row.appendChild(popButton('Fermer', false, closePopups));
        row.appendChild(popButton('Enregistrer', true, function () {
            annotation.comment = textarea.value;
            setCommentIndicator(id, annotation.comment);
            closePopups();
            post({ t: 'lumora.annotation', a: 'comment', id: id, c: textarea.value });
        }));

        pop.appendChild(label);
        pop.appendChild(textarea);
        pop.appendChild(row);
        positionPopup(pop, mark.getBoundingClientRect());
        textarea.focus();
    }

    // ── API appelée par le C# ────────────────────────────────────────────────
    function openWith(annotations) {
        state.annotations = (annotations || []).map(function (item) {
            return {
                id: item.id, quote: item.quote,
                prefix: item.prefix || '', suffix: item.suffix || '',
                comment: item.comment || ''
            };
        });
        buildOverlay();
        applyAnnotations();
        return 'on';
    }

    window.__lumoraReader = {
        isOpen: function () { return !!state.overlay; },

        // Ouvre (ou ferme si déjà ouvert) le mode lecture avec les annotations
        // connues de la page. Retourne 'on' / 'off' pour la barre d'état.
        toggle: function (annotations) {
            if (state.overlay) { exitReader(); return 'off'; }
            return openWith(annotations);
        },

        // Ouverture garantie (reprise depuis le panneau Notes) : si le mode
        // lecture est déjà affiché, il est reconstruit avec les annotations à
        // jour au lieu d'être fermé par un bascule aveugle.
        open: function (annotations) {
            if (state.overlay) { exitReader(); }
            return openWith(annotations);
        },

        // Le C# a enregistré l'annotation : remplace l'id provisoire.
        confirmAdd: function (tempId, realId) {
            var annotation = annotationById(tempId);
            if (annotation) { annotation.id = realId; }
            var marks = marksFor(tempId);
            for (var i = 0; i < marks.length; i++) {
                marks[i].setAttribute('data-lumora-id', realId);
            }
        }
    };
})();
