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

    // Utilise par les entrees "Déplacer avant"/"Déplacer après" du menu
    // contextuel (2026-08-14) pour desactiver l'entree qui ne ferait rien
    // (deja premier/dernier parmi ses freres) plutot que de la laisser
    // cliquable sans effet.
    private (bool IsFirst, bool IsLast) BookmarkSiblingBoundaries(BookmarkNode node)
    {
        var siblings = _allBookmarkNodes
            .Where(n => n.ParentId.Equals(node.ParentId, StringComparison.Ordinal))
            .OrderBy(n => n.Position)
            .ToList();
        var index = siblings.FindIndex(n => n.Id == node.Id);
        if (index < 0)
        {
            return (true, true);
        }
        return (index == 0, index == siblings.Count - 1);
    }

    private void BookmarkContextMoveBefore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: BookmarkNode node }) return;
        if (_bookmarks.MoveNodeAdjacent(node.Id, moveForward: false))
        {
            ReloadBookmarks();
        }
    }

    private void BookmarkContextMoveAfter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: BookmarkNode node }) return;
        if (_bookmarks.MoveNodeAdjacent(node.Id, moveForward: true))
        {
            ReloadBookmarks();
        }
    }

    private async void BookmarkContextMoveTo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: BookmarkNode node }) return;
        await MoveBookmarkNodesAsync(new[] { node });
    }

    // En-tete discret du dossier de favoris ouvert depuis la barre :
    // contrairement a AddLumoraMenuHeader (utilise ailleurs - historique,
    // groupes d'onglets, Studio Lumora, et le menu contextuel des favoris),
    // le nom du dossier n'est PAS repete ici. Le bouton qu'on vient de
    // cliquer l'affiche deja - le redire etait juge inutile par l'utilisateur
    // (2026-08-22, capture d'ecran a l'appui). Seul le nombre d'elements est
    // garde, en retrait (petit, attenue, majuscules espacees) plutot qu'a la
    // meme taille qu'un vrai item de menu.
    private void AddBookmarkFolderCountHeader(IList<MenuFlyoutItemBase> items, string folderId)
    {
        items.Add(new MenuFlyoutItem
        {
            Text = BookmarkFolderChildCountLabel(folderId).ToUpperInvariant(),
            IsEnabled = false,
            FontSize = 11,
            CharacterSpacing = 40,
            Foreground = (Brush)RootShell.Resources["NovaTextMutedBrush"]
        });
    }

    private MenuFlyout CreateBookmarkFolderFlyout(BookmarkNode folder)
    {
        var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.Bottom };
        AddBookmarkFolderCountHeader(flyout.Items, folder.Id);

        // Contenu construit a la premiere ouverture seulement, pas au rendu de
        // la barre : un dossier issu d'un gros import navigateur (ex. "Autres
        // favoris") peut contenir des milliers d'items, et reconstruire tout
        // l'arbre de menus recursivement a CHAQUE rendu de la barre (import,
        // mais aussi chaque redimensionnement de fenetre) gelait totalement
        // l'application - signale par l'utilisateur, 2026-08-22.
        var built = false;
        flyout.Opening += (_, _) =>
        {
            if (built) return;
            built = true;
            AddBookmarkFlyoutItems(flyout.Items, folder.Id);
        };
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

            // Alternative au glisser (2026-08-14, demande explicite
            // utilisateur : "un clic droit sur le favori que je veux
            // d\u00E9placer... \u00E7a \u00E9vitera les fausses manipulations" - apres 3
            // correctifs reels du glisser-depose bases sur documentation puis
            // trace, celui-ci echouait encore chez l'utilisateur en
            // conditions reelles). Deplace d'UN cran parmi les freres du meme
            // parent (BookmarkStore.MoveNodeAdjacent, reutilise ReorderNode
            // deja teste) - fiable car base sur un simple clic, aucun geste
            // souris a interpreter. Desactive aux extremites plutot que
            // masque : garde une position stable dans le menu.
            var (isFirst, isLast) = BookmarkSiblingBoundaries(node);

            var moveBeforeItem = new MenuFlyoutItem
            {
                Text = "D\u00E9placer avant",
                Tag = node,
                Icon = CreateMenuGlyphIcon("\uE76B"),
                IsEnabled = !isFirst
            };
            moveBeforeItem.Click += BookmarkContextMoveBefore_Click;
            flyout.Items.Add(moveBeforeItem);

            var moveAfterItem = new MenuFlyoutItem
            {
                Text = "D\u00E9placer apr\u00E8s",
                Tag = node,
                Icon = CreateMenuGlyphIcon("\uE76C"),
                IsEnabled = !isLast
            };
            moveAfterItem.Click += BookmarkContextMoveAfter_Click;
            flyout.Items.Add(moveAfterItem);

            // "D\u00E9placer vers\u2026" (2026-08-31, session "gestion des favoris") :
            // contrairement aux deux entr\u00E9es ci-dessus (un cran parmi les
            // freres du meme parent), celle-ci range le favori/dossier DANS
            // n'importe quel autre dossier - voir MainWindow.BookmarksMoveTo.cs.
            var moveToItem = new MenuFlyoutItem
            {
                Text = "D\u00E9placer vers\u2026",
                Tag = node,
                Icon = new SymbolIcon(Symbol.MoveToFolder)
            };
            moveToItem.Click += BookmarkContextMoveTo_Click;
            flyout.Items.Add(moveToItem);

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

        // "Gerer les favoris" remplace le sous-menu "Studio Lumora" ici
        // (2026-08-10, "choses a revoir" - demande explicite utilisateur : le
        // reglage de la disposition de la fenetre n'a rien a faire sur le
        // clic droit d'UN favori precis, et l'acces au gestionnaire manquait -
        // convention deja etablie par Chrome/Edge/Firefox). Ouvre le panneau
        // directement sur le dossier concerne (celui qui contient ce favori,
        // ou le favori/dossier lui-meme s'il s'agit deja d'un dossier) plutot
        // que systematiquement a la racine.
        flyout.Items.Add(new MenuFlyoutSeparator());
        var manageItem = new MenuFlyoutItem
        {
            Text = "G\u00E9rer les favoris",
            Tag = node,
            Icon = CreateMenuGlyphIcon("\uE735")
        };
        manageItem.Click += BookmarkContextManage_Click;
        flyout.Items.Add(manageItem);
        HookFlyoutPointerSupport(flyout);

        return flyout;
    }

    private void BookmarkContextManage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: BookmarkNode node })
        {
            return;
        }

        var folderId = node.Kind == BookmarkKind.Folder ? node.Id : node.ParentId;
        ShowBookmarksFolder(folderId, "Favoris Lumora");
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

                // Ouvrir le dossier/Renommer/Supprimer retires d'ici (2026-08-22,
                // demande explicite utilisateur, capture d'ecran a l'appui) :
                // deja disponibles au clic droit (ContextFlyout ci-dessus,
                // CreateBookmarkContextFlyout) - les repeter dans CHAQUE
                // sous-dossier encombrait la liste sans rien apporter. Ce
                // sous-menu ne sert plus qu'a naviguer dans le contenu du dossier.
                //
                // MenuFlyoutSubItem n'expose pas d'evenement Opening dans ce
                // Windows App SDK (contrairement a MenuFlyout, voir
                // CreateBookmarkFolderFlyout) : impossible de differer la
                // construction de ce niveau precis. Le vrai gain vient du
                // niveau racine (CreateBookmarkFolderFlyout, ci-dessus) qui
                // n'est plus reconstruit automatiquement a chaque rendu de la
                // barre - seulement quand ce dossier est effectivement ouvert.
                AddBookmarkFlyoutItems(sub.Items, child.Id);
                if (sub.Items.Count == 0)
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
