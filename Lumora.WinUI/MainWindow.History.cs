using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Windows.System;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{

    private void HistoryMenu_Click(object sender, RoutedEventArgs e)
    {
        _historyPanel.SearchTerm = string.Empty;
        HistorySearchBox.Text = string.Empty;
        RenderHistory();
        ShowPanel(HistoryPanel, "Historique");
    }

    private void DownloadsMenu_Click(object sender, RoutedEventArgs e)
    {
        RenderDownloads();
        ShowPanel(DownloadsPanel, "Telechargements");
        _unseenDownloadsCount = 0;
        RefreshDownloadsIndicator();
    }
    private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _historyPanel.SearchTerm = HistorySearchBox.Text.Trim();
        RenderHistory();
        _ = AugmentHistoryWithSemanticResultsAsync(_historyPanel.SearchTerm);
    }

    // Complete les resultats de la recherche par mot-cle (deja affiches par
    // RenderHistory) avec les pages trouvees par sens uniquement, sans les
    // dupliquer. Ignore silencieusement si la recherche a change entre-temps
    // (l'utilisateur continue de taper) - evite d'injecter des resultats
    // perimes apres coup.
    private async Task AugmentHistoryWithSemanticResultsAsync(string query)
    {
        if (!_uiSettings.HistorySemanticSearchEnabled || query.Length < 4) return;

        var semanticItems = await SearchHistorySemanticAsync(query);
        if (!string.Equals(_historyPanel.SearchTerm, query, StringComparison.Ordinal)) return;

        var existingUrls = _historyPanel.Items
            .Where(i => i.Entry is not null)
            .Select(i => i.Entry!.Url)
            .ToHashSet();
        foreach (var item in semanticItems)
        {
            if (item.Entry is not null && existingUrls.Add(item.Entry.Url))
            {
                _historyPanel.Items.Add(item);
            }
        }
    }

    private void HistoryList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not HistoryListItem { Entry: not null } item)
        {
            return;
        }

        NavigateCurrentTab(item.Entry.Url, item.Entry.Title);
        ShowPanel(BrowserPanel, item.Entry.Title);
    }

    // Entree ouvre l'element selectionne, comme le double-clic - meme
    // correctif que BookmarksList (audit accessibilite moteur/motricite,
    // palier 0.93.x).
    private void HistoryList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        if (HistoryList.SelectedItem is not HistoryListItem { Entry: not null } item) return;

        e.Handled = true;
        NavigateCurrentTab(item.Entry.Url, item.Entry.Title);
        ShowPanel(BrowserPanel, item.Entry.Title);
    }

    private void HistoryList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (FindHistoryItem(e.OriginalSource as DependencyObject) is not { Entry: not null } item)
        {
            return;
        }

        HistoryList.SelectedItem = item;
        CreateHistoryContextFlyout(item.Entry).ShowAt(HistoryList, e.GetPosition(HistoryList));
        e.Handled = true;
    }

    private static HistoryListItem? FindHistoryItem(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: HistoryListItem item })
            {
                return item;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private MenuFlyout CreateHistoryContextFlyout(HistoryEntry entry)
    {
        var flyout = new MenuFlyout();
        AddLumoraMenuHeader(
            flyout.Items,
            entry.Title,
            TabHeaderHost(entry.Url),
            "\uE81C");
        var openItem = new MenuFlyoutItem { Text = "Ouvrir", Tag = entry };
        openItem.Click += (s, _) =>
        {
            if (s is MenuFlyoutItem { Tag: HistoryEntry i })
            {
                NavigateCurrentTab(i.Url, i.Title);
                ShowPanel(BrowserPanel, i.Title);
            }
        };
        flyout.Items.Add(openItem);
        var openInNewTabItem = new MenuFlyoutItem { Text = "Ouvrir dans un nouvel onglet", Tag = entry };
        openInNewTabItem.Click += (s, _) =>
        {
            if (s is MenuFlyoutItem { Tag: HistoryEntry i })
            {
                AddTab(i.Title, i.Url, select: true);
                ShowPanel(BrowserPanel, i.Title);
            }
        };
        flyout.Items.Add(openInNewTabItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        var removeItem = new MenuFlyoutItem { Text = "Supprimer de l'historique", Tag = entry };
        removeItem.Click += (s, _) =>
        {
            if (s is MenuFlyoutItem { Tag: HistoryEntry i })
            {
                _historyPanel.Store.Remove(i);
                _semanticIndex.RemoveByUrl(i.Url);
                RenderHistory();
                StatusText.Text = "Entree supprimee de l'historique.";
            }
        };
        flyout.Items.Add(removeItem);
        HookFlyoutPointerSupport(flyout);
        return flyout;
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        _historyPanel.Store.Clear();
        _semanticIndex.Clear();
        RenderHistory();
        StatusText.Text = "Historique efface.";
    }

    private void ClearDownloadsButton_Click(object sender, RoutedEventArgs e)
    {
        _historyPanel.Downloads.Clear();
        RenderDownloads();
        StatusText.Text = "Historique des telechargements efface.";
    }

    private void CoreWebView2_DownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
    {
        // Lumora a son propre suivi (panneau Telechargements + historique) :
        // on desactive la boite de dialogue native d'Edge/WebView2, qui peut
        // s'afficher detachee de la fenetre, y compris sur un autre ecran.
        args.Handled = true;

        var entry = new DownloadEntry(args.DownloadOperation);
        entry.OnChanged += () => DispatcherQueue.TryEnqueue(() =>
        {
            _historyPanel.Downloads.Upsert(entry.ToHistoryEntry());
            RenderDownloads();
        });
        _historyPanel.Downloads.Upsert(entry.ToHistoryEntry());
        NotifyDownloadStarted();
        DispatcherQueue.TryEnqueue(() =>
        {
            RenderDownloads();
            StatusText.Text = $"Telechargement demarre: {entry.FileName}";
        });
    }

    private void AddHistoryEntry(string url, string title)
    {
        if (_isGuestMode) return;
        _historyPanel.Store.Add(url, title);
    }

    private void RenderHistory()
    {
        _historyPanel.Items.Clear();
        var entries = string.IsNullOrWhiteSpace(_historyPanel.SearchTerm)
            ? _historyPanel.Store.AllEntries()
            : (IEnumerable<HistoryEntry>)_historyPanel.Store.Search(_historyPanel.SearchTerm);

        // Les entrees arrivent deja triees du plus recent au plus ancien
        // (HistoryStore.Add insere en tete) : un simple suivi du dernier
        // groupe rencontre suffit, pas besoin de trier/regrouper a part.
        string? lastGroup = null;
        foreach (var entry in entries)
        {
            var group = DateGroupLabel(entry.VisitedAt);
            if (group != lastGroup)
            {
                _historyPanel.Items.Add(HistoryListItem.GroupHeader(group));
                lastGroup = group;
            }

            _historyPanel.Items.Add(HistoryListItemFor(entry));
        }
    }

    private static string DateGroupLabel(DateTimeOffset visitedAt)
    {
        var today = DateTimeOffset.Now.Date;
        var days = (today - visitedAt.Date).Days;
        return days switch
        {
            0 => "Aujourd'hui",
            1 => "Hier",
            >= 2 and <= 6 => "Cette semaine",
            >= 7 and <= 30 => "Ce mois-ci",
            _ => "Plus tot"
        };
    }

    private HistoryListItem HistoryListItemFor(HistoryEntry entry)
    {
        _faviconCache.TryGetValue(entry.Url, out var iconPath);
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            iconPath = CachedFaviconPathFor(entry.Url);
        }

        var hasIcon = !string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath);
        var domain = Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) ? uri.Host : entry.Url;
        return new HistoryListItem(
            BookmarkGlyphs.Link,
            hasIcon ? new Uri(iconPath!).AbsoluteUri : string.Empty,
            hasIcon ? Visibility.Visible : Visibility.Collapsed,
            hasIcon ? Visibility.Collapsed : Visibility.Visible,
            string.IsNullOrWhiteSpace(entry.Title) ? domain : entry.Title,
            domain,
            HistoryTimeFormatter.Format(entry.VisitedAt),
            entry);
    }

    private void RenderDownloads()
    {
        DownloadsPanelItems.Children.Clear();
        var downloads = _historyPanel.Downloads.AllEntries();
        ClearDownloadsButton.IsEnabled = downloads.Count > 0;
        if (downloads.Count == 0)
        {
            DownloadsPanelItems.Children.Add(new TextBlock
            {
                Text = "Aucun telechargement local.",
                Opacity = 0.65,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var dl in downloads)
        {
            DownloadsPanelItems.Children.Add(BuildDownloadCard(dl));
        }
    }

    private UIElement BuildDownloadCard(DownloadHistoryEntry dl)
    {
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var fileNameText = new TextBlock
        {
            Text = dl.FileName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = AccessibilityBodyFontSize(),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(fileNameText, 0);
        headerRow.Children.Add(fileNameText);
        var domainText = new TextBlock
        {
            Text = dl.SourceDomain,
            Opacity = 0.65,
            FontSize = AccessibilitySecondaryFontSize(),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(domainText, 1);
        headerRow.Children.Add(domainText);

        var progressBar = new ProgressBar
        {
            Value = dl.ProgressPercent,
            Maximum = 100,
            Margin = new Thickness(0, 8, 0, 4),
            IsIndeterminate = !dl.HasKnownSize && dl.IsActive,
            Visibility = dl.IsCompleted || dl.IsFailed ? Visibility.Collapsed : Visibility.Visible
        };

        var fileExists = !string.IsNullOrWhiteSpace(dl.LocalPath) && File.Exists(dl.LocalPath);
        var footerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0)
        };
        footerPanel.Children.Add(new TextBlock
        {
            Text = fileExists || !dl.IsCompleted ? dl.StateLabel : "Fichier absent",
            Opacity = 0.68,
            FontSize = AccessibilitySecondaryFontSize(),
            VerticalAlignment = VerticalAlignment.Center
        });
        if (dl.IsCompleted && fileExists)
        {
            var openBtn = new Button { Content = "Ouvrir", Padding = new Thickness(12, 4, 12, 4), FontSize = AccessibilitySecondaryFontSize() };
            ApplyNovaControlAccessibility(openBtn, $"Ouvrir le telechargement {dl.FileName}");
            openBtn.Click += (_, _) => OpenDownloadFile(dl.LocalPath);
            footerPanel.Children.Add(openBtn);
            var folderBtn = new Button { Content = "Dossier", Padding = new Thickness(12, 4, 12, 4), FontSize = AccessibilitySecondaryFontSize() };
            ApplyNovaControlAccessibility(folderBtn, $"Ouvrir le dossier du telechargement {dl.FileName}");
            folderBtn.Click += (_, _) => OpenDownloadFolder(dl.LocalPath);
            footerPanel.Children.Add(folderBtn);
        }
        var removeBtn = new Button { Content = "Retirer", Padding = new Thickness(12, 4, 12, 4), FontSize = AccessibilitySecondaryFontSize() };
        ApplyNovaControlAccessibility(removeBtn, $"Retirer le telechargement {dl.FileName} de l'historique");
        removeBtn.Click += (_, _) =>
        {
            _historyPanel.Downloads.Remove(dl.Id);
            RenderDownloads();
            StatusText.Text = "Telechargement retire de l'historique.";
        };
        footerPanel.Children.Add(removeBtn);

        var body = new StackPanel { Spacing = 0 };
        body.Children.Add(headerRow);
        body.Children.Add(progressBar);
        body.Children.Add(footerPanel);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = body
        };
    }

    private static void OpenDownloadFile(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
    }

    private static void OpenDownloadFolder(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                { UseShellExecute = true });
        }
        catch { }
    }
}
