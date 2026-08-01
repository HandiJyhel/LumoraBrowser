using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Flux RSS/Atom ────────────────────────────────────────────────────────
    // Liste de flux a gauche, articles du flux selectionne a droite - meme
    // agencement que le panneau Notes. Les articles ne sont jamais persistes :
    // juste recuperes en direct a chaque selection/rafraichissement (seul
    // l'identifiant des articles lus est retenu, dans RssFeedStore).

    private string? _selectedRssFeedId;
    private IReadOnlyList<RssArticle> _currentRssArticles = Array.Empty<RssArticle>();
    private bool _suppressRssSelection;
    private DispatcherTimer? _rssTimer;

    // Verification periodique en arriere-plan (toutes les 30 min) : le but du
    // RSS etant d'etre tenu au courant, pas seulement de consulter a la
    // demande. Pas de flux en mode invite (RssFeedStore les vide de toute
    // facon), donc rien a verifier dans ce cas.
    private void InitRssTimer()
    {
        _rssTimer?.Stop();
        _rssTimer = null;
        if (_isGuestMode) return;

        _rssTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _rssTimer.Tick += RssTimer_Tick;
        _rssTimer.Start();
    }

    private async void RssTimer_Tick(object? sender, object e) =>
        await CheckRssFeedsForNewArticlesAsync();

    private async Task CheckRssFeedsForNewArticlesAsync()
    {
        foreach (var feed in _rssFeeds.All())
        {
            try
            {
                var result = await RssFeedService.FetchAsync(feed.Url);
                _rssFeeds.MarkFetched(feed.Id, result.Title, error: null);
                _rssFeeds.MarkArticlesKnown(feed.Id, result.Articles.Select(a => a.Id));
            }
            catch (Exception ex)
            {
                _rssFeeds.MarkFetched(feed.Id, title: null, ex.Message);
            }
        }

        UpdateRssBadge();
        if (RssPanel.Visibility == Visibility.Visible) RefreshRssFeedsList();
    }

    private void RssMenu_Click(object sender, RoutedEventArgs e)
    {
        RefreshRssFeedsList();
        ShowPanel(RssPanel, "Flux RSS");
    }

    private void RefreshRssFeedsList()
    {
        _suppressRssSelection = true;
        RssFeedsList.Items.Clear();
        var feeds = _rssFeeds.All();
        foreach (var feed in feeds)
        {
            var item = new ListViewItem { Content = RssFeedListItemContent(feed), Tag = feed.Id };
            AutomationProperties.SetName(item, feed.Title);
            RssFeedsList.Items.Add(item);
            if (feed.Id == _selectedRssFeedId)
            {
                RssFeedsList.SelectedItem = item;
            }
        }
        RssFeedsEmptyText.Visibility = feeds.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _suppressRssSelection = false;

        if (_selectedRssFeedId is null || feeds.All(f => f.Id != _selectedRssFeedId))
        {
            _selectedRssFeedId = null;
            _currentRssArticles = Array.Empty<RssArticle>();
            RssArticlesHost.Visibility = Visibility.Collapsed;
            RssArticlesEmptyText.Visibility = Visibility.Visible;
        }

        UpdateRssBadge();
    }

    // Compteur d'articles non lus (deja rencontres, jamais ouverts) sur
    // l'icone Flux RSS - mis a jour a chaque rafraichissement de la liste des
    // flux et par la verification periodique en arriere-plan.
    private void UpdateRssBadge()
    {
        var unread = _rssFeeds.TotalUnreadCount();
        RssModuleBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
        RssModuleBadgeText.Text = unread > 99 ? "99+" : unread.ToString();

        // Le badge visuel ne bougeait jamais le nom accessible du bouton : un
        // lecteur d'ecran ne pouvait jamais savoir qu'un article non lu etait
        // arrive sans ouvrir le panneau.
        var label = unread > 0
            ? $"Flux RSS - {unread} non lu(s)"
            : "Flux RSS";
        AutomationProperties.SetName(RssModuleButton, label);
    }

    private static StackPanel RssFeedListItemContent(RssFeed feed)
    {
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(4) };
        panel.Children.Add(new TextBlock { Text = feed.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var status = feed.LastError is not null
            ? $"Erreur : {feed.LastError}"
            : feed.LastFetchedAt is { } fetched
                ? $"Actualisé {fetched:dd/MM HH:mm}"
                : "Jamais actualisé";
        panel.Children.Add(new TextBlock { Text = status, Opacity = 0.6, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
        return panel;
    }

    private void RssFeedsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressRssSelection) return;
        if (RssFeedsList.SelectedItem is not ListViewItem { Tag: string feedId }) return;
        _ = LoadRssFeedArticlesAsync(feedId);
    }

    private async Task LoadRssFeedArticlesAsync(string feedId)
    {
        var feed = _rssFeeds.Find(feedId);
        if (feed is null) return;

        _selectedRssFeedId = feedId;
        RssArticlesHost.Visibility = Visibility.Visible;
        RssArticlesEmptyText.Visibility = Visibility.Collapsed;
        RssFeedTitleText.Text = feed.Title;
        ToolTipService.SetToolTip(RssFeedTitleText, feed.Title);
        RssArticlesList.Items.Clear();
        RssArticlesStatusText.Text = "Chargement...";
        RssRefreshButton.IsEnabled = false;

        try
        {
            var result = await RssFeedService.FetchAsync(feed.Url);
            _rssFeeds.MarkFetched(feedId, result.Title, error: null);
            _rssFeeds.MarkArticlesKnown(feedId, result.Articles.Select(a => a.Id));
            DisplayFetchedArticles(feed, result.Articles);
        }
        catch (Exception ex)
        {
            _rssFeeds.MarkFetched(feedId, title: null, ex.Message);
            _currentRssArticles = Array.Empty<RssArticle>();
            RssArticlesList.Items.Clear();
            RssArticlesStatusText.Text = $"Échec du chargement : {ex.Message}";
        }
        finally
        {
            RssRefreshButton.IsEnabled = true;
            // Met a jour l'etat (erreur/date d'actualisation) affiche dans la liste des flux.
            RefreshRssFeedsList();
        }
    }

    // Factorise l'affichage d'un lot d'articles deja recupere - utilise par
    // LoadRssFeedArticlesAsync (apres un fetch) et par l'ajout de flux (qui a
    // deja le resultat sous la main, pas besoin d'un deuxieme fetch juste
    // pour afficher ce qu'on vient de recuperer).
    private void DisplayFetchedArticles(RssFeed feed, IReadOnlyList<RssArticle> articles)
    {
        RssArticlesHost.Visibility = Visibility.Visible;
        RssArticlesEmptyText.Visibility = Visibility.Collapsed;
        _currentRssArticles = articles
            .OrderByDescending(a => a.Published ?? DateTimeOffset.MinValue)
            .ToList();
        RssFeedTitleText.Text = feed.Title;
        ToolTipService.SetToolTip(RssFeedTitleText, feed.Title);
        RenderRssArticles(feed);
        RssArticlesStatusText.Text = _currentRssArticles.Count == 0
            ? "Aucun article dans ce flux."
            : $"{_currentRssArticles.Count} article(s).";
    }

    private void RenderRssArticles(RssFeed feed)
    {
        RssArticlesList.Items.Clear();
        foreach (var article in _currentRssArticles)
        {
            var isRead = feed.ReadArticleIds.Contains(article.Id);
            var item = new ListViewItem
            {
                Content = RssArticleListItemContent(article, isRead),
                Tag = article.Id,
                Opacity = isRead ? 0.55 : 1.0
            };
            AutomationProperties.SetName(item, isRead ? $"{article.Title} (lu)" : article.Title);
            RssArticlesList.Items.Add(item);
        }
    }

    private static StackPanel RssArticleListItemContent(RssArticle article, bool isRead)
    {
        var panel = new StackPanel { Spacing = 3, Margin = new Thickness(4, 8, 4, 8) };
        panel.Children.Add(new TextBlock
        {
            Text = article.Title,
            FontWeight = isRead ? Microsoft.UI.Text.FontWeights.Normal : Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        if (article.Published is { } published)
        {
            panel.Children.Add(new TextBlock { Text = published.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), Opacity = 0.6, FontSize = 12 });
        }
        if (!string.IsNullOrWhiteSpace(article.Summary))
        {
            panel.Children.Add(new TextBlock { Text = article.Summary, Opacity = 0.75, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis });
        }
        return panel;
    }

    // Clic = ouvre l'article dans un nouvel onglet et le marque lu - une liste
    // de flux sert a lire les articles sur le vrai site, pas a les consulter
    // dans le panneau lui-meme.
    private void RssArticlesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        // ItemClickEventArgs.ClickedItem rapporte le Content du ListViewItem
        // (le StackPanel construit par RssArticleListItemContent), pas le
        // ListViewItem lui-meme - constate en conditions reelles (le cast
        // direct en ListViewItem echouait systematiquement). Il faut donc
        // retrouver le conteneur dont le Content correspond, pour lire son Tag.
        var container = RssArticlesList.Items.OfType<ListViewItem>()
            .FirstOrDefault(item => ReferenceEquals(item.Content, e.ClickedItem));
        if (container is not { Tag: string articleId }) return;

        var article = _currentRssArticles.FirstOrDefault(a => a.Id == articleId);
        if (article is null || string.IsNullOrWhiteSpace(article.Link)) return;

        if (_selectedRssFeedId is not null)
        {
            var feed = _rssFeeds.Find(_selectedRssFeedId);
            if (feed is not null)
            {
                _rssFeeds.MarkArticleRead(_selectedRssFeedId, articleId);
                RenderRssArticles(feed);
                UpdateRssBadge();
            }
        }

        AddTab(article.Title, article.Link, select: true);
        ShowPanel(BrowserPanel, article.Title);
    }

    private void RssRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRssFeedId is not null) _ = LoadRssFeedArticlesAsync(_selectedRssFeedId);
    }

    private async void RssRemoveFeedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRssFeedId is null) return;
        var feed = _rssFeeds.Find(_selectedRssFeedId);
        if (feed is null) return;

        var confirm = new ContentDialog
        {
            Title = "Supprimer ce flux",
            Content = $"Ne plus suivre « {feed.Title} » ?",
            PrimaryButtonText = "Supprimer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        _rssFeeds.Remove(feed.Id);
        _selectedRssFeedId = null;
        RefreshRssFeedsList();
        StatusText.Text = $"{feed.Title} : flux supprimé.";
    }

    private bool _rssAddDialogOpen;

    // Garde de reentrance : ContentDialog.ShowAsync leve une exception non
    // rattrapable ("Only a single ContentDialog can be open at any time")
    // si ce clic se declenche une deuxieme fois avant la fermeture du premier
    // dialogue (ex. double-clic, ou une deuxieme activation pendant l'attente
    // asynchrone) - constate en conditions reelles (crash de l'app).
    private async void RssAddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rssAddDialogOpen) return;
        _rssAddDialogOpen = true;
        try
        {
            await ShowRssAddDialogAsync();
        }
        finally
        {
            _rssAddDialogOpen = false;
        }
    }

    private async Task ShowRssAddDialogAsync()
    {
        var urlBox = new TextBox { PlaceholderText = "https://exemple.com/flux.xml" };
        AutomationProperties.SetName(urlBox, "Adresse du flux RSS");
        var errorText = new TextBlock { Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(LumoraTheme.UiColor(229, 72, 77)), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "Adresse du flux (RSS ou Atom)", Opacity = 0.7 });
        panel.Children.Add(urlBox);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = "Ajouter un flux RSS",
            Content = panel,
            PrimaryButtonText = "Ajouter",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        RssFeed? addedFeed = null;
        RssFeedService.FetchResult? fetchResult = null;

        // Valide/telecharge avant de fermer le dialogue : on ne veut pas ajouter
        // un flux dont l'URL est invalide ou injoignable sans retour clair.
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                var url = urlBox.Text.Trim();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                {
                    errorText.Text = "Adresse invalide (http/https attendu).";
                    errorText.Visibility = Visibility.Visible;
                    args.Cancel = true;
                    return;
                }
                if (_rssFeeds.ContainsUrl(url))
                {
                    errorText.Text = "Ce flux est déjà suivi.";
                    errorText.Visibility = Visibility.Visible;
                    args.Cancel = true;
                    return;
                }

                errorText.Visibility = Visibility.Collapsed;
                var result = await RssFeedService.FetchAsync(url);
                var feed = _rssFeeds.Add(url, result.Title);
                _rssFeeds.MarkFetched(feed.Id, result.Title, error: null);
                // Marque connus (pas lus) : le lot initial compte bien comme
                // "non lu" pour le badge, mais ne doit pas non plus disparaitre
                // ni etre traite comme "nouveau" par la verification periodique.
                _rssFeeds.MarkArticlesKnown(feed.Id, result.Articles.Select(a => a.Id));
                _selectedRssFeedId = feed.Id;
                addedFeed = feed;
                fetchResult = result;
            }
            catch (Exception ex)
            {
                errorText.Text = $"Impossible de récupérer ce flux : {ex.Message}";
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
            finally
            {
                deferral.Complete();
            }
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && addedFeed is not null && fetchResult is not null)
        {
            RefreshRssFeedsList();
            // RefreshRssFeedsList seul ne suffit pas : le nouveau flux est
            // deja selectionne mais silencieusement (_suppressRssSelection),
            // donc RssFeedsList_SelectionChanged ne s'est jamais declenche -
            // sans cet appel, le panneau restait bloque sur "Selectionnez un
            // flux" malgre le flux visiblement selectionne dans la liste
            // (constate en conditions reelles).
            DisplayFetchedArticles(addedFeed, fetchResult.Articles);
            StatusText.Text = "Flux RSS ajouté.";
        }
    }
}
