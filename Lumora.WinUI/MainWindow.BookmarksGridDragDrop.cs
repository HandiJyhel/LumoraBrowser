using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

// Glisser-deposer direct dans le gestionnaire de favoris (2026-08-31, session
// "gestion des favoris" - jusqu'ici seul un reordonnancement d'UN cran via le
// menu contextuel existait, voir "Déplacer avant/après" dans
// MainWindow.BookmarksFlyouts.cs). Meme architecture que le glisser de la
// barre de favoris (MainWindow.BookmarksDragDrop.cs) : suivi manuel du
// pointeur, capture sur un PANNEAU plutot que sur un controle interactif
// (ListViewItem gere sa propre capture pour son etat visuel Pressed/
// selection, meme piege documente pour Button - voir MEMORY.md) - ici
// BookmarksList.ItemsPanelRoot.
//
// Portee volontairement limitee aux ENFANTS DIRECTS DU DOSSIER OUVERT (comme
// la grille elle-meme les affiche) : deposer sur une vignette-dossier de
// cette meme grille range dedans (favicon dossier survolee = surbrillance),
// deposer entre deux vignettes reordonne. Atteindre un dossier AILLEURS dans
// l'arborescence (pas visible dans le dossier ouvert) passe par "Déplacer
// vers…" (MainWindow.BookmarksMoveTo.cs), deja capable de cibler n'importe
// quel dossier - le glisser reste local, comme dans Chrome/Firefox.
public sealed partial class MainWindow
{
    private const double BookmarkGridDragThreshold = 8;

    private string? _bookmarkGridDraggedId;
    private ListViewItem? _bookmarkGridDraggedContainer;
    private bool _bookmarkGridIsDragging;
    private double _bookmarkGridDragStartX;
    private double _bookmarkGridDragStartY;
    private string? _bookmarkGridPendingTargetId;
    private bool _bookmarkGridPendingIntoFolder;
    private ListViewItem? _bookmarkGridHighlightedContainer;
    private Brush? _bookmarkGridHighlightedOriginalBackground;
    private Panel? _bookmarkGridWiredPanel;

    // Cable une seule fois par instance de conteneur (recycle par
    // virtualisation - le marqueur Tag empeche un double-cablage a chaque
    // ContainerContentChanging, meme principe que CreateBookmarkBarButton qui,
    // lui, cree une instance de bouton par favori et n'a donc pas ce
    // probleme de recyclage).
    private void BookmarksList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not ListViewItem { Tag: not "drag-wired" } container) return;
        container.Tag = "drag-wired";
        container.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(BookmarkGridItem_PointerPressed), true);
    }

    // ItemsPanelRoot n'existe qu'apres le premier rendu de la liste ET change
    // d'instance a chaque bascule de mode d'affichage (ApplyBookmarkViewMode
    // remplace ItemsPanel) - cable paresseusement au premier PointerPressed
    // plutot qu'une fois pour toutes au demarrage comme WireBookmarkDragPanels
    // (la barre, elle, a des panneaux fixes definis dans le XAML).
    private void EnsureBookmarkGridDragPanelWired()
    {
        if (BookmarksList.ItemsPanelRoot is not { } panel || ReferenceEquals(_bookmarkGridWiredPanel, panel))
        {
            return;
        }

        panel.PointerMoved += BookmarkGridDragPanel_PointerMoved;
        panel.PointerReleased += BookmarkGridDragPanel_PointerReleased;
        panel.PointerCaptureLost += BookmarkGridDragPanel_PointerCaptureLost;
        _bookmarkGridWiredPanel = panel;
    }

    private void BookmarkGridItem_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ListViewItem { Content: BookmarkListItem item } container) return;
        if (item.Node.IsRoot) return;

        EnsureBookmarkGridDragPanelWired();
        if (BookmarksList.ItemsPanelRoot is not { } panel) return;

        _bookmarkGridDraggedId = item.Node.Id;
        _bookmarkGridDraggedContainer = container;
        _bookmarkGridIsDragging = false;
        _bookmarkGridPendingTargetId = null;
        _bookmarkGridPendingIntoFolder = false;
        var point = e.GetCurrentPoint(panel).Position;
        _bookmarkGridDragStartX = point.X;
        _bookmarkGridDragStartY = point.Y;
        panel.CapturePointer(e.Pointer);
    }

    private void BookmarkGridDragPanel_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_bookmarkGridDraggedId is null || sender is not Panel panel) return;

        var point = e.GetCurrentPoint(panel).Position;

        if (!_bookmarkGridIsDragging)
        {
            if (!HorizontalDragReorderMath.ExceedsDragThreshold(
                    _bookmarkGridDragStartX, _bookmarkGridDragStartY, point.X, point.Y, BookmarkGridDragThreshold))
                return;
            _bookmarkGridIsDragging = true;
            if (_bookmarkGridDraggedContainer is not null) _bookmarkGridDraggedContainer.Opacity = 0.55;
        }

        var items = panel.Children
            .OfType<ListViewItem>()
            .Where(child => child.Content is BookmarkListItem)
            .Select(child =>
            {
                var content = (BookmarkListItem)child.Content;
                var origin = child.TransformToVisual(panel).TransformPoint(new Windows.Foundation.Point(0, 0));
                return (Container: child, Drop: new BookmarkDropMath.DropItem(
                    content.Node.Id,
                    content.Node.Kind == BookmarkKind.Folder,
                    origin.X, origin.Y, child.ActualWidth, child.ActualHeight));
            })
            .ToList();

        var (targetId, into) = BookmarkDropMath.ComputeGridDropTarget(
            items.ConvertAll(entry => entry.Drop), _bookmarkGridDraggedId, point.X, point.Y);

        _bookmarkGridPendingTargetId = targetId;
        _bookmarkGridPendingIntoFolder = into;

        var highlighted = into ? items.FirstOrDefault(entry => entry.Drop.Id == targetId).Container : null;
        SetGridDropHighlight(highlighted);
    }

    // Surligne la vignette-dossier survolee pendant le glisser (fond accent,
    // meme convention semantique que la surbrillance doree de la maquette
    // montree avant codage : "ranger dedans" doit se voir distinctement d'un
    // simple reordonnancement). Cible le visuel du gabarit (ContentTemplateRoot),
    // pas le ListViewItem lui-meme : c'est lui qui remplit reellement la
    // vignette a l'ecran.
    private void SetGridDropHighlight(ListViewItem? container)
    {
        if (ReferenceEquals(_bookmarkGridHighlightedContainer, container)) return;

        if (_bookmarkGridHighlightedContainer?.ContentTemplateRoot is FrameworkElement previousRoot)
        {
            SetTemplateRootBackground(previousRoot, _bookmarkGridHighlightedOriginalBackground);
        }

        _bookmarkGridHighlightedContainer = container;
        _bookmarkGridHighlightedOriginalBackground = null;

        if (container?.ContentTemplateRoot is FrameworkElement newRoot)
        {
            _bookmarkGridHighlightedOriginalBackground = GetTemplateRootBackground(newRoot);
            SetTemplateRootBackground(newRoot, (Brush)RootShell.Resources["NovaChromeButtonAccentBackgroundBrush"]);
        }
    }

    private static Brush? GetTemplateRootBackground(FrameworkElement root) => root switch
    {
        Border border => border.Background,
        Panel panel => panel.Background,
        _ => null
    };

    private static void SetTemplateRootBackground(FrameworkElement root, Brush? brush)
    {
        switch (root)
        {
            case Border border: border.Background = brush; break;
            case Panel panel: panel.Background = brush; break;
        }
    }

    private void BookmarkGridDragPanel_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement releasedPanel) releasedPanel.ReleasePointerCapture(e.Pointer);
        if (_bookmarkGridDraggedContainer is not null) _bookmarkGridDraggedContainer.Opacity = 1;
        SetGridDropHighlight(null);

        if (_bookmarkGridIsDragging && _bookmarkGridDraggedId is { } draggedId && _bookmarkGridPendingTargetId is { } targetId)
        {
            var into = _bookmarkGridPendingIntoFolder;
            // Differe l'operation reelle : eviter de reconstruire la grille
            // (ReloadBookmarks) pendant que ce meme gestionnaire, declenche
            // par un conteneur de cette grille, est encore sur la pile
            // d'appels - meme precaution que BookmarkDragPanel_PointerReleased
            // (MainWindow.BookmarksDragDrop.cs).
            DispatcherQueue.TryEnqueue(() =>
            {
                var moved = into
                    ? _bookmarks.MoveNode(draggedId, targetId)
                    : _bookmarks.ReorderNode(draggedId, targetId);
                if (moved)
                {
                    ReloadBookmarks();
                }
            });
        }

        ResetBookmarkGridDragState();
    }

    private void BookmarkGridDragPanel_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_bookmarkGridDraggedContainer is not null) _bookmarkGridDraggedContainer.Opacity = 1;
        SetGridDropHighlight(null);
        ResetBookmarkGridDragState();
    }

    private void ResetBookmarkGridDragState()
    {
        _bookmarkGridDraggedId = null;
        _bookmarkGridDraggedContainer = null;
        _bookmarkGridIsDragging = false;
        _bookmarkGridPendingTargetId = null;
        _bookmarkGridPendingIntoFolder = false;
    }
}
