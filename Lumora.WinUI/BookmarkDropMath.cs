namespace Lumora.WinUI;

// Logique pure du glisser-deposer "vers un dossier" du gestionnaire de
// favoris (2026-08-31, "gestion des favoris a chier" - jusqu'ici seul un
// reordonnancement entre freres du meme parent existait, voir
// HorizontalDragReorderMath.cs pour la barre et AdjacentMoveMath.cs pour le
// menu contextuel). Isolee de tout type WinUI expres (meme principe que les
// deux fichiers ci-dessus) : testable avec de simples tuples, sans instancier
// de vrais FrameworkElement ni BookmarkStore.
internal static class BookmarkDropMath
{
    public readonly record struct DropItem(string Id, bool IsFolder, double Left, double Top, double Width, double Height);

    // Marge interieure (ratio de la largeur/hauteur de la vignette) au-dela de
    // laquelle un survol compte comme "ranger DANS ce dossier" plutot que
    // "reordonner juste avant lui". Sans cette marge, la moindre imprecision
    // au bord d'une vignette dossier deplacerait le favori dedans au lieu de
    // le reordonner - la marge donne une zone de reordonnancement franche sur
    // le pourtour, comme la plupart des gestionnaires de fichiers.
    public const double InnerMarginRatio = 0.22;

    // Grille de vignettes (BookmarksList) : trouve, parmi items (hors
    // draggedId), celui dont le CENTRE est le plus proche du pointeur (meme
    // principe que HorizontalDragReorderMath.FindTargetId, etendu a 2
    // dimensions car une grille peut comporter plusieurs rangees).
    // IntoFolder est vrai uniquement quand la cible est un dossier ET que le
    // pointeur est dans sa zone interieure (voir InnerMarginRatio) - sinon le
    // geste reste un reordonnancement "avant cette cible" parmi les freres
    // (meme convention que ReorderNode).
    public static (string? TargetId, bool IntoFolder) ComputeGridDropTarget(
        IReadOnlyList<DropItem> items, string draggedId, double pointerX, double pointerY)
    {
        DropItem? best = null;
        var bestDistanceSquared = double.MaxValue;

        foreach (var item in items)
        {
            if (item.Id == draggedId) continue;

            var centerX = item.Left + item.Width / 2;
            var centerY = item.Top + item.Height / 2;
            var dx = pointerX - centerX;
            var dy = pointerY - centerY;
            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                best = item;
            }
        }

        if (best is not { } target)
        {
            return (null, false);
        }

        return (target.Id, target.IsFolder && IsInsideInnerZone(target, pointerX, pointerY));
    }

    private static bool IsInsideInnerZone(DropItem item, double pointerX, double pointerY)
    {
        var marginX = item.Width * InnerMarginRatio;
        var marginY = item.Height * InnerMarginRatio;
        return pointerX >= item.Left + marginX && pointerX <= item.Left + item.Width - marginX &&
               pointerY >= item.Top + marginY && pointerY <= item.Top + item.Height - marginY;
    }

    // Arborescence des dossiers (BookmarkFoldersList) : chaque ligne y est
    // TOUJOURS un dossier (BookmarkTreePresenter.FlattenFolders ne liste que
    // des BookmarkKind.Folder) - pas besoin de marge interieure ni de calcul
    // de distance, un simple test de contenance verticale suffit ("sur
    // quelle ligne est le pointeur, le cas echeant").
    public static string? FindTreeRowAt(IReadOnlyList<(string Id, double Top, double Height)> rows, double pointerY)
    {
        foreach (var row in rows)
        {
            if (pointerY >= row.Top && pointerY <= row.Top + row.Height)
            {
                return row.Id;
            }
        }

        return null;
    }
}
