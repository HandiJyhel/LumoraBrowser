namespace PulseBrowser.WinUI;

internal static class TabGroupOrdering
{
    public static int NextColorIndex(int existingGroupCount, int paletteSize = 6) =>
        existingGroupCount % paletteSize;

    // Pas de vrai glisser-déposer d'onglets dans ce navigateur : quand un onglet
    // rejoint (ou quitte) un groupe, on le replace juste après le dernier onglet
    // du groupe cible pour que le rail vertical affiche des grappes visuellement
    // contiguës, sans toucher à l'ordre relatif des autres onglets.
    public static List<int> ReorderForGroup(
        IReadOnlyList<int> currentOrder,
        int movingTabId,
        int? targetGroupId,
        IReadOnlyDictionary<int, int?> groupByTabId)
    {
        var withoutMoving = currentOrder.Where(id => id != movingTabId).ToList();

        if (targetGroupId is null)
        {
            var originalIndex = currentOrder.ToList().IndexOf(movingTabId);
            var insertAt = Math.Min(originalIndex, withoutMoving.Count);
            if (insertAt < 0)
            {
                insertAt = withoutMoving.Count;
            }
            withoutMoving.Insert(insertAt, movingTabId);
            return withoutMoving;
        }

        var lastGroupMemberIndex = -1;
        for (var i = 0; i < withoutMoving.Count; i++)
        {
            if (groupByTabId.TryGetValue(withoutMoving[i], out var groupId) && groupId == targetGroupId)
            {
                lastGroupMemberIndex = i;
            }
        }

        withoutMoving.Insert(lastGroupMemberIndex + 1, movingTabId);
        return withoutMoving;
    }
}
