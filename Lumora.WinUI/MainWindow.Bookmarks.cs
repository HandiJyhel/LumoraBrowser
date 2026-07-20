using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private void BookmarksMenu_Click(object sender, RoutedEventArgs e)
    {
        ShowBookmarksFolder(_currentBookmarkFolderId, "Favoris Lumora");
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
            node.Kind == BookmarkKind.Url && SameBookmarkUrl(node.Url, address));
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
        var saved = _bookmarks.AddOrUpdateUrl(existing?.Id, result.FolderId, address, result.Title, iconPath);
        ReloadBookmarks();
        if (saved is not null && saved.ParentId == BookmarkStore.ToolbarRootId)
        {
            RevealBookmarkInBar(saved.Id);
        }

        StatusText.Text = existing is null
            ? "Favori ajoute dans la barre des favoris."
            : "Favori mis a jour.";
    }

    private async void ImportHtmlButton_Click(object sender, RoutedEventArgs e) =>
        await ImportBookmarksHtmlAsync();

    private async void ImportBrowserButton_Click(object sender, RoutedEventArgs e) =>
        await ImportSelectedBrowserSourceAsync(replaceExisting: false);

    private async void ReplaceBrowserButton_Click(object sender, RoutedEventArgs e) =>
        await ImportSelectedBrowserSourceAsync(replaceExisting: true);

    private async Task ImportSelectedBrowserSourceAsync(bool replaceExisting)
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
        StatusText.Text = $"Recuperation des icones {source.Browser}...";
        var icons = await source.CopyFaviconsAsync(_profile.FaviconsDir);
        foreach (var pair in icons)
        {
            _faviconCache[pair.Key] = pair.Value;
        }

        var tree = source.ReadTree(icons);
        var imported = replaceExisting
            ? _bookmarks.ReplaceWithImport(tree)
            : _bookmarks.MergeImport(tree);
        ReloadBookmarks();
        var mode = replaceExisting ? "remplacement" : "fusion";
        StatusText.Text = $"Import navigateur ({mode}) {source.Label}: {imported} favoris traites, {icons.Count} icones recuperees.";
        ShowBookmarksFolder(BookmarkStore.ToolbarRootId, "Favoris importes");
    }

    private async void ExportHtmlButton_Click(object sender, RoutedEventArgs e) =>
        await ExportBookmarksHtmlAsync();

    private async void DeleteBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedBookmarkNodes().Where(node => !node.IsRoot).ToList();
        if (selected.Count == 0)
        {
            StatusText.Text = "Selectionne un ou plusieurs favoris a supprimer.";
            return;
        }

        var confirmed = await ConfirmBookmarkActionAsync(
            "Supprimer la selection",
            selected.Count == 1
                ? $"Supprimer {BookmarkReadableTitle(selected[0])} ?"
                : $"Supprimer {selected.Count} elements et leurs sous-dossiers ?",
            "Supprimer");
        if (!confirmed)
        {
            StatusText.Text = "Suppression annulee.";
            return;
        }

        var parentId = selected.FirstOrDefault()?.ParentId ?? BookmarkStore.ToolbarRootId;
        var removed = _bookmarks.RemoveNodes(selected.Select(node => node.Id));
        if (selected.Any(node => _currentBookmarkFolderId == node.Id || IsDescendantOf(node.Id, _currentBookmarkFolderId)))
        {
            _currentBookmarkFolderId = string.IsNullOrWhiteSpace(parentId) ? BookmarkStore.ToolbarRootId : parentId;
        }

        ReloadBookmarks();
        StatusText.Text = $"{removed} element(s) supprime(s) des favoris.";
    }

    private void SelectAllBookmarksButton_Click(object sender, RoutedEventArgs e)
    {
        if (_bookmarkItems.Count == 0)
        {
            StatusText.Text = "Aucun favori a selectionner dans ce dossier.";
            return;
        }

        BookmarksList.SelectAll();
        StatusText.Text = $"{_bookmarkItems.Count} element(s) selectionne(s).";
    }

    private async void ClearBookmarksButton_Click(object sender, RoutedEventArgs e)
    {
        var count = _allBookmarkNodes.Count(node => !node.IsRoot);
        if (count == 0)
        {
            StatusText.Text = "Aucun favori a vider.";
            return;
        }

        var confirmed = await ConfirmBookmarkActionAsync(
            "Vider tous les favoris",
            $"Supprimer les {count} favoris et dossiers de Lumora ? Une sauvegarde locale sera creee avant la suppression.",
            "Vider");
        if (!confirmed)
        {
            StatusText.Text = "Vidage annule.";
            return;
        }

        var removed = _bookmarks.ClearUserBookmarks();
        _currentBookmarkFolderId = BookmarkStore.ToolbarRootId;
        _bookmarkSearch = string.Empty;
        BookmarksSearchBox.Text = string.Empty;
        ReloadBookmarks();
        StatusText.Text = $"{removed} element(s) supprime(s). Pret pour tester une importation propre.";
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
        var selected = SelectedBookmarkNodes();
        var node = selected.Count == 1
            ? selected[0]
            : (BookmarkFoldersList.SelectedItem as BookmarkListItem)?.Node;
        if (node is null)
        {
            StatusText.Text = "Selectionne un seul favori ou dossier a renommer.";
            return;
        }

        await RenameBookmarkNodeAsync(node);
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

        var title = await PromptTextAsync("Renommer", "Nouveau nom", BookmarkStore.IsIconOnlyTitle(node.Title) ? string.Empty : node.Title);
        if (title is null)
        {
            StatusText.Text = "Renommage annule.";
            return;
        }

        _bookmarks.RenameNode(node.Id, title);
        ReloadBookmarks();
        StatusText.Text = $"Element renomme: {BookmarkReadableTitle(node with { Title = title })}";
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
        if (BookmarksList.SelectedItems.Count > 1)
        {
            StatusText.Text = $"{BookmarksList.SelectedItems.Count} element(s) selectionne(s).";
        }
        else if (BookmarksList.SelectedItem is BookmarkListItem item)
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
            if (node.Kind == BookmarkKind.Url &&
                !string.IsNullOrWhiteSpace(node.IconPath) &&
                FaviconQuality.IsUsablePngFile(node.IconPath))
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

        if (_faviconCache.TryGetValue(address, out var cachedPath) &&
            FaviconQuality.IsUsablePngFile(cachedPath))
        {
            return cachedPath;
        }

        var origin = OriginOf(address);
        if (_faviconCache.TryGetValue(origin, out var originCached) &&
            FaviconQuality.IsUsablePngFile(originCached))
        {
            _faviconCache[address] = originCached;
            return originCached;
        }

        foreach (var originPath in FaviconHashPathCandidates(address))
        {
            if (FaviconQuality.IsUsablePngFile(originPath))
            {
                _faviconCache[address] = originPath;
                _faviconCache[origin] = originPath;
                return originPath;
            }
        }

        return null;
    }

    // Fichiers hash-origine candidats sur le disque. L'icône est enregistrée sous
    // l'origine de la page VISITÉE (https en pratique), alors qu'un favori importé
    // d'un autre navigateur est souvent resté en http:// : les deux schemes doivent
    // être tentés, sinon des favoris pourtant visités (ex. allocine.fr, amazon.fr)
    // ne retrouvent jamais leur icône.
    private IEnumerable<string> FaviconHashPathCandidates(string url)
    {
        yield return Path.Combine(_profile.FaviconsDir, $"{HashOrigin(url)}.png");

        var alternate = AlternateSchemeUrl(url);
        if (alternate is not null)
            yield return Path.Combine(_profile.FaviconsDir, $"{HashOrigin(alternate)}.png");
    }

    private static string? AlternateSchemeUrl(string url)
    {
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return $"http://{url["https://".Length..]}";
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return $"https://{url["http://".Length..]}";
        return null;
    }

    private void EnrichNodesWithFaviconCache(List<BookmarkNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.Kind != BookmarkKind.Url) continue;
            if (!string.IsNullOrWhiteSpace(node.IconPath) &&
                FaviconQuality.IsUsablePngFile(node.IconPath)) continue;

            // IconPath mort mais fichier probablement encore là : un profil déplacé ou
            // renommé (ex. E:\...\PulseBrowser\... → E:\...\LumoraBrowser\...) invalide
            // tous les chemins absolus mémorisés dans les favoris, alors que les .png
            // existent toujours dans le dossier favicons actuel sous le même nom.
            if (!string.IsNullOrWhiteSpace(node.IconPath))
            {
                var relocated = Path.Combine(_profile.FaviconsDir, Path.GetFileName(node.IconPath));
                if (FaviconQuality.IsUsablePngFile(relocated))
                {
                    _faviconCache[node.Url] = relocated;
                    _faviconCache[OriginOf(node.Url)] = relocated;
                    nodes[i] = node with { IconPath = relocated };
                    continue;
                }
            }

            // Chercher dans le cache en mémoire d'abord
            if (_faviconCache.TryGetValue(node.Url, out var cached) &&
                FaviconQuality.IsUsablePngFile(cached))
            {
                nodes[i] = node with { IconPath = cached };
                continue;
            }
            var origin = OriginOf(node.Url);
            if (_faviconCache.TryGetValue(origin, out var originCached) &&
                FaviconQuality.IsUsablePngFile(originCached))
            {
                nodes[i] = node with { IconPath = originCached };
                continue;
            }

            // Chercher le fichier hash-origin sur le disque (les deux schemes)
            foreach (var hashPath in FaviconHashPathCandidates(node.Url))
            {
                if (FaviconQuality.IsUsablePngFile(hashPath))
                {
                    _faviconCache[node.Url] = hashPath;
                    _faviconCache[origin]   = hashPath;
                    nodes[i] = node with { IconPath = hashPath };
                    break;
                }
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
        RenderBookmarksBar();
        UpdateBookmarkStar();
    }

    // ── Étoile d'état de la barre d'outils ───────────────────────────────────

    // Le WebView2 rapporte « https://site.fr/ » quand l'utilisateur a saisi
    // « https://site.fr » : l'égalité d'URL de favori ignore le slash final.
    private static bool SameBookmarkUrl(string? a, string? b) =>
        a is not null && b is not null &&
        a.TrimEnd('/').Equals(b.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    private Brush? _bookmarkStarDefaultForeground;

    // Reflète dans la barre d'outils si la page courante est en favori :
    // étoile pleine couleur accent si oui, contour neutre sinon.
    private void UpdateBookmarkStar(string? address = null)
    {
        address ??= CurrentTab()?.Address;
        var bookmarked = !string.IsNullOrWhiteSpace(address) &&
                         BookmarkStore.IsWebUrl(address) &&
                         _allBookmarkNodes.Any(node =>
                             node.Kind == BookmarkKind.Url && SameBookmarkUrl(node.Url, address));

        _bookmarkStarDefaultForeground ??= BookmarkStarIcon.Foreground;
        BookmarkStarIcon.Glyph = bookmarked ? "\uE735" : "\uE734";
        BookmarkStarIcon.Foreground = bookmarked
            ? (Brush)RootShell.Resources["NovaAccentBrush"]
            : _bookmarkStarDefaultForeground;
        var label = bookmarked ? "Page en favori - modifier ou retirer" : "Ajouter aux favoris";
        ToolTipService.SetToolTip(AddBookmarkButton, label);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(AddBookmarkButton, label);

        // Le bouton Mode lecture suit la même cadence de rafraîchissement que
        // l'étoile (navigation, changement d'onglet, restauration).
        UpdateReaderModeUi(address);
    }

    private void RenderBookmarksBar()
    {
        BookmarksBarPanel.Children.Clear();
        OtherBookmarksBarHost.Children.Clear();
        var toolbarNodes = _allBookmarkNodes
            .Where(node => node.ParentId == BookmarkStore.ToolbarRootId)
            .OrderBy(node => node.Position)
            .ToList();
        var visibleCount = VisibleBookmarkBarCount(toolbarNodes);
        var visibleNodes = toolbarNodes.Take(visibleCount).ToList();
        var overflowNodes = toolbarNodes.Skip(visibleCount).ToList();

        foreach (var node in visibleNodes)
        {
            BookmarksBarPanel.Children.Add(CreateBookmarkBarButton(node));
        }

        if (overflowNodes.Count > 0)
        {
            BookmarksBarPanel.Children.Add(CreateBookmarksOverflowButton(overflowNodes));
        }

        if (toolbarNodes.Count == 0)
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
            OtherBookmarksBarHost.Children.Add(CreateBookmarkBarButton(otherRoot));
        }
    }

    private int VisibleBookmarkBarCount(IReadOnlyList<BookmarkNode> nodes)
    {
        if (nodes.Count <= 0)
        {
            return 0;
        }

        var available = BookmarksBarRow.ActualWidth - OtherBookmarksBarHost.ActualWidth - 56;
        if (available <= 0)
        {
            return Math.Min(nodes.Count, 20);
        }

        var used = 0d;
        var count = 0;
        for (var index = 0; index < nodes.Count; index++)
        {
            var remaining = nodes.Count - index - 1;
            var reserveOverflow = remaining > 0 ? 28d : 0d;
            var nextWidth = EstimateBookmarkBarWidth(nodes[index]);
            if (count > 0 && used + nextWidth + reserveOverflow > available)
            {
                break;
            }

            used += nextWidth;
            count++;
        }

        return Math.Clamp(count, 1, nodes.Count);
    }

    private static double EstimateBookmarkBarWidth(BookmarkNode node)
    {
        if (BookmarkStore.IsIconOnlyTitle(node.Title))
        {
            return 24d;
        }

        var title = BookmarkBarTitle(node);
        var textWidth = Math.Min(Math.Max(title.Length * 7d, 30d), 118d);
        return 28d + textWidth;
    }

    private Button CreateBookmarkBarButton(BookmarkNode node)
    {
        var button = new Button
        {
            Content = BookmarkButtonContent(node),
            Tag = node,
            ContextFlyout = CreateBookmarkContextFlyout(node),
            Height = 24,
            MinHeight = 24,
            MinWidth = 0,
            Padding = BookmarkStore.IsIconOnlyTitle(node.Title)
                ? new Thickness(4, 0, 4, 0)
                : new Thickness(6, 0, 8, 0),
            CornerRadius = new CornerRadius(4),
            FontSize = 11,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0)
        };
        ApplyNovaControlAccessibility(button, AccessibleBookmarkLabel(node));
        if (node.Kind == BookmarkKind.Folder)
        {
            button.Flyout = CreateBookmarkFolderFlyout(node.Id);
        }
        else
        {
            button.Click += BookmarkBarButton_Click;
        }

        return button;
    }

    private Button CreateBookmarksOverflowButton(IReadOnlyList<BookmarkNode> overflowNodes)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = "\u00BB",
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                LineHeight = 16,
                VerticalAlignment = VerticalAlignment.Center
            },
            Height = 24,
            MinHeight = 24,
            Width = 24,
            MinWidth = 24,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Flyout = CreateBookmarksOverflowFlyout(overflowNodes)
        };
        ApplyNovaControlAccessibility(button, $"Afficher {overflowNodes.Count} favori(s) supplementaire(s)");
        ToolTipService.SetToolTip(button, "Favoris supplementaires");
        return button;
    }

    private MenuFlyout CreateBookmarksOverflowFlyout(IReadOnlyList<BookmarkNode> overflowNodes)
    {
        var flyout = new MenuFlyout();
        foreach (var node in overflowNodes)
        {
            if (node.Kind == BookmarkKind.Folder)
            {
                var sub = new MenuFlyoutSubItem
                {
                    Text = BookmarkReadableTitle(node),
                    Tag = node,
                    ContextFlyout = CreateBookmarkContextFlyout(node)
                };
                AddBookmarkFlyoutItems(sub.Items, node.Id);
                if (sub.Items.Count == 0)
                {
                    sub.Items.Add(new MenuFlyoutItem { Text = "Dossier vide", IsEnabled = false });
                }

                flyout.Items.Add(sub);
                continue;
            }

            var item = new MenuFlyoutItem
            {
                Text = BookmarkReadableTitle(node),
                Tag = node,
                ContextFlyout = CreateBookmarkContextFlyout(node)
            };
            item.Click += BookmarkFlyoutUrl_Click;
            flyout.Items.Add(item);
        }

        return flyout;
    }

    private void BookmarksBarRow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_allBookmarkNodes.Count > 0)
        {
            RenderBookmarksBar();
        }
    }

    private static string AccessibleBookmarkLabel(BookmarkNode node)
    {
        var title = BookmarkReadableTitle(node);
        return node.Kind == BookmarkKind.Folder
            ? $"Ouvrir le dossier de favoris {title}"
            : $"Ouvrir le favori {title}";
    }

    private void RevealBookmarkInBar(string bookmarkId)
    {
        if (string.IsNullOrWhiteSpace(bookmarkId))
        {
            return;
        }

        void BringIntoView()
        {
            BookmarksBarPanel.UpdateLayout();
            foreach (var child in BookmarksBarPanel.Children.OfType<FrameworkElement>())
            {
                if (child.Tag is BookmarkNode node && node.Id == bookmarkId)
                {
                    child.StartBringIntoView(new BringIntoViewOptions
                    {
                        AnimationDesired = true,
                        HorizontalAlignmentRatio = 0.5
                    });
                    break;
                }
            }
        }

        BringIntoView();
        DispatcherQueue.TryEnqueue(BringIntoView);
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
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(BookmarkIconElement(node, 16));
        var title = BookmarkBarTitle(node);
        if (!string.IsNullOrEmpty(title))
        {
            panel.Children.Add(new TextBlock
            {
                Text = title,
                MaxWidth = 118,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                LineHeight = 15
            });
        }
        return panel;
    }

    private static FrameworkElement BookmarkIconElement(BookmarkNode node, double size)
    {
        if (node.Kind == BookmarkKind.Url &&
            !string.IsNullOrWhiteSpace(node.IconPath) &&
            FaviconQuality.IsUsablePngFile(node.IconPath))
        {
            return new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(node.IconUri)),
                Width = size,
                Height = size
            };
        }

        return new SymbolIcon
        {
            Symbol = node.Kind == BookmarkKind.Folder ? Symbol.Folder : Symbol.Link,
            Width = size,
            Height = size
        };
    }

    private static string BookmarkBarTitle(BookmarkNode node)
    {
        if (BookmarkStore.IsIconOnlyTitle(node.Title))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(node.Title) ? "(sans nom)" : node.Title;
    }

    private static string BookmarkReadableTitle(BookmarkNode node)
    {
        if (BookmarkStore.IsIconOnlyTitle(node.Title))
        {
            return "(icone seule)";
        }

        return string.IsNullOrWhiteSpace(node.Title) ? "(sans nom)" : node.Title.Trim();
    }

    private void PreloadBookmarkFavicon(BookmarkNode node)
    {
        if (string.IsNullOrWhiteSpace(node.IconPath) ||
            !FaviconQuality.IsUsablePngFile(node.IconPath)) return;
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

}
