using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PulseBrowser.WinUI;

public sealed partial class MainWindow
{
    private sealed record BookmarkFolderChoice(string Id, string Label);

    private void BookmarksMenu_Click(object sender, RoutedEventArgs e)
    {
        ShowBookmarksFolder(_currentBookmarkFolderId, "Favoris Pulse");
    }

    private void ToolbarBookmarksMenu_Click(object sender, RoutedEventArgs e) =>
        ShowBookmarksFolder(BookmarkStore.ToolbarRootId, "Barre des favoris");

    private void OtherBookmarksMenu_Click(object sender, RoutedEventArgs e) =>
        ShowBookmarksFolder(BookmarkStore.OtherRootId, "Autres favoris");

    private void ImportMenu_Click(object sender, RoutedEventArgs e)
    {
        ReloadImportSources();
        ShowPanel(ImportPanel, "Import/export favoris");
    }

    private async void ExportMenu_Click(object sender, RoutedEventArgs e) =>
        await ExportBookmarksHtmlAsync();

    private void ToggleBookmarksBarMenu_Click(object sender, RoutedEventArgs e)
    {
        BookmarksBarSwitch.IsOn = !BookmarksBarSwitch.IsOn;
        ApplyBookmarksBarVisibility();
        SaveUiSettings();
        StatusText.Text = BookmarksBarSwitch.IsOn && !_compactModeEnabled
            ? "Barre de favoris visible."
            : "Barre de favoris masquee.";
    }

    private async void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var address = NormalizeAddress(AddressBox.Text);
        if (!BookmarkStore.IsWebUrl(address) && CurrentTab() is { } currentTab)
        {
            address = NormalizeAddress(currentTab.Address);
        }

        if (!BookmarkStore.IsWebUrl(address))
        {
            StatusText.Text = "Ouvre une page web avant de l'ajouter aux favoris.";
            return;
        }

        var existing = _allBookmarkNodes.FirstOrDefault(node =>
            node.Kind == BookmarkKind.Url &&
            node.Url.Equals(address, StringComparison.OrdinalIgnoreCase));
        var currentTitle = CurrentTab()?.Title ?? DisplayTitle(address);
        var result = await PromptBookmarkEditorAsync(address, currentTitle, existing);
        if (result.Cancelled)
        {
            StatusText.Text = "Ajout aux favoris annule.";
            return;
        }

        if (result.DeleteExisting && existing is not null)
        {
            DeleteBookmarkNode(existing);
            StatusText.Text = "Favori retire.";
            return;
        }

        var iconPath = CachedFaviconPathFor(address) ?? existing?.IconPath ?? string.Empty;
        _bookmarks.AddOrUpdateUrl(existing?.Id, result.FolderId, address, result.Title, iconPath);
        ReloadBookmarks();
        StatusText.Text = existing is null ? "Favori ajoute." : "Favori mis a jour.";
    }

    private async void ImportHtmlButton_Click(object sender, RoutedEventArgs e) =>
        await ImportBookmarksHtmlAsync();

    private void ImportBrowserButton_Click(object sender, RoutedEventArgs e) =>
        ImportSelectedBrowserSource(replaceExisting: false);

    private void ReplaceBrowserButton_Click(object sender, RoutedEventArgs e) =>
        ImportSelectedBrowserSource(replaceExisting: true);

    private void ImportSelectedBrowserSource(bool replaceExisting)
    {
        var selectedIndex = ImportSourcesList.SelectedIndex;
        if (selectedIndex < 0 && _importSources.Count > 0)
        {
            selectedIndex = 0;
        }

        if (selectedIndex < 0 || selectedIndex >= _importSources.Count)
        {
            StatusText.Text = "Aucune source navigateur compatible detectee.";
            return;
        }

        var source = _importSources[selectedIndex];
        var imported = replaceExisting
            ? _bookmarks.ReplaceWithImport(source.ReadTree())
            : _bookmarks.MergeImport(source.ReadTree());
        ReloadBookmarks();
        var mode = replaceExisting ? "remplacement" : "fusion";
        StatusText.Text = $"Import navigateur ({mode}) {source.Label}: {imported} favoris traites.";
        ShowBookmarksFolder(BookmarkStore.ToolbarRootId, "Favoris importes");
    }

    private async void ExportHtmlButton_Click(object sender, RoutedEventArgs e) =>
        await ExportBookmarksHtmlAsync();

    private void DeleteBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var item = BookmarksList.SelectedItem as BookmarkListItem
            ?? BookmarkFoldersList.SelectedItem as BookmarkListItem;
        if (item is null)
        {
            StatusText.Text = "Selectionne un favori ou un dossier a supprimer.";
            return;
        }

        DeleteBookmarkNode(item.Node);
    }

    private async void NewBookmarkFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var title = await PromptTextAsync("Nouveau dossier", "Nom du dossier", string.Empty);
        if (string.IsNullOrWhiteSpace(title))
        {
            StatusText.Text = "Creation de dossier annulee.";
            return;
        }

        _bookmarks.AddFolder(_currentBookmarkFolderId, title.Trim());
        ReloadBookmarks();
        StatusText.Text = $"Dossier cree: {title.Trim()}";
    }

    private async void RenameBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var item = BookmarksList.SelectedItem as BookmarkListItem
            ?? BookmarkFoldersList.SelectedItem as BookmarkListItem;
        if (item is null)
        {
            StatusText.Text = "Selectionne un favori ou un dossier a renommer.";
            return;
        }

        await RenameBookmarkNodeAsync(item.Node);
    }

    private void DeleteBookmarkNode(BookmarkNode node)
    {
        if (node.IsRoot)
        {
            StatusText.Text = "Les racines de favoris ne peuvent pas etre supprimees.";
            return;
        }

        var parentId = node.ParentId;
        _bookmarks.RemoveNode(node.Id);
        if (_currentBookmarkFolderId == node.Id || IsDescendantOf(node.Id, _currentBookmarkFolderId))
        {
            _currentBookmarkFolderId = string.IsNullOrWhiteSpace(parentId) ? BookmarkStore.ToolbarRootId : parentId;
        }

        ReloadBookmarks();
        StatusText.Text = "Element supprime des favoris.";
    }

    private async Task RenameBookmarkNodeAsync(BookmarkNode node)
    {
        if (node.IsRoot)
        {
            StatusText.Text = "Les racines de favoris ne peuvent pas etre renommees.";
            return;
        }

        var title = await PromptTextAsync("Renommer", "Nouveau nom", node.Title);
        if (string.IsNullOrWhiteSpace(title))
        {
            StatusText.Text = "Renommage annule.";
            return;
        }

        _bookmarks.RenameNode(node.Id, title.Trim());
        ReloadBookmarks();
        StatusText.Text = $"Element renomme: {title.Trim()}";
    }

    private bool IsDescendantOf(string ancestorId, string nodeId)
    {
        var current = _allBookmarkNodes.FirstOrDefault(node => node.Id == nodeId);
        while (current is not null && !string.IsNullOrWhiteSpace(current.ParentId))
        {
            if (current.ParentId == ancestorId)
            {
                return true;
            }

            current = _allBookmarkNodes.FirstOrDefault(node => node.Id == current.ParentId);
        }

        return false;
    }

    private void BookmarksSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _bookmarkSearch = BookmarksSearchBox.Text.Trim();
        RenderBookmarkContent();
    }

    private void BookmarkFoldersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BookmarkFoldersList.SelectedItem is not BookmarkListItem item)
        {
            return;
        }

        _currentBookmarkFolderId = item.Node.Id;
        _bookmarkSearch = string.Empty;
        BookmarksSearchBox.Text = string.Empty;
        RenderBookmarkContent();
        StatusText.Text = $"Dossier: {item.Node.Title}";
    }

    private void BookmarksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BookmarksList.SelectedItem is BookmarkListItem item)
        {
            StatusText.Text = item.Node.Kind == BookmarkKind.Url ? item.Node.Url : item.Node.Title;
        }
    }

    private void BookmarksList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (BookmarksList.SelectedItem is not BookmarkListItem item)
        {
            return;
        }

        OpenBookmarkNode(item.Node);
    }

    private void BookmarkFoldersList_RightTapped(object sender, RightTappedRoutedEventArgs e) =>
        ShowBookmarkListContextMenu(BookmarkFoldersList, e);

    private void BookmarksList_RightTapped(object sender, RightTappedRoutedEventArgs e) =>
        ShowBookmarkListContextMenu(BookmarksList, e);

    private void ShowBookmarkListContextMenu(ListView listView, RightTappedRoutedEventArgs e)
    {
        if (FindBookmarkItem(e.OriginalSource as DependencyObject) is not BookmarkListItem item)
        {
            return;
        }

        listView.SelectedItem = item;
        CreateBookmarkContextFlyout(item.Node).ShowAt(listView, e.GetPosition(listView));
        e.Handled = true;
    }

    private static BookmarkListItem? FindBookmarkItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: BookmarkListItem item })
            {
                return item;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void BookmarksBarSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            ApplyBookmarksBarVisibility();
            SaveUiSettings();
        }
    }
    private void LoadFaviconCacheFromBookmarks()
    {
        foreach (var node in _allBookmarkNodes)
        {
            if (node.Kind == BookmarkKind.Url && !string.IsNullOrWhiteSpace(node.IconPath) && File.Exists(node.IconPath))
            {
                _faviconCache[node.Url] = node.IconPath;
                // Indexer aussi par origine pour absorber les variantes (trailing slash, www., etc.)
                _faviconCache[OriginOf(node.Url)] = node.IconPath;
            }
        }
    }

    private string? CachedFaviconPathFor(string address)
    {
        if (!BookmarkStore.IsWebUrl(address))
        {
            return null;
        }

        if (_faviconCache.TryGetValue(address, out var cachedPath) && File.Exists(cachedPath))
        {
            return cachedPath;
        }

        var origin = OriginOf(address);
        if (_faviconCache.TryGetValue(origin, out var originCached) && File.Exists(originCached))
        {
            _faviconCache[address] = originCached;
            return originCached;
        }

        var originPath = Path.Combine(_profile.FaviconsDir, $"{HashOrigin(address)}.png");
        if (File.Exists(originPath))
        {
            _faviconCache[address] = originPath;
            _faviconCache[origin] = originPath;
            return originPath;
        }

        return null;
    }

    private void EnrichNodesWithFaviconCache(List<BookmarkNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.Kind != BookmarkKind.Url) continue;
            if (!string.IsNullOrWhiteSpace(node.IconPath) && File.Exists(node.IconPath)) continue;

            // Chercher dans le cache en mémoire d'abord
            if (_faviconCache.TryGetValue(node.Url, out var cached) && File.Exists(cached))
            {
                nodes[i] = node with { IconPath = cached };
                continue;
            }
            var origin = OriginOf(node.Url);
            if (_faviconCache.TryGetValue(origin, out var originCached) && File.Exists(originCached))
            {
                nodes[i] = node with { IconPath = originCached };
                continue;
            }

            // Chercher le fichier hash-origin sur le disque
            var hashPath = Path.Combine(_profile.FaviconsDir, $"{HashOrigin(node.Url)}.png");
            if (File.Exists(hashPath))
            {
                _faviconCache[node.Url] = hashPath;
                _faviconCache[origin]   = hashPath;
                nodes[i] = node with { IconPath = hashPath };
            }
        }
    }

    private void ReloadBookmarks()
    {
        _allBookmarkNodes = _bookmarks.AllNodes();
        EnrichNodesWithFaviconCache(_allBookmarkNodes);
        LoadFaviconCacheFromBookmarks();
        if (_allBookmarkNodes.All(node => node.Id != _currentBookmarkFolderId))
        {
            _currentBookmarkFolderId = BookmarkStore.ToolbarRootId;
        }

        RenderBookmarksTree();
        RenderBookmarkContent();

        BookmarksBarPanel.Children.Clear();
        OtherBookmarksBarHost.Children.Clear();
        var toolbarCount = 0;
        foreach (var node in _allBookmarkNodes
                     .Where(node => node.ParentId == BookmarkStore.ToolbarRootId)
                     .OrderBy(node => node.Position)
                     .Take(18))
        {
            var button = new Button
            {
                Content = BookmarkButtonContent(node),
                Tag = node,
                ContextFlyout = CreateBookmarkContextFlyout(node),
                Height = 26,
                MinHeight = 26,
                Padding = new Thickness(8, 1, 8, 1),
                CornerRadius = new CornerRadius(7),
                FontSize = 12
            };
            if (node.Kind == BookmarkKind.Folder)
            {
                button.Flyout = CreateBookmarkFolderFlyout(node.Id);
            }
            else
            {
                button.Click += BookmarkBarButton_Click;
            }
            BookmarksBarPanel.Children.Add(button);
            toolbarCount++;
        }

        if (toolbarCount == 0)
        {
            BookmarksBarPanel.Children.Add(new TextBlock
            {
                Text = "Barre vide",
                Opacity = 0.62,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        var otherRoot = _allBookmarkNodes.FirstOrDefault(node => node.Id == BookmarkStore.OtherRootId);
        if (otherRoot is not null)
        {
            OtherBookmarksBarHost.Children.Add(new Button
            {
                Content = BookmarkButtonContent(otherRoot),
                Tag = otherRoot,
                Flyout = CreateBookmarkFolderFlyout(otherRoot.Id),
                ContextFlyout = CreateBookmarkContextFlyout(otherRoot),
                Height = 26,
                MinHeight = 26,
                Padding = new Thickness(8, 1, 8, 1),
                CornerRadius = new CornerRadius(7),
                FontSize = 12
            });
        }
    }

    private void BookmarkBarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not BookmarkNode node)
        {
            return;
        }

        OpenBookmarkNode(node);
    }

    private static StackPanel BookmarkButtonContent(BookmarkNode node)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        panel.Children.Add(BookmarkIconElement(node, 14));
        panel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(node.Title) ? "(sans nom)" : node.Title,
            MaxWidth = 150,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });
        return panel;
    }

    private static FrameworkElement BookmarkIconElement(BookmarkNode node, double size)
    {
        if (node.Kind == BookmarkKind.Url && !string.IsNullOrWhiteSpace(node.IconPath) && File.Exists(node.IconPath))
        {
            return new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(node.IconUri)),
                Width = size,
                Height = size
            };
        }

        return new FontIcon
        {
            Glyph = GlyphFor(node),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = size
        };
    }

    private static string GlyphFor(BookmarkNode node) => BookmarkGlyphs.For(node);

    private void PreloadBookmarkFavicon(BookmarkNode node)
    {
        if (string.IsNullOrWhiteSpace(node.IconPath) || !File.Exists(node.IconPath)) return;
        var origin = OriginOf(node.Url);
        _faviconCache[node.Url] = node.IconPath;
        _faviconCache[origin] = node.IconPath;
    }

    private void OpenBookmarkNode(BookmarkNode node)
    {
        if (node.Kind == BookmarkKind.Url)
        {
            PreloadBookmarkFavicon(node);
            NavigateCurrentTab(node.Url, node.Title);
            ShowPanel(BrowserPanel, node.Title);
            return;
        }

        _currentBookmarkFolderId = node.Id;
        _bookmarkSearch = string.Empty;
        BookmarksSearchBox.Text = string.Empty;
        RenderBookmarksTree();
        RenderBookmarkContent();
        ShowPanel(BookmarksPanel, node.Title);
        StatusText.Text = $"Dossier: {node.Title}";
    }

    private MenuFlyout CreateBookmarkFolderFlyout(string folderId)
    {
        var flyout = new MenuFlyout();
        AddBookmarkFlyoutItems(flyout.Items, folderId);
        if (flyout.Items.Count == 0)
        {
            flyout.Items.Add(new MenuFlyoutItem
            {
                Text = "Dossier vide",
                IsEnabled = false
            });
        }

        return flyout;
    }

    private MenuFlyout CreateBookmarkContextFlyout(BookmarkNode node)
    {
        var flyout = new MenuFlyout();
        var openItem = new MenuFlyoutItem
        {
            Text = node.Kind == BookmarkKind.Url ? "Ouvrir" : "Ouvrir le dossier",
            Tag = node
        };
        openItem.Click += BookmarkContextOpen_Click;
        flyout.Items.Add(openItem);

        if (!node.IsRoot)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());

            var renameItem = new MenuFlyoutItem
            {
                Text = "Renommer",
                Tag = node
            };
            renameItem.Click += BookmarkContextRename_Click;
            flyout.Items.Add(renameItem);

            var deleteItem = new MenuFlyoutItem
            {
                Text = "Supprimer",
                Tag = node
            };
            deleteItem.Click += BookmarkContextDelete_Click;
            flyout.Items.Add(deleteItem);
        }

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
                    Text = child.Title,
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
                    Text = child.Title,
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

    private void ReloadImportSources()
    {
        _importSources.Clear();
        _importSources.AddRange(BrowserImportSource.Discover());
        ImportSourcesList.ItemsSource = _importSources.Select(source => source.Label).ToList();
        ImportSourcesStatusText.Text = _importSources.Count == 0
            ? "Aucun profil Chrome, Edge, Brave, Chromium ou Vivaldi compatible detecte sur cette session."
            : $"{_importSources.Count} source(s) detectee(s). Selectionne une source puis fusionne ou remplace les favoris Pulse.";
        if (_importSources.Count > 0)
        {
            ImportSourcesList.SelectedIndex = 0;
        }
        else
        {
            ImportSourcesList.SelectedIndex = -1;
        }
    }

    private async Task ImportBookmarksHtmlAsync()
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".html");
        picker.FileTypeFilter.Add(".htm");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            StatusText.Text = "Import annule.";
            return;
        }

        var content = await FileIO.ReadTextAsync(file);
        var imported = _bookmarks.MergeImport(BookmarkImportTree.FromHtml(content, Path.GetFileNameWithoutExtension(file.Name)));
        ReloadBookmarks();
        StatusText.Text = $"Import HTML: {imported} favoris ajoutes.";
        ShowPanel(BookmarksPanel, "Favoris importes");
    }

    private async Task ExportBookmarksHtmlAsync()
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = "pulse-browser-favoris";
        picker.FileTypeChoices.Add("Favoris HTML", new List<string> { ".html" });

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            StatusText.Text = "Export annule.";
            return;
        }

        var content = BookmarkHtmlExporter.Export(_bookmarks.AllNodes());
        await FileIO.WriteTextAsync(file, content);
        StatusText.Text = $"Favoris exportes: {file.Path}";
    }

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
            Text = existing?.Title ?? suggestedTitle,
            PlaceholderText = "Nom du favori",
            MinWidth = 360,
            MaxLength = 160
        };

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

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(titleBox);
        panel.Children.Add(folderBox);
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
        return (false, false, titleBox.Text.Trim(), selected?.Id ?? BookmarkStore.ToolbarRootId);
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
