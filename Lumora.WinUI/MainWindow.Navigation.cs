using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.System;
using Lumora.Privacy.HttpsEnforcer;
using Lumora.WinUI.Credentials;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // Moteur (onglet) → URL http d'origine promue en https. Sert à proposer un repli
    // en HTTP si la version sécurisée échoue (site sans HTTPS).
    private readonly Dictionary<WebView2, string> _httpsUpgradeOriginals = new();

    // ── Onglets ───────────────────────────────────────────────────────────────

    private void BrowserTabs_AddTabButtonClick(TabView sender, object args) =>
        AddTab("Nouvel onglet", "lumora://accueil", select: true);

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
        AddressBox.Text = DisplayAddressForBar(tab.Address);
        UpdateBookmarkStar(tab.Address);
        ShowPanel(BrowserPanel, tab.Title);

        foreach (var other in _tabs)
        {
            if (other.View is not null)
            {
                other.View.Visibility = other.Id == tab.Id ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        _browserView = tab.View;
        _currentPageDomain = ExtractDomain(tab.Address);

        // Les barres de remplissage appartiennent à la page quittée.
        AutoFillBar.Visibility = Visibility.Collapsed;
        WalletFillBar.Visibility = Visibility.Collapsed;
        SuggestPasswordBar.Visibility = Visibility.Collapsed;
        HideSiteNotFoundBar();
        _pendingAutoFillCandidates = Array.Empty<VaultCredential>();
        _pendingGeneratedPassword = null;

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
    private void EnsureTabView(BrowserTabState tab) =>
        _ = EnsureTabViewReadyAsync(tab);

    private async Task<WebView2?> EnsureTabViewReadyAsync(BrowserTabState tab, bool setPendingAddress = true)
    {
        if (tab.View is not null)
        {
            return tab.View;
        }

        if (!_browserSurfaceReady || _webViewDisabled)
        {
            return null;
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
        if (setPendingAddress)
        {
            tab.PendingAddress ??= tab.Address;
        }
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

            return view;
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
            return null;
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
            CloseTab(state);
        }
    }

    private void CloseTab(BrowserTabState state)
    {
        // Garde-fou : si on ferme le dernier onglet d'un groupe, le groupement va
        // disparaître ; proposer de le ranger dans la bibliothèque avant.
        if (state.GroupId is int closingGroupId &&
            _tabs.Count(tab => tab.GroupId == closingGroupId) == 1 &&
            _tabGroups.FirstOrDefault(group => group.Id == closingGroupId) is { } emptyingGroup)
        {
            OfferKeepGroupIfUnsaved(emptyingGroup);
            _tabGroups.Remove(emptyingGroup);
            _collapsedGroupIds.Remove(closingGroupId);
            _savedGroupIds.Remove(closingGroupId);
        }

        RememberClosedTab(state);
        var wasActive = CurrentTab()?.Id == state.Id;
        var oldIndex = _tabs.FindIndex(tab => tab.Id == state.Id);
        _tabs.Remove(state);
        CloseTabView(state);

        var item = BrowserTabs.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(candidate => candidate.Tag is int id && id == state.Id);
        if (item is not null)
        {
            BrowserTabs.TabItems.Remove(item);
        }

        if (BrowserTabs.TabItems.Count == 0)
        {
            AddTab("Nouvel onglet", "lumora://accueil", select: true);
        }
        else if (wasActive)
        {
            var nextIndex = Math.Clamp(oldIndex, 0, BrowserTabs.TabItems.Count - 1);
            if (BrowserTabs.TabItems[nextIndex] is TabViewItem nextItem)
            {
                BrowserTabs.SelectedItem = nextItem;
                if (nextItem.Tag is int nextId && _tabs.FirstOrDefault(tab => tab.Id == nextId) is { } nextTab)
                {
                    ActivateTab(nextTab);
                }
            }
        }

        RenderVerticalTabs();
        SaveTabSession();
        UpdateTitleBarDragRegion();
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
        _popupParentTabIds.Remove(state.Id);
        _federatedIdentityPopupTabIds.Remove(state.Id);
        _httpsUpgradeOriginals.Remove(view);
        _navHealth.ForgetTab(state.Id);
        var core = view.CoreWebView2;
        if (core is not null)
        {
            core.ContainsFullScreenElementChanged -= BrowserCore_ContainsFullScreenElementChanged;
            _credentialService.Detach(core);
            _cosmeticScriptIds.Remove(core);
            _consentScriptIds.Remove(core);
            _loginCompatibilityScriptIds.Remove(core);
            _loginDiagnosticScriptIds.Remove(core);
            if (ReferenceEquals(_contentFullScreenCore, core))
            {
                CompleteContentFullScreenExit("Mode plein ecran quitte.");
            }
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

        var tab = TabForView(sender);
        if (tab is null)
        {
            return;
        }

        // Stockage 100% maison : Chromium ne conserve NI ne remplit aucun mot de passe.
        // Le seul coffre est vault.lumora ; la capture et le remplissage sont faits par l'app.
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
        sender.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
        sender.CoreWebView2.WindowCloseRequested += BrowserCore_WindowCloseRequested;
        sender.CoreWebView2.ContainsFullScreenElementChanged += BrowserCore_ContainsFullScreenElementChanged;

        // Interception réseau pour le bloqueur de pubs/trackers
        sender.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        sender.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;

        // Popups / target=_blank / window.open : ouvrir dans un onglet Lumora au lieu
        // d'une fenêtre parasite non contrôlée.
        sender.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

        // Scripts privacy + passkeys + capture d'identifiants : chaque moteur reçoit
        // les siens (un WebView2 par onglet). Attendus avant la navigation ci-dessous :
        // sinon, quand la page cible s'ouvre dans un onglet tout neuf (ex. connexion
        // ouverte via un nouvel onglet), son DOM peut se créer avant l'enregistrement
        // de ces scripts, qui ratent alors ce chargement sans jamais se rattraper.
        await Task.WhenAll(
            RegisterCosmeticScriptOnCoreAsync(sender.CoreWebView2),
            RegisterConsentScriptOnCoreAsync(sender.CoreWebView2),
            RegisterGeolocationSpoofScriptOnCoreAsync(sender.CoreWebView2),
            RegisterFingerprintProtectionScriptOnCoreAsync(sender.CoreWebView2),
            RegisterLoginCompatibilityScriptOnCoreAsync(sender.CoreWebView2),
            RegisterLoginDiagnosticScriptOnCoreAsync(sender.CoreWebView2),
            RegisterPasskeyMonitorAsync(sender.CoreWebView2),
            RegisterPaymentMonitorAsync(sender.CoreWebView2),
            RegisterFullScreenExitMonitorAsync(sender.CoreWebView2),
            _credentialService.AttachAsync(sender.CoreWebView2, tab.Id));
        // Migration + nettoyage : importer ce que Chromium avait déjà, puis vider son coffre.
        // Ne bloque pas la navigation : sans lien avec la détection de page.
        _ = MigrateAndClearBrowserPasswordsAsync(sender.CoreWebView2);

        StatusText.Text = "Moteur web WinUI initialise.";
        WinUiRuntimeTrace.Write("CoreWebView2 initialized");

        // Sessions éphémères : purge des cookies de la visite précédente AVANT la
        // première navigation web, pour qu'aucune page ne charge d'anciens cookies.
        // (Guardée par lancement : seul le premier moteur purge, le profil est partagé.)
        await PurgeStartupSessionsAsync(sender.CoreWebView2);

        var pendingAddress = tab.PendingAddress;
        tab.PendingAddress = null;
        if (!string.IsNullOrWhiteSpace(pendingAddress))
        {
            WinUiRuntimeTrace.Write($"Navigation vers {pendingAddress} (scripts par-moteur deja enregistres)");
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
            var sourceAddress = tab.View?.Source?.ToString() ?? tab.Address;
            var addressForTab = BookmarkStore.IsWebUrl(sourceAddress) ? sourceAddress : tab.Address;
            UpdateTab(tab, title, addressForTab, updateHeaderOnly: true);
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
        RecordLoginDiagnosticForNavigation(sender.Source?.ToString(), "navigation-starting", args.Uri);
        var startingTab = TabForView(sender);

        // Suivi du document principal (redirections comprises : l'événement est
        // relevé à chaque saut) pour la détection 5xx et la mémoire des
        // déménagements. Voir MainWindow.SiteNotFound.cs.
        _navHealth.TrackNavigationStart(startingTab?.Id, args.Uri, args.IsRedirected, args.IsUserInitiated);

        if (startingTab is not null && IsFederatedIdentityIntermediary(args.Uri))
        {
            _federatedIdentityPopupTabIds.Add(startingTab.Id);
            ReturnToPopupParentIfVisible(startingTab);
            HideCredentialAutomationBars();
        }

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

            // La légitimité « demandé explicitement » suit l'URL nettoyée : sinon
            // un domaine tapé à la main serait re-vérifié (et bloqué) après nettoyage.
            var userInitiatedChain = startingTab is not null &&
                                     _navHealth.IsUserInitiatedNavigationChain(startingTab.Id);
            if (_navHealth.TakeExplicitNavigation(args.Uri) || args.IsUserInitiated || userInitiatedChain)
            {
                _navHealth.RegisterExplicitNavigation(cleaned);
            }

            args.Cancel = true;
            DispatcherQueue.TryEnqueue(() => sender.CoreWebView2?.Navigate(cleaned));
            return;
        }

        // Détournement de l'onglet : domaine répertorié publicitaire, clic
        // capturé vers un domaine tiers sur un site sous pression publicitaire,
        // ou tab-under. Navigation annulée SILENCIEUSEMENT : l'utilisateur
        // reste sur sa page. Jamais pour une adresse demandée via l'UI.
        var adVerdict = ClassifyNavigationForAdShield(
            startingTab?.Id, sender.Source?.ToString(), args.Uri, args.IsUserInitiated);
        if (adVerdict != NavigationVerdict.Allow)
        {
            args.Cancel = true;
            ReportBlockedNavigation(adVerdict, args.Uri, sender.Source?.ToString(), IsActiveView(sender));
            return;
        }

        // Compteur par-page, domaine du bouclier et statut : uniquement pour l'onglet visible.
        if (IsActiveView(sender))
        {
            _privacy.ResetPageBlockedCount();
            _telemetryBlocker?.ResetPageBlockedCount();
            UpdateToolbarPrivacyIndicator();
            // La proposition de carte appartient à la page quittée.
            WalletFillBar.Visibility = Visibility.Collapsed;
            HideSiteNotFoundBar();
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
        if (!args.IsSuccess && wasHttpsUpgrade && isActive && originalHttpUrl is not null &&
            IndicatesHttpsUnsupported(args.WebErrorStatus))
        {
            _ = PromptHttpsFallbackAsync(tab, originalHttpUrl);
        }

        var address = sender.Source?.ToString() ?? tab.Address;
        RecordLoginDiagnosticForNavigation(address, args.IsSuccess ? "navigation-completed" : "navigation-failed", address, args.IsSuccess ? null : args.WebErrorStatus.ToString());
        var title = sender.CoreWebView2?.DocumentTitle;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = DisplayTitle(address);
        }

        // L'adresse logique de l'onglet (ex. lumora://accueil) ne doit JAMAIS être
        // écrasée par la source technique "about:blank" de NavigateToString :
        // sinon tab.Address ne vaut plus jamais "lumora://accueil" une fois la
        // page d'accueil chargée, et RefreshNovaHomePages() (qui filtre sur
        // cette adresse pour recharger l'onglet après ajout d'un raccourci) ne
        // trouve plus jamais l'onglet à rafraîchir.
        var addressForTab = BookmarkStore.IsWebUrl(address) ? address : tab.Address;
        UpdateTab(tab, title, addressForTab, updateHeaderOnly: true);
        SyncActiveAddressBar(tab, addressForTab);
        _ = CaptureFaviconForTabAsync(tab);

        // Réinitialisation AVANT OfferAutoFill : c'est elle qui ré-affiche la barre
        // si un identifiant du coffre correspond à la page. Uniquement pour l'onglet
        // visible : une page d'arrière-plan ne pilote pas les barres.
        if (isActive)
        {
            WinUiRuntimeTrace.Write($"NavigationCompleted: address={address} isSuccess={args.IsSuccess} status={args.WebErrorStatus}");
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
            if (isActive)
                _ = OfferTranslationIfNeededAsync(sender, address);
        }
        else if (isActive)
        {
            TranslateBar.Visibility = Visibility.Collapsed;
        }
        // On garde la barre de sauvegarde tant qu'un identifiant est en attente, quelle
        // que soit la redirection post-login (souvent vers une autre origine). Elle n'est
        // fermée que par l'utilisateur (Enregistrer / Ignorer).
        if (_pendingCredential is null)
            CredentialSaveBar.Visibility = Visibility.Collapsed;

        // Reprise « site introuvable » : domaine qui ne se résout plus, serveur
        // qui ne répond plus (timeout, connexion refusée) ou erreur serveur 5xx
        // sur le document principal (ex. 522 Cloudflare = origine morte). Réseau
        // local coupé (Disconnected) exclu, et jamais pendant une promotion
        // HTTPS (le repli HTTP a son propre dialogue).
        var mainDocumentHttpError = _navHealth.TakeMainDocumentHttpError(tab.Id);
        if (isActive && !args.IsSuccess && !wasHttpsUpgrade && BookmarkStore.IsWebUrl(address))
        {
            if (IndicatesSiteDead(args.WebErrorStatus) || mainDocumentHttpError is not null)
            {
                OfferSiteNotFoundRecovery(address, mainDocumentHttpError);
            }
            else if (args.WebErrorStatus == CoreWebView2WebErrorStatus.Unknown)
            {
                // Erreur HTTP probable dont la réponse (5xx) n'est pas encore
                // arrivée : WebResourceResponseReceived déclenchera la barre.
                _navHealth.ArmPendingUnknownFailure(tab.Id, address);
            }
        }

        if (isActive)
        {
            StatusText.Text = args.IsSuccess ? $"Page chargee: {title}" : $"Navigation echouee: {args.WebErrorStatus}";
            if (args.IsSuccess && BookmarkStore.IsWebUrl(address))
            {
                // Reprise d'activité : signaler les annotations laissées sur cette
                // page, et ouvrir le mode lecture si le panneau Notes l'a demandé.
                var annotationCount = _annotations.CountForPage(address);
                if (annotationCount > 0)
                {
                    StatusText.Text = $"Page chargee: {title} - {annotationCount} annotation(s) a retrouver en mode lecture.";
                }
                OpenReaderIfPending(address);
            }
        }
        UpdatePrivacyUi();
    }

    // Ne proposer le passage en HTTP que si l'échec signifie vraiment « ce site ne
    // sert pas de HTTPS utilisable » : certificat invalide ou connexion refusée sur
    // le port TLS. Une panne transitoire (timeout, DNS, réseau coupé) toucherait
    // aussi la version HTTP — proposer un repli non chiffré serait à la fois inutile
    // et un faux signal « site en HTTP » pour un site parfaitement sécurisé.
    private static bool IndicatesHttpsUnsupported(CoreWebView2WebErrorStatus status) => status is
        CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect or
        CoreWebView2WebErrorStatus.CertificateExpired or
        CoreWebView2WebErrorStatus.ClientCertificateContainsErrors or
        CoreWebView2WebErrorStatus.CertificateRevoked or
        CoreWebView2WebErrorStatus.CertificateIsInvalid or
        CoreWebView2WebErrorStatus.CannotConnect or
        CoreWebView2WebErrorStatus.ConnectionAborted or
        CoreWebView2WebErrorStatus.ConnectionReset or
        CoreWebView2WebErrorStatus.ErrorHttpInvalidServerResponse;

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
            if (IsFederatedIdentityIntermediary(address))
            {
                _federatedIdentityPopupTabIds.Add(tab.Id);
                ReturnToPopupParentIfVisible(tab);
                HideCredentialAutomationBars();
            }
        }
    }

    // Fait suivre la barre d'adresse à l'URL réelle de l'onglet ACTIF. Point de
    // synchronisation unique appelé à chaque changement de source ou fin de navigation
    // (redirections et clics compris). Ne réécrit PAS la barre pendant que l'utilisateur
    // la modifie, pour ne pas écraser sa saisie.
    private void SyncActiveAddressBar(BrowserTabState tab, string address)
    {
        if (!IsActiveView(tab.View) || string.IsNullOrWhiteSpace(address)) return;
        // L'étoile de favori suit la page réelle, même pendant une saisie.
        UpdateBookmarkStar(address);
        if (AddressBox.FocusState != FocusState.Unfocused) return;
        var displayAddress = DisplayAddressForBar(address);
        if (AddressBox.Text != displayAddress) AddressBox.Text = displayAddress;
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
        // Le popup de suggestions consomme flèches, Échap et Entrée-sur-sélection.
        if (HandleAddressSuggestionsKey(e))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            NavigateFromAddressBox();
            e.Handled = true;
        }
    }

    private BrowserTabState AddTab(string title, string address, bool select, int? groupId = null, bool createViewWhenSelected = true, bool pinned = false)
    {
        var state = new BrowserTabState(_nextTabId++, title, address)
        {
            IconPath = CachedFaviconPathFor(address) ?? string.Empty,
            GroupId = groupId,
            Pinned = pinned
        };
        _tabs.Add(state);

        var tab = new TabViewItem
        {
            Tag = state.Id,
            IsClosable = !state.Pinned
        };
        tab.Header = TabHeaderContent(state, compact: state.Pinned);
        tab.ContextFlyout = CreateTabContextFlyout(state);
        // Nom d'accessibilite explicite : un onglet epingle n'affiche que son icone,
        // le lecteur d'ecran a besoin du titre pour rester utilisable.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tab, state.Title);

        BrowserTabs.TabItems.Add(tab);
        RenderVerticalTabs();
        SaveTabSession();
        UpdateTitleBarDragRegion();
        if (select)
        {
            // SelectionChanged active l'onglet ; filet direct si l'événement est
            // supprimé (bascule onglets verticaux) ou si la sélection ne change pas.
            BrowserTabs.SelectedItem = tab;
            if (createViewWhenSelected && (!ReferenceEquals(_browserView, state.View) || state.View is null))
            {
                ActivateTab(state);
            }
            else if (!createViewWhenSelected)
            {
                AddressBox.Text = DisplayAddressForBar(state.Address);
                UpdateBookmarkStar(state.Address);
                ShowPanel(BrowserPanel, state.Title);
            }
        }

        return state;
    }

    private void NavigateFromAddressBox()
    {
        CloseAddressSuggestions();
        var address = NormalizeAddress(AddressBox.Text);
        var title = address == "lumora://accueil" ? "Accueil Lumora" : DisplayTitle(address);
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
            AddressBox.Text = DisplayAddressForBar(address);
            UpdateBookmarkStar(address);
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
            item.Header = TabHeaderContent(tab, compact: tab.Pinned);
            item.IsClosable = !tab.Pinned;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, tab.Title);
        }
    }

    // Palette de couleurs de groupe : reprend les teintes de l'identité visuelle
    // (lumiere/web) complétées par des teintes distinguables.
    private static readonly Windows.UI.Color[] TabGroupPalette =
    {
        UiColor(255, 185, 53),
        UiColor(67, 219, 209),
        UiColor(94, 156, 235),
        UiColor(219, 112, 147),
        UiColor(154, 140, 226),
        UiColor(255, 127, 53)
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

            if (_verticalTabsCompact || tab.Pinned)
            {
                content = TabIconElement(tab, 22);
                button = new Button
                {
                    Width = 44,
                    Height = 38,
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
                    MaxWidth = 118,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var grid = new Grid { ColumnSpacing = 6 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(row, 0);
                grid.Children.Add(row);
                var close = new Button
                {
                    Width = 26,
                    Height = 26,
                    MinWidth = 26,
                    Padding = new Thickness(0),
                    Tag = tab.Id,
                    Content = new SymbolIcon(Symbol.Cancel),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0)
                };
                ToolTipService.SetToolTip(close, "Fermer l'onglet");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(close, $"Fermer {tab.Title}");
                close.Click += VerticalTabCloseButton_Click;
                Grid.SetColumn(close, 1);
                grid.Children.Add(close);
                content = grid;
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
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tab.Title);
            button.Click += VerticalTabButton_Click;
            button.ContextFlyout = CreateTabContextFlyout(tab);
            button.CanDrag = true;
            button.AllowDrop = true;
            button.DragStarting += VerticalTabButton_DragStarting;
            button.DragOver += VerticalTabButton_DragOver;
            button.Drop += VerticalTabButton_Drop;
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

        var pinItem = new MenuFlyoutItem { Text = tab.Pinned ? "Desepingler l'onglet" : "Epingler l'onglet", Tag = tab };
        pinItem.Click += TabContextTogglePin_Click;
        flyout.Items.Add(pinItem);

        var closeItem = new MenuFlyoutItem { Text = "Fermer l'onglet", Tag = tab };
        closeItem.Click += TabContextClose_Click;
        flyout.Items.Add(closeItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

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

        var saveItem = new MenuFlyoutItem { Text = "Enregistrer le groupe", Tag = group };
        saveItem.Click += SaveGroupToLibrary_Click;
        flyout.Items.Add(saveItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

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

    private void TabContextClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            CloseTab(tab);
        }
    }

    private void TabContextTogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            TogglePinTab(tab);
        }
    }

    // Epingler un onglet le compacte en icone seule et le regroupe au debut de la
    // liste (comme Chrome/Edge), sans toucher a l'ordre relatif des autres onglets.
    private void TogglePinTab(BrowserTabState tab)
    {
        tab.Pinned = !tab.Pinned;
        ReorderPinnedFirst();
        RefreshTabViewOrder();
        UpdateTabHeader(tab);
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void ReorderPinnedFirst()
    {
        var pinned = _tabs.Where(t => t.Pinned).ToList();
        var others = _tabs.Where(t => !t.Pinned).ToList();
        _tabs.Clear();
        _tabs.AddRange(pinned);
        _tabs.AddRange(others);
    }

    // Reconstruit l'ordre visuel de la barre horizontale pour qu'il corresponde a
    // l'ordre courant de _tabs (source de verite pour l'ordre des onglets).
    private void RefreshTabViewOrder()
    {
        _suppressTabOrderSync = true;
        try
        {
            var itemsByTag = BrowserTabs.TabItems
                .OfType<TabViewItem>()
                .Where(item => item.Tag is int)
                .ToDictionary(item => (int)item.Tag!);
            var selected = BrowserTabs.SelectedItem;

            BrowserTabs.TabItems.Clear();
            foreach (var tab in _tabs)
            {
                if (itemsByTag.TryGetValue(tab.Id, out var item))
                {
                    BrowserTabs.TabItems.Add(item);
                }
            }

            if (selected is not null)
            {
                BrowserTabs.SelectedItem = selected;
            }
        }
        finally
        {
            _suppressTabOrderSync = false;
        }
    }

    // Glisser-deposer d'onglets dans la barre horizontale (natif WinUI, TabItems est
    // observable) : on ne resynchronise _tabs qu'apres un vrai deplacement (Move),
    // pas apres un Add/Remove deja gere par AddTab/CloseTab.
    private void BrowserTabs_TabItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_suppressTabOrderSync || e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Move)
        {
            return;
        }

        var visualOrder = BrowserTabs.TabItems
            .OfType<TabViewItem>()
            .Where(item => item.Tag is int)
            .Select(item => (int)item.Tag!)
            .ToList();

        var byId = _tabs.ToDictionary(t => t.Id);
        var pinnedIds = visualOrder.Where(id => byId.TryGetValue(id, out var t) && t.Pinned).ToList();
        var otherIds = visualOrder.Where(id => !pinnedIds.Contains(id)).ToList();
        var finalOrder = pinnedIds.Concat(otherIds).ToList();

        _tabs.Clear();
        _tabs.AddRange(finalOrder.Select(id => byId[id]));

        // L'utilisateur a essaye de melanger epingles/non epingles : on corrige la
        // barre visuelle pour qu'elle respecte cet ordre force.
        if (!finalOrder.SequenceEqual(visualOrder))
        {
            RefreshTabViewOrder();
        }

        RenderVerticalTabs();
        SaveTabSession();
    }

    // Glisser-deposer d'onglets dans le rail vertical (implementation manuelle : le
    // rail est une pile de boutons construits a la main, pas un ItemsControl natif).
    private void VerticalTabButton_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is Button { Tag: int id })
        {
            args.Data.Properties.Add("novaTabId", id);
            args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }

    private void VerticalTabButton_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Properties.ContainsKey("novaTabId"))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }

    private void VerticalTabButton_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Button { Tag: int targetId } ||
            !e.DataView.Properties.TryGetValue("novaTabId", out var raw) ||
            raw is not int movingId ||
            movingId == targetId)
        {
            return;
        }

        ReorderTab(movingId, targetId);
    }

    // Un glisser-deposer ne peut pas melanger un onglet epingle et un onglet normal
    // (meme regle que pour la barre horizontale) : deposer hors de sa section n'a
    // simplement aucun effet plutot que de produire un ordre incoherent.
    private void ReorderTab(int movingId, int targetId)
    {
        var moving = _tabs.FirstOrDefault(t => t.Id == movingId);
        var target = _tabs.FirstOrDefault(t => t.Id == targetId);
        if (moving is null || target is null || moving.Pinned != target.Pinned)
        {
            return;
        }

        _tabs.Remove(moving);
        var targetIndex = _tabs.IndexOf(target);
        _tabs.Insert(targetIndex, moving);

        RefreshTabViewOrder();
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void VerticalTabCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int id } &&
            _tabs.FirstOrDefault(tab => tab.Id == id) is { } tab)
        {
            CloseTab(tab);
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

        // Garde-fou : le groupement va etre perdu ; proposer de le ranger d'abord.
        OfferKeepGroupIfUnsaved(group);

        foreach (var tab in _tabs.Where(t => t.GroupId == group.Id))
        {
            tab.GroupId = null;
        }
        _tabGroups.Remove(group);
        _collapsedGroupIds.Remove(group.Id);
        _savedGroupIds.Remove(group.Id);
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

        var tab = _tabs.FirstOrDefault(candidate => candidate.Id == id);
        if (tab is null)
        {
            return;
        }

        var item = BrowserTabs.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(candidate => candidate.Tag is int tabId && tabId == id);
        if (item is not null)
        {
            _suppressTabNavigation = true;
            BrowserTabs.SelectedItem = item;
            _suppressTabNavigation = false;
        }

        ActivateTab(tab);
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
        ShowPanel(BrowserPanel, tab?.Title ?? "Accueil Lumora");
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
            if (address.Equals("lumora://accueil", StringComparison.OrdinalIgnoreCase))
            {
                browser.CoreWebView2.NavigateToString(HomePageHtml());
                _ = ClearHomeSearchFieldAfterDelayAsync(blur: true);
                StatusText.Text = "Accueil Lumora.";
                return;
            }

            if (BookmarkStore.IsWebUrl(address))
            {
                _navHealth.RegisterExplicitNavigation(address);
                browser.CoreWebView2.Navigate(address);
                StatusText.Text = $"Chargement: {DisplayTitle(address)}";
                return;
            }

            var normalized = NormalizeAddress(address);
            _navHealth.RegisterExplicitNavigation(normalized);
            browser.CoreWebView2.Navigate(normalized);
            StatusText.Text = $"Chargement: {DisplayTitle(normalized)}";
        }
        finally
        {
            _isProgrammaticNavigation = false;
        }
    }

    private async Task ClearHomeSearchFieldAfterDelayAsync(bool blur)
    {
        await Task.Delay(80);
        await ClearHomeSearchFieldAsync(blur);
        await Task.Delay(280);
        await ClearHomeSearchFieldAsync(blur);
    }

    private async Task ClearHomeSearchFieldAsync(bool blur)
    {
        var tab = CurrentTab();
        var core = tab?.View?.CoreWebView2;
        if (tab is null || core is null ||
            !tab.Address.Equals("lumora://accueil", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var blurCall = blur ? "q.blur();" : string.Empty;
        var script =
            "(()=>{const q=document.getElementById('q');" +
            "if(q){q.value='';q.setAttribute('value','');" +
            blurCall +
            "}})();";

        try
        {
            await core.ExecuteScriptAsync(script);
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Home search clear skipped: {error.GetType().Name}");
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
            Tabs = _tabs.Select(t => new SavedTab(t.Title, t.Address, t.IconPath, t.GroupId, t.Pinned)).ToList(),
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
            AddTab(saved.Title, saved.Address, select: false, groupId: saved.GroupId, pinned: saved.Pinned);
        }

        var safeIndex = Math.Clamp(session.ActiveIndex, 0, BrowserTabs.TabItems.Count - 1);
        if (BrowserTabs.TabItems[safeIndex] is TabViewItem activeItem)
        {
            BrowserTabs.SelectedItem = activeItem;
            var restoredTab = _tabs.ElementAtOrDefault(safeIndex);
            if (restoredTab is not null)
            {
                AddressBox.Text = DisplayAddressForBar(restoredTab.Address);
                UpdateBookmarkStar(restoredTab.Address);
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
        <title>Accueil Lumora</title>
        <style>
        *{box-sizing:border-box;margin:0;padding:0}
        body{font-family:'Segoe UI',system-ui,sans-serif;background:{{NewTabPageBackgroundCss()}};color:{{(_uiSettings.AccessibilityHighContrast ? "#fff" : NewTabTextColorCss())}};min-height:100vh;display:flex;align-items:flex-start;justify-content:center;padding:{{NewTabBodyPaddingCss()}};font-size:{{(_uiSettings.AccessibilityLargeText ? "17px" : "15px")}};position:relative;overflow-x:hidden}
        body::before{content:"";position:fixed;inset:0;background:{{NewTabBackdropCss()}};pointer-events:none}
        body::after{content:"";position:fixed;inset:-22%;background:{{NewTabLightTraceCss()}};opacity:{{NewTabLightTraceOpacityCss()}};pointer-events:none;mix-blend-mode:screen;transform:translate3d(-6%,0,0) rotate(0.001deg)}
        main{width:min(1320px,100%);display:flex;flex-direction:column;align-items:stretch;gap:38px;position:relative}
        .home-grid{width:100%;display:grid;grid-template-columns:minmax(420px,1.04fr) minmax(380px,.82fr);gap:72px;align-items:center}
        .home-primary{display:flex;flex-direction:column;align-items:stretch;gap:30px;padding-top:18px}
        .mode-side{display:flex;flex-direction:column;gap:16px;justify-self:end;width:min(500px,100%)}
        .brand{display:flex;flex-direction:column;align-items:flex-start;gap:13px}
        .mark{display:flex;align-items:center;gap:14px}
        .logo-icon{width:66px;height:66px;border-radius:15px;object-fit:contain;flex-shrink:0;filter:drop-shadow(0 18px 30px rgba(0,0,0,.32)) drop-shadow(0 0 22px rgba(255,185,53,.18))}
        .logo-name{font-size:42px;font-weight:650;line-height:1;letter-spacing:0;color:{{(_uiSettings.AccessibilityHighContrast ? "#fff" : NewTabLogoTextColorCss())}}}
        .accent-line{width:168px;height:3px;border-radius:999px;background:{{(_uiSettings.AccessibilityHighContrast ? "#ffd500" : NewTabAccentLineCss())}};opacity:.98;box-shadow:0 0 18px {{NewTabAccentGlowCss()}};position:relative;overflow:hidden}
        .accent-line::after{content:"";position:absolute;inset:0;background:linear-gradient(90deg,transparent,rgba(255,255,255,.82),transparent);transform:translateX(-105%)}
        .search{width:100%;height:{{(_uiSettings.AccessibilityLargeText ? "54px" : "50px")}};border-radius:25px;background:{{(_uiSettings.AccessibilityHighContrast ? "#fff" : "#fbf4e8")}};display:flex;align-items:center;gap:12px;padding:0 20px;border:{{(_uiSettings.AccessibilityVisibleFocus ? "2px" : "1px")}} solid {{(_uiSettings.AccessibilityHighContrast ? "#fff" : "rgba(255,248,235,.24)")}};box-shadow:0 14px 38px rgba(0,0,0,.24)}
        .search:focus-within{border-color:{{(_uiSettings.AccessibilityHighContrast ? "#ffd500" : NewTabFocusBorderCss())}};box-shadow:0 14px 38px rgba(0,0,0,.27),0 0 0 3px {{NewTabFocusRingCss()}}}
        .search svg{width:18px;height:18px;color:#796f63;flex-shrink:0}
        .search input{width:100%;height:100%;border:0;outline:0;background:transparent;color:#201f1b;font-size:{{(_uiSettings.AccessibilityLargeText ? "17px" : "15px")}}}
        .search input::placeholder{color:#81786d}
        .mode-panel{width:100%;display:grid;grid-template-columns:minmax(0,1fr) auto;grid-template-areas:"copy visual" "actions actions";column-gap:18px;row-gap:14px;align-items:start;padding:18px 20px;border:1px solid {{NewTabModeBorderCss()}};border-radius:10px;background:{{NewTabModeSurfaceCss()}};box-shadow:0 14px 32px rgba(0,0,0,.12);position:relative;overflow:hidden}
        .mode-panel::before{content:"";position:absolute;inset:0 auto 0 0;width:5px;background:{{NewTabModeSignatureBarCss()}}}
        .mode-panel::after{content:"";position:absolute;inset:0;background:{{NewTabModePanelPatternCss()}};opacity:.5;pointer-events:none}
        .mode-copy,.mode-actions,.mode-visual{position:relative;z-index:1}
        .mode-copy{grid-area:copy}
        .mode-eyebrow{font-size:12px;color:{{NewTabModeMutedCss()}};margin-bottom:3px}
        .mode-title{font-size:18px;font-weight:650;color:{{NewTabLogoTextColorCss()}}}
        .mode-text{font-size:13px;color:{{NewTabModeMutedCss()}};line-height:1.42;margin-top:4px;max-width:34ch}
        .mode-visual{grid-area:visual;width:54px;height:42px;border:1px solid {{NewTabModeBorderCss()}};border-radius:8px;background:rgba(255,255,255,.04);display:flex;align-items:center;justify-content:center;gap:5px;padding:8px;overflow:hidden;align-self:start}
        .mode-visual span{display:block;border-radius:999px;background:{{NewTabModeVisualCss()}};box-shadow:0 0 16px {{NewTabModeVisualGlowCss()}}}
        .mode-visual span:nth-child(1){width:7px;height:18px}
        .mode-visual span:nth-child(2){width:7px;height:28px}
        .mode-visual span:nth-child(3){width:7px;height:22px}
        .mode-visual span:nth-child(4){width:7px;height:34px}
        .mode-actions{grid-area:actions;display:flex;gap:8px;flex-wrap:wrap;justify-content:flex-start}
        .mode-chip{border:1px solid {{NewTabModeBorderCss()}};border-radius:999px;background:rgba(255,255,255,.05);color:{{NewTabTextColorCss()}};font-size:12px;padding:7px 10px;white-space:nowrap}
        .mode-intro{width:100%;display:grid;grid-template-columns:minmax(0,1fr) auto;gap:16px;align-items:start;padding:14px 16px;border:{{(_uiSettings.AccessibilityVisibleFocus ? "2px" : "1px")}} solid {{NewTabModeBorderCss()}};border-radius:10px;background:rgba(255,255,255,.02);box-shadow:none}
        .mode-intro-title{font-size:15px;font-weight:650;color:{{NewTabLogoTextColorCss()}}}
        .mode-intro-text{font-size:12px;line-height:1.35;color:{{NewTabModeMutedCss()}};margin-top:4px}
        .mode-intro-points{display:flex;gap:6px;flex-wrap:wrap;margin-top:8px}
        .mode-intro-point{border:1px solid {{NewTabModeBorderCss()}};border-radius:999px;background:rgba(255,255,255,.045);color:{{NewTabTextColorCss()}};font-size:12px;padding:6px 9px}
        .mode-context{width:100%;display:grid;grid-template-columns:minmax(0,1.1fr) minmax(220px,.82fr);gap:18px;padding:18px;border:1px solid {{NewTabModeBorderCss()}};border-radius:10px;background:rgba(255,255,255,.032);box-shadow:0 14px 34px rgba(0,0,0,.12)}
        .mode-context-copy{display:flex;flex-direction:column;gap:10px;min-width:0}
        .mode-context-eyebrow{font-size:12px;color:{{NewTabModeMutedCss()}}}
        .mode-context-title{font-size:17px;font-weight:650;color:{{NewTabLogoTextColorCss()}}}
        .mode-context-text{font-size:12px;line-height:1.35;color:{{NewTabModeMutedCss()}}}
        .mode-draft{width:100%;min-height:116px;resize:vertical;border:1px solid {{NewTabModeBorderCss()}};border-radius:8px;background:{{(_uiSettings.AccessibilityHighContrast ? "#000" : "rgba(255,255,255,.06)")}};color:{{(_uiSettings.AccessibilityHighContrast ? "#fff" : NewTabTextColorCss())}};font:inherit;font-size:13px;line-height:1.35;padding:12px;outline:none}
        .mode-draft:focus{border-color:{{(_uiSettings.AccessibilityHighContrast ? "#ffd500" : NewTabFocusBorderCss())}};box-shadow:0 0 0 3px {{NewTabFocusRingCss()}}}
        .mode-context-actions{display:flex;flex-direction:column;gap:10px}
        .mode-context-button{border:1px solid {{NewTabModeBorderCss()}};border-radius:8px;background:rgba(255,255,255,.05);color:{{NewTabTextColorCss()}};font:inherit;font-size:12px;padding:11px 12px;cursor:pointer;text-align:left}
        .mode-context-button.primary{background:{{NewTabAccentSolidCss()}};border-color:{{NewTabAccentSolidCss()}};color:#15130e;font-weight:650;text-align:center}
        .mode-context-button:hover,.mode-context-button:focus,.mode-intro-button:hover,.mode-intro-button:focus{transform:translateY(-1px);box-shadow:0 10px 22px rgba(0,0,0,.18);outline:none}
        .mode-note-status{min-height:16px;font-size:12px;color:{{NewTabModeMutedCss()}}}
        .mode-intro-button{border:1px solid {{NewTabModeBorderCss()}};border-radius:999px;background:rgba(255,255,255,.06);color:{{NewTabTextColorCss()}};font:inherit;font-size:12px;padding:8px 12px;cursor:pointer;white-space:nowrap}
        .mode-focus .mode-panel{box-shadow:0 16px 38px rgba(0,0,0,.2),0 0 0 1px {{NewTabAccentSolidCss()}}22}
        .mode-focus .mode-visual{gap:3px;background:rgba(255,255,255,.03)}
        .mode-focus .mode-visual span{width:4px;border-radius:3px}
        .mode-focus .mode-visual span:nth-child(1){height:30px}
        .mode-focus .mode-visual span:nth-child(2){height:36px}
        .mode-focus .mode-visual span:nth-child(3){height:18px;opacity:.55}
        .mode-focus .mode-visual span:nth-child(4){height:24px;opacity:.7}
        .mode-reading .mode-panel{background:rgba(255,255,255,.07)}
        .mode-reading .mode-visual{flex-direction:column;align-items:stretch;gap:6px;background:rgba(255,248,234,.07)}
        .mode-reading .mode-visual span{width:auto;height:4px}
        .mode-reading .mode-visual span:nth-child(1){width:90%}
        .mode-reading .mode-visual span:nth-child(2){width:100%}
        .mode-reading .mode-visual span:nth-child(3){width:72%}
        .mode-reading .mode-visual span:nth-child(4){width:52%}
        .mode-creative .mode-panel{background:linear-gradient(135deg,{{NewTabModeSurfaceCss()}},{{NewTabAccentSolidCss()}}18)}
        .mode-creative .mode-visual{transform:rotate(-2deg);background:linear-gradient(135deg,rgba(255,255,255,.07),{{NewTabAccent2SolidCss()}}18)}
        .mode-creative .mode-visual span:nth-child(1){height:16px}
        .mode-creative .mode-visual span:nth-child(2){height:32px}
        .mode-creative .mode-visual span:nth-child(3){height:24px}
        .mode-creative .mode-visual span:nth-child(4){height:38px}
        .mode-research .mode-panel{border-style:dashed}
        .mode-research .mode-visual{flex-direction:column;align-items:stretch;gap:5px;border-style:dashed;background:rgba(67,219,209,.045)}
        .mode-research .mode-visual span{height:3px;width:auto;border-radius:2px}
        .mode-research .mode-visual span:nth-child(1){width:100%}
        .mode-research .mode-visual span:nth-child(2){width:76%}
        .mode-research .mode-visual span:nth-child(3){width:92%}
        .mode-research .mode-visual span:nth-child(4){width:58%}
        .mode-night .mode-panel{background:rgba(6,10,18,.68);border-color:rgba(160,190,255,.24)}
        .mode-night .mode-visual{flex-direction:column;align-items:stretch;gap:7px;background:rgba(6,10,18,.5)}
        .mode-night .mode-visual span{height:3px;width:auto;opacity:.68}
        .mode-night .mode-visual span:nth-child(1){width:68%}
        .mode-night .mode-visual span:nth-child(2){width:92%}
        .mode-night .mode-visual span:nth-child(3){width:50%}
        .mode-night .mode-visual span:nth-child(4){width:74%}
        .mode-workbench{width:min(820px,100%);display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px}
        .mode-tool{min-height:78px;border:1px solid {{NewTabModeBorderCss()}};border-radius:8px;background:rgba(255,255,255,.038);color:{{NewTabTextColorCss()}};text-align:left;padding:12px;cursor:pointer;font:inherit;display:flex;flex-direction:column;gap:5px}
        .mode-tool:hover,.mode-tool:focus{transform:translateY(-1px);background:rgba(255,255,255,.07);box-shadow:0 14px 28px rgba(0,0,0,.18);outline:none}
        .mode-tool-title{font-size:13px;font-weight:650;color:{{NewTabLogoTextColorCss()}}}
        .mode-tool-text{font-size:12px;line-height:1.32;color:{{NewTabModeMutedCss()}}}
        .mode-focus main{width:min(1040px,100%);gap:22px}
        .mode-focus .brand{transform:scale(.94);transform-origin:center top}
        .mode-focus .mode-workbench{grid-template-columns:1fr}
        .mode-focus .mode-tool{min-height:64px}
        .mode-focus .shortcuts{display:none}
        .mode-reading main{gap:26px}
        .mode-reading .search{background:#fff8ee}
        .mode-reading .mode-workbench{width:min(720px,100%)}
        .mode-creative main{width:min(1160px,100%)}
        .mode-creative .mode-workbench{width:min(900px,100%)}
        .mode-creative .mode-tool:nth-child(1){background:linear-gradient(135deg,rgba(255,255,255,.07),{{NewTabAccentSolidCss()}}18)}
        .mode-creative .mode-tool:nth-child(2){background:linear-gradient(135deg,rgba(255,255,255,.06),{{NewTabAccent2SolidCss()}}1f)}
        .mode-research main{width:min(1160px,100%)}
        .mode-research .mode-panel,.mode-research .mode-workbench{width:100%}
        .mode-research .mode-tool{border-style:dashed}
        .mode-night{filter:brightness(.9)}
        .mode-night .search{background:#e8e0d3;box-shadow:0 10px 28px rgba(0,0,0,.22)}
        .mode-night .mode-tool{background:rgba(8,12,22,.58);border-color:rgba(160,190,255,.23)}
        .mode-balanced main{width:min(1320px,100%)}
        .balanced-home{width:100%;min-height:72vh;display:grid;grid-template-columns:minmax(460px,1.08fr) minmax(360px,.82fr);gap:78px;align-items:center}
        .balanced-lead{display:flex;flex-direction:column;align-items:stretch;gap:28px;min-width:0}
        .balanced-brand{display:flex;align-items:center;gap:16px}
        .balanced-logo{width:64px;height:64px;border-radius:15px;object-fit:contain;flex-shrink:0;filter:drop-shadow(0 18px 30px rgba(0,0,0,.32)) drop-shadow(0 0 20px {{NewTabAccentGlowCss()}})}
        .balanced-title{font-size:46px;font-weight:650;line-height:1;letter-spacing:0;color:{{NewTabLogoTextColorCss()}}}
        .balanced-greeting{font-size:13px;color:{{NewTabModeMutedCss()}}}
        .balanced-dock{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;align-content:start}
        .balanced-mode-panel{grid-column:1 / -1}
        .balanced-wide{grid-column:1 / -1}
        .balanced-card{border:1px solid {{NewTabModeBorderCss()}};border-radius:10px;background:linear-gradient(135deg,rgba(255,255,255,.04),{{NewTabAccent2SolidCss()}}10);padding:16px;box-shadow:0 14px 32px rgba(0,0,0,.12)}
        .balanced-card-title{font-size:15px;font-weight:650;color:{{NewTabLogoTextColorCss()}};margin-bottom:10px}
        .balanced-actions{display:grid;gap:8px}
        .balanced-action{border:1px solid {{NewTabModeBorderCss()}};border-radius:8px;background:rgba(255,255,255,.036);color:{{NewTabTextColorCss()}};font:inherit;font-size:13px;padding:12px;text-align:left;cursor:pointer}
        .balanced-action strong{display:block;font-size:13px;color:{{NewTabLogoTextColorCss()}};margin-bottom:3px}
        .balanced-action span{display:block;font-size:12px;line-height:1.28;color:{{NewTabModeMutedCss()}}}
        .balanced-action:hover,.balanced-action:focus{transform:translateY(-1px);background:rgba(255,255,255,.07);box-shadow:0 12px 26px rgba(0,0,0,.18);outline:none}
        .mode-neutral{align-items:center}
        .mode-neutral::after{display:none}
        .mode-neutral main{width:min(860px,100%);gap:0}
        .neutral-home{width:100%;min-height:72vh;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:22px}
        .neutral-brand{display:flex;align-items:center;justify-content:center;gap:10px;opacity:.9}
        .neutral-logo{width:46px;height:46px;border-radius:12px;object-fit:contain;filter:drop-shadow(0 12px 24px rgba(0,0,0,.3))}
        .neutral-name{font-size:20px;font-weight:600;color:{{NewTabLogoTextColorCss()}}}
        .neutral-clock{font-size:{{(_uiSettings.AccessibilityLargeText ? "68px" : "58px")}};font-weight:520;letter-spacing:0;color:{{NewTabLogoTextColorCss()}};line-height:1}
        .neutral-search{width:min(660px,100%);box-shadow:0 16px 42px rgba(0,0,0,.22)}
        .mode-neutral .shortcuts{justify-content:center;max-width:660px}
        .mode-neutral .shortcut-card{background:rgba(255,255,255,.03)}
        .personalize-invite{width:100%;display:grid;grid-template-columns:minmax(0,1fr) auto;gap:16px;align-items:center;padding:16px;border:1px solid {{NewTabModeBorderCss()}};border-radius:10px;background:{{NewTabPersonalizationInviteSurfaceCss()}};box-shadow:0 16px 38px rgba(0,0,0,.16)}
        .invite-eyebrow{font-size:12px;color:{{NewTabModeMutedCss()}};margin-bottom:3px}
        .invite-title{font-size:19px;font-weight:650;color:{{NewTabLogoTextColorCss()}}}
        .invite-text{font-size:13px;color:{{NewTabModeMutedCss()}};line-height:1.38;margin-top:4px;max-width:520px}
        .invite-actions{display:flex;gap:8px;flex-wrap:wrap;justify-content:flex-end}
        .invite-button{border:1px solid {{NewTabModeBorderCss()}};border-radius:999px;background:rgba(255,255,255,.06);color:{{NewTabTextColorCss()}};font:inherit;font-size:12px;padding:8px 12px;cursor:pointer;white-space:nowrap}
        .invite-button.primary{background:{{NewTabAccentSolidCss()}};border-color:{{NewTabAccentSolidCss()}};color:#15130e;font-weight:650}
        .invite-button:hover,.invite-button:focus{transform:translateY(-1px);box-shadow:0 10px 22px rgba(0,0,0,.18)}
        {{NewTabResponsiveCss()}}
        .shortcuts{display:flex;gap:12px;flex-wrap:wrap;justify-content:flex-start;max-width:760px}
        .shortcut-card{position:relative;width:92px;min-height:88px;border-radius:8px;padding:8px 6px 7px;display:flex;flex-direction:column;align-items:center;gap:8px;color:#eee2d4;text-decoration:none;font-size:12px;border:1px solid transparent}
        .shortcut-card:hover,.shortcut-card:focus-within{background:rgba(255,255,255,.06);border-color:{{NewTabShortcutBorderCss()}}}
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
        .add-shortcut:hover .shortcut-dot{border-color:{{NewTabAccentSolidCss()}};color:#fff}
        .minimal .accent-line,.minimal .hint{display:none}
        .minimal main{gap:22px}
        .calm .shortcut-card:hover,.calm .shortcut-card:focus-within{background:rgba(255,255,255,.045)}
        .hint{font-size:12px;color:#a7a096;margin-top:2px}
        {{NewTabTransitionCss()}}
        {{NewTabMotionCss()}}
        </style>
        </head>
        <body class="{{NewTabMarkup.HtmlAttribute(NewTabStyleClass() + " " + NewTabMotionClass() + " " + NewTabUsageModeClass())}}">
        <main>
          {{NewTabHomeContentHtml()}}
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
        function novaMessage(payload){
          try{window.chrome.webview.postMessage(JSON.stringify(payload));}catch(_){}
        }
        function addShortcut(){
          novaMessage({t:'newtab_add_shortcut'});
        }
        function editShortcut(index,title,url){
          novaMessage({t:'newtab_edit_shortcut',i:index,title:title,url:url});
        }
        function deleteShortcut(index,title){
          if(confirm('Supprimer le raccourci "'+title+'" ?')){
            novaMessage({t:'newtab_delete_shortcut',i:index});
          }
        }
        function personalizeLumora(){
          novaMessage({t:'newtab_personalize'});
        }
        function openLumoraModules(){
          novaMessage({t:'newtab_modules'});
        }
        function modeAction(action){
          novaMessage({t:'newtab_mode_action',action:action});
        }
        function dismissModeIntro(mode){
          novaMessage({t:'newtab_mode_intro_dismiss',mode:mode});
        }
        function saveModeQuickNote(mode,title){
          const draft=document.getElementById('modeDraft');
          const status=document.getElementById('modeNoteStatus');
          const content=(draft?.value||'').trim();
          if(!content){
            if(status)status.textContent='Note vide.';
            draft?.focus();
            return;
          }
          novaMessage({t:'newtab_mode_quick_note',mode:mode,title:title,content:content});
          if(status)status.textContent='Gardé dans Lumie.';
        }
        function clearSearch(lock){
          const q=document.getElementById('q');
          if(!q)return;
          q.value='';
          q.setAttribute('value','');
          if(lock)q.setAttribute('readonly','readonly');
        }
        function unlockSearch(){
          const q=document.getElementById('q');
          if(!q)return;
          q.value='';
          q.removeAttribute('readonly');
        }
        clearSearch(true);
        window.addEventListener('pageshow',()=>clearSearch(true));
        document.addEventListener('DOMContentLoaded',()=>clearSearch(true));
        setTimeout(unlockSearch,500);
        document.getElementById('q')?.addEventListener('pointerdown',unlockSearch);
        document.getElementById('q')?.addEventListener('focus',unlockSearch,{once:true});
        </script>
        </body>
        </html>
        """;

    private string NewTabHomeContentHtml()
    {
        if (NewTabUsageMode() == "neutral")
        {
            return $$"""
              <section class="neutral-home" aria-label="Accueil neutre Lumora">
                <div class="neutral-brand" aria-label="Lumora">
                  <img class="neutral-logo" src="{{LumoraLogoDataUri()}}" alt="" aria-hidden="true">
                  <span class="neutral-name">Lumora</span>
                </div>
                <time class="neutral-clock" datetime="{{DateTime.Now:HH\:mm}}" aria-label="Heure locale">{{DateTime.Now:HH:mm}}</time>
                {{NewTabSearchFormHtml("neutral-search")}}
                {{NewTabShortcutsHtml()}}
              </section>
            """;
        }

        if (NewTabUsageMode() == "balanced")
        {
            return $$"""
              <section class="balanced-home" aria-label="Accueil quotidien Lumora">
                <div class="balanced-lead">
                  <div class="balanced-brand">
                    <img class="balanced-logo" src="{{LumoraLogoDataUri()}}" alt="" aria-hidden="true">
                    <div>
                      <div class="balanced-title">{{NewTabMarkup.HtmlText(_uiSettings.NewTabTitle)}}</div>
                      <div class="balanced-greeting">{{NewTabMarkup.HtmlText(NewTabGreeting())}}</div>
                    </div>
                  </div>
                  {{NewTabSearchFormHtml()}}
                  {{NewTabShortcutsHtml()}}
                </div>
                <div class="balanced-dock">
                  <section class="mode-panel balanced-mode-panel" aria-label="Mode Lumora">
                    <div class="mode-copy">
                      <div class="mode-eyebrow">Navigation quotidienne</div>
                      <div class="mode-title">{{NewTabMarkup.HtmlText(NewTabUsageModeTitle())}}</div>
                      <div class="mode-text">{{NewTabMarkup.HtmlText(NewTabUsageModeText())}}</div>
                    </div>
                    <div class="mode-visual" aria-hidden="true"><span></span><span></span><span></span><span></span></div>
                    <div class="mode-actions">
                      {{NewTabUsageModeActionsHtml()}}
                    </div>
                  </section>
                  <section class="balanced-card" aria-label="Actions Lumora">
                    <div class="balanced-card-title">Aujourd'hui</div>
                    <div class="balanced-actions">
                      <button class="balanced-action" type="button" onclick="modeAction('bookmarks')"><strong>Favoris</strong><span>Retrouver les pages gardées localement.</span></button>
                      <button class="balanced-action" type="button" onclick="modeAction('history')"><strong>Historique</strong><span>Reprendre une navigation récente.</span></button>
                      <button class="balanced-action" type="button" onclick="modeAction('modules')"><strong>Modules</strong><span>Ajuster les outils visibles.</span></button>
                    </div>
                  </section>
                  {{NewTabPersonalizationInviteHtml()}}
                </div>
              </section>
            """;
        }

        return $$"""
          <section class="home-grid" aria-label="Accueil Lumora">
            <div class="home-primary">
              <div class="brand">
                <div class="mark">
                  <img class="logo-icon" src="{{LumoraLogoDataUri()}}" alt="" aria-hidden="true">
                  <div class="logo-name">{{NewTabMarkup.HtmlText(_uiSettings.NewTabTitle)}}</div>
                </div>
                <div class="accent-line"></div>
              </div>
              {{NewTabSearchFormHtml()}}
              {{NewTabShortcutsHtml()}}
              <p class="hint">{{Version}}</p>
            </div>
            <div class="mode-side">
              <section class="mode-panel" aria-label="Mode Lumora">
                <div class="mode-copy">
                  <div class="mode-eyebrow">{{NewTabMarkup.HtmlText(NewTabGreeting())}}</div>
                  <div class="mode-title">{{NewTabMarkup.HtmlText(NewTabUsageModeTitle())}}</div>
                  <div class="mode-text">{{NewTabMarkup.HtmlText(NewTabUsageModeText())}}</div>
                </div>
                <div class="mode-visual" aria-hidden="true"><span></span><span></span><span></span><span></span></div>
                <div class="mode-actions">
                  {{NewTabUsageModeActionsHtml()}}
                </div>
              </section>
              {{NewTabModeIntroHtml()}}
              {{NewTabModeWorkbenchHtml()}}
              {{NewTabPersonalizationInviteHtml()}}
            </div>
          </section>
        """;
    }

    private string NewTabSearchFormHtml(string extraClass = "")
    {
        var searchClass = string.IsNullOrWhiteSpace(extraClass) ? "search" : $"search {extraClass}";
        return $$"""
              <form class="{{NewTabMarkup.HtmlAttribute(searchClass)}}" onsubmit="go(event)">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M9.5 3a6.5 6.5 0 0 1 5.16 10.45l4.44 4.45-1.2 1.2-4.45-4.44A6.5 6.5 0 1 1 9.5 3m0 1.7a4.8 4.8 0 1 0 0 9.6 4.8 4.8 0 0 0 0-9.6"/></svg>
                <input id="q" value="" readonly {{(_uiSettings.NewTabFocusSearchOnOpen ? "autofocus" : "")}} autocomplete="new-password" autocapitalize="off" autocorrect="off" spellcheck="false" inputmode="search" placeholder="Rechercher ou saisir une URL">
              </form>
        """;
    }

    private string LumoraLogoDataUri()
    {
        try
        {
            foreach (var path in new[]
                     {
                         Path.Combine(AppContext.BaseDirectory, "Assets", "LumoraApp.png"),
                         Path.Combine(Environment.CurrentDirectory, "Lumora.WinUI", "Assets", "LumoraApp.png"),
                         Path.Combine(Environment.CurrentDirectory, "Assets", "LumoraApp.png")
                     })
            {
                if (File.Exists(path))
                {
                    return "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(path));
                }
            }
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Lumora logo data uri skipped: {error.GetType().Name}");
        }

        return string.Empty;
    }

    private string NewTabStyleClass() =>
        (_uiSettings.NewTabStyle ?? "signature").ToLowerInvariant() switch
        {
            "calm" => "calm",
            "minimal" => "minimal",
            _ => "signature"
        };

    private string NewTabMotionClass()
    {
        if (_uiSettings.AccessibilityReduceMotion || _uiSettings.AccessibilityHighContrast)
        {
            return "motion-static";
        }

        return (_uiSettings.PersonalizationMotionStyle ?? "luminous").ToLowerInvariant() switch
        {
            "subtle" => "motion-subtle",
            "dynamic" => "motion-dynamic",
            _ => "motion-luminous"
        };
    }

    private string NewTabUsageMode() =>
        (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant() switch
        {
            "neutral" => "neutral",
            "balanced" => "balanced",
            "focus" => "focus",
            "reading" => "reading",
            "creative" => "creative",
            "research" => "research",
            "night" => "night",
            _ => "neutral"
        };

    private string NewTabUsageModeClass() => "mode-" + NewTabUsageMode();

    private string NewTabUsageModeTitle() => NewTabUsageMode() switch
    {
        "neutral" => "Mode Neutre",
        "focus" => "Mode Focus",
        "reading" => "Mode Lecture",
        "creative" => "Mode Création",
        "research" => "Mode Recherche",
        "night" => "Mode Nuit",
        _ => "Mode Équilibre"
    };

    private string NewTabUsageModeText() => NewTabUsageMode() switch
    {
        "neutral" => "L'accueil de base : l'heure, une recherche et seulement les outils essentiels du navigateur.",
        "focus" => "Une entrée compacte pour reprendre vite, limiter le bruit visuel et garder les outils essentiels à portée.",
        "reading" => "Un accueil calme pour lire, annoter, reprendre les pages longues et garder la navigation douce.",
        "creative" => "Un espace plus expressif pour ouvrir des idées, lancer des recherches et garder les notes proches.",
        "research" => "Un point de départ orienté collecte : recherche, onglets, historique local et sources à comparer.",
        "night" => "Un rythme plus doux pour naviguer tard, avec moins d'éclat et une présence visuelle plus posée.",
        _ => "Un équilibre entre confort, rapidité et repères personnels pour la navigation quotidienne."
    };

    private string NewTabGreeting()
    {
        var hour = DateTime.Now.Hour;
        var moment = hour switch
        {
            >= 5 and < 12 => "Bonjour",
            >= 12 and < 18 => "Bon après-midi",
            >= 18 and < 23 => "Bonsoir",
            _ => "Navigation nocturne"
        };

        return $"{moment} · {DateTime.Now:HH:mm}";
    }

    private string NewTabUsageModeActionsHtml()
    {
        var actions = NewTabUsageMode() switch
        {
            "balanced" => new[] { "Favoris", "Historique", "Modules" },
            "focus" => new[] { "Reprendre", "Épingler", "Ctrl+K" },
            "reading" => new[] { "Lire", "Annoter", "Calmer" },
            "creative" => new[] { "Idées", "Notes", "Explorer" },
            "research" => new[] { "Comparer", "Sources", "Historique" },
            "night" => new[] { "Douceur", "Focus", "Lecture" },
            _ => Array.Empty<string>()
        };

        var html = new StringBuilder();
        foreach (var action in actions)
        {
            html.Append("<span class=\"mode-chip\">")
                .Append(NewTabMarkup.HtmlText(action))
                .Append("</span>");
        }

        return html.ToString();
    }

    private string NewTabModeIntroHtml()
    {
        var mode = NewTabUsageMode();
        if (mode == "neutral" || mode == "balanced")
        {
            return string.Empty;
        }

        if (string.Equals(_uiSettings.LastIntroducedUsageMode, mode, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var points = NewTabModeIntroPoints(mode);
        var pointHtml = string.Join("", points.Select(point =>
            $"<span class=\"mode-intro-point\">{NewTabMarkup.HtmlText(point)}</span>"));

        return $$"""
          <section class="mode-intro" aria-label="Présentation du mode actif">
            <div>
              <div class="mode-intro-title">{{NewTabMarkup.HtmlText(NewTabUsageModeTitle())}} activé</div>
              <div class="mode-intro-text">{{NewTabMarkup.HtmlText(NewTabModeIntroText(mode))}}</div>
              <div class="mode-intro-points">{{pointHtml}}</div>
            </div>
            <button class="mode-intro-button" type="button" onclick="dismissModeIntro('{{NewTabMarkup.JsString(mode)}}')">Compris</button>
          </section>
        """;
    }

    private static string NewTabModeIntroText(string mode) =>
        mode switch
        {
            "neutral" => "Lumora se met en retrait : heure, recherche et navigation essentielle, sans compagnon affiché.",
            "balanced" => "Lumora garde les repères quotidiens visibles sans imposer un contexte spécialisé.",
            "focus" => "Lumora réduit le bruit, rapproche les commandes rapides et garde une seule priorité visible.",
            "reading" => "Lumora adoucit l'accueil, rapproche lecture, annotations et voix locale.",
            "creative" => "Lumora ouvre un espace d'idées : post-it local, notes et relance créative.",
            "research" => "Lumora privilégie la collecte locale : sources, historique, favoris et comparaison.",
            "night" => "Lumora baisse la présence visuelle et garde les actions utiles pour naviguer tard.",
            _ => "Lumora reste polyvalent : repères personnels, recherche et navigation quotidienne."
        };

    private static string[] NewTabModeIntroPoints(string mode) =>
        mode switch
        {
            "neutral" => ["Heure locale", "Recherche", "Outils essentiels"],
            "balanced" => ["Favoris", "Historique local", "Modules"],
            "focus" => ["Accueil compact", "Objectif immédiat", "Commandes rapides"],
            "reading" => ["Lecture calme", "Annotations", "Voix locale"],
            "creative" => ["Post-it local", "Notes proches", "Idées à relancer"],
            "research" => ["Sources", "Historique local", "Comparaison"],
            "night" => ["Moins d'éclat", "Lecture douce", "Actions minimales"],
            _ => ["Navigation normale", "Repères personnels", "Confort"]
        };

    private string NewTabModeWorkbenchHtml()
    {
        var mode = NewTabUsageMode();
        if (mode == "neutral")
        {
            return string.Empty;
        }

        var context = ResolveNewTabModeContext(mode);
        var actionsHtml = new StringBuilder();

        actionsHtml.AppendLine($"""
              <button class="mode-context-button primary" type="button" onclick="saveModeQuickNote('{NewTabMarkup.JsString(mode)}','{NewTabMarkup.JsString(context.NoteTitle)}')">{NewTabMarkup.HtmlText(context.SaveLabel)}</button>
        """);

        foreach (var (action, label) in context.Actions)
        {
            actionsHtml.AppendLine($"""
              <button class="mode-context-button" type="button" onclick="modeAction('{NewTabMarkup.JsString(action)}')">{NewTabMarkup.HtmlText(label)}</button>
            """);
        }

        return $$"""
          <section class="mode-context" aria-label="{{NewTabMarkup.HtmlAttribute(context.AriaLabel)}}">
            <div class="mode-context-copy">
              <div class="mode-context-eyebrow">{{NewTabMarkup.HtmlText(context.Eyebrow)}}</div>
              <div class="mode-context-title">{{NewTabMarkup.HtmlText(context.Title)}}</div>
              <div class="mode-context-text">{{NewTabMarkup.HtmlText(context.Text)}}</div>
              <textarea id="modeDraft" class="mode-draft" aria-label="{{NewTabMarkup.HtmlAttribute(context.DraftLabel)}}" placeholder="{{NewTabMarkup.HtmlAttribute(context.Placeholder)}}">{{NewTabMarkup.HtmlText(CompanionMemory(mode))}}</textarea>
              <div id="modeNoteStatus" class="mode-note-status" aria-live="polite"></div>
            </div>
            <div class="mode-context-actions">
              {{actionsHtml}}            </div>
          </section>
        """;
    }

    private static NewTabModeContext ResolveNewTabModeContext(string mode) =>
        mode switch
        {
            "focus" => new NewTabModeContext(
                "Priorité locale",
                "Objectif de maintenant",
                "Notez la seule chose à terminer. La note reste dans le profil Lumora.",
                "Ex. Finir la page de réglages, vérifier un bug, répondre à un message important...",
                "Enregistrer l'objectif",
                "Objectif Focus",
                "Saisir l'objectif du mode Focus",
                "Outil Focus",
                [("command_palette", "Ouvrir Ctrl+K"), ("fullscreen", "Plein écran")]),
            "reading" => new NewTabModeContext(
                "Lecture active",
                "Marque-page de lecture",
                "Gardez une phrase, une idée ou une page à reprendre sans quitter l'accueil.",
                "Ex. À relire : section sécurité locale, vérifier les annotations, reprendre ce passage...",
                "Garder la note",
                "Note de lecture",
                "Saisir une note de lecture",
                "Outil Lecture",
                [("reader", "Mode lecture"), ("read_aloud", "Voix locale")]),
            "creative" => new NewTabModeContext(
                "Post-it local",
                "Capture d'idée",
                "Un vrai post-it de création : une idée maintenant, enregistrée dans les Notes Lumora.",
                "Ex. Ajouter une animation au changement de mode, tester un panneau plus vivant...",
                "Coller le post-it",
                "Post-it Creation",
                "Saisir une idée créative",
                "Outil Creation",
                [("notes", "Ouvrir Notes"), ("search_assist", "Relancer l'idée"), ("add_shortcut", "Ajouter un repère")]),
            "research" => new NewTabModeContext(
                "Collecte locale",
                "Source ou piste à vérifier",
                "Notez une source, une hypothèse ou une comparaison. Lumora garde la trace localement.",
                "Ex. Comparer WebView2/CEF, vérifier une doc, garder une URL ou une question...",
                "Ajouter la piste",
                "Piste de recherche",
                "Saisir une piste de recherche",
                "Outil Recherche",
                [("history", "Historique local"), ("bookmarks", "Sources gardées"), ("command_palette", "Comparer vite")]),
            "night" => new NewTabModeContext(
                "Navigation calme",
                "Rappel pour plus tard",
                "Posez une note courte pour demain et gardez l'interface légère maintenant.",
                "Ex. Reprendre cette recherche demain, fermer les onglets inutiles, lire hors écran...",
                "Garder pour demain",
                "Rappel Nuit",
                "Saisir un rappel du mode Nuit",
                "Outil Nuit",
                [("reader", "Lecture douce"), ("read_aloud", "Écoute locale"), ("fullscreen", "Réduire l'éclat")]),
            _ => new NewTabModeContext(
                "Repère rapide",
                "Note de navigation",
                "Gardez une note simple sans transformer l'accueil en tableau de bord.",
                "Ex. Page à consulter, idée rapide, rappel lié à la navigation...",
                "Enregistrer la note",
                "Note rapide Lumora",
                "Saisir une note rapide",
                "Outil Equilibre",
                [("personalize", "Mon Lumora"), ("modules", "Modules")])
        };

    private bool ShouldShowNewTabPersonalizationInvite() =>
        NewTabUsageMode() != "neutral" &&
        !_uiSettings.PinnedModuleIds.Any() &&
        !_uiSettings.NewTabShortcutsVisible &&
        _uiSettings.NewTabShortcuts.Count == 0;

    private string NewTabPersonalizationInviteHtml()
    {
        if (!ShouldShowNewTabPersonalizationInvite())
        {
            return string.Empty;
        }

        return """
          <section class="personalize-invite" aria-label="Personnalisation Lumora">
            <div>
              <div class="invite-eyebrow">Profil épuré</div>
              <div class="invite-title">Construisez votre Lumora</div>
              <div class="invite-text">Aucun module ni raccourci n'est imposé. Choisissez votre mode, vos outils visibles et l'accueil qui vous met le plus à l'aise.</div>
            </div>
            <div class="invite-actions">
              <button class="invite-button primary" type="button" onclick="personalizeLumora()">Personnaliser</button>
              <button class="invite-button" type="button" onclick="openLumoraModules()">Modules</button>
            </div>
          </section>
        """;
    }

    private static string NewTabResponsiveCss() =>
        "@media(max-width:860px){main{width:min(760px,100%)}.home-grid,.balanced-home,.balanced-dock{grid-template-columns:1fr;gap:20px}.home-primary{padding-top:0}.mode-side{width:100%;justify-self:stretch}.brand{align-items:center}.mark,.balanced-brand{justify-content:center}.balanced-lead{text-align:center}.accent-line{align-self:center}.shortcuts{justify-content:center}.mode-panel,.mode-intro,.mode-context,.personalize-invite{grid-template-columns:1fr;grid-template-areas:none}.mode-copy,.mode-actions,.mode-visual{grid-area:auto}.mode-visual{width:100%;height:34px;justify-content:flex-start}.mode-actions,.invite-actions{justify-content:flex-start}.mode-context-actions{flex-direction:row;flex-wrap:wrap}.mode-context-button{flex:1 1 150px}.mode-workbench{grid-template-columns:1fr}.mode-tool{min-height:64px}.balanced-mode-panel,.balanced-wide{grid-column:auto}}";

    private string NewTabTransitionCss() =>
        _uiSettings.AccessibilityReduceMotion
            ? ".search,.mode-panel,.mode-intro,.mode-context,.mode-context-button,.balanced-action,.personalize-invite,.invite-button,.shortcut-card,.shortcut-dot,.shortcut-actions{transition:none}"
            : ".search,.mode-panel,.mode-chip,.mode-intro,.mode-context,.mode-context-button,.balanced-action,.personalize-invite,.invite-button,.shortcut-card,.shortcut-dot,.shortcut-actions{transition:background .12s ease,border-color .12s ease,box-shadow .12s ease,color .12s ease,opacity .12s ease,transform .12s ease}";

    private string NewTabMotionCss()
    {
        if (NewTabMotionClass() == "motion-static")
        {
            return """
            .motion-static::after{display:none}
            .motion-static .brand,.motion-static .balanced-brand,.motion-static .neutral-brand,.motion-static .neutral-clock,.motion-static .search,.motion-static .shortcut-card,.motion-static .logo-icon,.motion-static .balanced-logo,.motion-static .neutral-logo,.motion-static .accent-line::after,.motion-static .mode-visual span,.motion-static .mode-panel::after,.motion-static .mode-intro,.motion-static .mode-context{animation:none}
            """;
        }

        var palette = NewTabPalette();
        return """
        @keyframes lumoraRise{from{opacity:0;transform:translateY(12px)}to{opacity:1;transform:translateY(0)}}
        @keyframes lumoraShortcutIn{from{opacity:0;transform:translateY(10px) scale(.98)}to{opacity:1;transform:translateY(0) scale(1)}}
        @keyframes lumoraTrace{from{transform:translate3d(-8%,0,0) rotate(0.001deg)}to{transform:translate3d(8%,0,0) rotate(0.001deg)}}
        @keyframes lumoraBreathe{0%,100%{filter:drop-shadow(0 18px 30px rgba(0,0,0,.32)) drop-shadow(0 0 18px {Accent}22);transform:translateY(0)}50%{filter:drop-shadow(0 22px 34px rgba(0,0,0,.36)) drop-shadow(0 0 30px {Accent}42);transform:translateY(-2px)}}
        @keyframes lumoraSweep{0%,36%{transform:translateX(-110%)}68%,100%{transform:translateX(110%)}}
        @keyframes lumoraDynamicLift{0%{opacity:0;transform:translateY(18px) scale(.985)}100%{opacity:1;transform:translateY(0) scale(1)}}
        @keyframes lumoraFocusBeat{0%,100%{opacity:.48}48%,58%{opacity:1}}
        @keyframes lumoraReadFlow{0%,100%{transform:translateX(0);opacity:.62}50%{transform:translateX(5px);opacity:1}}
        @keyframes lumoraCreativeSpark{0%,100%{transform:translateY(0) scaleY(1)}45%{transform:translateY(-3px) scaleY(1.16)}}
        @keyframes lumoraResearchScan{0%{transform:translateX(-70%);opacity:.14}45%,55%{opacity:.42}100%{transform:translateX(70%);opacity:.14}}
        @keyframes lumoraNightDrift{0%,100%{opacity:.42}50%{opacity:.8}}
        .motion-subtle .brand,.motion-subtle .balanced-brand,.motion-subtle .neutral-brand{animation:lumoraRise .34s ease-out both}
        .motion-subtle .neutral-clock{animation:lumoraRise .36s .03s ease-out both}
        .motion-subtle .search{animation:lumoraRise .38s .05s ease-out both}
        .motion-subtle .shortcut-card{animation:lumoraShortcutIn .28s ease-out both;animation-delay:calc(var(--d,0ms) + 90ms)}
        .motion-luminous::after{animation:lumoraTrace 18s ease-in-out infinite alternate}
        .motion-luminous .brand,.motion-luminous .balanced-brand,.motion-luminous .neutral-brand{animation:lumoraRise .44s cubic-bezier(.2,.8,.2,1) both}
        .motion-luminous .neutral-clock{animation:lumoraRise .46s .05s cubic-bezier(.2,.8,.2,1) both}
        .motion-luminous .search{animation:lumoraRise .46s .08s cubic-bezier(.2,.8,.2,1) both}
        .motion-luminous .shortcut-card{animation:lumoraShortcutIn .34s cubic-bezier(.2,.8,.2,1) both;animation-delay:calc(var(--d,0ms) + 130ms)}
        .motion-luminous .logo-icon,.motion-luminous .balanced-logo{animation:lumoraBreathe 5.8s ease-in-out infinite}
        .motion-luminous .accent-line::after{animation:lumoraSweep 3.8s ease-in-out infinite}
        .motion-dynamic::after{animation:lumoraTrace 10s ease-in-out infinite alternate;opacity:{DynamicTraceOpacity}}
        .motion-dynamic .brand,.motion-dynamic .balanced-brand{animation:lumoraDynamicLift .5s cubic-bezier(.16,1,.3,1) both}
        .motion-dynamic .neutral-brand,.motion-dynamic .neutral-clock{animation:lumoraRise .42s ease-out both}
        .motion-dynamic .search{animation:lumoraDynamicLift .54s .08s cubic-bezier(.16,1,.3,1) both}
        .motion-dynamic .shortcut-card{animation:lumoraShortcutIn .38s cubic-bezier(.16,1,.3,1) both;animation-delay:calc(var(--d,0ms) + 150ms)}
        .motion-dynamic .logo-icon,.motion-dynamic .balanced-logo{animation:lumoraBreathe 3.9s ease-in-out infinite}
        .motion-dynamic .accent-line::after{animation:lumoraSweep 2.6s ease-in-out infinite}
        .motion-dynamic .shortcut-card:hover{transform:translateY(-2px)}
        .mode-focus.motion-subtle .mode-visual span,.mode-focus.motion-luminous .mode-visual span,.mode-focus.motion-dynamic .mode-visual span{animation:lumoraFocusBeat 1.8s steps(2,end) infinite}
        .mode-focus .mode-visual span:nth-child(2){animation-delay:.16s}
        .mode-focus .mode-visual span:nth-child(3){animation-delay:.32s}
        .mode-reading.motion-subtle .mode-visual span,.mode-reading.motion-luminous .mode-visual span,.mode-reading.motion-dynamic .mode-visual span{animation:lumoraReadFlow 5.8s ease-in-out infinite}
        .mode-reading .mode-visual span:nth-child(2){animation-delay:.35s}
        .mode-reading .mode-visual span:nth-child(3){animation-delay:.7s}
        .mode-creative.motion-luminous .mode-visual span,.mode-creative.motion-dynamic .mode-visual span{animation:lumoraCreativeSpark 2.1s ease-in-out infinite}
        .mode-creative .mode-visual span:nth-child(2){animation-delay:.18s}
        .mode-creative .mode-visual span:nth-child(4){animation-delay:.34s}
        .mode-research.motion-luminous .mode-panel::after,.mode-research.motion-dynamic .mode-panel::after{animation:lumoraResearchScan 4.2s ease-in-out infinite}
        .mode-night.motion-subtle .mode-visual span,.mode-night.motion-luminous .mode-visual span,.mode-night.motion-dynamic .mode-visual span{animation:lumoraNightDrift 6.4s ease-in-out infinite}
        """
        .Replace("{Accent}", palette.Accent, StringComparison.Ordinal)
        .Replace("{DynamicTraceOpacity}", NewTabDynamicTraceOpacityCss(), StringComparison.Ordinal);
    }

    private (string Accent, string Accent2, string Ink, string Surface, string Text, string LogoText) NewTabPalette()
    {
        if (_uiSettings.AccessibilityHighContrast)
        {
            return ("#ffd500", "#00ffe2", "#000000", "#ffffff", "#ffffff", "#ffffff");
        }

        return (_uiSettings.AccentPalette ?? "lumora").ToLowerInvariant() switch
        {
            "ocean" => ("#5cbcff", "#89e2d6", "#14242b", "#f2fbff", "#f3fbff", "#f3fbff"),
            "forest" => ("#80cc7c", "#56d0c2", "#1a241c", "#f4fbef", "#f5faef", "#f5faef"),
            "ember" => ("#eb7e4a", "#f6ce68", "#29201c", "#fff6ee", "#fff5eb", "#fff5eb"),
            _ => ("#ffb935", "#43dbd1", "#0d1822", "#f8fbf8", "#fff8ea", "#fff8ea")
        };
    }

    private string NewTabPageBackgroundCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "#000";
        var palette = NewTabPalette();
        return palette.Ink;
    }

    private string NewTabTextColorCss() => NewTabPalette().Text;

    private string NewTabLogoTextColorCss() => NewTabPalette().LogoText;

    private string NewTabModeSurfaceCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "#000";
        return NewTabUsageMode() switch
        {
            "neutral" => "rgba(255,255,255,.035)",
            "focus" => "rgba(255,255,255,.055)",
            "reading" => "rgba(255,248,234,.065)",
            "creative" => "rgba(255,255,255,.06)",
            "research" => "rgba(67,219,209,.07)",
            "night" => "rgba(8,12,22,.72)",
            _ => "rgba(255,255,255,.052)"
        };
    }

    private string NewTabModeSignatureBarCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "#ffd500";
        var palette = NewTabPalette();
        return NewTabUsageMode() switch
        {
            "neutral" => $"linear-gradient(180deg,{palette.Accent2},{palette.Accent})",
            "focus" => $"linear-gradient(180deg,{palette.Accent},#ffffff)",
            "reading" => "linear-gradient(180deg,#f8d49b,#fff5de)",
            "creative" => $"linear-gradient(180deg,{palette.Accent2},{palette.Accent},#ff7f35)",
            "research" => $"repeating-linear-gradient(180deg,{palette.Accent2} 0 8px,transparent 8px 13px)",
            "night" => "linear-gradient(180deg,#a7c3ff,#536aa3)",
            _ => $"linear-gradient(180deg,{palette.Accent},{palette.Accent2})"
        };
    }

    private string NewTabModePanelPatternCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "none";
        var palette = NewTabPalette();
        return NewTabUsageMode() switch
        {
            "neutral" => "linear-gradient(90deg,rgba(255,255,255,.035),transparent 38%)",
            "focus" => "linear-gradient(90deg,rgba(255,255,255,.08),transparent 44%)",
            "reading" => "repeating-linear-gradient(180deg,transparent 0 18px,rgba(255,248,234,.055) 18px 19px)",
            "creative" => $"linear-gradient(135deg,{palette.Accent2}18 0 20%,transparent 20% 42%,{palette.Accent}12 42% 58%,transparent 58%),linear-gradient(90deg,transparent,{palette.Accent}10)",
            "research" => $"linear-gradient(90deg,{palette.Accent2}16 1px,transparent 1px),linear-gradient(180deg,{palette.Accent2}10 1px,transparent 1px)",
            "night" => "linear-gradient(180deg,rgba(160,190,255,.08),transparent 48%)",
            _ => "linear-gradient(90deg,rgba(255,255,255,.05),transparent 42%)"
        };
    }

    private string NewTabModeVisualCss()
    {
        var palette = NewTabPalette();
        return NewTabUsageMode() switch
        {
            "neutral" => palette.Accent,
            "reading" => "#f8d49b",
            "night" => "#a7c3ff",
            _ => palette.Accent2
        };
    }

    private string NewTabModeVisualGlowCss() => NewTabModeVisualCss() + "55";

    private string NewTabPersonalizationInviteSurfaceCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "#000";
        var palette = NewTabPalette();
        return $"linear-gradient(135deg,rgba(255,255,255,.06),{palette.Accent2}14)";
    }

    private string NewTabModeBorderCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "#fff";
        var palette = NewTabPalette();
        return NewTabUsageMode() switch
        {
            "neutral" => "rgba(255,255,255,.10)",
            "focus" => palette.Accent + "44",
            "creative" => palette.Accent2 + "48",
            "research" => palette.Accent2 + "58",
            "night" => "rgba(160,190,255,.24)",
            _ => "rgba(255,255,255,.14)"
        };
    }

    private string NewTabModeMutedCss() =>
        _uiSettings.AccessibilityHighContrast ? "#fff" : "rgba(255,248,234,.72)";

    private string NewTabBodyPaddingCss() =>
        NewTabUsageMode() == "neutral" ? "0 32px" : (NewTabStyleClass() == "minimal" ? "14vh 32px 32px" : "11vh 32px 32px");

    private string NewTabBackdropCss()
    {
        if (_uiSettings.AccessibilityHighContrast) return "none";
        var palette = NewTabPalette();
        var mode = NewTabUsageMode();
        if (mode == "neutral")
        {
            return $"linear-gradient(180deg,{palette.Ink} 0%,#0b1116 100%)";
        }
        if (mode == "focus")
        {
            return $"linear-gradient(90deg,{palette.Accent}18,transparent 30%),linear-gradient(180deg,{palette.Ink} 0%,#0b1118 100%)";
        }
        if (mode == "reading")
        {
            return $"repeating-linear-gradient(180deg,transparent 0 46px,{palette.Accent}0f 46px 47px),linear-gradient(180deg,#19201d 0%,#171715 100%)";
        }
        if (mode == "creative")
        {
            return $"linear-gradient(135deg,{palette.Accent2}16 0 16%,transparent 16% 42%,{palette.Accent}14 42% 60%,transparent 60%),linear-gradient(180deg,{palette.Ink} 0%,#11131c 100%)";
        }
        if (mode == "research")
        {
            return $"linear-gradient(90deg,{palette.Accent2}10 1px,transparent 1px),linear-gradient(180deg,{palette.Accent2}0d 1px,transparent 1px),linear-gradient(180deg,{palette.Ink} 0%,#0c1920 100%)";
        }
        if (mode == "night")
        {
            return "linear-gradient(90deg,rgba(160,190,255,.12),transparent 32%),linear-gradient(180deg,#080d16 0%,#04070c 100%)";
        }

        return NewTabStyleClass() switch
        {
            "calm" => $"linear-gradient(180deg,{palette.Ink} 0%,#171a1a 100%)",
            "minimal" => $"linear-gradient(180deg,{palette.Ink} 0%,#101820 100%)",
            _ => $"radial-gradient(circle at 48% 26%,{palette.Accent}33,transparent 23%),radial-gradient(circle at 72% 18%,{palette.Accent2}24,transparent 26%),radial-gradient(circle at 18% 72%,#ff7f3520,transparent 28%),linear-gradient(180deg,{palette.Ink} 0%,#101820 100%)"
        };
    }

    private string NewTabLightTraceCss()
    {
        if (_uiSettings.AccessibilityHighContrast || _uiSettings.AccessibilityReduceMotion)
        {
            return "none";
        }

        var palette = NewTabPalette();
        if (NewTabUsageMode() == "neutral")
        {
            return "none";
        }

        return $"linear-gradient(112deg,transparent 0 38%,{palette.Accent}18 46%,{palette.Accent2}24 52%,transparent 63%)";
    }

    private string NewTabLightTraceOpacityCss() =>
        NewTabMotionClass() switch
        {
            "motion-subtle" => ".18",
            "motion-dynamic" => ".42",
            "motion-static" => "0",
            _ => ".28"
        };

    private string NewTabDynamicTraceOpacityCss() => ".46";

    private string NewTabAccentLineCss()
    {
        var palette = NewTabPalette();
        return $"linear-gradient(90deg,{palette.Accent},{palette.Accent2})";
    }

    private string NewTabAccentGlowCss() => NewTabPalette().Accent + "55";

    private string NewTabFocusBorderCss() => NewTabPalette().Accent;

    private string NewTabFocusRingCss() => NewTabPalette().Accent + "33";

    private string NewTabShortcutBorderCss() => NewTabPalette().Accent + "42";

    private string NewTabAccentSolidCss() => NewTabPalette().Accent;

    private string NewTabAccent2SolidCss() => NewTabPalette().Accent2;

    private string NewTabShortcutsHtml()
    {
        var shortcuts = _uiSettings.NewTabShortcuts
            .Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.Title) && !string.IsNullOrWhiteSpace(shortcut.Url))
            .Take(12)
            .ToList();

        // "Afficher les raccourcis" ne masque que la LISTE des raccourcis deja
        // enregistres. Le bouton "+" reste toujours disponible tant qu'il reste de
        // la place (<12) : un utilisateur ne doit jamais se retrouver bloque sans
        // aucun moyen d'ajouter un raccourci depuis la page (ni parce que le reglage
        // est desactive, ni parce que sa liste est actuellement vide).
        var showExisting = _uiSettings.NewTabShortcutsVisible && shortcuts.Count > 0;
        var canAddMore = shortcuts.Count < 12;
        if (!showExisting && !canAddMore)
            return string.Empty;

        var html = new StringBuilder();
        html.AppendLine("""<div class="shortcuts" aria-label="Raccourcis Lumora">""");
        if (showExisting)
        {
            for (var i = 0; i < shortcuts.Count; i++)
            {
                var shortcut = shortcuts[i];
                var title = NewTabMarkup.HtmlText(shortcut.Title);
                var url = NewTabMarkup.HtmlAttribute(NewTabMarkup.NormalizeShortcutUrl(shortcut.Url));
                var initial = NewTabMarkup.HtmlText(NewTabMarkup.ShortcutInitial(shortcut.Title));
                var jsTitle = NewTabMarkup.JsString(shortcut.Title);
                var jsUrl = NewTabMarkup.JsString(NewTabMarkup.NormalizeShortcutUrl(shortcut.Url));
                html.AppendLine($"""
                <div class="shortcut-card" style="--d:{i * 42}ms">
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
        }
        if (canAddMore)
        {
            html.AppendLine($"""
            <button class="shortcut-card add-shortcut" type="button" onclick="addShortcut()" title="Ajouter un raccourci" style="--d:{shortcuts.Count * 42}ms">
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

    private async void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        RecordLoginDiagnosticForNavigation(sender.Source, "new-window-requested", args.Uri, args.IsUserInitiated ? "user-initiated" : "not-user-initiated");

        var parentTab = TabForCore(sender);

        // Popups indésirables (popunder automatique, clic détourné vers un domaine
        // publicitaire, rafale sur un même geste) : bloquées AVANT toute création
        // d'onglet. Politique pure dans PopupPolicy ; les fenêtres
        // d'authentification passent toujours.
        var popupVerdict = DecidePopupVerdict(args.Uri, sender.Source, args.IsUserInitiated, parentTab?.Id);
        if (popupVerdict != PopupVerdict.Allow)
        {
            ReportBlockedPopup(popupVerdict, args.Uri, sender.Source);
            args.Handled = true;
            return;
        }

        // Les flux OAuth (Google, Microsoft, etc.) utilisent souvent window.open puis
        // window.opener/postMessage pour rendre la session au site d'origine. Il faut
        // donc fournir un vrai CoreWebView2 a WebView2 au lieu de naviguer nous-mêmes
        // vers l'URL dans un onglet standard.
        args.Handled = true;
        var deferral = args.GetDeferral();
        var uri = string.IsNullOrWhiteSpace(args.Uri) ? "about:blank" : args.Uri;
        try
        {
            var isFederatedIdentity = IsFederatedIdentityIntermediary(uri);
            var popupTab = AddTab(PopupTabTitle(uri), uri, select: !isFederatedIdentity, createViewWhenSelected: false);
            if (parentTab is not null)
            {
                _popupParentTabIds[popupTab.Id] = parentTab.Id;
                _navHealth.RegisterPopupOpened(parentTab.Id, DateTimeOffset.Now);
            }
            if (isFederatedIdentity)
            {
                _federatedIdentityPopupTabIds.Add(popupTab.Id);
            }

            var popupView = await EnsureTabViewReadyAsync(popupTab, setPendingAddress: false);
            if (popupView?.CoreWebView2 is not null)
            {
                args.NewWindow = popupView.CoreWebView2;
                if (!isFederatedIdentity)
                {
                    _browserView = popupView;
                }
                StatusText.Text = isFederatedIdentity
                    ? "Connexion Google en cours dans une fenetre Lumora rattachee."
                    : "Fenetre de connexion ouverte dans un onglet Lumora.";
            }
            else
            {
                StatusText.Text = "Fenetre de connexion impossible a ouvrir.";
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static string PopupTabTitle(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return "Nouvel onglet";
        }

        var host = parsed.Host;
        return host.Contains("accounts.google", StringComparison.OrdinalIgnoreCase) ||
               host.Contains("oauth", StringComparison.OrdinalIgnoreCase) ||
               host.Contains("login", StringComparison.OrdinalIgnoreCase)
            ? "Connexion"
            : "Nouvel onglet";
    }

    private void ReturnToPopupParentIfVisible(BrowserTabState popupTab)
    {
        if (!_popupParentTabIds.TryGetValue(popupTab.Id, out var parentId) ||
            CurrentTab()?.Id != popupTab.Id)
        {
            return;
        }

        var parentTab = _tabs.FirstOrDefault(tab => tab.Id == parentId);
        if (parentTab is not null)
        {
            DispatcherQueue.TryEnqueue(() => ActivateTab(parentTab));
        }
    }

    private void HideCredentialAutomationBars()
    {
        AutoFillBar.Visibility = Visibility.Collapsed;
        SuggestPasswordBar.Visibility = Visibility.Collapsed;
        _pendingAutoFillCandidates = Array.Empty<VaultCredential>();
        _pendingGeneratedPassword = null;
    }

    private static bool IsFederatedIdentityIntermediary(string? uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        var host = parsed.Host;
        var path = parsed.AbsolutePath;
        if (!host.Equals("accounts.google.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.StartsWith("/gsi/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/o/oauth2/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/signin/oauth", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuthenticationCriticalRequest(string requestUri, string pageUri)
    {
        if (IsFederatedIdentityIntermediary(requestUri))
        {
            return true;
        }

        if (!IsFederatedIdentityIntermediary(pageUri) ||
            !Uri.TryCreate(requestUri, UriKind.Absolute, out var request))
        {
            return false;
        }

        var host = request.Host;
        return host.Equals("accounts.google.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("ssl.gstatic.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("www.gstatic.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".gstatic.com", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsLoginCompatibilitySite(string? uriOrHost)
    {
        if (string.IsNullOrWhiteSpace(uriOrHost))
        {
            return false;
        }

        var root = uriOrHost.Contains("://", StringComparison.Ordinal)
            ? PublicSuffixService.RootDomainOf(uriOrHost)
            : PublicSuffixService.RootDomainOf("https://" + uriOrHost);
        return !string.IsNullOrWhiteSpace(root) &&
               _uiSettings.LoginCompatibilitySites.Contains(root, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsLoginCompatibilityAuthenticationRequest(string requestUri, string pageUri)
    {
        if (!IsLoginCompatibilitySite(pageUri) &&
            !IsFederatedIdentityIntermediary(pageUri))
        {
            return false;
        }

        if (!Uri.TryCreate(requestUri, UriKind.Absolute, out var request))
        {
            return false;
        }

        var host = request.Host;
        if (host.Equals("accounts.google.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("apis.google.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("ssl.gstatic.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("www.gstatic.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("accounts.gstatic.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".gstatic.com", StringComparison.OrdinalIgnoreCase))
        {
            return !host.Contains("analytics", StringComparison.OrdinalIgnoreCase) &&
                   !host.Contains("ads", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private bool IsLoginCompatibilitySameSiteSessionRequest(string requestUri, string pageUri)
    {
        if (!IsLoginCompatibilitySite(pageUri) ||
            !Uri.TryCreate(requestUri, UriKind.Absolute, out var request))
        {
            return false;
        }

        var pageRoot = PublicSuffixService.RootDomainOf(pageUri);
        var requestRoot = PublicSuffixService.RootDomainOf(request.Host);
        if (string.IsNullOrWhiteSpace(pageRoot) ||
            !pageRoot.Equals(requestRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = request.AbsolutePath.ToLowerInvariant();
        return path.Contains("login", StringComparison.Ordinal) ||
               path.Contains("signin", StringComparison.Ordinal) ||
               path.Contains("oauth", StringComparison.Ordinal) ||
               path.Contains("auth", StringComparison.Ordinal) ||
               path.Contains("session", StringComparison.Ordinal) ||
               path.Contains("account", StringComparison.Ordinal) ||
               path.Contains("modal", StringComparison.Ordinal);
    }

    private void BrowserCore_WindowCloseRequested(object? sender, object e)
    {
        if (sender is CoreWebView2 core && TabForCore(core) is { } tab)
        {
            CloseTab(tab);
        }
    }

    private async Task RegisterPasskeyMonitorAsync(CoreWebView2 core)
    {
        try
        {
            await core.AddScriptToExecuteOnDocumentCreatedAsync("""
                (function(){
                    if(window.__nova_passkey_monitor)return;
                    window.__nova_passkey_monitor=true;

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

    // Signal redondant pour la sortie du plein ecran contenu : l'evenement natif
    // ContainsFullScreenElementChanged de WebView2 s'est revele peu fiable en
    // pratique pour restaurer la fenetre Lumora (rapporte par l'utilisateur en
    // 0.60.6-dev). Ce listener DOM ecoute directement fullscreenchange et
    // previent le C# par WebMessage, independamment de l'evenement WinRT.
    private async Task RegisterFullScreenExitMonitorAsync(CoreWebView2 core)
    {
        try
        {
            await core.AddScriptToExecuteOnDocumentCreatedAsync("""
                (function(){
                    if(window.__nova_fullscreen_monitor)return;
                    window.__nova_fullscreen_monitor=true;
                    document.addEventListener('fullscreenchange', function(){
                        if(!document.fullscreenElement){
                            try{window.chrome.webview.postMessage(JSON.stringify({t:'nova.fullscreenExit'}));}catch(_){}
                        }
                    });
                })();
                """);
        }
        catch { }
    }

    private sealed record NewTabModeContext(
        string Eyebrow,
        string Title,
        string Text,
        string Placeholder,
        string SaveLabel,
        string NoteTitle,
        string DraftLabel,
        string AriaLabel,
        IReadOnlyList<(string Action, string Label)> Actions);
}
