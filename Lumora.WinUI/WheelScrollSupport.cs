using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

// Portage du mecanisme de molette fiable deja valide sur MainWindow
// (HookAutomaticPointerFocus/AttachScrollViewerPointerSupport, serie de
// diagnostics 0.84.1.15-0.84.1.18) vers n'importe quelle autre fenetre WinUI
// qui n'a pas cette plomberie - constate le 2026-07-26 : LumoraAppWindow n'a
// aucun de ces rattachements, donc aucun ScrollViewer/Flyout n'y defile a la
// molette. Une instance par fenetre : l'etat (ScrollViewer deja rattaches)
// ne doit jamais etre partage entre deux fenetres differentes.
//
// MainWindow garde sa propre implementation historique intacte (aucun risque
// de regression sur un comportement deja tres difficilement mis au point) ;
// ce module est la version reutilisable pour toute NOUVELLE fenetre.
internal sealed class WheelScrollSupport
{
    private readonly HashSet<ScrollViewer> _hoverFocusHookedScrollViewers = new();
    private readonly HashSet<ScrollViewer> _manualWheelHookedScrollViewers = new();
    private readonly Dictionary<UIElement, ScrollViewer> _manualWheelSourceOwners = new();
    private readonly HashSet<FlyoutBase> _overlayHookedFlyouts = new();
    private readonly HashSet<Popup> _overlayHookedPopups = new();
    private XamlRoot? _xamlRoot;

    // A appeler une fois le contenu de la fenetre charge (et de nouveau sur
    // Loaded si le contenu peut etre reconstruit) : rattache recursivement
    // tout ScrollViewer existant, et pose une ecoute sur chaque Flyout/Popup
    // pour rattacher aussi leur contenu au moment ou ils s'ouvrent (un
    // Flyout n'existe pas dans l'arbre visuel principal tant qu'il n'a
    // jamais ete ouvert).
    public void Attach(FrameworkElement root)
    {
        _xamlRoot = root.XamlRoot;
        AttachScrollViewerPointerSupport(root);
        HookTransientOverlaySupport(root);
        HookOpenPopupsForXamlRoot(_xamlRoot);
    }

    private void AttachScrollViewerPointerSupport(DependencyObject root, ScrollViewer? activeWheelOwner = null)
    {
        if (root is ScrollViewer viewer && _hoverFocusHookedScrollViewers.Add(viewer))
        {
            viewer.IsTabStop = true;
            viewer.PointerEntered += ScrollViewer_PointerEntered;
        }

        if (root is ScrollViewer hookedViewer)
        {
            if (_manualWheelHookedScrollViewers.Add(hookedViewer))
            {
                hookedViewer.AddHandler(
                    UIElement.PointerWheelChangedEvent,
                    new PointerEventHandler(ScrollViewer_PointerWheelChanged),
                    handledEventsToo: true);
            }

            // Meme correctif que MainWindow.xaml.cs (2026-08-01) : un ScrollViewer
            // purement horizontal ne doit pas devenir le proprietaire de la molette
            // verticale de ses descendants, sinon WinUI l'avale en la convertissant
            // en defilement horizontal.
            if (hookedViewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
            {
                activeWheelOwner = hookedViewer;
            }
        }
        else if (activeWheelOwner is not null && root is UIElement wheelSource)
        {
            HookScrollViewerWheelSource(wheelSource, activeWheelOwner);
        }

        HookFrameworkElementTransientOverlays(root);

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            AttachScrollViewerPointerSupport(VisualTreeHelper.GetChild(root, index), activeWheelOwner);
        }
    }

    private void HookScrollViewerWheelSource(UIElement wheelSource, ScrollViewer owner)
    {
        if (_manualWheelSourceOwners.TryGetValue(wheelSource, out var existingOwner) &&
            ReferenceEquals(existingOwner, owner))
        {
            return;
        }

        _manualWheelSourceOwners[wheelSource] = owner;
        wheelSource.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler(ScrollViewerDescendant_PointerWheelChanged),
            handledEventsToo: true);
    }

    private void HookTransientOverlaySupport(DependencyObject root)
    {
        HookFrameworkElementTransientOverlays(root);

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            HookTransientOverlaySupport(VisualTreeHelper.GetChild(root, index));
        }
    }

    private void HookFrameworkElementTransientOverlays(DependencyObject root)
    {
        if (root is Popup popup)
        {
            HookPopupPointerSupport(popup);
        }

        if (root is not FrameworkElement element)
        {
            return;
        }

        if (element.ContextFlyout is { } contextFlyout)
        {
            HookFlyoutPointerSupport(contextFlyout);
        }

        if (element is Button { Flyout: { } buttonFlyout })
        {
            HookFlyoutPointerSupport(buttonFlyout);
        }

        if (element is SplitButton { Flyout: { } splitButtonFlyout })
        {
            HookFlyoutPointerSupport(splitButtonFlyout);
        }

        if (element is ToggleSplitButton { Flyout: { } toggleSplitButtonFlyout })
        {
            HookFlyoutPointerSupport(toggleSplitButtonFlyout);
        }

        if (element is DropDownButton { Flyout: { } dropDownButtonFlyout })
        {
            HookFlyoutPointerSupport(dropDownButtonFlyout);
        }
    }

    private void HookFlyoutPointerSupport(FlyoutBase flyout)
    {
        if (_overlayHookedFlyouts.Add(flyout))
        {
            flyout.Opened += FlyoutPointerSupport_Opened;
        }
    }

    private void FlyoutPointerSupport_Opened(object? sender, object e)
    {
        if (sender is Flyout { Content: DependencyObject content })
        {
            AttachScrollViewerPointerSupport(content);
            HookTransientOverlaySupport(content);
        }

        HookOpenPopupsForXamlRoot(_xamlRoot);
    }

    private void HookPopupPointerSupport(Popup popup)
    {
        if (_overlayHookedPopups.Add(popup))
        {
            popup.Opened += PopupPointerSupport_Opened;
        }

        if (popup.IsOpen)
        {
            HookPopupChildTree(popup);
        }
    }

    private void PopupPointerSupport_Opened(object? sender, object e)
    {
        if (sender is Popup popup)
        {
            HookPopupChildTree(popup);
        }
    }

    private void HookPopupChildTree(Popup popup)
    {
        if (popup.Child is not UIElement child)
        {
            return;
        }

        AttachScrollViewerPointerSupport(child);
        HookTransientOverlaySupport(child);
    }

    private void HookOpenPopupsForXamlRoot(XamlRoot? xamlRoot)
    {
        if (xamlRoot is null)
        {
            return;
        }

        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
        {
            HookPopupPointerSupport(popup);
        }
    }

    private void ScrollViewer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is ScrollViewer { Visibility: Visibility.Visible, IsEnabled: true } viewer)
        {
            viewer.Focus(FocusState.Pointer);
        }
    }

    private void ScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (e.Handled || sender is not ScrollViewer viewer)
        {
            return;
        }

        TryApplyScrollViewerWheel(viewer, e, "viewer");
    }

    private void ScrollViewerDescendant_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (e.Handled ||
            sender is not UIElement wheelSource ||
            !_manualWheelSourceOwners.TryGetValue(wheelSource, out var viewer))
        {
            return;
        }

        TryApplyScrollViewerWheel(viewer, e, wheelSource.GetType().Name);
    }

    private static void TryApplyScrollViewerWheel(ScrollViewer viewer, PointerRoutedEventArgs e, string sourceLabel)
    {
        if (viewer.Visibility != Visibility.Visible ||
            !viewer.IsEnabled ||
            viewer.ScrollableHeight <= 0)
        {
            return;
        }

        var delta = e.GetCurrentPoint(viewer).Properties.MouseWheelDelta;
        if (!WheelScrollMath.TryComputeNextVerticalOffset(viewer.VerticalOffset, viewer.ScrollableHeight, delta, out var newOffset))
        {
            return;
        }

        viewer.ChangeView(null, newOffset, null, false);
        e.Handled = true;
        WinUiRuntimeTrace.Write($"Molette ScrollViewer ({sourceLabel}) : defilement applique, nouvel offset={newOffset:F0}.");
    }
}
