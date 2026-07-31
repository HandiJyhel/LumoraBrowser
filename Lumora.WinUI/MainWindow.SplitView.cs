using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

// Vue divisee (0.93.x) : deux onglets deja ouverts affiches cote a cote dans
// BrowserHost, chacun gardant sa propre identite (WebView2, historique, adresse,
// fermeture independante). Volontairement PAS une fusion de deux pages dans un
// seul onglet - modele plus simple et plus robuste, discute et valide avec
// l'utilisateur avant implementation. Etat non persiste entre sessions : au
// redemarrage, les deux onglets rouvrent normalement, plus cote a cote.
public sealed partial class MainWindow
{
    private const double SplitDividerWidth = 6;
    private Border? _splitDivider;
    private bool _splitDividerDragging;
    private double _splitDividerDragStartX;
    private double _splitDividerDragStartLeftStar;

    private bool IsTabInSplitView(int tabId) =>
        _splitView is { } sv && (sv.LeftId == tabId || sv.RightId == tabId);

    // Point d'entree direct du bouton de barre d'outils (0.93.4.0) : avant cette
    // version, split view n'etait atteignable que par clic droit sur un onglet
    // (sous-menu ou selection de 2 onglets), jamais par un bouton visible.
    private void SplitViewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_splitView is not null)
        {
            var restoreTarget = CurrentTab();
            ExitSplitView();
            if (restoreTarget is not null)
            {
                SelectTab(restoreTarget);
            }
            return;
        }

        var active = CurrentTab();
        if (active is null)
        {
            return;
        }

        var others = _tabs.Where(t => t.Id != active.Id).ToList();
        if (others.Count == 0)
        {
            StatusText.Text = "Ouvre un autre onglet pour diviser l'ecran.";
            return;
        }

        if (others.Count == 1)
        {
            _ = EnterSplitViewAsync(active, others[0]);
            return;
        }

        var flyout = new MenuFlyout();
        foreach (var other in others)
        {
            var item = new MenuFlyoutItem { Text = other.Title, Tag = (active, other) };
            item.Click += SplitViewWith_Click;
            flyout.Items.Add(item);
        }
        HookFlyoutPointerSupport(flyout);
        flyout.ShowAt((FrameworkElement)sender);
    }

    // Reflete l'etat split/simple sur le bouton (infobulle + nom accessible),
    // meme pattern que les badges de compteur (ex. RssModuleButton).
    private void UpdateSplitViewButtonState()
    {
        var label = _splitView is not null
            ? "Retablir la vue simple"
            : "Diviser l'écran entre deux onglets";
        ToolTipService.SetToolTip(SplitViewButton, label);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(SplitViewButton, label);
    }

    private async void SplitViewWith_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: (BrowserTabState left, BrowserTabState right) })
        {
            await EnterSplitViewAsync(left, right);
        }
    }

    private async void SplitViewWithSelection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: List<BrowserTabState> selection } && selection.Count == 2)
        {
            await EnterSplitViewAsync(selection[0], selection[1]);
            ClearTabSelection();
        }
    }

    private void RestoreSingleView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            ExitSplitView();
            SelectTab(tab);
        }
    }

    private async Task EnterSplitViewAsync(BrowserTabState left, BrowserTabState right)
    {
        var leftView = await EnsureTabViewReadyAsync(left);
        var rightView = await EnsureTabViewReadyAsync(right);
        if (leftView is null || rightView is null)
        {
            StatusText.Text = "Impossible de diviser l'ecran : moteur web indisponible.";
            return;
        }

        _splitView = (left.Id, right.Id);

        foreach (var other in _tabs)
        {
            if (other.View is not null)
            {
                other.View.Visibility = other.Id == left.Id || other.Id == right.Id
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        BrowserHost.ColumnDefinitions.Clear();
        BrowserHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        BrowserHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SplitDividerWidth) });
        BrowserHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(leftView, 0);
        Grid.SetColumn(rightView, 2);
        EnsureSplitDivider();

        FocusSplitPane(left);
        RenderVerticalTabs();
        RefreshHorizontalTabHeaders();
        UpdateSplitViewButtonState();
        StatusText.Text = "Ecran divise entre deux onglets.";
    }

    // Quitte le split sans decider quel onglet redevient l'onglet unique affiche :
    // c'est a l'appelant (ActivateTab, fermeture d'un des deux onglets, "Retablir
    // la vue simple") de le faire juste apres, dans le meme appel synchrone.
    private void ExitSplitView()
    {
        if (_splitView is null)
        {
            return;
        }

        _splitView = null;
        BrowserHost.ColumnDefinitions.Clear();
        if (_splitDivider is not null)
        {
            _splitDivider.Visibility = Visibility.Collapsed;
        }

        foreach (var tab in _tabs)
        {
            if (tab.View is not null)
            {
                Grid.SetColumn(tab.View, 0);
            }
        }

        RenderVerticalTabs();
        RefreshHorizontalTabHeaders();
        UpdateSplitViewButtonState();
    }

    private void EnsureSplitDivider()
    {
        if (_splitDivider is not null)
        {
            Grid.SetColumn(_splitDivider, 1);
            _splitDivider.Visibility = Visibility.Visible;
            return;
        }

        var divider = new Border
        {
            Background = (Brush)RootShell.Resources["NovaChromeStrokeSoftBrush"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        divider.PointerPressed += SplitDivider_PointerPressed;
        divider.PointerMoved += SplitDivider_PointerMoved;
        divider.PointerReleased += SplitDivider_PointerReleased;
        Grid.SetColumn(divider, 1);
        _splitDivider = divider;
        BrowserHost.Children.Add(divider);
    }

    private void SplitDivider_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border divider || BrowserHost.ColumnDefinitions.Count < 3)
        {
            return;
        }

        _splitDividerDragging = true;
        _splitDividerDragStartX = e.GetCurrentPoint(BrowserHost).Position.X;
        _splitDividerDragStartLeftStar = BrowserHost.ColumnDefinitions[0].Width.Value;
        divider.CapturePointer(e.Pointer);
    }

    private void SplitDivider_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_splitDividerDragging || BrowserHost.ColumnDefinitions.Count < 3)
        {
            return;
        }

        var totalWidth = BrowserHost.ActualWidth - SplitDividerWidth;
        if (totalWidth <= 0)
        {
            return;
        }

        var deltaX = e.GetCurrentPoint(BrowserHost).Position.X - _splitDividerDragStartX;
        // Deux colonnes en Star, 2 unites au total au depart (1+1) : on convertit le
        // deplacement en pixels en unites Star, borne pour qu'aucun volet ne
        // descende sous ~15% de la largeur.
        var leftStarWidth = Math.Clamp(_splitDividerDragStartLeftStar + deltaX / totalWidth * 2, 0.15, 1.85);
        BrowserHost.ColumnDefinitions[0].Width = new GridLength(leftStarWidth, GridUnitType.Star);
        BrowserHost.ColumnDefinitions[2].Width = new GridLength(2 - leftStarWidth, GridUnitType.Star);
    }

    private void SplitDivider_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _splitDividerDragging = false;
        if (sender is Border divider)
        {
            divider.ReleasePointerCapture(e.Pointer);
        }
    }

    // Bascule le "chrome" (barre d'adresse, etoile favori, titre) sur le volet qui
    // vient de recevoir le focus, sans toucher a la visibilite ni aux colonnes -
    // les deux volets restent affiches. Voir BrowserView_GotFocus (abonnement pose
    // a la creation de chaque WebView2 dans EnsureTabViewReadyAsync).
    private void FocusSplitPane(BrowserTabState tab)
    {
        if (tab.View is null)
        {
            return;
        }

        _browserView = tab.View;
        _currentPageDomain = ExtractDomain(tab.Address);
        AddressBox.Text = DisplayAddressForBar(tab.Address);
        UpdateBookmarkStar(tab.Address);
        ShowPanel(BrowserPanel, tab.Title);
        tab.View.Focus(FocusState.Programmatic);
    }

    private void BrowserView_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_splitView is null || sender is not WebView2 view || ReferenceEquals(_browserView, view))
        {
            return;
        }

        var tab = TabForView(view);
        if (tab is not null && IsTabInSplitView(tab.Id))
        {
            FocusSplitPane(tab);
        }
    }
}
