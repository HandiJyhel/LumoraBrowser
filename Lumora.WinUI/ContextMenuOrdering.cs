namespace Lumora.WinUI;

// Logique pure de filtrage/reordonnancement du menu contextuel (2026-09-11,
// chantier "Reglages sur-mesure") - extraite dans sa propre classe pour
// etre testable sans WebView2 (voir MainWindow.PageContextMenu.cs, qui
// l'appelle avec les vrais CoreWebView2ContextMenuItem). Un separateur/
// sous-menu (nameSelector renvoie null ou vide) n'est jamais masque ni
// deplace pour son propre compte - seuls les elements nommes le sont,
// les autres suivent au fil de l'eau.
internal static class ContextMenuOrdering
{
    public static List<T> Apply<T>(
        IEnumerable<T> items,
        Func<T, string?> nameSelector,
        IReadOnlyCollection<string> hiddenNames,
        IReadOnlyList<string> order)
    {
        var kept = new List<T>();
        foreach (var item in items)
        {
            var name = nameSelector(item);
            if (!string.IsNullOrEmpty(name) && hiddenNames.Contains(name))
            {
                continue;
            }
            kept.Add(item);
        }

        if (order.Count == 0 || kept.Count == 0)
        {
            return kept;
        }

        // Les elements explicitement ordonnes passent en tete, dans l'ordre
        // choisi ; tout le reste (elements non ordonnes, separateurs, sous-
        // menus) suit ensuite dans son ordre relatif d'origine.
        var placed = new HashSet<int>();
        var result = new List<T>(kept.Count);
        foreach (var name in order)
        {
            for (var i = 0; i < kept.Count; i++)
            {
                if (placed.Contains(i))
                {
                    continue;
                }
                if (string.Equals(nameSelector(kept[i]), name, StringComparison.Ordinal))
                {
                    result.Add(kept[i]);
                    placed.Add(i);
                    break;
                }
            }
        }
        for (var i = 0; i < kept.Count; i++)
        {
            if (!placed.Contains(i))
            {
                result.Add(kept[i]);
            }
        }
        return result;
    }
}
