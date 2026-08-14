namespace Lumora.WinUI;

// Logique pure du deplacement "d'un cran" des favoris (2026-08-14, alternative
// au glisser demandee explicitement par l'utilisateur apres 3 correctifs
// reels du glisser-depose qui echouaient encore en conditions reelles).
// Isolee de tout type WinUI expres (meme principe que
// HorizontalDragReorderMath.cs) pour rester testable avec de simples listes,
// sans instancier BookmarkStore (qui depend de Microsoft.UI.Xaml et
// Microsoft.Web.WebView2.Core, absents du projet de test "pur" - voir
// Lumora.Tests.csproj). Consommee par BookmarkStore.MoveNodeAdjacent
// (Models/Bookmarks.cs), qui se charge ensuite d'appeler ReorderNode (deja
// teste) avec le "beforeId" ici calcule.
internal static class AdjacentMoveMath
{
    /// <summary>
    /// Calcule la cible ("beforeId" a passer a ReorderNode) pour deplacer
    /// <paramref name="movedId"/> d'UN cran parmi <paramref name="orderedSiblingIds"/>
    /// (deja tries par position, movedId inclus). <c>CanMove=false</c> si
    /// movedId est absent de la liste ou deja a l'extremite demandee (rien a
    /// faire). <c>BeforeId=null</c> signifie "placer en dernier" (ReorderNode
    /// interprete beforeId=null/vide comme "en dernier").
    /// </summary>
    public static (bool CanMove, string? BeforeId) ComputeTarget(
        IReadOnlyList<string> orderedSiblingIds, string movedId, bool moveForward)
    {
        var index = -1;
        for (var i = 0; i < orderedSiblingIds.Count; i++)
        {
            if (orderedSiblingIds[i] == movedId)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return (false, null);
        }

        if (!moveForward)
        {
            if (index == 0)
            {
                return (false, null);
            }
            return (true, orderedSiblingIds[index - 1]);
        }

        if (index >= orderedSiblingIds.Count - 1)
        {
            return (false, null);
        }

        var beforeId = index + 2 < orderedSiblingIds.Count ? orderedSiblingIds[index + 2] : null;
        return (true, beforeId);
    }
}
