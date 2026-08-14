namespace Lumora.WinUI;

// Logique pure de "sur quel voisin se trouve le pointeur" pour le
// glisser-deposer manuel de la barre d'outils et des favoris (2026-08-14).
// Remplace le systeme CanDrag/DragStarting natif de WinUI 3, qui ne
// fonctionne pas sur des controles interactifs comme Button - confirme par
// la documentation officielle Microsoft ("WinUI3 is intentionally designed
// to only allow dragging of specific, non-interactive content... Since
// Button is an interactive control meant for user input, it's excluded from
// the default drag-and-drop behavior"), voir MEMORY.md.
// Isolee de tout type WinUI expres : testable directement (donnees pures en
// entree/sortie), sans avoir besoin d'instancier de vrais FrameworkElement.
internal static class HorizontalDragReorderMath
{
    // items : position/largeur actuelles de chaque element du rang horizontal
    // (favoris ou boutons d'outils), dans le repere du conteneur parent.
    // draggedId : l'element actuellement glisse, exclu de la recherche (on ne
    // se compare jamais a soi-meme). pointerX : position actuelle du pointeur
    // dans ce meme repere.
    // Retourne l'id de l'element dont le CENTRE est le plus proche du
    // pointeur, ou null si items est vide ou ne contient que l'element
    // glisse lui-meme.
    public static string? FindTargetId(
        IReadOnlyList<(string Id, double Left, double Width)> items,
        string draggedId,
        double pointerX)
    {
        string? bestId = null;
        var bestDistance = double.MaxValue;

        foreach (var item in items)
        {
            if (item.Id == draggedId) continue;

            var center = item.Left + item.Width / 2;
            var distance = Math.Abs(pointerX - center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestId = item.Id;
            }
        }

        return bestId;
    }

    // Vrai des que le deplacement depuis le point de depart depasse le seuil
    // (distance de Manhattan, suffisante ici - pas besoin d'une vraie
    // distance euclidienne pour un simple seuil "a bouge/n'a pas bouge") :
    // en dessous, on laisse le clic normal du bouton se produire (ouvrir le
    // favori, action du module...), au-dessus, on entre en glissement.
    public static bool ExceedsDragThreshold(double startX, double startY, double currentX, double currentY, double threshold) =>
        Math.Abs(currentX - startX) + Math.Abs(currentY - startY) >= threshold;
}
