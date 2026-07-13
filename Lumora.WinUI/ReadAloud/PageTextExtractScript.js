(function () {
    var selector = "p, li, h1, h2, h3, h4, h5, h6, td, th, blockquote, figcaption, dd, dt";
    var nodes = document.querySelectorAll(selector);
    var out = [];
    var count = 0;
    var maxBlocks = 150;
    for (var i = 0; i < nodes.length && count < maxBlocks; i++) {
        var el = nodes[i];
        if (el.closest("script, style, noscript")) continue;
        if (el.children.length > 0) continue; // évite de traduire un parent ET ses enfants
        var text = (el.textContent || "").replace(/\s+/g, " ").trim();
        if (text.length < 2 || text.length > 400) continue;
        if (!/[a-zA-Z]/.test(text)) continue;
        var id = "nova-tid-" + count;
        el.setAttribute("data-nova-tid", id);
        out.push({ id: id, text: text });
        count++;
    }
    return out;
})();
