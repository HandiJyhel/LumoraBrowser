using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;

namespace PulseBrowser.WinUI;

public sealed partial class MainWindow
{

    private void HistoryMenu_Click(object sender, RoutedEventArgs e)
    {
        _historySearch = string.Empty;
        HistorySearchBox.Text = string.Empty;
        RenderHistory();
        ShowPanel(HistoryPanel, "Historique");
    }

    private void DownloadsMenu_Click(object sender, RoutedEventArgs e)
    {
        RenderDownloads();
        ShowPanel(DownloadsPanel, "Telechargements");
    }
    private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _historySearch = HistorySearchBox.Text.Trim();
        RenderHistory();
    }

    private void HistoryList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not HistoryListItem item)
        {
            return;
        }

        NavigateCurrentTab(item.Entry.Url, item.Entry.Title);
        ShowPanel(BrowserPanel, item.Entry.Title);
    }

    private void HistoryList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (FindHistoryItem(e.OriginalSource as DependencyObject) is not HistoryListItem item)
        {
            return;
        }

        HistoryList.SelectedItem = item;
        CreateHistoryContextFlyout(item).ShowAt(HistoryList, e.GetPosition(HistoryList));
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

    private MenuFlyout CreateHistoryContextFlyout(HistoryListItem item)
    {
        var flyout = new MenuFlyout();
        var openItem = new MenuFlyoutItem { Text = "Ouvrir", Tag = item };
        openItem.Click += (s, _) =>
        {
            if (s is MenuFlyoutItem { Tag: HistoryListItem i })
            {
                NavigateCurrentTab(i.Entry.Url, i.Entry.Title);
                ShowPanel(BrowserPanel, i.Entry.Title);
            }
        };
        flyout.Items.Add(openItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        var removeItem = new MenuFlyoutItem { Text = "Supprimer de l'historique", Tag = item };
        removeItem.Click += (s, _) =>
        {
            if (s is MenuFlyoutItem { Tag: HistoryListItem i })
            {
                _history.Remove(i.Entry);
                RenderHistory();
                StatusText.Text = "Entree supprimee de l'historique.";
            }
        };
        flyout.Items.Add(removeItem);
        return flyout;
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        _history.Clear();
        RenderHistory();
        StatusText.Text = "Historique efface.";
    }

    private void CoreWebView2_DownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
    {
        var entry = new DownloadEntry(args.DownloadOperation);
        entry.OnChanged += () => DispatcherQueue.TryEnqueue(RenderDownloads);
        _downloads.Insert(0, entry);
        DispatcherQueue.TryEnqueue(() =>
        {
            RenderDownloads();
            StatusText.Text = $"Telechargement demarre: {entry.FileName}";
        });
    }

    private void AddHistoryEntry(string url, string title)
    {
        if (_isGuestMode) return;
        _history.Add(url, title);
    }

    private void RenderHistory()
    {
        _historyItems.Clear();
        var entries = string.IsNullOrWhiteSpace(_historySearch)
            ? _history.AllEntries()
            : (IEnumerable<HistoryEntry>)_history.Search(_historySearch);
        foreach (var entry in entries)
        {
            _historyItems.Add(HistoryListItemFor(entry));
        }
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
            FormatHistoryTime(entry.VisitedAt),
            entry);
    }

    private static string FormatHistoryTime(DateTimeOffset time)
    {
        var diff = DateTimeOffset.Now.Date - time.LocalDateTime.Date;
        if (diff.TotalDays < 1) return $"Aujourd'hui {time.LocalDateTime:HH:mm}";
        if (diff.TotalDays < 2) return $"Hier {time.LocalDateTime:HH:mm}";
        if (diff.TotalDays < 7)
        {
            return time.LocalDateTime.ToString("ddd HH:mm",
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
        }

        return time.LocalDateTime.ToString("dd/MM/yyyy HH:mm");
    }

    private void RenderDownloads()
    {
        DownloadsPanelItems.Children.Clear();
        if (_downloads.Count == 0)
        {
            DownloadsPanelItems.Children.Add(new TextBlock
            {
                Text = "Aucun telechargement dans cette session.",
                Opacity = 0.65,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var dl in _downloads)
        {
            DownloadsPanelItems.Children.Add(BuildDownloadCard(dl));
        }
    }

    private UIElement BuildDownloadCard(DownloadEntry dl)
    {
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var fileNameText = new TextBlock
        {
            Text = dl.FileName,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(fileNameText, 0);
        headerRow.Children.Add(fileNameText);
        var domainText = new TextBlock
        {
            Text = dl.SourceDomain,
            Opacity = 0.65,
            FontSize = 12,
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
            IsIndeterminate = dl.TotalBytes == 0 && !dl.IsCompleted && !dl.IsFailed,
            Visibility = dl.IsCompleted || dl.IsFailed ? Visibility.Collapsed : Visibility.Visible
        };

        var footerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0)
        };
        footerPanel.Children.Add(new TextBlock
        {
            Text = dl.State,
            Opacity = 0.68,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });
        if (dl.IsCompleted)
        {
            var openBtn = new Button { Content = "Ouvrir", Padding = new Thickness(12, 4, 12, 4) };
            openBtn.Click += (_, _) => OpenDownloadFile(dl.LocalPath);
            footerPanel.Children.Add(openBtn);
            var folderBtn = new Button { Content = "Dossier", Padding = new Thickness(12, 4, 12, 4) };
            folderBtn.Click += (_, _) => OpenDownloadFolder(dl.LocalPath);
            footerPanel.Children.Add(folderBtn);
        }

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
