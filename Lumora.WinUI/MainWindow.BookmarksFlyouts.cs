using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lumora.WinUI;

// Menus contextuels dynamiques des favoris + rendu de l'arbre/contenu
// (delegue a BookmarkTreePresenter). Extrait de MainWindow.Bookmarks.cs
// (god file), aucun changement de comportement.
public sealed partial class MainWindow
{
    // "rangements Lumora" (texte de remplissage identique sur tous les
    // dossiers, sans aucune information reelle) remplace par le nombre
    // d'elements du dossier - signale par l'utilisateur comme l'une des
    // "2 informations inutiles" du menu, avec le sens d'ouverture vers le
    // haut (Placement jamais precise, la valeur par defaut de FlyoutBase
    // est Top, pas Bottom - piege classique WinUI).
    private string BookmarkFolderChildCountLabel(string folderId)
    {
        var count = _allBookmarkNodes.Count(node => node.ParentId == folderId);
        return count switch
        {
            0 => "Dossier vide",
            1 => "1 element",
            _ => $"{count} elements"
        };
    }

    private MenuFlyout CreateBookmarkFolderFlyout(BookmarkNode folder)
    {
        var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.Bottom };
        AddLumoraMenuHeader(
            flyout.Items,
            BookmarkReadableTitle(folder),
            BookmarkFolderChildCountLabel(folder.Id),
            "\uE8B7");
        AddBookmarkFlyoutItems(flyout.Items, folder.Id);
        HookFlyoutPointerSupport(flyout);

        return flyout;
    }

    private MenuFlyout CreateBookmarkContextFlyout(BookmarkNode node)
    {
        var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.Bottom };
        AddLumoraMenuHeader(
            flyout.Items,
            node.Kind == BookmarkKind.Url ? BookmarkReadableTitle(node) : $"Dossier : {BookmarkReadableTitle(node)}",
            node.Kind == BookmarkKind.Url ? TabHeaderHost(node.Url) : BookmarkFolderChildCountLabel(node.Id),
            node.Kind == BookmarkKind.Url ? "\uE774" : "\uE8B7");
        var openItem = new MenuFlyoutItem
        {
            Text = node.Kind == BookmarkKind.Url ? $"Ouvrir {BookmarkReadableTitle(node)}" : "Ouvrir le dossier",
            Tag = node,
            Icon = node.Kind == BookmarkKind.Url ? new SymbolIcon(Symbol.OpenFile) : new SymbolIcon(Symbol.Folder)
        };
        openItem.Click += BookmarkContextOpen_Click;
        flyout.Items.Add(openItem);

        if (node.Kind == BookmarkKind.Url)
        {
            var openInNewTabItem = new MenuFlyoutItem
            {
                Text = "Ouvrir dans un nouvel onglet",
                Tag = node,
                Icon = new SymbolIcon(Symbol.Add)
            };
            openInNewTabItem.Click += BookmarkContextOpenInNewTab_Click;
            flyout.Items.Add(openInNewTabItem);
        }

        if (!node.IsRoot)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());

            var renameItem = new MenuFlyoutItem
            {
                Text = "Renommer",
                Tag = node,
                Icon = CreateMenuGlyphIcon("\uE8AC")
            };
            renameItem.Click += BookmarkContextRename_Click;
            flyout.Items.Add(renameItem);

            var deleteItem = new MenuFlyoutItem
            {
                Text = "Supprimer",
                Tag = node,
                Icon = new SymbolIcon(Symbol.Delete)
            };
            deleteItem.Click += BookmarkContextDelete_Click;
            flyout.Items.Add(deleteItem);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        var studioSubItem = new MenuFlyoutSubItem
        {
            Text = "Studio Lumora",
            Icon = CreateMenuGlyphIcon("\uE790")
        };
        AddWorkspaceLayoutMenuItems(studioSubItem.Items, includeTheme: true);
        flyout.Items.Add(studioSubItem);
        HookFlyoutPointerSupport(flyout);

        return flyout;
    }

    private void BookmarkContextOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BookmarkNode node })
        {
            OpenBookmarkNode(node);
        }
    }

    private async void BookmarkContextRename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BookmarkNode node })
        {
            await RenameBookmarkNodeAsync(node);
        }
    }

    private void BookmarkContextOpenInNewTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: BookmarkNode node } || node.Kind != BookmarkKind.Url)
        {
            return;
        }

        PreloadBookmarkFavicon(node);
        AddTab(DisplayTitle(node.Url), node.Url, select: true);
        ShowPanel(BrowserPanel, node.Title);
        UpdateStatusText($"Favori ouvert dans un nouvel onglet : {BookmarkReadableTitle(node)}");
    }

    private void BookmarkContextDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BookmarkNode node })
        {
            DeleteBookmarkNode(node);
        }
    }

    private void AddBookmarkFlyoutItems(IList<MenuFlyoutItemBase> items, string folderId)
    {
        foreach (var child in _allBookmarkNodes
                     .Where(node => node.ParentId == folderId)
                     .OrderBy(node => node.Kind == BookmarkKind.Url ? 1 : 0)
                     .ThenBy(node => node.Position))
        {
            if (child.Kind == BookmarkKind.Folder)
            {
                var sub = new MenuFlyoutSubItem
                {
                    Text = BookmarkReadableTitle(child),
                    ContextFlyout = CreateBookmarkContextFlyout(child)
                };
                var openFolder = new MenuFlyoutItem
                {
                    Text = "Ouvrir le dossier",
                    Tag = child
                };
                openFolder.Click += BookmarkContextOpen_Click;
                sub.Items.Add(openFolder);

                if (!child.IsRoot)
                {
                    var renameFolder = new MenuFlyoutItem
                    {
                        Text = "Renommer",
                        Tag = child
                    };
                    renameFolder.Click += BookmarkContextRename_Click;
                    sub.Items.Add(renameFolder);

                    var deleteFolder = new MenuFlyoutItem
                    {
                        Text = "Supprimer",
                        Tag = child
                    };
                    deleteFolder.Click += BookmarkContextDelete_Click;
                    sub.Items.Add(deleteFolder);
                }

                sub.Items.Add(new MenuFlyoutSeparator());
                var contentStartIndex = sub.Items.Count;
                AddBookmarkFlyoutItems(sub.Items, child.Id);
                if (sub.Items.Count == contentStartIndex)
                {
                    sub.Items.Add(new MenuFlyoutItem
                    {
                        Text = "Dossier vide",
                        IsEnabled = false
                    });
                }
                items.Add(sub);
            }
            else
            {
                var item = new MenuFlyoutItem
                {
                    Text = BookmarkReadableTitle(child),
                    Tag = child,
                    ContextFlyout = CreateBookmarkContextFlyout(child)
                };
                item.Click += BookmarkFlyoutUrl_Click;
                items.Add(item);
            }
        }
    }

    private void BookmarkFlyoutUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BookmarkNode node })
        {
            PreloadBookmarkFavicon(node);
            NavigateCurrentTab(node.Url, node.Title);
            ShowPanel(BrowserPanel, node.Title);
        }
    }

    private void RenderBookmarksTree()
    {
        _bookmarkFolderItems.Clear();
        foreach (var folder in BookmarkTreePresenter.FlattenFolders(_allBookmarkNodes))
        {
            _bookmarkFolderItems.Add(folder);
        }

        var selected = _bookmarkFolderItems.FirstOrDefault(item => item.Node.Id == _currentBookmarkFolderId);
        if (selected is not null && !ReferenceEquals(BookmarkFoldersList.SelectedItem, selected))
        {
            BookmarkFoldersList.SelectedItem = selected;
        }
    }

    private void ShowBookmarksFolder(string folderId, string status)
    {
        _currentBookmarkFolderId = folderId;
        _bookmarkSearch = string.Empty;
        BookmarksSearchBox.Text = string.Empty;
        ReloadBookmarks();
        ShowPanel(BookmarksPanel, status);
    }

    private void RenderBookmarkContent()
    {
        _bookmarkItems.Clear();
        var folder = _allBookmarkNodes.FirstOrDefault(node => node.Id == _currentBookmarkFolderId)
            ?? _allBookmarkNodes.First(node => node.Id == BookmarkStore.ToolbarRootId);
        CurrentBookmarkFolderTitle.Text = folder.Title;
        BookmarkBreadcrumbText.Text = BookmarkTreePresenter.Breadcrumb(_allBookmarkNodes, folder.Id);

        var content = string.IsNullOrWhiteSpace(_bookmarkSearch)
            ? _allBookmarkNodes
                .Where(node => node.ParentId == folder.Id)
                .OrderBy(node => node.Kind == BookmarkKind.Url ? 1 : 0)
                .ThenBy(node => node.Position)
                .Select(node => BookmarkTreePresenter.ItemForNode(_allBookmarkNodes, node, 0))
            : BookmarkTreePresenter.Search(_allBookmarkNodes, _bookmarkSearch);

        foreach (var item in content)
        {
            _bookmarkItems.Add(item);
        }
    }

}
