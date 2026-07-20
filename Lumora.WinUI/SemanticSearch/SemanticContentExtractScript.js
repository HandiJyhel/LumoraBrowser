(function () {
    var selector = "p, li, h1, h2, h3, h4, h5, h6, td, th, blockquote, figcaption, dd, dt";
    var nodes = document.querySelectorAll(selector);
    var parts = [];
    var total = 0;
    var maxChars = 3000;
    for (var i = 0; i < nodes.length && total < maxChars; i++) {
        var el = nodes[i];
        if (el.closest("script, style, noscript")) continue;
        if (el.children.length > 0) continue; // évite les doublons parent+enfant
        var text = (el.textContent || "").replace(/\s+/g, " ").trim();
        if (text.length < 2) continue;
        parts.push(text);
        total += text.length + 1;
    }
    return parts.join(" ").slice(0, maxChars);
})();
