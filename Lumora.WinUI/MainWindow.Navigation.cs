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
        AddNewBlankTab();

    // Partagé par le bouton "+" (BrowserTabs_AddTabButtonClick) et le menu
    // "Nouvel onglet" (NewTabMenu_Click, MainWindow.xaml.cs) : un seul point
    // de decision pour ne pas dupliquer la logique de positionnement.
    //
    // En mode vertical, le nouvel onglet atterrit desormais juste APRES
    // l'onglet actif plutot qu'en toute fin de liste (retour utilisateur
    // 2026-08-13 : sur une longue liste verticale, l'onglet fraichement
    // ouvert se retrouvait loin de celui d'ou on venait). Reutilise
    // PlaceTabAfter (MainWindow.TabGroups.cs), deja eprouve pour "Dupliquer
    // l'onglet" - meme mecanisme, pas une nouvelle logique de reordonnancement.
    // Mode horizontal inchange (comportement existant : fin de liste).
    private void AddNewBlankTab()
    {
        var previousActive = CurrentTab();
        var newTab = AddTab("Nouvel onglet", "lumora://accueil", select: true);
        if (_verticalTabsEnabled && previousActive is not null)
        {
            PlaceTabAfter(newTab, previousActive);
        }
    }

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

        HandleTabSelectionModifiers(tab);
        ActivateTab(tab);
    }

    // Rend l'onglet visible SANS recharger sa page : chaque onglet garde son propre
    // WebView2, le changement d'onglet est un simple basculement de visibilité.
    private void ActivateTab(BrowserTabState tab)
    {
        // Un clic simple sur un onglet quitte toujours la vue divisee (modele le
        // plus previsible : le split ne persiste que via son propre menu, jamais
        // implicitement). Voir MainWindow.SplitView.cs.
        ExitSplitView();

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
            // devient l'onglet actif, pas seulement au premier clic. FocusState.Pointer
            // et non Programmatic (2026-08-02) : Programmatic reste au niveau de
            // l'enveloppe XAML sans se propager jusqu'à Chromium (meme lecon que
            // BrowserView_NavigationCompleted/RefocusActiveWebViewIfVisible) - source
            // reelle d'une partie de l'intermittence signalee sur la molette.
            tab.View.Focus(FocusState.Pointer);
            _ = ApplyReadingGuideAsync(tab.View);
        }

        RenderVerticalTabs();
        RefreshHorizontalTabHeaders();
        SaveTabSession();
        UpdateTitleBarDragRegion();
        RefreshPopupRecoveryIndicator();
        RefreshConsentIndicator();
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
        view.PointerEntered += BrowserView_PointerEntered;
        view.NavigationStarting += BrowserView_NavigationStarting;
        view.NavigationCompleted += BrowserView_NavigationCompleted;
        view.CoreWebView2Initialized += BrowserView_CoreWebView2Initialized;
        // Ne fait rien tant qu'aucune vue divisee n'est active (cf. MainWindow.SplitView.cs) :
        // sans danger a abonner systematiquement des la creation de l'onglet.
        view.GotFocus += BrowserView_GotFocus;

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
            // qu'on n'a pas cliqué dans la page). FocusState.Pointer (2026-08-02, meme
            // correctif qu'ActivateTab) : Programmatic ne se propage pas jusqu'a
            // Chromium.
            if (CurrentTab()?.Id == tab.Id)
            {
                view.Focus(FocusState.Pointer);
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

    // rememberInClosedHistory=false pour un détachement vers une nouvelle fenêtre
    // (DetachTabToNewWindow) : l'onglet reste vivant ailleurs, "Onglets récemment
    // fermés" ne doit pas proposer de le rouvrir en double (bug réel trouvé le
    // 2026-08-19).
    private void CloseTab(BrowserTabState state, bool rememberInClosedHistory = true)
    {
        // Fermer un onglet qui fait partie de la vue divisee la fait disparaitre :
        // pas de sens de garder un split a un seul volet, on repasse en vue simple.
        if (IsTabInSplitView(state.Id))
        {
            ExitSplitView();
        }
        _selectedTabIds.Remove(state.Id);
        if (_selectionAnchorTabId == state.Id)
        {
            _selectionAnchorTabId = null;
        }

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

        if (rememberInClosedHistory)
        {
            RememberClosedTab(state);
        }
        var wasActive = CurrentTab()?.Id == state.Id;
        var oldIndex = _tabs.FindIndex(tab => tab.Id == state.Id);
        // Capturé avant CloseTabView : celui-ci oublie l'appartenance popup->parent
        // (_popupParentTabIds.Remove). Une fenêtre de connexion (Google, etc.) qui se
        // ferme elle-même (window.close()) doit rendre la main à l'onglet qui l'a
        // ouverte, pas au voisin le plus proche dans la barre d'onglets.
        var popupParentId = _popupParentTabIds.TryGetValue(state.Id, out var parentId) ? parentId : (int?)null;
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
            var parentTab = popupParentId is int wantedParentId
                ? _tabs.FirstOrDefault(tab => tab.Id == wantedParentId)
                : null;
            var parentItem = parentTab is null
                ? null
                : BrowserTabs.TabItems.OfType<TabViewItem>()
                    .FirstOrDefault(candidate => candidate.Tag is int id && id == parentTab.Id);

            if (parentTab is not null && parentItem is not null)
            {
                BrowserTabs.SelectedItem = parentItem;
                ActivateTab(parentTab);
            }
            else
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
            // Oubliés ici avant le 2026-08-19 : comme AttachedCores() n'énumère que les
            // onglets encore ouverts (_tabs), une entrée orpheline pour cet onglet fermé
            // restait indéfiniment dans ces deux dictionnaires (fuite lente sur une
            // session longue, bug réel trouvé en audit).
            _geolocationSpoofScriptIds.Remove(core);
            _fingerprintProtectionScriptIds.Remove(core);
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

        // Sans ca, WebView2 suit le theme clair/sombre de WINDOWS (via
        // prefers-color-scheme cote page), pas celui de Lumora - un site comme
        // Google s'affichait en sombre alors que Lumora etait passe en clair,
        // des que le theme systeme differait du theme choisi dans Lumora.
        // Signale par l'utilisateur. Applique au premier chargement ici ;
        // ApplyPreferredColorSchemeToOpenTabs() le reapplique a chaud sur les
        // onglets deja ouverts quand le theme change en cours de session.
        ApplyPreferredColorScheme(sender.CoreWebView2);

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

        // Menu contextuel (clic droit) restylise a l'identite Lumora
        // (chantier identite visuelle, 2026-08-10) - voir
        // MainWindow.PageContextMenu.cs.
        sender.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;

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

        StatusText.Text = "Moteur web WinUI initialisé.";
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

    private void ApplyPreferredColorScheme(CoreWebView2 core)
    {
        try
        {
            core.Profile.PreferredColorScheme = LumoraTheme.ResolveIsDarkTheme(_uiSettings)
                ? CoreWebView2PreferredColorScheme.Dark
                : CoreWebView2PreferredColorScheme.Light;
        }
        catch { }
    }

    // Reapplique le theme aux onglets DEJA ouverts quand l'utilisateur change de
    // theme en cours de session (le reglage ci-dessus ne s'appliquait sinon qu'aux
    // moteurs pas encore crees). Change la valeur lue par prefers-color-scheme,
    // pas le contenu deja rendu : une page ouverte peut avoir besoin d'un
    // rechargement pour en tenir compte, comme dans les autres navigateurs.
    private void ApplyPreferredColorSchemeToOpenTabs()
    {
        foreach (var tab in _tabs)
        {
            if (tab.View?.CoreWebView2 is { } core)
            {
                ApplyPreferredColorScheme(core);
            }
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
            // Ne PAS ramener l'onglet parent au premier plan ici : le flux Google est
            // potentiellement encore en cours d'interaction (choix de compte,
            // consentement) sur CET onglet. Le retour au parent se fait via la
            // fermeture de la fenêtre par le site lui-même (BrowserCore_WindowCloseRequested)
            // - voir bug réel 2026-08-19 (Deliveroo, fenêtre masquée avant interaction).
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

        // Nouvelle page pour cet onglet : l'icône de refus de cookies appartenait à
        // la page quittée, pas à celle-ci (attend un nouveau signal du script).
        if (startingTab is not null)
        {
            startingTab.ConsentHandledMethod = null;
        }

        // Compteur par-page, domaine du bouclier et statut : uniquement pour l'onglet visible.
        if (IsActiveView(sender))
        {
            _privacy.ResetPageBlockedCount();
            _telemetryBlocker?.ResetPageBlockedCount();
            UpdateToolbarPrivacyIndicator();
            RefreshPopupRecoveryIndicator();
            RefreshConsentIndicator();
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

        // Filet de securite pour "la molette reste muette" : le focus au
        // survol (BrowserHost_PointerEntered) ne se redeclenche QUE si le
        // pointeur ENTRE dans la zone - si la souris est deja au-dessus de
        // la page au moment ou une navigation se termine (URL tapee puis
        // Entree sans bouger la souris), aucun survol n'a lieu et le focus
        // ne bascule jamais sur le contenu charge. Rattrape ici, uniquement
        // pour l'onglet actif et hors ecran de connexion (ne pas voler le
        // focus a un mot de passe en cours de saisie).
        if (isActive && LoginOverlay.Visibility != Visibility.Visible)
        {
            sender.Focus(FocusState.Pointer);
        }

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
            CaptureSemanticContentAsync(sender, address, title);
            if (isActive)
            {
                OfferAutoFill(address);
                // Complement a l'offre "rester connecte" branchee sur la capture
                // d'identifiants (voir CredentialService_CredentialCaptured) : couvre
                // les connexions dont le POST n'est jamais capture (flux JS
                // multi-etapes, ex. Google) mais dont un champ mot de passe a bien
                // ete vu peu avant. Hors mode invite : aucune offre n'a de sens sans
                // profil a qui l'associer.
                if (!_isGuestMode && sender.CoreWebView2 is { } coreForSessionKeep)
                    _ = MaybeOfferSessionKeepFromRecentLoginAsync(coreForSessionKeep, address);
            }
            _ = ApplySiteComfortAsync(sender, address);
            _ = ApplyReadingGuideAsync(sender);
            _ = ApplyAccessibilityVisionAsync(sender);
            _ = InjectSiteCosmeticAsync(sender.CoreWebView2, address);
            _ = InjectConsentRetryAsync(sender.CoreWebView2);
            if (isActive)
                _ = OfferTranslationIfNeededAsync(sender, address);
        }
        else
        {
            _ = ApplySiteComfortAsync(sender, addressForTab);
            _ = ApplyReadingGuideAsync(sender);
            _ = ApplyAccessibilityVisionAsync(sender);
            if (isActive)
            {
                TranslateBar.Visibility = Visibility.Collapsed;
            }
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
            StatusText.Text = args.IsSuccess ? $"Page chargée: {title}" : $"Navigation échouée: {args.WebErrorStatus}";
            if (args.IsSuccess && BookmarkStore.IsWebUrl(address))
            {
                // Reprise d'activité : signaler les annotations laissées sur cette
                // page, et ouvrir le mode lecture si le panneau Notes l'a demandé.
                var annotationCount = _annotations.CountForPage(address);
                if (annotationCount > 0)
                {
                    StatusText.Text = $"Page chargée: {title} - {annotationCount} annotation(s) à retrouver en mode lecture.";
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
            Title = "Connexion non sécurisée",
            Content = $"{uri.Host} ne prend pas en charge HTTPS. Continuer en HTTP ? La connexion ne sera pas chiffrée.",
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
            _ = ApplySiteComfortAsync(tab.View, address);
            if (IsFederatedIdentityIntermediary(address))
            {
                _federatedIdentityPopupTabIds.Add(tab.Id);
                // Même raison que dans BrowserView_NavigationStarting ci-dessus : ne pas
                // masquer l'onglet tant que le flux Google peut encore demander une
                // interaction.
                HideCredentialAutomationBars();
            }
        }
        else
        {
            _ = ApplySiteComfortAsync(tab.View, address);
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

    // Utilisée uniquement au moment d'installer une application web. Contrairement
    // à CaptureFaviconForTabAsync (optimisée pour la navigation courante, réutilise
    // un cache de 24h et essaie d'abord GetFaviconAsync), celle-ci force une
    // re-capture et inverse l'ordre : le lien <link rel=icon> déclaré par la page
    // + téléchargement direct passent en premier, GetFaviconAsync en dernier
    // recours seulement. Constat réel (Wikipedia, 2026-07-26) : GetFaviconAsync
    // peut renvoyer une icône générique/transitoire de WebView2 (pas encore la
    // vraie icône du site) qui passe la détection de générique (un seul hash
    // exact mémorisé, donc fragile) et bloque alors tout essai des méthodes 2/3
    // via le cache 24h de CaptureFaviconForTabAsync. Pour une action déclarée et
    // unique comme l'installation, la bonne icône compte plus que la vitesse.
    private async Task<bool> CaptureAuthoritativeFaviconForInstallAsync(BrowserTabState tab)
    {
        var browser = tab.View;
        var core = browser?.CoreWebView2;
        if (browser is null || core is null) return false;

        var address = browser.Source?.ToString() ?? tab.Address;
        if (!BookmarkStore.IsWebUrl(address)) return false;

        var originPath = Path.Combine(_profile.FaviconsDir, $"{HashOrigin(address)}.png");
        var saved = await DownloadFaviconFallbackAsync(core, address, originPath);

        if (!saved)
        {
            try
            {
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
                WinUiRuntimeTrace.Write($"GetFaviconAsync (install fallback) skipped: {ex.GetType().Name}");
            }
        }

        if (!saved || !FaviconQuality.IsUsablePngFile(originPath)) return false;

        _faviconCache[address] = originPath;
        _faviconCache[OriginOf(address)] = originPath;
        ApplyFaviconToUi(tab, address, originPath);
        return true;
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
            StatusText.Text = "Aucune page précédente.";
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
        StatusText.Text = "Chargement arrêté.";
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
        tab.Header = TabHeaderContent(state, compact: state.Pinned, active: false);
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
            item.Header = TabHeaderContent(tab, compact: tab.Pinned, active: CurrentTab()?.Id == tab.Id || IsTabInSplitView(tab.Id));
            item.IsClosable = !tab.Pinned;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, tab.Title);
        }
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

    private BrowserTabState? CurrentTab()
    {
        // En vue divisée, BrowserTabs.SelectedItem reste figé sur l'onglet choisi à
        // l'entrée du split (le faire suivre le focus déclencherait ActivateTab ->
        // ExitSplitView à chaque clic dans l'autre volet, voir MainWindow.SplitView.cs).
        // Le volet réellement actif est suivi par _browserView (FocusSplitPane). Sans ce
        // détour, adresse/coffre/favoris/"Installer en application" agissaient tous sur
        // le volet gauche même quand l'utilisateur regardait et cliquait dans le volet
        // droit (bug réel trouvé le 2026-08-19).
        if (_splitView is not null && _browserView is not null)
        {
            var focused = TabForView(_browserView);
            if (focused is not null && IsTabInSplitView(focused.Id))
            {
                return focused;
            }
        }

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
            ReportBlockedPopup(popupVerdict, args.Uri, sender.Source, parentTab?.Id);
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
            // Toujours au premier plan : un flux Google (choix de compte, consentement...)
            // peut demander une vraie interaction, pas seulement un aller-retour silencieux
            // (postMessage). Une fenêtre restée en arrière-plan est une fenêtre que
            // l'utilisateur ne voit jamais - bug réel constaté le 2026-08-19 (Deliveroo).
            var popupTab = AddTab(PopupTabTitle(uri), uri, select: true, createViewWhenSelected: false);
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
                // Suit la sélection ci-dessus : sans ça, la barre d'adresse et les
                // actions de la barre d'outils continueraient de viser l'onglet parent
                // pendant que l'onglet visible est la fenêtre de connexion.
                _browserView = popupView;
                StatusText.Text = isFederatedIdentity
                    ? "Connexion Google ouverte dans un onglet Lumora."
                    : "Fenetre de connexion ouverte dans un onglet Lumora.";
            }
            else
            {
                StatusText.Text = "Fenêtre de connexion impossible à ouvrir.";
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

}
