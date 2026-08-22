using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lumora.WinUI;

// Plomberie d'import/export des favoris (sources de navigateurs tiers,
// HTML). Extrait de MainWindow.Bookmarks.cs (god file), aucun changement
// de comportement.
public sealed partial class MainWindow
{
    private void ReloadImportSources()
    {
        _importSources.Clear();
        _importSources.AddRange(BrowserImportSource.Discover());
        ImportSourcesList.ItemsSource = _importSources.Select(source => source.Label).ToList();
        ImportSourcesStatusText.Text = _importSources.Count == 0
            ? "Aucun profil Chrome, Edge, Brave, Chromium ou Vivaldi compatible détecté sur cette session."
            : $"{_importSources.Count} source(s) détectée(s). Sélectionne une source puis fusionne ou remplace les favoris Lumora.";
        if (_importSources.Count > 0)
        {
            ImportSourcesList.SelectedIndex = 0;
            UpdateImportComparisonText();
        }
        else
        {
            ImportSourcesList.SelectedIndex = -1;
            ImportComparisonText.Text = "Aucune comparaison disponible.";
        }
    }

    private List<BookmarkNode> SelectedBookmarkNodes()
    {
        var selected = BookmarksList.SelectedItems
            .OfType<BookmarkListItem>()
            .Select(item => item.Node)
            .ToList();

        if (selected.Count == 0 && BookmarksList.SelectedItem is BookmarkListItem listItem)
        {
            selected.Add(listItem.Node);
        }
        else if (selected.Count == 0 && BookmarkFoldersList.SelectedItem is BookmarkListItem folderItem)
        {
            selected.Add(folderItem.Node);
        }

        return selected;
    }

    private async Task<bool> ConfirmBookmarkActionAsync(string title, string message, string primaryText)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = primaryText,
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void ImportSourcesList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateImportComparisonText();

    private void UpdateImportComparisonText()
    {
        if (ImportComparisonText is null)
        {
            return;
        }

        var novaUrls = _bookmarks.AllNodes()
            .Where(node => node.Kind == BookmarkKind.Url)
            .Select(node => node.Url)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedIndex = ImportSourcesList.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= _importSources.Count)
        {
            ImportComparisonText.Text = $"{novaUrls.Count} favori(s) dans Lumora.";
            return;
        }

        try
        {
            var source = _importSources[selectedIndex];
            var sourceUrls = FlattenImportUrls(source.ReadTree()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = sourceUrls.Count(url => !novaUrls.Contains(url));
            var already = sourceUrls.Count - missing;
            ImportComparisonText.Text =
                $"{source.Browser} {source.Profile}: {sourceUrls.Count} favori(s), {already} déjà dans Lumora, {missing} absent(s).";
        }
        catch
        {
            ImportComparisonText.Text = $"{novaUrls.Count} favori(s) dans Lumora. Comparaison de la source impossible.";
        }
    }

    private static IEnumerable<string> FlattenImportUrls(BookmarkImportTree tree) =>
        FlattenImportUrls(tree.Toolbar).Concat(FlattenImportUrls(tree.Other));

    private static IEnumerable<string> FlattenImportUrls(IEnumerable<BookmarkImportItem> items)
    {
        foreach (var item in items)
        {
            if (item.Url is not null)
            {
                yield return item.Url;
                continue;
            }

            foreach (var child in FlattenImportUrls(item.Children))
            {
                yield return child;
            }
        }
    }

    private async Task ImportBookmarksHtmlAsync()
    {
        if (_bookmarkImportInProgress)
        {
            StatusText.Text = "Un import de favoris est déjà en cours.";
            return;
        }

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".html");
        picker.FileTypeFilter.Add(".htm");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            StatusText.Text = "Import annulé.";
            return;
        }

        _bookmarkImportInProgress = true;
        try
        {
            var content = await FileIO.ReadTextAsync(file);
            StatusText.Text = "Import des favoris en cours...";
            var tree = BookmarkImportTree.FromHtml(content, Path.GetFileNameWithoutExtension(file.Name));
            var imported = await Task.Run(() => _bookmarks.MergeImport(tree));
            ReloadBookmarks();
            StatusText.Text = $"Import HTML: {imported} favoris ajoutés.";
            ShowPanel(BookmarksPanel, "Favoris importés");
        }
        finally
        {
            _bookmarkImportInProgress = false;
        }
    }

    private async Task ExportBookmarksHtmlAsync()
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = "lumora-favoris";
        picker.FileTypeChoices.Add("Favoris HTML", new List<string> { ".html" });

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            StatusText.Text = "Export annulé.";
            return;
        }

        var content = BookmarkHtmlExporter.Export(_bookmarks.AllNodes());
        await FileIO.WriteTextAsync(file, content);
        StatusText.Text = $"Favoris exportés : {file.Path}";
    }

}
