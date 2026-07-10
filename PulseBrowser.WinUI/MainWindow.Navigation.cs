using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.System;
using PulseBrowser.Privacy.HttpsEnforcer;

namespace PulseBrowser.WinUI;

public sealed partial class MainWindow
{
    // Moteur (onglet) → URL http d'origine promue en https. Sert à proposer un repli
    // en HTTP si la version sécurisée échoue (site sans HTTPS).
    private readonly Dictionary<WebView2, string> _httpsUpgradeOriginals = new();

    // ── Onglets ───────────────────────────────────────────────────────────────

    private void BrowserTabs_AddTabButtonClick(TabView sender, object args) =>
        AddTab("Nouvel onglet", "pulse://accueil", select: true);

    private void BrowserTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTabNavigation) return;
        if (BrowserTabs.SelectedItem is not TabViewItem item || item.Tag is not int id)
        {
            return;
        }

        var tab = _tabs.FirstOrDefault(candidate => candidate.Id == id);
        if (tab is null)
        {
            return;
        }

        ActivateTab(tab);
    }

    // Rend l'onglet visible SANS recharger sa page : chaque onglet garde son propre
    // WebView2, le changement d'onglet est un simple basculement de visibilité.
    private void ActivateTab(BrowserTabState tab)
    {
        AddressBox.Text = tab.Address;
        ShowPanel(BrowserPanel, tab.Title);

        foreach (var other in _tabs)
        {
            if (other.View is not null)
            {
                other.View.Visibility = other.Id == tab.Id ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        _browserView = tab.View;
        _credentialService.SetActiveCore(tab.View?.CoreWebView2);
        _currentPageDomain = ExtractDomain(tab.Address);

        // Les barres de remplissage appartiennent à la page quittée.
        AutoFillBar.Visibility = Visibility.Collapsed;
        WalletFillBar.Visibility = Visibility.Collapsed;
        _pendingAutoFill = null;

        if (tab.View is null)
        {
            tab.PendingAddress ??= tab.Address;
            EnsureTabView(tab);
        }
        else
        {
            OfferAutoFill(tab.Address);
            // Sans focus explicite, la molette ne route vers aucune fenêtre tant que
            // l'utilisateur n'a pas cliqué dans la page (le focus OS suit le focus
            // clavier, pas le curseur) : le WebView2 doit recevoir le focus dès qu'il
            // devient l'onglet actif, pas seulement au premier clic.
            tab.View.Focus(FocusState.Programmatic);
        }

        RenderVerticalTabs();
        SaveTabSession();
        UpdateTitleBarDragRegion();
    }

    // Création paresseuse du moteur d'un onglet. Ne fait rien tant que la fenêtre
    // n'est pas activée (_browserSurfaceReady) : l'adresse reste en attente et sera
    // chargée dès l'initialisation du moteur.
    private async void EnsureTabView(BrowserTabState tab)
    {
        if (tab.View is not null || !_browserSurfaceReady || _webViewDisabled)
        {
            return;
        }

        var view = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        view.NavigationStarting += BrowserView_NavigationStarting;
        view.NavigationCompleted += BrowserView_NavigationCompleted;
        view.CoreWebView2Initialized += BrowserView_CoreWebView2Initialized;

        tab.View = view;
        tab.PendingAddress ??= tab.Address;
        var isActive = CurrentTab()?.Id == tab.Id;
        view.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        if (isActive)
        {
            _browserView = view;
        }

        BrowserHost.Children.Add(view);
        WinUiRuntimeTrace.Write($"WebView2 created for tab {tab.Id}");

        try
        {
            await view.EnsureCoreWebView2Async();
            // Le focus doit être (re)posé après l'attente async : l'onglet actif a pu
            // changer entre-temps, et un WebView2 fraîchement créé n'a jamais le focus
            // OS par défaut (cf. ActivateTab : sans ça, la molette reste muette tant
            // qu'on n'a pas cliqué dans la page).
            if (CurrentTab()?.Id == tab.Id)
            {
                view.Focus(FocusState.Programmatic);
            }
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"WebView2 creation failed: {error.GetType().Name}");
            tab.View = null;
            BrowserHost.Children.Remove(view);
            if (ReferenceEquals(_browserView, view))
            {
                _browserView = null;
            }
            StatusText.Text = $"Moteur web indisponible: {error.Message}";
        }
    }

    private BrowserTabState? TabForView(WebView2? view) =>
        view is null ? null : _tabs.FirstOrDefault(tab => ReferenceEquals(tab.View, view));

    private BrowserTabState? TabForCore(CoreWebView2? core) =>
        core is null ? null : _tabs.FirstOrDefault(tab => ReferenceEquals(tab.View?.CoreWebView2, core));

    private bool IsActiveView(WebView2? view) =>
        view is not null && ReferenceEquals(_browserView, view);

    private void BrowserTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is not int id)
        {
            return;
        }

        var state = _tabs.FirstOrDefault(tab => tab.Id == id);
        if (state is not null)
        {
            _tabs.Remove(state);
            CloseTabView(state);
        }

        sender.TabItems.Remove(args.Tab);
        RenderVerticalTabs();
        SaveTabSession();
        UpdateTitleBarDragRegion();

        if (sender.TabItems.Count == 0)
        {
            AddTab("Nouvel onglet", "pulse://accueil", select: true);
        }
    }

    // Libère le moteur d'un onglet fermé : détache le module Credentials, oublie
    // les scripts privacy enregistrés sur ce moteur, retire la vue et la ferme.
    private void CloseTabView(BrowserTabState state)
    {
        var view = state.View;
        if (view is null)
        {
            return;
        }

        state.View = null;
        _httpsUpgradeOriginals.Remove(view);
        var core = view.CoreWebView2;
        if (core is not null)
        {
            _credentialService.Detach(core);
            _cosmeticScriptIds.Remove(core);
            _consentScriptIds.Remove(core);
        }

        BrowserHost.Children.Remove(view);
        if (ReferenceEquals(_browserView, view))
        {
            _browserView = null;
        }

        try { view.Close(); } catch { }
    }

    private async void BrowserView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        if (args.Exception is not null)
        {
            StatusText.Text = $"Moteur web indisponible: {args.Exception.Message}";
            return;
        }

        // Stockage 100% maison : Chromium ne conserve NI ne remplit aucun mot de passe.
        // Le seul coffre est vault.pulse ; la capture et le remplissage sont faits par l'app.
        sender.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
        sender.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;

        // SmartScreen envoie chaque URL visitée à Microsoft pour vérifier sa
        // réputation : coupé par défaut. Réactivable dans Paramètres > Confidentialité
        // pour qui préfère la protection anti-phishing. Try/catch : réglage absent
        // des runtimes WebView2 anciens.
        try { sender.CoreWebView2.Settings.IsReputationCheckingRequired = _uiSettings.SmartScreenEnabled; }
        catch { }

        sender.CoreWebView2.DocumentTitleChanged += BrowserCore_DocumentTitleChanged;
        sender.CoreWebView2.SourceChanged += BrowserCore_SourceChanged;
        sender.CoreWebView2.FaviconChanged += BrowserCore_FaviconChanged;
        sender.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
        sender.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;
        sender.CoreWebView2.WebMessageReceived += BrowserCore_WebMessageReceived;

        // Interception réseau pour le bloqueur de pubs/trackers
        sender.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        sender.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;

        // Popups / target=_blank / window.open : ouvrir dans un onglet Pulse au lieu
        // d'une fenêtre parasite non contrôlée.
        sender.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

        // Scripts privacy + passkeys + capture d'identifiants : chaque moteur reçoit
        // les siens (un WebView2 par onglet).
        _ = RegisterCosmeticScriptOnCoreAsync(sender.CoreWebView2);
        _ = RegisterConsentScriptOnCoreAsync(sender.CoreWebView2);
        _ = RegisterPasskeyMonitorAsync(sender.CoreWebView2);
        _ = RegisterPaymentMonitorAsync(sender.CoreWebView2);
        _ = _credentialService.AttachAsync(sender.CoreWebView2);
        // Migration + nettoyage : importer ce que Chromium avait déjà, puis vider son coffre.
        _ = MigrateAndClearBrowserPasswordsAsync(sender.CoreWebView2);

        StatusText.Text = "Moteur web WinUI initialise.";
        WinUiRuntimeTrace.Write("CoreWebView2 initialized");

        // Sessions éphémères : purge des cookies de la visite précédente AVANT la
        // première navigation web, pour qu'aucune page ne charge d'anciens cookies.
        // (Guardée par lancement : seul le premier moteur purge, le profil est partagé.)
        await PurgeStartupSessionsAsync(sender.CoreWebView2);

        var tab = TabForView(sender);
        if (tab is null)
        {
            return;
        }

        if (IsActiveView(sender))
        {
            _credentialService.SetActiveCore(sender.CoreWebView2);
        }

        var pendingAddress = tab.PendingAddress;
        tab.PendingAddress = null;
        if (!string.IsNullOrWhiteSpace(pendingAddress))
        {
            NavigateTabView(tab, pendingAddress);
        }
    }

    private void BrowserCore_DocumentTitleChanged(object? sender, object e)
    {
        if (sender is not CoreWebView2 core || TabForCore(core) is not { } tab)
        {
            return;
        }

        var title = core.DocumentTitle;
        if (!string.IsNullOrWhiteSpace(title))
        {
            UpdateTab(tab, title, tab.View?.Source?.ToString() ?? tab.Address, updateHeaderOnly: true);
        }
    }

    private async void BrowserCore_FaviconChanged(object? sender, object e)
    {
        if (sender is CoreWebView2 core && TabForCore(core) is { } tab)
        {
            await CaptureFaviconForTabAsync(tab);
        }
    }

    private void BrowserView_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        // Nettoyage URL (ParameterCleaner + HttpsEnforcer) — idempotent, pas de boucle infinie
        var cleaned = _privacy.CleanUrl(args.Uri);
        if (cleaned is not null && !string.Equals(cleaned, args.Uri, StringComparison.OrdinalIgnoreCase))
        {
            // Promotion HTTP→HTTPS : on retient l'URL d'origine pour pouvoir proposer un
            // repli si la version sécurisée échoue.
            if (args.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _httpsUpgradeOriginals[sender] = args.Uri;
            }

            args.Cancel = true;
            DispatcherQueue.TryEnqueue(() => sender.CoreWebView2?.Navigate(cleaned));
            return;
        }

        // Compteur par-page, domaine du bouclier et statut : uniquement pour l'onglet visible.
        if (IsActiveView(sender))
        {
            _privacy.ResetPageBlockedCount();
            _telemetryBlocker?.ResetPageBlockedCount();
            // La proposition de carte appartient à la page quittée.
            WalletFillBar.Visibility = Visibility.Collapsed;
            _currentPageDomain = ExtractDomain(args.Uri);
            StatusText.Text = $"Chargement: {DisplayTitle(args.Uri)}";
        }
    }

    private static string? ExtractDomain(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return null;
        try { return new Uri(uri).Host; }
        catch { return null; }
    }

    private void BrowserView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        var tab = TabForView(sender);
        if (tab is null)
        {
            return;
        }

        var isActive = IsActiveView(sender);

        // Repli HTTPS→HTTP : cette navigation venait d'une promotion http→https. Si elle
        // échoue, le site ne supporte probablement pas HTTPS → on propose de continuer en HTTP.
        var wasHttpsUpgrade = _httpsUpgradeOriginals.Remove(sender, out var originalHttpUrl);
        if (!args.IsSuccess && wasHttpsUpgrade && isActive && originalHttpUrl is not null)
        {
            _ = PromptHttpsFallbackAsync(tab, originalHttpUrl);
        }

        var address = sender.Source?.ToString() ?? tab.Address;
        var title = sender.CoreWebView2?.DocumentTitle;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = DisplayTitle(address);
        }

        UpdateTab(tab, title, address, updateHeaderOnly: true);
        // La barre d'adresse suit l'URL web réelle ; sur la page d'accueil (source
        // "about:blank"), on garde l'adresse logique de l'onglet (pulse://accueil).
        SyncActiveAddressBar(tab, BookmarkStore.IsWebUrl(address) ? address : tab.Address);
        _ = CaptureFaviconForTabAsync(tab);

        // Réinitialisation AVANT OfferAutoFill : c'est elle qui ré-affiche la barre
        // si un identifiant du coffre correspond à la page. Uniquement pour l'onglet
        // visible : une page d'arrière-plan ne pilote pas les barres.
        if (isActive)
        {
            AutoFillBar.Visibility = Visibility.Collapsed;
        }

        if (args.IsSuccess && BookmarkStore.IsWebUrl(address))
        {
            AddHistoryEntry(address, title);
            if (isActive)
            {
                OfferAutoFill(address);
            }
            _ = InjectSiteCosmeticAsync(sender.CoreWebView2, address);
            _ = InjectConsentRetryAsync(sender.CoreWebView2);
        }
        // On garde la barre de sauvegarde tant qu'un identifiant est en attente, quelle
        // que soit la redirection post-login (souvent vers une autre origine). Elle n'est
        // fermée que par l'utilisateur (Enregistrer / Ignorer).
        if (_pendingCredential is null)
            CredentialSaveBar.Visibility = Visibility.Collapsed;

        if (isActive)
        {
            StatusText.Text = args.IsSuccess ? $"Page chargee: {title}" : $"Navigation echouee: {args.WebErrorStatus}";
        }
        UpdatePrivacyUi();
    }

    // Le forçage HTTPS a échoué : proposer de charger la version HTTP (non chiffrée).
    // Sur acceptation, l'hôte est autorisé en HTTP pour la session et rechargé tel quel.
    private async Task PromptHttpsFallbackAsync(BrowserTabState tab, string httpUrl)
    {
        if (!Uri.TryCreate(httpUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Connexion non securisee",
            Content = $"{uri.Host} ne prend pas en charge HTTPS. Continuer en HTTP ? La connexion ne sera pas chiffree.",
            PrimaryButtonText = "Continuer en HTTP",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _privacy.Get<HttpsEnforcerModule>()?.AllowHttp(uri.Host);
        NavigateTabView(tab, httpUrl);
    }

    private void BrowserCore_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs args)
    {
        if (_isProgrammaticNavigation)
        {
            return;
        }

        if (sender is not CoreWebView2 core || TabForCore(core) is not { } tab)
        {
            return;
        }

        var address = tab.View?.Source?.ToString();
        if (!string.IsNullOrWhiteSpace(address) && BookmarkStore.IsWebUrl(address))
        {
            UpdateTab(tab, DisplayTitle(address), address, updateHeaderOnly: true);
            SyncActiveAddressBar(tab, address);
        }
    }

    // Fait suivre la barre d'adresse à l'URL réelle de l'onglet ACTIF. Point de
    // synchronisation unique appelé à chaque changement de source ou fin de navigation
    // (redirections et clics compris). Ne réécrit PAS la barre pendant que l'utilisateur
    // la modifie, pour ne pas écraser sa saisie.
    private void SyncActiveAddressBar(BrowserTabState tab, string address)
    {
        if (!IsActiveView(tab.View) || string.IsNullOrWhiteSpace(address)) return;
        if (AddressBox.FocusState != FocusState.Unfocused) return;
        if (AddressBox.Text != address) AddressBox.Text = address;
    }

    private async Task CaptureFaviconForTabAsync(BrowserTabState tab)
    {
        var browser = tab.View;
        var core = browser?.CoreWebView2;
        if (browser is null || core is null) return;

        var address = browser.Source?.ToString() ?? tab.Address;
        if (!BookmarkStore.IsWebUrl(address)) return;

        var originPath = Path.Combine(_profile.FaviconsDir, $"{HashOrigin(address)}.png");

        // Réutiliser le favicon existant si récent (moins de 24h) ET utilisable :
        // d'anciens caches ecrits avant la conversion de format ou un globe
        // generique WebView2 ne doivent pas bloquer une nouvelle tentative.
        if (File.Exists(originPath) &&
            (DateTimeOffset.Now - File.GetLastWriteTimeUtc(originPath)).TotalHours < 24 &&
            FaviconQuality.IsUsablePngFile(originPath))
        {
            _faviconCache[address] = originPath;
            _faviconCache[OriginOf(address)] = originPath;
            ApplyFaviconToUi(tab, address, originPath);
            return;
        }

        var saved = false;
        try
        {
            // Méthode 1 : WebView2 GetFaviconAsync (retourne PNG)
            using var ras = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            if (ras is not null && ras.Size > 0)
            {
                using var stream = ras.AsStreamForRead();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                var png = memory.ToArray();
                if (FaviconQuality.IsUsablePng(png))
                {
                    Directory.CreateDirectory(_profile.FaviconsDir);
                    await File.WriteAllBytesAsync(originPath, png);
                    saved = true;
                }
            }
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"GetFaviconAsync skipped: {ex.GetType().Name}");
        }

        // Méthode 2 : JS pour lire link[rel=icon], puis téléchargement HTTP
        if (!saved)
        {
            saved = await DownloadFaviconFallbackAsync(core, address, originPath);
        }

        if (!saved || !FaviconQuality.IsUsablePngFile(originPath)) return;

        _faviconCache[address] = originPath;
        _faviconCache[OriginOf(address)] = originPath;
        ApplyFaviconToUi(tab, address, originPath);
    }

    private async Task<bool> DownloadFaviconFallbackAsync(CoreWebView2 core, string address, string outputPath)
    {
        // Source 1 : FaviconUri de WebView2 (déjà téléchargé par Chromium, le plus fiable)
        string? iconUrl = null;
        try
        {
            var wv2Uri = core.FaviconUri;
            if (!string.IsNullOrWhiteSpace(wv2Uri) && Uri.TryCreate(wv2Uri, UriKind.Absolute, out _))
                iconUrl = wv2Uri;
        }
        catch { }

        // Source 2 : balise <link rel="icon"> via JS (si FaviconUri absent)
        if (iconUrl is null)
        {
            try
            {
                var script = """
                    (function(){
                        var sel=['link[rel="icon"]','link[rel="shortcut icon"]','link[rel="apple-touch-icon"]','link[rel*="icon"]'];
                        for(var s of sel){var el=document.querySelector(s);if(el&&el.href)return el.href;}
                        return '';
                    })()
                    """;
                var result = await core.ExecuteScriptAsync(script);
                if (!string.IsNullOrEmpty(result) && result != "null" && result.Length > 2)
                {
                    var candidate = result.Trim('"');
                    if (Uri.TryCreate(candidate, UriKind.Absolute, out _)) iconUrl = candidate;
                }
            }
            catch { }
        }

        // Source 3 : /favicon.ico standard
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(iconUrl)) candidates.Add(iconUrl);
        candidates.Add($"{OriginOf(address)}/favicon.ico");

        foreach (var url in candidates)
        {
            // Ce téléchargement sort du moteur (HttpClient direct) : on le soumet quand
            // même au bloqueur pour ne pas contacter une origine que l'utilisateur bloque.
            if (_privacy.IsBlocked(url, address))
            {
                continue;
            }

            try
            {
                var bytes = await FaviconHttpClient.GetByteArrayAsync(url);
                if (bytes.Length == 0) continue;

                // Beaucoup de sites servent encore un favicon.ico réel (format ICO/BMP,
                // pas PNG) même sur un lien nommé "icon" : converti en PNG véritable
                // avant mise en cache, sinon le fichier .png sur disque contient en
                // réalité des octets ICO que les contrôles Image peuvent refuser
                // d'afficher (ex. allocine.fr, favicon .ico multi-résolution).
                var png = await FaviconImageConverter.ToPngAsync(bytes);
                if (png is null || !FaviconQuality.IsUsablePng(png)) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await File.WriteAllBytesAsync(outputPath, png);
                return true;
            }
            catch { }
        }

        return false;
    }

    private void ApplyFaviconToUi(BrowserTabState tab, string address, string faviconPath)
    {
        if (tab.Address.Equals(address, StringComparison.OrdinalIgnoreCase))
        {
            tab.IconPath = faviconPath;
            UpdateTabHeader(tab);
            RenderVerticalTabs();
        }

        // Alimenter le cache en mémoire pour que EnrichNodesWithFaviconCache trouve l'icône
        // même si SetIconForOrigin ne trouve pas de signet correspondant (ex. URL avec chemin).
        _faviconCache[address]          = faviconPath;
        _faviconCache[OriginOf(address)] = faviconPath;

        _bookmarks.SetIconForOrigin(address, faviconPath);
        var faviconOrigin = OriginOf(address);
        if (_allBookmarkNodes.Any(n =>
                n.Kind == BookmarkKind.Url &&
                OriginOf(n.Url).Equals(faviconOrigin, StringComparison.OrdinalIgnoreCase)))
        {
            ReloadBookmarks();
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_browserView?.CanGoBack == true)
        {
            _browserView.GoBack();
        }
        else
        {
            StatusText.Text = "Aucune page precedente.";
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_browserView?.CanGoForward == true)
        {
            _browserView.GoForward();
        }
        else
        {
            StatusText.Text = "Aucune page suivante.";
        }
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e) =>
        _browserView?.Reload();

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _browserView?.CoreWebView2?.Stop();
        StatusText.Text = "Chargement arrete.";
    }

    private void GoButton_Click(object sender, RoutedEventArgs e) =>
        NavigateFromAddressBox();

    private void AddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            NavigateFromAddressBox();
            e.Handled = true;
        }
    }

    private void AddTab(string title, string address, bool select, int? groupId = null)
    {
        var state = new BrowserTabState(_nextTabId++, title, address)
        {
            IconPath = CachedFaviconPathFor(address) ?? string.Empty,
            GroupId = groupId
        };
        _tabs.Add(state);

        var tab = new TabViewItem
        {
            Tag = state.Id,
            IsClosable = true
        };
        tab.Header = TabHeaderContent(state, compact: false);

        BrowserTabs.TabItems.Add(tab);
        RenderVerticalTabs();
        SaveTabSession();
        UpdateTitleBarDragRegion();
        if (select)
        {
            // SelectionChanged active l'onglet ; filet direct si l'événement est
            // supprimé (bascule onglets verticaux) ou si la sélection ne change pas.
            BrowserTabs.SelectedItem = tab;
            if (!ReferenceEquals(_browserView, state.View) || state.View is null)
            {
                ActivateTab(state);
            }
        }
    }

    private void NavigateFromAddressBox()
    {
        var address = NormalizeAddress(AddressBox.Text);
        var title = address == "pulse://accueil" ? "Accueil Pulse" : DisplayTitle(address);
        NavigateCurrentTab(address, title);
        ShowPanel(BrowserPanel, title);
    }

    private void NavigateCurrentTab(string address, string title)
    {
        var tab = CurrentTab();
        if (tab is null)
        {
            AddTab(title, address, select: true);
            return;
        }

        UpdateTab(tab, title, address, updateHeaderOnly: false);
        NavigateBrowser(address);
    }

    private void UpdateTab(BrowserTabState tab, string title, string address, bool updateHeaderOnly)
    {
        tab.Address = address;
        tab.Title = string.IsNullOrWhiteSpace(title) ? DisplayTitle(address) : title;
        var iconPath = CachedFaviconPathFor(address);
        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            tab.IconPath = iconPath;
        }

        UpdateTabHeader(tab);

        RenderVerticalTabs();

        if (!updateHeaderOnly && CurrentTab()?.Id == tab.Id)
        {
            AddressBox.Text = address;
        }

        SaveTabSession();
    }

    private void UpdateTabHeader(BrowserTabState tab)
    {
        var item = BrowserTabs.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(candidate => candidate.Tag is int id && id == tab.Id);
        if (item is not null)
        {
            item.Header = TabHeaderContent(tab, compact: false);
        }
    }

    // Palette de couleurs de groupe : reprend les teintes de l'identité visuelle
    // (orange/teal de l'accueil) complétées par des teintes distinguables.
    private static readonly Windows.UI.Color[] TabGroupPalette =
    {
        UiColor(225, 120, 24),
        UiColor(102, 209, 190),
        UiColor(94, 156, 235),
        UiColor(219, 112, 147),
        UiColor(154, 140, 226),
        UiColor(226, 187, 60)
    };

    private static Windows.UI.Color TabGroupColor(TabGroup group) =>
        TabGroupPalette[((group.ColorIndex % TabGroupPalette.Length) + TabGroupPalette.Length) % TabGroupPalette.Length];

    private void RenderVerticalTabs()
    {
        if (!_verticalTabsEnabled)
        {
            VerticalTabsPanelItems.Children.Clear();
            return;
        }

        VerticalTabsPanelItems.Children.Clear();
        var current = CurrentTab();
        int? lastGroupId = null;

        foreach (var tab in _tabs)
        {
            if (tab.GroupId != lastGroupId && tab.GroupId is int headerGroupId)
            {
                var headerGroup = _tabGroups.FirstOrDefault(g => g.Id == headerGroupId);
                if (headerGroup is not null)
                {
                    VerticalTabsPanelItems.Children.Add(GroupHeaderElement(headerGroup));
                }
            }
            lastGroupId = tab.GroupId;

            if (tab.GroupId is int collapsedGroupId && _collapsedGroupIds.Contains(collapsedGroupId))
            {
                continue;
            }

            var isActive = current?.Id == tab.Id;
            Button button;
            FrameworkElement content;

            if (_verticalTabsCompact)
            {
                content = TabIconElement(tab, 20);
                button = new Button
                {
                    Width = 36,
                    Height = 32,
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = tab.Id,
                    Margin = new Thickness(0, 1, 0, 1)
                };
            }
            else
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                row.Children.Add(TabIconElement(tab, 16));
                row.Children.Add(new TextBlock
                {
                    Text = tab.Title,
                    MaxWidth = 136,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                });
                content = row;
                button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(8, 5, 8, 5),
                    Tag = tab.Id,
                    Margin = new Thickness(0, 1, 0, 1)
                };
            }

            if (tab.GroupId is int tabGroupId && _tabGroups.FirstOrDefault(g => g.Id == tabGroupId) is { } tabGroup)
            {
                var wrapper = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                wrapper.Children.Add(new Border
                {
                    Width = 3,
                    CornerRadius = new CornerRadius(1.5),
                    Background = new SolidColorBrush(TabGroupColor(tabGroup)),
                    VerticalAlignment = VerticalAlignment.Stretch
                });
                wrapper.Children.Add(content);
                button.Content = wrapper;
            }
            else
            {
                button.Content = content;
            }

            if (isActive)
            {
                button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }

            ToolTipService.SetToolTip(button, tab.Title);
            button.Click += VerticalTabButton_Click;
            button.ContextFlyout = CreateTabContextFlyout(tab);
            VerticalTabsPanelItems.Children.Add(button);
        }
    }

    private FrameworkElement GroupHeaderElement(TabGroup group)
    {
        var collapsed = _collapsedGroupIds.Contains(group.Id);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new FontIcon
        {
            Glyph = collapsed ? "\uE76C" : "\uE70D",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 10
        });
        row.Children.Add(new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(TabGroupColor(group)),
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(new TextBlock
        {
            Text = group.Name,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            MaxWidth = 130,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });

        var header = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 4, 0, 1),
            Content = row,
            Tag = group.Id
        };
        header.Click += (_, _) => ToggleGroupCollapsed(group.Id);
        header.ContextFlyout = CreateGroupHeaderFlyout(group);
        ToolTipService.SetToolTip(header, group.Name);
        return header;
    }

    private MenuFlyout CreateTabContextFlyout(BrowserTabState tab)
    {
        var flyout = new MenuFlyout();

        var addToGroup = new MenuFlyoutSubItem { Text = "Ajouter au groupe" };
        foreach (var group in _tabGroups)
        {
            var groupItem = new MenuFlyoutItem { Text = group.Name, Tag = (tab, group) };
            groupItem.Click += AddTabToGroup_Click;
            addToGroup.Items.Add(groupItem);
        }
        if (_tabGroups.Count > 0)
        {
            addToGroup.Items.Add(new MenuFlyoutSeparator());
        }
        var newGroupItem = new MenuFlyoutItem { Text = "Nouveau groupe...", Tag = tab };
        newGroupItem.Click += CreateGroupWithTab_Click;
        addToGroup.Items.Add(newGroupItem);
        flyout.Items.Add(addToGroup);

        if (tab.GroupId is not null)
        {
            var removeItem = new MenuFlyoutItem { Text = "Retirer du groupe", Tag = tab };
            removeItem.Click += RemoveTabFromGroup_Click;
            flyout.Items.Add(removeItem);
        }

        return flyout;
    }

    private MenuFlyout CreateGroupHeaderFlyout(TabGroup group)
    {
        var flyout = new MenuFlyout();

        var renameItem = new MenuFlyoutItem { Text = "Renommer le groupe", Tag = group };
        renameItem.Click += RenameGroup_Click;
        flyout.Items.Add(renameItem);

        var dissolveItem = new MenuFlyoutItem { Text = "Dissoudre le groupe", Tag = group };
        dissolveItem.Click += DissolveGroup_Click;
        flyout.Items.Add(dissolveItem);

        return flyout;
    }

    private void AddTabToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: (BrowserTabState tab, TabGroup group) })
        {
            AssignTabToGroup(tab, group.Id);
        }
    }

    private void RemoveTabFromGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            AssignTabToGroup(tab, null);
        }
    }

    private async void CreateGroupWithTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            return;
        }

        var name = await PromptTextAsync("Nouveau groupe", "Nom du groupe", "");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var group = new TabGroup(_nextGroupId++, name.Trim(), TabGroupOrdering.NextColorIndex(_tabGroups.Count));
        _tabGroups.Add(group);
        AssignTabToGroup(tab, group.Id);
    }

    private async void RenameGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: TabGroup group })
        {
            return;
        }

        var name = await PromptTextAsync("Renommer le groupe", "Nom du groupe", group.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        group.Name = name.Trim();
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void DissolveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: TabGroup group })
        {
            return;
        }

        foreach (var tab in _tabs.Where(t => t.GroupId == group.Id))
        {
            tab.GroupId = null;
        }
        _tabGroups.Remove(group);
        _collapsedGroupIds.Remove(group.Id);
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void ToggleGroupCollapsed(int groupId)
    {
        if (!_collapsedGroupIds.Remove(groupId))
        {
            _collapsedGroupIds.Add(groupId);
        }
        RenderVerticalTabs();
    }

    private void AssignTabToGroup(BrowserTabState tab, int? groupId)
    {
        var order = TabGroupOrdering.ReorderForGroup(
            _tabs.Select(t => t.Id).ToList(),
            tab.Id,
            groupId,
            _tabs.ToDictionary(t => t.Id, t => t.GroupId));

        var byId = _tabs.ToDictionary(t => t.Id);
        _tabs.Clear();
        _tabs.AddRange(order.Select(id => byId[id]));

        tab.GroupId = groupId;
        RenderVerticalTabs();
        SaveTabSession();
    }

    private static StackPanel TabHeaderContent(BrowserTabState tab, bool compact)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = compact ? 0 : 8
        };
        panel.Children.Add(TabIconElement(tab, compact ? 20 : 16));
        if (!compact)
        {
            panel.Children.Add(new TextBlock
            {
                Text = tab.Title,
                MaxWidth = 170,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        return panel;
    }

    private static FrameworkElement TabIconElement(BrowserTabState tab, double size)
    {
        if (!string.IsNullOrWhiteSpace(tab.IconPath) && FaviconQuality.IsUsablePngFile(tab.IconPath))
        {
            return new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(tab.IconUri)),
                Width = size,
                Height = size
            };
        }

        return new FontIcon
        {
            Glyph = BookmarkGlyphs.Link,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = size
        };
    }

    private void VerticalTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
        {
            return;
        }

        var item = BrowserTabs.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(candidate => candidate.Tag is int tabId && tabId == id);
        if (item is not null)
        {
            BrowserTabs.SelectedItem = item;
        }

        ReturnToBrowserIfHidden();
    }

    // Cliquer sur un onglet déjà actif ne déclenche pas SelectionChanged : depuis un
    // panneau interne (coffre, historique...), ce clic doit quand même ramener à la page.
    private void ReturnToBrowserIfHidden()
    {
        if (BrowserPanel.Visibility == Visibility.Visible)
        {
            return;
        }

        var tab = CurrentTab();
        ShowPanel(BrowserPanel, tab?.Title ?? "Accueil Pulse");
    }

    private void BrowserTabs_Tapped(object sender, TappedRoutedEventArgs e) =>
        ReturnToBrowserIfHidden();

    private void NavigateBrowser(string address)
    {
        ShowPanel(BrowserPanel, DisplayTitle(address));
        var tab = CurrentTab();
        if (tab is null)
        {
            StatusText.Text = "Aucun onglet actif.";
            return;
        }

        NavigateTabView(tab, address);
    }

    // Charge une adresse dans le moteur d'un onglet donné. Si le moteur n'existe
    // pas encore (création paresseuse) ou n'est pas initialisé, l'adresse est mise
    // en attente et chargée dès que le moteur est prêt.
    private void NavigateTabView(BrowserTabState tab, string address)
    {
        var browser = tab.View;
        if (browser is null)
        {
            tab.PendingAddress = address;
            EnsureTabView(tab);
            return;
        }

        if (browser.CoreWebView2 is null)
        {
            tab.PendingAddress = address;
            StatusText.Text = "Initialisation du moteur web...";
            WinUiRuntimeTrace.Write($"Navigation delayed: {DisplayTitle(address)}");
            return;
        }

        _isProgrammaticNavigation = true;
        try
        {
            if (address.Equals("pulse://accueil", StringComparison.OrdinalIgnoreCase))
            {
                browser.CoreWebView2.NavigateToString(HomePageHtml());
                StatusText.Text = "Accueil Pulse.";
                return;
            }

            if (BookmarkStore.IsWebUrl(address))
            {
                browser.CoreWebView2.Navigate(address);
                StatusText.Text = $"Chargement: {DisplayTitle(address)}";
                return;
            }

            var normalized = NormalizeAddress(address);
            browser.CoreWebView2.Navigate(normalized);
            StatusText.Text = $"Chargement: {DisplayTitle(normalized)}";
        }
        finally
        {
            _isProgrammaticNavigation = false;
        }
    }

    private void SaveTabSession()
    {
        if (_suppressTabSave || _isGuestMode)
        {
            return;
        }

        var tabItems = BrowserTabs.TabItems.OfType<TabViewItem>().ToList();
        var activeIndex = 0;
        for (var i = 0; i < tabItems.Count; i++)
        {
            if (ReferenceEquals(tabItems[i], BrowserTabs.SelectedItem))
            {
                activeIndex = i;
                break;
            }
        }

        new TabSession
        {
            ActiveIndex = activeIndex,
            Tabs = _tabs.Select(t => new SavedTab(t.Title, t.Address, t.IconPath, t.GroupId)).ToList(),
            Groups = _tabGroups.ToList()
        }.Save(_profile.TabsFile);
    }

    private bool RestoreTabSession()
    {
        var session = TabSession.Load(_profile.TabsFile, _profile.LegacyTabsFile);
        if (session.Tabs.Count == 0)
        {
            return false;
        }

        _tabGroups.Clear();
        _tabGroups.AddRange(session.Groups);
        _nextGroupId = _tabGroups.Count == 0 ? 1 : _tabGroups.Max(g => g.Id) + 1;

        foreach (var saved in session.Tabs)
        {
            AddTab(saved.Title, saved.Address, select: false, groupId: saved.GroupId);
        }

        var safeIndex = Math.Clamp(session.ActiveIndex, 0, BrowserTabs.TabItems.Count - 1);
        if (BrowserTabs.TabItems[safeIndex] is TabViewItem activeItem)
        {
            BrowserTabs.SelectedItem = activeItem;
            var restoredTab = _tabs.ElementAtOrDefault(safeIndex);
            if (restoredTab is not null)
            {
                AddressBox.Text = restoredTab.Address;
            }
        }

        return true;
    }

    private string HomePageHtml() =>
        $$"""
        <!doctype html>
        <html lang="fr">
        <head>
        <meta charset="utf-8">
        <title>Accueil Pulse</title>
        <style>
        *{box-sizing:border-box;margin:0;padding:0}
        body{font-family:'Segoe UI',system-ui,sans-serif;background:{{(_uiSettings.AccessibilityHighContrast ? "#000" : "#1f211f")}};color:{{(_uiSettings.AccessibilityHighContrast ? "#fff" : "#fff7eb")}};min-height:100vh;display:flex;align-items:flex-start;justify-content:center;padding:11vh 32px 32px;font-size:{{(_uiSettings.AccessibilityLargeText ? "17px" : "15px")}};position:relative;overflow-x:hidden}
        body::before{content:"";position:fixed;inset:0;background:{{(_uiSettings.AccessibilityHighContrast ? "none" : "linear-gradient(135deg,rgba(225,120,24,.18),transparent 34%),linear-gradient(225deg,rgba(102,209,190,.14),transparent 42%),linear-gradient(180deg,#242520 0%,#1f211f 46%,#181a19 100%)")}};pointer-events:none}
        main{width:min(820px,100%);display:flex;flex-direction:column;align-items:center;gap:28px;position:relative}
        .brand{display:flex;flex-direction:column;align-items:center;gap:13px}
        .mark{display:flex;align-items:center;gap:14px}
        .logo-icon{width:48px;height:48px;border-radius:8px;background:{{(_uiSettings.AccessibilityHighContrast ? "#ffd500" : "linear-gradient(135deg,#f08a24 0%,#e17818 58%,#66d1be 100%)")}};display:flex;align-items:center;justify-content:center;color:{{(_uiSettings.AccessibilityHighContrast ? "#000" : "#fff")}};flex-shrink:0;box-shadow:0 13px 30px rgba(0,0,0,.26);position:relative;overflow:hidden}
        .logo-icon::before{content:"";width:30px;height:30px;border:5px solid currentColor;border-left-color:transparent;border-radius:50%;transform:rotate(-24deg);opacity:.96}
        .logo-icon::after{content:"";position:absolute;width:9px;height:9px;border-radius:50%;background:currentColor;right:13px;bottom:13px}
        .logo-name{font-size:42px;font-weight:650;line-height:1;letter-spacing:0;color:#fff8ef}
        .accent-line{width:132px;height:2px;border-radius:999px;background:{{(_uiSettings.AccessibilityHighContrast ? "#ffd500" : "linear-gradient(90deg,#e17818,#66d1be)")}};opacity:.95}
        .search{width:min(660px,100%);height:{{(_uiSettings.AccessibilityLargeText ? "54px" : "50px")}};border-radius:25px;background:{{(_uiSettings.AccessibilityHighContrast ? "#fff" : "#fbf4e8")}};display:flex;align-items:center;gap:12px;padding:0 20px;border:{{(_uiSettings.AccessibilityVisibleFocus ? "2px" : "1px")}} solid {{(_uiSettings.AccessibilityHighContrast ? "#fff" : "rgba(255,248,235,.24)")}};box-shadow:0 14px 38px rgba(0,0,0,.24)}
        .search:focus-within{border-color:{{(_uiSettings.AccessibilityHighContrast ? "#ffd500" : "rgba(102,209,190,.82)")}};box-shadow:0 14px 38px rgba(0,0,0,.27),0 0 0 3px rgba(102,209,190,.18)}
        .search svg{width:18px;height:18px;color:#796f63;flex-shrink:0}
        .search input{width:100%;height:100%;border:0;outline:0;background:transparent;color:#201f1b;font-size:{{(_uiSettings.AccessibilityLargeText ? "17px" : "15px")}}}
        .search input::placeholder{color:#81786d}
        .shortcuts{display:flex;gap:12px;flex-wrap:wrap;justify-content:center;max-width:760px}
        .shortcut-card{position:relative;width:92px;min-height:88px;border-radius:8px;padding:8px 6px 7px;display:flex;flex-direction:column;align-items:center;gap:8px;color:#eee2d4;text-decoration:none;font-size:12px;border:1px solid transparent}
        .shortcut-card:hover,.shortcut-card:focus-within{background:rgba(255,255,255,.06);border-color:rgba(102,209,190,.24)}
        .shortcut-link{display:flex;flex-direction:column;align-items:center;gap:8px;color:inherit;text-decoration:none;width:100%;min-width:0}
        .shortcut-title{width:100%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-align:center}
        .shortcut-dot{width:46px;height:46px;border-radius:8px;background:#31342d;border:1px solid #48483c;display:flex;align-items:center;justify-content:center;color:#fff1df;font-size:17px;font-weight:600}
        .shortcut-card:hover .shortcut-dot{background:#373a32;border-color:#6d8e82;color:#fff}
        .shortcut-actions{position:absolute;top:3px;right:3px;display:flex;gap:2px;opacity:0;pointer-events:none}
        .shortcut-card:hover .shortcut-actions,.shortcut-card:focus-within .shortcut-actions{opacity:1;pointer-events:auto}
        .shortcut-action{width:24px;height:24px;border:0;border-radius:8px;background:rgba(0,0,0,.42);color:#fff;cursor:pointer;font-size:13px;line-height:1}
        .shortcut-action:hover{background:rgba(225,120,24,.9)}
        .add-shortcut{border:1px dashed #5b675f;background:rgba(255,255,255,.035);cursor:pointer}
        .add-shortcut .shortcut-dot{background:transparent;border-style:dashed;color:#cfc5ba}
        .add-shortcut:hover .shortcut-dot{border-color:#66d1be;color:#fff}
        .hint{font-size:12px;color:#a7a096;margin-top:2px}
        {{(_uiSettings.AccessibilityReduceMotion ? ".search,.shortcut-card,.shortcut-dot,.shortcut-actions{transition:none}" : ".search,.shortcut-card,.shortcut-dot,.shortcut-actions{transition:background .12s ease,border-color .12s ease,box-shadow .12s ease,color .12s ease,opacity .12s ease}")}}
        </style>
        </head>
        <body>
        <main>
          <div class="brand">
            <div class="mark">
              <div class="logo-icon" aria-hidden="true"></div>
              <div class="logo-name">{{NewTabMarkup.HtmlText(_uiSettings.NewTabTitle)}}</div>
            </div>
            <div class="accent-line"></div>
          </div>
          <form class="search" onsubmit="go(event)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M9.5 3a6.5 6.5 0 0 1 5.16 10.45l4.44 4.45-1.2 1.2-4.45-4.44A6.5 6.5 0 1 1 9.5 3m0 1.7a4.8 4.8 0 1 0 0 9.6 4.8 4.8 0 0 0 0-9.6"/></svg>
            <input id="q" autofocus autocomplete="off" spellcheck="false" placeholder="Rechercher ou saisir une URL">
          </form>
          {{NewTabShortcutsHtml()}}
          <p class="hint">{{Version}}</p>
        </main>
        <script>
        function go(event){
          event.preventDefault();
          const input=document.getElementById('q');
          const value=(input.value||'').trim();
          if(!value)return;
          const web=/^https?:\/\//i.test(value);
          const host=/^[\w.-]+\.[a-z]{2,}(\/.*)?$/i.test(value) || /^localhost(:\d+)?(\/.*)?$/i.test(value);
          const search='{{NewTabMarkup.JsString(SearchUrlTemplate())}}'.replace('%s', encodeURIComponent(value));
          location.href=web?value:(host?'https://'+value:search);
        }
        function pulseMessage(payload){
          try{window.chrome.webview.postMessage(JSON.stringify(payload));}catch(_){}
        }
        function addShortcut(){
          pulseMessage({t:'newtab_add_shortcut'});
        }
        function editShortcut(index,title,url){
          pulseMessage({t:'newtab_edit_shortcut',i:index,title:title,url:url});
        }
        function deleteShortcut(index,title){
          if(confirm('Supprimer le raccourci "'+title+'" ?')){
            pulseMessage({t:'newtab_delete_shortcut',i:index});
          }
        }
        </script>
        </body>
        </html>
        """;

    private string NewTabShortcutsHtml()
    {
        if (!_uiSettings.NewTabShortcutsVisible)
            return string.Empty;

        var shortcuts = _uiSettings.NewTabShortcuts
            .Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.Title) && !string.IsNullOrWhiteSpace(shortcut.Url))
            .Take(12)
            .ToList();
        if (shortcuts.Count == 0)
            return string.Empty;

        var html = new StringBuilder();
        html.AppendLine("""<div class="shortcuts" aria-label="Raccourcis Pulse">""");
        for (var i = 0; i < shortcuts.Count; i++)
        {
            var shortcut = shortcuts[i];
            var title = NewTabMarkup.HtmlText(shortcut.Title);
            var url = NewTabMarkup.HtmlAttribute(NewTabMarkup.NormalizeShortcutUrl(shortcut.Url));
            var initial = NewTabMarkup.HtmlText(NewTabMarkup.ShortcutInitial(shortcut.Title));
            var jsTitle = NewTabMarkup.JsString(shortcut.Title);
            var jsUrl = NewTabMarkup.JsString(NewTabMarkup.NormalizeShortcutUrl(shortcut.Url));
            html.AppendLine($"""
            <div class="shortcut-card">
              <a class="shortcut-link" href="{url}" title="{url}">
                <span class="shortcut-dot">{initial}</span>
                <span class="shortcut-title">{title}</span>
              </a>
              <div class="shortcut-actions">
                <button class="shortcut-action" type="button" title="Modifier" onclick="editShortcut({i},'{jsTitle}','{jsUrl}')">✎</button>
                <button class="shortcut-action" type="button" title="Supprimer" onclick="deleteShortcut({i},'{jsTitle}')">×</button>
              </div>
            </div>
            """);
        }
        if (shortcuts.Count < 12)
        {
            html.AppendLine("""
            <button class="shortcut-card add-shortcut" type="button" onclick="addShortcut()" title="Ajouter un raccourci">
              <span class="shortcut-dot">+</span>
              <span class="shortcut-title">Ajouter</span>
            </button>
            """);
        }
        html.Append("</div>");
        return html.ToString();
    }

    private string SearchUrlTemplate() => _uiSettings.SearchEngine switch
    {
        "duckduckgo" => "https://duckduckgo.com/?q=%s",
        "brave" => "https://search.brave.com/search?q=%s",
        "bing" => "https://www.bing.com/search?q=%s",
        _ => "https://www.google.com/search?q=%s"
    };

    private BrowserTabState? CurrentTab()
    {
        if (BrowserTabs.SelectedItem is not TabViewItem item || item.Tag is not int id)
        {
            return null;
        }

        return _tabs.FirstOrDefault(tab => tab.Id == id);
    }

    private void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        // On refuse la fenêtre séparée et on ouvre l'URL dans un nouvel onglet Pulse.
        args.Handled = true;
        var uri = args.Uri;
        if (string.IsNullOrWhiteSpace(uri) || uri == "about:blank")
            return;
        DispatcherQueue.TryEnqueue(() => AddTab("Nouvel onglet", uri, select: true));
    }

    private async Task RegisterPasskeyMonitorAsync(CoreWebView2 core)
    {
        try
        {
            await core.AddScriptToExecuteOnDocumentCreatedAsync("""
                (function(){
                    if(window.__pulse_passkey_monitor)return;
                    window.__pulse_passkey_monitor=true;

                    var _origCreate=navigator.credentials.create.bind(navigator.credentials);
                    navigator.credentials.create=function(options){
                        // Retourner null pour les passkeys : évite la popup Windows Hello sans crasher Chromium
                        if(options&&options.publicKey){
                            return Promise.resolve(null);
                        }
                        var p=_origCreate(options);
                        if(p&&typeof p.then==='function'){
                            p.then(function(cred){
                                if(cred){
                                    try{window.chrome.webview.postMessage(JSON.stringify({t:'passkey_created',o:location.origin}));}catch(_){}
                                }
                            }).catch(function(){});
                        }
                        return p;
                    };

                    var _origGet=navigator.credentials.get.bind(navigator.credentials);
                    navigator.credentials.get=function(options){
                        // Retourner null pour les passkeys : le site tombe sur le formulaire mot de passe classique
                        if(options&&options.publicKey){
                            return Promise.resolve(null);
                        }
                        var p=_origGet(options);
                        if(p&&typeof p.then==='function'){
                            p.then(function(cred){
                                if(cred){
                                    try{window.chrome.webview.postMessage(JSON.stringify({t:'passkey_used',o:location.origin}));}catch(_){}
                                }
                            }).catch(function(){});
                        }
                        return p;
                    };
                })();
                """);
        }
        catch { }
    }
}
