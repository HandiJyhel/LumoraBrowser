namespace Lumora.WinUI.Accessibility;

// Aide accessibilite "Reordonner sans glisser" (chantier "Ajustement n2",
// 2026-09-11 - voir AccessibilityClickToReorderEnabled, Models/UiSettings.cs) :
// un clic decroche un element, un second clic sur un emplacement le repose,
// sans avoir a maintenir le bouton de la souris enfonce pendant un glisser-
// depose. Logique pure et generique (pas specifique a la barre de favoris,
// premiere liste a la consommer - voir MainWindow.Bookmarks.cs) : ne connait
// que des indices/identifiants, jamais un controle XAML, pour rester testable
// sans WebView2/WinUI (meme esprit que ContextMenuOrdering.cs).
internal static class ClickToReorder
{
    // Un "gap" (emplacement ou reposer l'element decroche) se situe entre
    // deux elements de la liste AVANT tout retrait ; gapIndex va de 0 (avant
    // le premier element) a items.Count (apres le dernier). Les 2 gaps qui
    // encadrent directement l'element decroche ne deplacent rien (le reposer
    // juste avant ou juste apres sa propre position actuelle est un no-op) -
    // on les exclut plutot que de laisser l'utilisateur cliquer pour rien.
    public static bool IsDropTarget(int gapIndex, int pickedIndex) =>
        pickedIndex >= 0 && gapIndex != pickedIndex && gapIndex != pickedIndex + 1;

    // Traduit un gap (index dans la liste affichee, decrochage compris) en
    // "beforeId" au sens de BookmarkStore.ReorderNode : l'identifiant devant
    // lequel reinserer l'element decroche, ou null pour la derniere position.
    public static string? BeforeIdForGap(IReadOnlyList<string> orderedIds, int gapIndex) =>
        gapIndex >= 0 && gapIndex < orderedIds.Count ? orderedIds[gapIndex] : null;

    // Position humaine (1-based) qu'occupera l'element une fois repose a ce
    // gap - pour l'annonce d'accessibilite ("repose en position N"), qui doit
    // rester correcte que le gap soit avant ou apres la position d'origine.
    public static int DisplayPositionForGap(int gapIndex, int pickedIndex) =>
        (gapIndex > pickedIndex ? gapIndex - 1 : gapIndex) + 1;
}
