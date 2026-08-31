using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// "Déplacer vers…" (2026-08-31, session "gestion des favoris" - jusqu'ici
// aucun moyen de ranger un favori DANS un dossier n'existait, ni par glisser
// ni par menu, voir BookmarkStore.MoveNode). Repli clavier/accessible au
// glisser-déposer (MainWindow.BookmarksGridDragDrop.cs) : réutilise le même
// picker "dossier + fil d'ariane" que l'ajout/édition d'un favori
// (BuildBookmarkFolderChoices, MainWindow.BookmarksDialogs.cs) plutôt qu'un
// nouvel arbre de dossiers dédié - déjà testé, déjà familier à l'utilisateur.
public sealed partial class MainWindow
{
    private async void MoveSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedBookmarkNodes().Where(node => !node.IsRoot).ToList();
        if (selected.Count == 0)
        {
            StatusText.Text = "Sélectionne un ou plusieurs favoris à déplacer.";
            return;
        }

        await MoveBookmarkNodesAsync(selected);
    }

    // Partagé avec le menu contextuel d'un favori/dossier (CreateBookmarkContextFlyout,
    // MainWindow.BookmarksFlyouts.cs) et avec le glisser-déposer quand il cible
    // un dossier trop loin pour être visible à l'écran (repli, voir
    // MainWindow.BookmarksGridDragDrop.cs).
    private async Task MoveBookmarkNodesAsync(IReadOnlyList<BookmarkNode> nodes)
    {
        if (nodes.Count == 0) return;

        // Un dossier ne peut pas être déplacé dans lui-même ni dans l'un de
        // ses propres sous-dossiers - BookmarkStore.MoveNode refuse déjà ce
        // cas, mais l'exclure ICI du picker évite de proposer un choix qui
        // échouerait silencieusement sans explication.
        var excludedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            excludedIds.Add(node.Id);
            if (node.Kind != BookmarkKind.Folder) continue;
            foreach (var candidate in _allBookmarkNodes)
            {
                if (IsDescendantOf(node.Id, candidate.Id))
                {
                    excludedIds.Add(candidate.Id);
                }
            }
        }

        var folders = BuildBookmarkFolderChoices().Where(choice => !excludedIds.Contains(choice.Id)).ToList();
        if (folders.Count == 0)
        {
            StatusText.Text = "Aucun autre dossier disponible pour ce déplacement.";
            return;
        }

        var folderBox = new ComboBox
        {
            Header = "Dossier de destination",
            ItemsSource = folders,
            DisplayMemberPath = nameof(BookmarkFolderChoice.Label),
            MinWidth = 360
        };
        var currentParentId = nodes.Select(node => node.ParentId).Distinct().Count() == 1 ? nodes[0].ParentId : null;
        folderBox.SelectedItem = folders.FirstOrDefault(folder => folder.Id == currentParentId) ?? folders.FirstOrDefault();

        var label = nodes.Count == 1
            ? $"Déplacer « {BookmarkReadableTitle(nodes[0])} » vers :"
            : $"Déplacer {nodes.Count} éléments vers :";

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(folderBox);

        var dialog = new ContentDialog
        {
            Title = "Déplacer vers…",
            Content = panel,
            PrimaryButtonText = "Déplacer ici",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary || folderBox.SelectedItem is not BookmarkFolderChoice target)
        {
            StatusText.Text = "Déplacement annulé.";
            return;
        }

        var moved = nodes.Count(node => _bookmarks.MoveNode(node.Id, target.Id));
        ReloadBookmarks();
        StatusText.Text = moved > 0
            ? $"{moved} élément(s) déplacé(s) vers {BookmarkTreePresenter.Breadcrumb(_allBookmarkNodes, target.Id)}."
            : "Rien n'a pu être déplacé.";
    }
}
