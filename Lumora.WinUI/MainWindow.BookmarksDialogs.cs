using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lumora.WinUI;

// Dialogues de creation/renommage de favoris et de dossiers. Extrait de
// MainWindow.Bookmarks.cs (god file), aucun changement de comportement.
public sealed partial class MainWindow
{
    private sealed record BookmarkFolderChoice(string Id, string Label);

    private const string NewBookmarkFolderChoiceId = "__new_folder__";

    private async Task<(bool Cancelled, bool DeleteExisting, string Title, string FolderId)> PromptBookmarkEditorAsync(
        string address,
        string suggestedTitle,
        BookmarkNode? existing)
    {
        var folders = BuildBookmarkFolderChoices();
        var selectedFolderId = existing?.ParentId ?? BookmarkStore.ToolbarRootId;
        var titleBox = new TextBox
        {
            Header = "Nom",
            Text = existing is not null && BookmarkStore.IsIconOnlyTitle(existing.Title)
                ? string.Empty
                : existing?.Title ?? suggestedTitle,
            PlaceholderText = "Nom du favori",
            MinWidth = 360,
            MaxLength = 160
        };
        var iconOnlyBox = new CheckBox
        {
            Content = "Nom invisible (icone seule dans la barre)",
            IsChecked = existing is not null && BookmarkStore.IsIconOnlyTitle(existing.Title)
        };

        folders.Add(new BookmarkFolderChoice(NewBookmarkFolderChoiceId, "+ Nouveau dossier..."));

        var folderBox = new ComboBox
        {
            Header = "Dossier",
            ItemsSource = folders,
            DisplayMemberPath = nameof(BookmarkFolderChoice.Label),
            MinWidth = 360
        };
        folderBox.SelectedItem = folders.FirstOrDefault(folder => folder.Id == selectedFolderId)
                                 ?? folders.FirstOrDefault(folder => folder.Id == BookmarkStore.ToolbarRootId)
                                 ?? folders.FirstOrDefault();

        var newFolderNameBox = new TextBox
        {
            PlaceholderText = "Nom du nouveau dossier",
            MinWidth = 360,
            MaxLength = 160,
            Visibility = Visibility.Collapsed
        };
        folderBox.SelectionChanged += (_, _) =>
        {
            newFolderNameBox.Visibility = (folderBox.SelectedItem as BookmarkFolderChoice)?.Id == NewBookmarkFolderChoiceId
                ? Visibility.Visible
                : Visibility.Collapsed;
        };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(titleBox);
        panel.Children.Add(iconOnlyBox);
        panel.Children.Add(folderBox);
        panel.Children.Add(newFolderNameBox);
        panel.Children.Add(new TextBlock
        {
            Text = address,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.68,
            FontSize = 12
        });

        var dialog = new ContentDialog
        {
            Title = existing is null ? "Ajouter aux favoris" : "Modifier le favori",
            Content = panel,
            PrimaryButtonText = "Enregistrer",
            SecondaryButtonText = existing is null ? string.Empty : "Supprimer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Secondary && existing is not null)
        {
            return (false, true, string.Empty, existing.ParentId);
        }

        if (result != ContentDialogResult.Primary)
        {
            return (true, false, string.Empty, string.Empty);
        }

        var selected = folderBox.SelectedItem as BookmarkFolderChoice;
        var title = iconOnlyBox.IsChecked == true ? BookmarkStore.InvisibleTitle : titleBox.Text;
        var folderId = selected?.Id ?? BookmarkStore.ToolbarRootId;
        if (folderId == NewBookmarkFolderChoiceId)
        {
            var newFolderName = newFolderNameBox.Text?.Trim();
            folderId = string.IsNullOrWhiteSpace(newFolderName)
                ? BookmarkStore.ToolbarRootId
                : _bookmarks.AddFolder(BookmarkStore.ToolbarRootId, newFolderName);
        }

        return (false, false, title, folderId);
    }

    private List<BookmarkFolderChoice> BuildBookmarkFolderChoices() =>
        _allBookmarkNodes
            .Where(node => node.Kind == BookmarkKind.Folder)
            .OrderBy(node => node.IsRoot ? 0 : 1)
            .ThenBy(node => BookmarkTreePresenter.Breadcrumb(_allBookmarkNodes, node.Id), StringComparer.CurrentCultureIgnoreCase)
            .Select(node => new BookmarkFolderChoice(
                node.Id,
                BookmarkTreePresenter.Breadcrumb(_allBookmarkNodes, node.Id)))
            .ToList();

    private async Task<string?> PromptTextAsync(string title, string placeholder, string initialValue)
    {
        var textBox = new TextBox
        {
            Text = initialValue,
            PlaceholderText = placeholder,
            MinWidth = 360
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = textBox,
            PrimaryButtonText = "Valider",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? textBox.Text : null;
    }
}
