using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Lumora.WinUI;

// Glisser-deposer direct des favoris dans la barre (2026-08-13, demande
// explicite : "que les favoris aient le même comportement que dans tout autre
// navigateur"). A la difference de la reorganisation de la barre d'outils
// (MainWindow.ToolbarCustomization.cs), AUCUN mode dedie a activer - le
// glisser fonctionne en permanence, comme dans Chrome/Edge/Firefox.
//
// Suivi manuellement au pointeur (2026-08-14, remplace CanDrag/DragStarting) :
// confirme non fonctionnel sur un Button par la documentation officielle
// Microsoft ("Button is an interactive control... excluded from the default
// drag-and-drop behavior") - voir HorizontalDragReorderMath.cs et MEMORY.md.
// Contrairement a la barre d'outils, RenderBookmarksBar() RECREE les boutons
// a chaque rafraichissement (CreateBookmarkBarButton retourne de nouvelles
// instances a chaque appel) - impossible donc de reordonner "en direct"
// pendant le glisser sans perdre la capture du pointeur en cours de route
// (le bouton glisse serait detruit, la capture deviendrait orpheline). La
// reorganisation reelle n'a lieu qu'au relachement (PointerReleased) ; une
// legere transparence signale visuellement le glissement en cours.
//
// Capture du pointeur sur le PANNEAU, pas sur le bouton (2026-08-14, 3e
// correctif sur ce mecanisme) : la trace de diagnostic reelle
// (winui-runtime-trace.log fourni par l'utilisateur) a prouve que
// _bookmarkIsDragging repassait deja a false AVANT PointerReleased -
// impossible sauf si PointerCaptureLost se declenchait silencieusement en
// plein glissement. Cause : un Button gere en interne sa propre capture du
// pointeur pour son etat visuel Pressed/Click, et la relache des que le
// pointeur sort de ses limites - ce qui arrive tres tot dans un glissement
// horizontal. Notre appel a CapturePointer sur CE MEME bouton se faisait
// donc ecraser par la logique interne du controle. Un StackPanel n'a aucune
// gestion de capture interne : capturer sur le panneau (BookmarksBarPanel ou
// BookmarksBottomBarPanel selon la position de la barre) elimine le
// conflit. Consequence directe : PointerMoved/PointerReleased/
// PointerCaptureLost ne sont plus cables PAR BOUTON (ils ne recevraient de
// toute facon plus rien, la capture appartenant desormais au panneau) mais
// UNE SEULE FOIS sur chacun des 2 panneaux, cables au demarrage de la
// fenetre (voir WireBookmarkDragPanels, appele depuis MainWindow.xaml.cs).
//
// Cable sur chaque bouton non-racine par CreateBookmarkBarButton
// (MainWindow.Bookmarks.cs, +une ligne seulement - toute la logique vit
// ici pour ne pas alourdir ce fichier deja consequent).
// Portee volontairement limitee a un reordonnancement ENTRE FRERES DU MEME
// PARENT (voir BookmarkStore.ReorderNode, Models/Bookmarks.cs) : deposer une
// icone A L'INTERIEUR d'un dossier existant (avec ouverture automatique du
// dossier au survol) est laisse hors perimetre pour cette passe.
public sealed partial class MainWindow : Window
{
    private const double BookmarkDragThreshold = 8;

    private string? _bookmarkDraggedId;
    private Button? _bookmarkDraggedButton;
    private FrameworkElement? _bookmarkDragPanel;
    private bool _bookmarkIsDragging;
    private double _bookmarkDragStartX;
    private double _bookmarkDragStartY;
    private string? _bookmarkPendingTargetId;
    // Consulte par BookmarkBarButton_Click (MainWindow.Bookmarks.cs) : un
    // clic qui suit immediatement un vrai glissement ne doit pas aussi
    // ouvrir le favori qu'on vient de deplacer.
    private bool _bookmarkSuppressNextClick;

    /// <summary>
    /// Cable UNE SEULE FOIS (constructeur) les 3 evenements de suivi du
    /// glisser sur les 2 panneaux possibles de la barre de favoris (haut/bas
    /// selon <c>_bookmarksBarPosition</c>). Ni l'un ni l'autre n'est un
    /// Button : une souscription normale suffit, pas besoin de
    /// handledEventsToo (reserve a BookmarkBarButton_PointerPressed, le seul
    /// evenement qui nait reellement sur un Button).
    /// </summary>
    private void WireBookmarkDragPanels()
    {
        foreach (var panel in new FrameworkElement?[] { BookmarksBarPanel, BookmarksBottomBarPanel })
        {
            if (panel is null) continue;
            panel.PointerMoved += BookmarkDragPanel_PointerMoved;
            panel.PointerReleased += BookmarkDragPanel_PointerReleased;
            panel.PointerCaptureLost += BookmarkDragPanel_PointerCaptureLost;
        }
    }

    private void BookmarkBarButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Trace de diagnostic (2026-08-14, LUMORA_TRACE_STARTUP=1) : posee
        // AVANT tout retour anticipe expres, pour distinguer "le gestionnaire
        // n'est jamais appele" de "il est appele mais sort tot". A retirer
        // une fois confirme fonctionnel en conditions reelles.
        WinUiRuntimeTrace.Write($"[drag-favoris] PointerPressed recu, sender={sender?.GetType().Name}");

        if (sender is not Button { Tag: BookmarkNode node } btn)
        {
            WinUiRuntimeTrace.Write("[drag-favoris] PointerPressed : sender n'est pas un Button avec BookmarkNode, abandon");
            return;
        }
        if (btn.Parent is not FrameworkElement panel)
        {
            WinUiRuntimeTrace.Write("[drag-favoris] PointerPressed : pas de parent FrameworkElement, abandon");
            return;
        }

        WinUiRuntimeTrace.Write($"[drag-favoris] PointerPressed OK pour '{node.Id}', capture du pointeur SUR LE PANNEAU (pas le bouton)");
        _bookmarkDraggedId = node.Id;
        _bookmarkDraggedButton = btn;
        _bookmarkDragPanel = panel;
        _bookmarkIsDragging = false;
        _bookmarkPendingTargetId = null;
        var point = e.GetCurrentPoint(panel).Position;
        _bookmarkDragStartX = point.X;
        _bookmarkDragStartY = point.Y;
        // Capture sur le PANNEAU, pas sur btn (voir commentaire en tete de
        // fichier) : un Button relache sa propre capture des que le pointeur
        // sort de ses limites, ce qui arrive des les premiers pixels d'un
        // glissement horizontal.
        panel.CapturePointer(e.Pointer);
    }

    private void BookmarkDragPanel_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_bookmarkDraggedId is null || _bookmarkDragPanel is not { } panel) return;

        var point = e.GetCurrentPoint(panel).Position;

        if (!_bookmarkIsDragging)
        {
            if (!HorizontalDragReorderMath.ExceedsDragThreshold(
                    _bookmarkDragStartX, _bookmarkDragStartY, point.X, point.Y, BookmarkDragThreshold))
                return;
            _bookmarkIsDragging = true;
            WinUiRuntimeTrace.Write($"[drag-favoris] Seuil depasse, glissement demarre pour '{_bookmarkDraggedId}'");
            if (_bookmarkDraggedButton is not null) _bookmarkDraggedButton.Opacity = 0.55;
        }

        if (panel is not Panel childrenPanel) return;

        var items = childrenPanel.Children
            .OfType<Button>()
            .Where(child => child.Tag is BookmarkNode)
            .Select(child =>
            {
                var origin = child.TransformToVisual(panel).TransformPoint(new Windows.Foundation.Point(0, 0));
                return (Id: ((BookmarkNode)child.Tag).Id, Left: origin.X, Width: child.ActualWidth);
            })
            .ToList();

        _bookmarkPendingTargetId = HorizontalDragReorderMath.FindTargetId(items, _bookmarkDraggedId, point.X);
    }

    private void BookmarkDragPanel_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        WinUiRuntimeTrace.Write($"[drag-favoris] PointerReleased, isDragging={_bookmarkIsDragging}, cible={_bookmarkPendingTargetId ?? "(aucune)"}");

        if (sender is UIElement releasedPanel) releasedPanel.ReleasePointerCapture(e.Pointer);
        if (_bookmarkDraggedButton is not null) _bookmarkDraggedButton.Opacity = 1;

        _bookmarkSuppressNextClick = _bookmarkIsDragging;

        if (_bookmarkIsDragging && _bookmarkDraggedId is { } draggedId && _bookmarkPendingTargetId is { } targetId)
        {
            // Differe la reorganisation reelle : eviter de reconstruire la
            // barre (ReloadBookmarks detruit et recree tous les boutons)
            // pendant que ce meme gestionnaire d'evenement, declenche par le
            // bouton glisse, est encore sur la pile d'appels.
            DispatcherQueue.TryEnqueue(() =>
            {
                // ReorderNode refuse silencieusement (retourne false) tout
                // deplacement hors perimetre (parent different, cible racine,
                // etc.) - rien a rafraichir dans ce cas.
                if (_bookmarks.ReorderNode(draggedId, targetId))
                {
                    ReloadBookmarks();
                }
            });
        }

        _bookmarkDraggedId = null;
        _bookmarkDraggedButton = null;
        _bookmarkDragPanel = null;
        _bookmarkIsDragging = false;
        _bookmarkPendingTargetId = null;
    }

    private void BookmarkDragPanel_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        WinUiRuntimeTrace.Write($"[drag-favoris] PointerCaptureLost (panneau), isDragging={_bookmarkIsDragging}");

        if (_bookmarkDraggedButton is not null) _bookmarkDraggedButton.Opacity = 1;
        _bookmarkDraggedId = null;
        _bookmarkDraggedButton = null;
        _bookmarkDragPanel = null;
        _bookmarkIsDragging = false;
        _bookmarkPendingTargetId = null;
    }
}
