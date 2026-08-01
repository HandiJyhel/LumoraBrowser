using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

// ── Reprise « site introuvable » et mémoire des déménagements ────────────────
// Quand un site ne répond plus (domaine mort, serveur en panne 5xx, site qui a
// changé de nom de domaine), Chrome envoie l'adresse en échec aux serveurs de
// Google pour proposer une correction. Lumora fait l'inverse : suggestion
// calculée 100% localement (déménagements appris, favoris, historique, onglets,
// variante www.), et la recherche web ne part QUE sur clic explicite, vers le
// moteur configuré (logique pure : SiteNotFoundRecovery / SiteRelocationStore).
public sealed partial class MainWindow
{
    private SiteRelocationStore _siteRelocations = null!;

    // Suivi par-onglet de l'état de navigation (5xx, redirections, exemptions) :
    // état et décisions extraits dans un collaborateur pur et testable. La colle
    // UI (barres, handlers) reste ici. Voir NavigationHealthTracker.
    private readonly NavigationHealthTracker _navHealth = new();

    private string? _siteNotFoundFailedUrl;
    private string? _siteNotFoundSuggestedUrl;
    private string? _siteMovedFromRoot;

    // ── Détection ────────────────────────────────────────────────────────────

    // Échecs réseau qui signifient « ce site ne répond plus » (et pas « ma
    // connexion est coupée » : Disconnected est volontairement exclu, sinon la
    // barre s'afficherait pour tous les sites dès que le réseau tombe).
    // Reste ici (typé WebView2) : le tracker pur ne dépend pas de CoreWebView2.
    private static bool IndicatesSiteDead(CoreWebView2WebErrorStatus status) => status is
        CoreWebView2WebErrorStatus.HostNameNotResolved or
        CoreWebView2WebErrorStatus.Timeout or
        CoreWebView2WebErrorStatus.CannotConnect or
        CoreWebView2WebErrorStatus.ConnectionAborted or
        CoreWebView2WebErrorStatus.ConnectionReset;

    // Observe les réponses du document principal : erreurs serveur (5xx, dont
    // les 52x Cloudflare « origine morte ») et redirections permanentes
    // (301/308) qui alimentent la mémoire des déménagements. L'extraction des
    // champs WebView2 (statut, en-tête Location) et l'action sur le verdict
    // restent ici ; la classification est déléguée au tracker pur.
    private void ObserveMainDocumentResponse(CoreWebView2WebResourceResponseReceivedEventArgs args)
    {
        var requestUri = args.Request.Uri;

        if (_navHealth.ShouldTraceResponse())
        {
            int? diagStatus = null;
            try { diagStatus = args.Response.StatusCode; } catch { }
            var isMainDocument = _navHealth.IsMainDocument(requestUri, out _);
            var uriForTrace = requestUri ?? string.Empty;
            if (uriForTrace.Length > 120) uriForTrace = uriForTrace[..120] + "...";
            WinUiRuntimeTrace.Write($"Response: status={diagStatus?.ToString() ?? "?"} mainDoc={isMainDocument} uri={uriForTrace}");
        }

        int? status = null;
        try { status = args.Response.StatusCode; } catch { }

        string? location = null;
        if (status is 301 or 308)
        {
            try
            {
                if (args.Response.Headers.Contains("Location"))
                    location = args.Response.Headers.GetHeader("Location");
            }
            catch { }
        }

        var signal = _navHealth.ClassifyMainDocumentResponse(requestUri, status, location);
        switch (signal.Kind)
        {
            case MainDocumentSignalKind.PermanentRedirect:
                OnPermanentRedirectObserved(signal.FromUri!, signal.ToUri!);
                break;

            case MainDocumentSignalKind.HttpError:
                WinUiRuntimeTrace.Write($"Main document HTTP {signal.Status}: {requestUri}");
                // La navigation avait déjà échoué (Unknown) avant l'arrivée de
                // cette réponse : proposer la barre maintenant, pour l'onglet actif.
                if (signal.TriggerPendingFailure && CurrentTab()?.Id == signal.TabId)
                {
                    _navHealth.TakeMainDocumentHttpError(signal.TabId);
                    var failedAddress = signal.PendingFailedAddress!;
                    DispatcherQueue.TryEnqueue(() => OfferSiteNotFoundRecovery(failedAddress, signal.Status));
                }
                break;
        }
    }

    // ── Barre « site introuvable » ───────────────────────────────────────────

    private void OfferSiteNotFoundRecovery(string failedUrl, int? httpStatus)
    {
        var host = SiteNotFoundRecovery.DisplayHostOf(failedUrl);
        if (host is null)
        {
            HideSiteNotFoundBar();
            return;
        }

        _siteNotFoundFailedUrl = failedUrl;

        // Priorité des suggestions (toutes calculées localement, aucune requête
        // réseau) : 1. déménagement appris (redirection permanente déjà observée),
        // 2. domaine très proche déjà connu (faute de frappe probable),
        // 3. variante avec/sans www.
        var relocated = _siteRelocations.TargetFor(failedUrl);
        var suggestion = relocated
                         ?? SiteNotFoundRecovery.ClosestKnownUrl(failedUrl, KnownUrlsForRecovery())
                         ?? SiteNotFoundRecovery.WwwVariantOf(failedUrl);
        _siteNotFoundSuggestedUrl = suggestion;

        SiteNotFoundText.Text = httpStatus is int code
            ? $"Site introuvable : {host} ne répond plus (erreur {code}). Il a peut-être changé d'adresse."
            : $"Site introuvable : le domaine {host} ne répond plus. Il a peut-être changé d'adresse.";

        if (suggestion is not null && Uri.TryCreate(suggestion, UriKind.Absolute, out var suggestedUri))
        {
            // Un déménagement appris est une destination sûre et connue : « Aller
            // sur » ; les autres suggestions restent des essais : « Essayer ».
            SiteNotFoundTrySuggestion.Content = relocated is not null
                ? $"Aller sur {suggestedUri.Host}"
                : $"Essayer {suggestedUri.Host}";
            SiteNotFoundTrySuggestion.Visibility = Visibility.Visible;
        }
        else
        {
            SiteNotFoundTrySuggestion.Visibility = Visibility.Collapsed;
        }

        SiteNotFoundBar.Visibility = Visibility.Visible;
    }

    private void HideSiteNotFoundBar()
    {
        SiteNotFoundBar.Visibility = Visibility.Collapsed;
        _siteNotFoundFailedUrl = null;
        _siteNotFoundSuggestedUrl = null;
    }

    // Tout ce que le profil connaît déjà comme adresses : onglets ouverts,
    // favoris, historique local. Rien ne sort de la machine.
    private IEnumerable<string?> KnownUrlsForRecovery()
    {
        foreach (var tab in _tabs)
        {
            yield return tab.Address;
        }

        foreach (var node in _allBookmarkNodes)
        {
            if (node.Kind == BookmarkKind.Url)
            {
                yield return node.Url;
            }
        }

        foreach (var entry in _historyPanel.Store.AllEntries())
        {
            yield return entry.Url;
        }
    }

    private void SiteNotFoundTrySuggestion_Click(object sender, RoutedEventArgs e)
    {
        var suggestion = _siteNotFoundSuggestedUrl;
        HideSiteNotFoundBar();
        if (suggestion is null)
        {
            return;
        }

        NavigateCurrentTab(suggestion, DisplayTitle(suggestion));
    }

    private void SiteNotFoundSearch_Click(object sender, RoutedEventArgs e)
    {
        var query = SiteNotFoundRecovery.SearchQueryFor(_siteNotFoundFailedUrl);
        HideSiteNotFoundBar();
        if (query is null)
        {
            return;
        }

        StatusText.Text = $"Recherche du site {query}...";
        NavigateCurrentTab(SearchUrl(query), query);
    }

    private void SiteNotFoundDismiss_Click(object sender, RoutedEventArgs e) =>
        HideSiteNotFoundBar();

    // ── Barre « site déménagé » : mise à jour des favoris/raccourcis ────────

    private void OnPermanentRedirectObserved(string fromUrl, string toUrl)
    {
        var relocation = _siteRelocations.RecordPermanentRedirect(fromUrl, toUrl, DateTimeOffset.Now);
        if (relocation is null)
        {
            return;
        }

        WinUiRuntimeTrace.Write($"Site relocation recorded: {relocation.FromRootDomain} -> {relocation.ToOrigin}");
        if (!relocation.UpdateOffered)
        {
            DispatcherQueue.TryEnqueue(() => OfferRelocationUpdates(relocation));
        }
    }

    private void OfferRelocationUpdates(SiteRelocation relocation)
    {
        var plan = ComputeRelocationPlan(relocation);
        if (plan.TotalChanges == 0)
        {
            // Rien à mettre à jour : inutile de re-proposer à la prochaine visite.
            _siteRelocations.MarkUpdateOffered(relocation.FromRootDomain);
            return;
        }

        _siteMovedFromRoot = relocation.FromRootDomain;
        var toHost = Uri.TryCreate(relocation.ToOrigin, UriKind.Absolute, out var toUri)
            ? toUri.Host
            : relocation.ToOrigin;
        SiteMovedText.Text = plan.TotalChanges == 1
            ? $"{relocation.FromRootDomain} a déménagé vers {toHost}. Mettre à jour l'entrée qui pointe encore vers l'ancienne adresse ?"
            : $"{relocation.FromRootDomain} a déménagé vers {toHost}. Mettre à jour {plan.TotalChanges} favoris/raccourcis qui pointent encore vers l'ancienne adresse ?";
        SiteMovedBar.Visibility = Visibility.Visible;
    }

    private SiteRelocationUpdatePlanner.Plan ComputeRelocationPlan(SiteRelocation relocation) =>
        SiteRelocationUpdatePlanner.Compute(
            relocation,
            _allBookmarkNodes
                .Where(node => node.Kind == BookmarkKind.Url)
                .Select(node => (node.Id, node.Url)),
            _uiSettings.NewTabShortcuts.Select(shortcut => (shortcut.Title, shortcut.Url)));

    private void SiteMovedUpdate_Click(object sender, RoutedEventArgs e)
    {
        var fromRoot = _siteMovedFromRoot;
        HideSiteMovedBar();
        if (fromRoot is null || _siteRelocations.Find(fromRoot) is not { } relocation)
        {
            return;
        }

        _siteRelocations.MarkUpdateOffered(fromRoot);
        var plan = ComputeRelocationPlan(relocation);

        var updatedBookmarks = _bookmarks.UpdateUrls(plan.BookmarkUrlsById);
        if (updatedBookmarks > 0)
        {
            ReloadBookmarks();
        }

        if (plan.ShortcutChanges > 0)
        {
            _uiSettings.NewTabShortcuts = plan.Shortcuts
                .Select(shortcut => new NewTabShortcut(shortcut.Title, shortcut.Url))
                .ToList();
            SaveUiSettings();
            RefreshNovaHomePages();
        }

        StatusText.Text = $"{plan.TotalChanges} adresse(s) mise(s) à jour vers le nouveau domaine.";
    }

    private void SiteMovedDismiss_Click(object sender, RoutedEventArgs e)
    {
        // « Non merci » vaut pour ce déménagement : ne pas re-proposer à chaque visite.
        if (_siteMovedFromRoot is { } fromRoot)
        {
            _siteRelocations.MarkUpdateOffered(fromRoot);
        }

        HideSiteMovedBar();
    }

    private void HideSiteMovedBar()
    {
        SiteMovedBar.Visibility = Visibility.Collapsed;
        _siteMovedFromRoot = null;
    }
}
