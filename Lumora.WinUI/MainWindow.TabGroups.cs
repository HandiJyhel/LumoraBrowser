using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.System;
using Lumora.Privacy.HttpsEnforcer;
using Lumora.WinUI.Credentials;

namespace Lumora.WinUI;

// Rendu des onglets verticaux, groupes d'onglets, glisser-deposer et menus
// contextuels associes. Extrait de MainWindow.Navigation.cs (god file) :
// possede en propre _tabGroups/_collapsedGroupIds/_savedGroupIds, aucun
// changement de comportement, uniquement un deplacement de code au sein de
// la meme classe partielle.
public sealed partial class MainWindow
{
    // Palette de couleurs de groupe : reprend les teintes de l'identité visuelle
    // (lumiere/web) complétées par des teintes distinguables.
    private static readonly Windows.UI.Color[] TabGroupPalette =
    {
        UiColor(255, 185, 53),
        UiColor(67, 219, 209),
        UiColor(94, 156, 235),
        UiColor(219, 112, 147),
        UiColor(154, 140, 226),
        UiColor(255, 127, 53)
    };

    private static Windows.UI.Color TabGroupColor(TabGroup group) =>
        TabGroupPalette[((group.ColorIndex % TabGroupPalette.Length) + TabGroupPalette.Length) % TabGroupPalette.Length];

    private void RenderVerticalTabs()
    {
        if (!_verticalTabsEnabled)
        {
            VerticalTabsPanelItems.Children.Clear();
            return;
        }

        VerticalTabsPanelItems.Children.Clear();
        var current = CurrentTab();
        int? lastGroupId = null;

        foreach (var tab in _tabs)
        {
            if (tab.GroupId != lastGroupId && tab.GroupId is int headerGroupId)
            {
                var headerGroup = _tabGroups.FirstOrDefault(g => g.Id == headerGroupId);
                if (headerGroup is not null)
                {
                    VerticalTabsPanelItems.Children.Add(GroupHeaderElement(headerGroup));
                }
            }
            lastGroupId = tab.GroupId;

            if (tab.GroupId is int collapsedGroupId && _collapsedGroupIds.Contains(collapsedGroupId))
            {
                continue;
            }

            var isActive = current?.Id == tab.Id;
            Button button;
            FrameworkElement content;

            if (_verticalTabsCompact || tab.Pinned)
            {
                content = TabIconElement(tab, 22);
                button = new Button
                {
                    Width = 44,
                    Height = 38,
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = tab.Id,
                    Margin = new Thickness(0, 1, 0, 1)
                };
            }
            else
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                row.Children.Add(TabIconElement(tab, 16));
                row.Children.Add(new TextBlock
                {
                    Text = tab.Title,
                    MaxWidth = 118,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var grid = new Grid { ColumnSpacing = 6 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(row, 0);
                grid.Children.Add(row);
                var close = new Button
                {
                    Width = 26,
                    Height = 26,
                    MinWidth = 26,
                    Padding = new Thickness(0),
                    Tag = tab.Id,
                    Content = new SymbolIcon(Symbol.Cancel),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0)
                };
                ToolTipService.SetToolTip(close, "Fermer l'onglet");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(close, $"Fermer {tab.Title}");
                close.Click += VerticalTabCloseButton_Click;
                Grid.SetColumn(close, 1);
                grid.Children.Add(close);
                content = grid;
                button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(8, 5, 8, 5),
                    Tag = tab.Id,
                    Margin = new Thickness(0, 1, 0, 1)
                };
            }

            if (tab.GroupId is int tabGroupId && _tabGroups.FirstOrDefault(g => g.Id == tabGroupId) is { } tabGroup)
            {
                var wrapper = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                wrapper.Children.Add(new Border
                {
                    Width = 3,
                    CornerRadius = new CornerRadius(1.5),
                    Background = new SolidColorBrush(TabGroupColor(tabGroup)),
                    VerticalAlignment = VerticalAlignment.Stretch
                });
                wrapper.Children.Add(content);
                button.Content = wrapper;
            }
            else
            {
                button.Content = content;
            }

            if (isActive)
            {
                button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }

            ToolTipService.SetToolTip(button, tab.Title);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tab.Title);
            button.Click += VerticalTabButton_Click;
            button.ContextFlyout = CreateTabContextFlyout(tab);
            button.CanDrag = true;
            button.AllowDrop = true;
            button.DragStarting += VerticalTabButton_DragStarting;
            button.DragOver += VerticalTabButton_DragOver;
            button.Drop += VerticalTabButton_Drop;
            VerticalTabsPanelItems.Children.Add(button);
        }
    }

    private FrameworkElement GroupHeaderElement(TabGroup group)
    {
        var collapsed = _collapsedGroupIds.Contains(group.Id);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new FontIcon
        {
            Glyph = collapsed ? "\uE76C" : "\uE70D",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 10
        });
        row.Children.Add(new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(TabGroupColor(group)),
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(new TextBlock
        {
            Text = group.Name,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            MaxWidth = 130,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });

        var header = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 4, 0, 1),
            Content = row,
            Tag = group.Id
        };
        header.Click += (_, _) => ToggleGroupCollapsed(group.Id);
        header.ContextFlyout = CreateGroupHeaderFlyout(group);
        ToolTipService.SetToolTip(header, group.Name);
        return header;
    }

    private MenuFlyout CreateTabContextFlyout(BrowserTabState tab)
    {
        var flyout = new MenuFlyout();

        var pinItem = new MenuFlyoutItem { Text = tab.Pinned ? "Desepingler l'onglet" : "Epingler l'onglet", Tag = tab };
        pinItem.Click += TabContextTogglePin_Click;
        flyout.Items.Add(pinItem);

        var closeItem = new MenuFlyoutItem { Text = "Fermer l'onglet", Tag = tab };
        closeItem.Click += TabContextClose_Click;
        flyout.Items.Add(closeItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

        var addToGroup = new MenuFlyoutSubItem { Text = "Ajouter au groupe" };
        foreach (var group in _tabGroups)
        {
            var groupItem = new MenuFlyoutItem { Text = group.Name, Tag = (tab, group) };
            groupItem.Click += AddTabToGroup_Click;
            addToGroup.Items.Add(groupItem);
        }
        if (_tabGroups.Count > 0)
        {
            addToGroup.Items.Add(new MenuFlyoutSeparator());
        }
        var newGroupItem = new MenuFlyoutItem { Text = "Nouveau groupe...", Tag = tab };
        newGroupItem.Click += CreateGroupWithTab_Click;
        addToGroup.Items.Add(newGroupItem);
        flyout.Items.Add(addToGroup);

        if (tab.GroupId is not null)
        {
            var removeItem = new MenuFlyoutItem { Text = "Retirer du groupe", Tag = tab };
            removeItem.Click += RemoveTabFromGroup_Click;
            flyout.Items.Add(removeItem);
        }

        return flyout;
    }

    private MenuFlyout CreateGroupHeaderFlyout(TabGroup group)
    {
        var flyout = new MenuFlyout();

        var renameItem = new MenuFlyoutItem { Text = "Renommer le groupe", Tag = group };
        renameItem.Click += RenameGroup_Click;
        flyout.Items.Add(renameItem);

        var saveItem = new MenuFlyoutItem { Text = "Enregistrer le groupe", Tag = group };
        saveItem.Click += SaveGroupToLibrary_Click;
        flyout.Items.Add(saveItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var dissolveItem = new MenuFlyoutItem { Text = "Dissoudre le groupe", Tag = group };
        dissolveItem.Click += DissolveGroup_Click;
        flyout.Items.Add(dissolveItem);

        return flyout;
    }

    private void AddTabToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: (BrowserTabState tab, TabGroup group) })
        {
            AssignTabToGroup(tab, group.Id);
        }
    }

    private void TabContextClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            CloseTab(tab);
        }
    }

    private void TabContextTogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            TogglePinTab(tab);
        }
    }

    // Epingler un onglet le compacte en icone seule et le regroupe au debut de la
    // liste (comme Chrome/Edge), sans toucher a l'ordre relatif des autres onglets.
    private void TogglePinTab(BrowserTabState tab)
    {
        tab.Pinned = !tab.Pinned;
        ReorderPinnedFirst();
        RefreshTabViewOrder();
        UpdateTabHeader(tab);
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void ReorderPinnedFirst()
    {
        var pinned = _tabs.Where(t => t.Pinned).ToList();
        var others = _tabs.Where(t => !t.Pinned).ToList();
        _tabs.Clear();
        _tabs.AddRange(pinned);
        _tabs.AddRange(others);
    }

    // Reconstruit l'ordre visuel de la barre horizontale pour qu'il corresponde a
    // l'ordre courant de _tabs (source de verite pour l'ordre des onglets).
    private void RefreshTabViewOrder()
    {
        _suppressTabOrderSync = true;
        try
        {
            var itemsByTag = BrowserTabs.TabItems
                .OfType<TabViewItem>()
                .Where(item => item.Tag is int)
                .ToDictionary(item => (int)item.Tag!);
            var selected = BrowserTabs.SelectedItem;

            BrowserTabs.TabItems.Clear();
            foreach (var tab in _tabs)
            {
                if (itemsByTag.TryGetValue(tab.Id, out var item))
                {
                    BrowserTabs.TabItems.Add(item);
                }
            }

            if (selected is not null)
            {
                BrowserTabs.SelectedItem = selected;
            }
        }
        finally
        {
            _suppressTabOrderSync = false;
        }
    }

    // Glisser-deposer d'onglets dans la barre horizontale (natif WinUI, TabItems est
    // observable) : on ne resynchronise _tabs qu'apres un vrai deplacement (Move),
    // pas apres un Add/Remove deja gere par AddTab/CloseTab.
    private void BrowserTabs_TabItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_suppressTabOrderSync || e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Move)
        {
            return;
        }

        var visualOrder = BrowserTabs.TabItems
            .OfType<TabViewItem>()
            .Where(item => item.Tag is int)
            .Select(item => (int)item.Tag!)
            .ToList();

        var byId = _tabs.ToDictionary(t => t.Id);
        var pinnedIds = visualOrder.Where(id => byId.TryGetValue(id, out var t) && t.Pinned).ToList();
        var otherIds = visualOrder.Where(id => !pinnedIds.Contains(id)).ToList();
        var finalOrder = pinnedIds.Concat(otherIds).ToList();

        _tabs.Clear();
        _tabs.AddRange(finalOrder.Select(id => byId[id]));

        // L'utilisateur a essaye de melanger epingles/non epingles : on corrige la
        // barre visuelle pour qu'elle respecte cet ordre force.
        if (!finalOrder.SequenceEqual(visualOrder))
        {
            RefreshTabViewOrder();
        }

        RenderVerticalTabs();
        SaveTabSession();
    }

    // Glisser-deposer d'onglets dans le rail vertical (implementation manuelle : le
    // rail est une pile de boutons construits a la main, pas un ItemsControl natif).
    private void VerticalTabButton_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is Button { Tag: int id })
        {
            args.Data.Properties.Add("novaTabId", id);
            args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }

    private void VerticalTabButton_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Properties.ContainsKey("novaTabId"))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }

    private void VerticalTabButton_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Button { Tag: int targetId } ||
            !e.DataView.Properties.TryGetValue("novaTabId", out var raw) ||
            raw is not int movingId ||
            movingId == targetId)
        {
            return;
        }

        ReorderTab(movingId, targetId);
    }

    // Un glisser-deposer ne peut pas melanger un onglet epingle et un onglet normal
    // (meme regle que pour la barre horizontale) : deposer hors de sa section n'a
    // simplement aucun effet plutot que de produire un ordre incoherent.
    private void ReorderTab(int movingId, int targetId)
    {
        var moving = _tabs.FirstOrDefault(t => t.Id == movingId);
        var target = _tabs.FirstOrDefault(t => t.Id == targetId);
        if (moving is null || target is null || moving.Pinned != target.Pinned)
        {
            return;
        }

        _tabs.Remove(moving);
        var targetIndex = _tabs.IndexOf(target);
        _tabs.Insert(targetIndex, moving);

        RefreshTabViewOrder();
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void VerticalTabCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int id } &&
            _tabs.FirstOrDefault(tab => tab.Id == id) is { } tab)
        {
            CloseTab(tab);
        }
    }

    private void RemoveTabFromGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            AssignTabToGroup(tab, null);
        }
    }

    private async void CreateGroupWithTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            return;
        }

        var name = await PromptTextAsync("Nouveau groupe", "Nom du groupe", "");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var group = new TabGroup(_nextGroupId++, name.Trim(), TabGroupOrdering.NextColorIndex(_tabGroups.Count));
        _tabGroups.Add(group);
        AssignTabToGroup(tab, group.Id);
    }

    private async void RenameGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: TabGroup group })
        {
            return;
        }

        var name = await PromptTextAsync("Renommer le groupe", "Nom du groupe", group.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        group.Name = name.Trim();
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void DissolveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: TabGroup group })
        {
            return;
        }

        // Garde-fou : le groupement va etre perdu ; proposer de le ranger d'abord.
        OfferKeepGroupIfUnsaved(group);

        foreach (var tab in _tabs.Where(t => t.GroupId == group.Id))
        {
            tab.GroupId = null;
        }
        _tabGroups.Remove(group);
        _collapsedGroupIds.Remove(group.Id);
        _savedGroupIds.Remove(group.Id);
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void ToggleGroupCollapsed(int groupId)
    {
        if (!_collapsedGroupIds.Remove(groupId))
        {
            _collapsedGroupIds.Add(groupId);
        }
        RenderVerticalTabs();
    }

    private void AssignTabToGroup(BrowserTabState tab, int? groupId)
    {
        var order = TabGroupOrdering.ReorderForGroup(
            _tabs.Select(t => t.Id).ToList(),
            tab.Id,
            groupId,
            _tabs.ToDictionary(t => t.Id, t => t.GroupId));

        var byId = _tabs.ToDictionary(t => t.Id);
        _tabs.Clear();
        _tabs.AddRange(order.Select(id => byId[id]));

        tab.GroupId = groupId;
        RenderVerticalTabs();
        SaveTabSession();
    }

    private static StackPanel TabHeaderContent(BrowserTabState tab, bool compact)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = compact ? 0 : 8
        };
        panel.Children.Add(TabIconElement(tab, compact ? 20 : 16));
        if (!compact)
        {
            panel.Children.Add(new TextBlock
            {
                Text = tab.Title,
                MaxWidth = 170,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        return panel;
    }

    private static FrameworkElement TabIconElement(BrowserTabState tab, double size)
    {
        if (!string.IsNullOrWhiteSpace(tab.IconPath) && FaviconQuality.IsUsablePngFile(tab.IconPath))
        {
            return new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(tab.IconUri)),
                Width = size,
                Height = size
            };
        }

        return new FontIcon
        {
            Glyph = BookmarkGlyphs.Link,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = size
        };
    }

    private void VerticalTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
        {
            return;
        }

        var tab = _tabs.FirstOrDefault(candidate => candidate.Id == id);
        if (tab is null)
        {
            return;
        }

        var item = BrowserTabs.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(candidate => candidate.Tag is int tabId && tabId == id);
        if (item is not null)
        {
            _suppressTabNavigation = true;
            BrowserTabs.SelectedItem = item;
            _suppressTabNavigation = false;
        }

        ActivateTab(tab);
        ReturnToBrowserIfHidden();
    }
}
