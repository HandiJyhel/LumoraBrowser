(function () {
    var map = __NOVA_TRANSLATIONS__;
    var applied = 0;
    for (var id in map) {
        var el = document.querySelector('[data-nova-tid="' + id + '"]');
        if (el) {
            el.textContent = map[id];
            applied++;
        }
    }
    return applied;
})();
