using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;
using WinRT.Interop;
using Lumora.Privacy;
using Lumora.Privacy.NetworkBlocker;
using Lumora.Privacy.TelemetryBlocker;
using Lumora.Privacy.ParameterCleaner;
using Lumora.Privacy.HttpsEnforcer;
using Lumora.Privacy.CnameUncloaker;
using Lumora.Privacy.CosmeticFilter;
using Lumora.Privacy.ConsentManager;
using Lumora.WinUI.Credentials;

namespace Lumora.WinUI;

// Fenêtre d'application web : un site épinglé, sans onglets ni barre d'adresse,
// partageant le profil (cookies/sessions) de la fenêtre principale. Mêmes
// protections que la fenêtre principale (bloqueur pubs/trackers,
// anti-télémétrie, HTTPS, CNAME, masquage visuel des pubs, refus automatique
// des bannières cookies, blocage des popups/redirections publicitaires) -
// demande explicite du 2026-07-26 : une application installée ne doit rien
// perdre de la confidentialité de Lumora, comme sur Chrome. AdShield
// (PopupPolicy + NavigationHijackPolicy) est porté en version simplifiée :
// pas de comptage de rafale de popups par geste, pas d'escalade "site sous
// pression publicitaire" et pas d'icône de récupération pour les popups
// ambigus (PendingUserChoice traité comme un blocage net) - ces raffinements
// dépendent du suivi multi-onglets (NavigationHealthTracker) de MainWindow,
// pas encore porté ici. La détection des domaines/paterns publicitaires de
// base, elle, est bien active.
public sealed partial class LumoraAppWindow : Window
{
    private readonly LumoraWebApp _app;
    private readonly LumoraProfilePaths _profile;
    private readonly UiSettings _uiSettings;
    private readonly PrivacyEngine _privacy = new();
    private CosmeticFilterModule? _cosmeticFilter;
    private ConsentManagerModule? _consentModule;
    private DownloadHistoryStore? _downloads;
    private WebView2? _view;
    private Microsoft.UI.Windowing.AppWindow? _appWindow;
    private double _titleBarSafeRight = 138;
    private readonly WheelScrollSupport _wheelSupport = new();

    internal LumoraAppWindow(LumoraWebApp app, LumoraProfilePaths profile)
    {
        _app = app;
        _profile = profile;
        _uiSettings = UiSettings.Load(profile.UiSettingsFile, profile.LegacyUiSettingsFile);

        InitializeComponent();
        // Version incluse (comme MainWindow) : sans ca, impossible de voir a
        // l'oeil qu'une fenetre d'application tourne sur une copie perimee
        // d'un lancement de developpement anterieur (constate le 2026-07-26).
        Title = $"{_app.Title} — Lumora {MainWindow.Version}";
        CustomTitleBarText.Text = _app.Title;

        var hwnd = WindowNative.GetWindowHandle(this);
        var winId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(winId);
        LumoraTheme.ApplySecondaryWindowTheme(RootGrid, _appWindow, _uiSettings, LumoraWindowThemeRole.WebApp);
        ApplyIcon();
        ApplyAlwaysOnTop();
        ApplyCustomTitleBar();

        InitPrivacyEngine();

        // Molette : cette fenetre n'a pas la plomberie de MainWindow
        // (HookAutomaticPointerFocus) - sans ca, ni le volet bouclier ni
        // aucun futur Flyout/ScrollViewer de cette fenetre ne defile a la
        // molette (signale par l'utilisateur le 2026-07-26). Rattache au
        // chargement (contenu garanti realise) et de nouveau apres, au cas
        // ou un Flyout ouvert avant le premier Loaded ait rate le rattachement.
        RootGrid.Loaded += (_, _) => _wheelSupport.Attach(RootGrid);
    }

    // ExtendsContentIntoTitleBar : sans ca, Windows dessine sa propre barre de
    // titre native par-dessus nos couleurs, avec un lisere de separation entre
    // elle et le contenu qui ne suit pas notre palette (signale par
    // l'utilisateur le 2026-07-26). CustomTitleBarRow (XAML) la remplace
    // entierement ; toute sa largeur (hors zone des boutons systeme) devient
    // une region de deplacement, recalculee au redimensionnement.
    private void ApplyCustomTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        ApplyTitleBarSafeArea();
        RootGrid.SizeChanged += (_, _) => UpdateTitleBarDragRegion();
    }

    private void ApplyTitleBarSafeArea()
    {
        var rightInset = _appWindow?.TitleBar.RightInset ?? 138;
        _titleBarSafeRight = Math.Max(100, rightInset + 12);
        CustomTitleBarText.Margin = new Thickness(12, 0, _titleBarSafeRight + 130, 0);

        // Bug reel signale le 2026-07-26 : sans cette marge, le panneau
        // d'actions (telechargements + bouclier) restait plaque au bord
        // droit REEL de la fenetre - donc SOUS les boutons systeme
        // (reduire/agrandir/fermer) que Windows dessine par-dessus en tant
        // que calque separe (ExtendsContentIntoTitleBar les superpose, ne
        // les deplace pas). Resultat constate : les deux boutons etaient
        // quasi entierement invisibles, seul un fragment de texte (le
        // compteur du bouclier) depassait encore visible a cote de la
        // croix de fermeture.
        TitleBarActionsPanel.Margin = new Thickness(0, 0, _titleBarSafeRight, 0);
        UpdateTitleBarDragRegion();
    }

    // Le bouton bouclier (ShieldQuickButton) doit rester en dehors de la
    // région de glisser, sinon un clic dessus deplace la fenetre au lieu
    // d'ouvrir le volet - meme logique que la reserve du bouton "+" dans
    // MainWindow.WindowChrome.cs (UpdateTitleBarDragRegion).
    private void UpdateTitleBarDragRegion()
    {
        if (_appWindow?.TitleBar is null || !ExtendsContentIntoTitleBar) return;

        try
        {
            var scale = Content?.XamlRoot?.RasterizationScale ?? 1.0;
            var windowWidth = RootGrid.ActualWidth;
            if (windowWidth <= 0) return;

            var actionsReserve = TitleBarActionsPanel.ActualWidth > 0 ? TitleBarActionsPanel.ActualWidth + 8 : 90;
            var dragWidth = Math.Max(0, windowWidth - _titleBarSafeRight - actionsReserve);
            var rowHeight = CustomTitleBarRow.ActualHeight > 0 ? CustomTitleBarRow.ActualHeight : 36;
            var rect = new RectInt32
            {
                X = 0,
                Y = 0,
                Width = (int)Math.Round(dragWidth * scale),
                Height = (int)Math.Round(rowHeight * scale)
            };
            _appWindow.TitleBar.SetDragRectangles(new[] { rect });
        }
        catch { }
    }

    private void ApplyIcon()
    {
        if (_appWindow is null) return;

        try
        {
            var iconPath = ResolveIconPath();
            if (iconPath is not null)
            {
                _appWindow.SetIcon(iconPath);
            }
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"App window icon apply failed: {ex.GetType().Name}");
        }
    }

    private string? ResolveIconPath()
    {
        if (!string.IsNullOrWhiteSpace(_app.IconFile))
        {
            var custom = Path.Combine(_profile.WebAppIconsDir, Path.GetFileName(_app.IconFile));
            if (FaviconQuality.IsUsablePngBackedIcoFile(custom)) return custom;
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "Assets", "LumoraApp.ico");
        return File.Exists(fallback) ? fallback : null;
    }

    private void ApplyAlwaysOnTop()
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = _app.AlwaysOnTop;
        }
    }

    private void InitPrivacyEngine()
    {
        var telemetry = new TelemetryBlockerModule { IsEnabled = _uiSettings.TelemetryBlockerEnabled };
        telemetry.SetUserWhitelist(_uiSettings.PrivacyWhitelist);
        _privacy.Register(telemetry);

        var network = new NetworkBlockerModule { IsEnabled = _uiSettings.NetworkBlockerEnabled };
        network.SetUserWhitelist(_uiSettings.PrivacyWhitelist);
        _privacy.Register(network);

        _privacy.Register(new ParameterCleanerModule { IsEnabled = _uiSettings.ParameterCleanerEnabled });
        _privacy.Register(new HttpsEnforcerModule    { IsEnabled = _uiSettings.HttpsEnforcerEnabled });
        _privacy.Register(new CnameUncloakerModule(network) { IsEnabled = _uiSettings.CnameUncloakerEnabled });

        _cosmeticFilter = new CosmeticFilterModule { IsEnabled = _uiSettings.CosmeticFilterEnabled };
        _consentModule  = new ConsentManagerModule { IsEnabled = _uiSettings.ConsentManagerEnabled };
        _consentModule.SetLoginCompatibilitySites(_uiSettings.LoginCompatibilitySites);

        _downloads = new DownloadHistoryStore(_profile.DownloadsFile);

        _ = network.LoadAsync();
        _ = _cosmeticFilter.LoadAsync();
    }

    public async void InitializeBrowserSurface()
    {
        var view = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _view = view;
        AppWebViewHost.Children.Add(view);

        try
        {
            await view.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"App window WebView2 creation failed: {ex.GetType().Name}");
            return;
        }

        var core = view.CoreWebView2;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        try { core.Settings.IsReputationCheckingRequired = _uiSettings.SmartScreenEnabled; } catch { }

        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += Core_WebResourceRequested;
        core.NavigationStarting += Core_NavigationStarting;
        core.NavigationCompleted += Core_NavigationCompleted;
        core.NewWindowRequested += Core_NewWindowRequested;
        core.DownloadStarting += Core_DownloadStarting;
        core.SourceChanged += (_, _) => UpdateExternalDomainBar(core.Source);
        core.DocumentTitleChanged += (_, _) => { /* titre de fenêtre volontairement stable (nom de l'app) */ };

        // Enregistrés AVANT la première navigation pour s'appliquer dès la
        // première page (pas seulement à partir de la deuxième) : masquage
        // visuel des pubs + refus automatique des bannières cookies, mêmes
        // scripts que la fenêtre principale.
        if (_cosmeticFilter is { IsEnabled: true })
        {
            try { await core.AddScriptToExecuteOnDocumentCreatedAsync(_cosmeticFilter.BuildGenericInjectionScript()); }
            catch (Exception ex) { WinUiRuntimeTrace.Write($"App window cosmetic script skipped: {ex.GetType().Name}"); }
        }

        if (_consentModule is { IsEnabled: true })
        {
            try { await core.AddScriptToExecuteOnDocumentCreatedAsync(_consentModule.BuildInjectionScript()); }
            catch (Exception ex) { WinUiRuntimeTrace.Write($"App window consent script skipped: {ex.GetType().Name}"); }
        }

        RefreshShieldQuickCount();
        view.Source = new Uri(_app.Url);
    }

    private void Core_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (_privacy.ShouldBlock(args.Request.Uri, sender.Source ?? string.Empty))
        {
            args.Response = sender.Environment.CreateWebResourceResponse(null, 200, "OK", string.Empty);
            DispatcherQueue.TryEnqueue(RefreshShieldQuickCount);
        }
    }

    // Detournement de l'onglet (clic capture vers un domaine publicitaire,
    // redirection automatique type tab-under) puis HTTPS (promotion
    // http->https) et nettoyage de parametres de tracking dans l'URL -
    // simplifie par rapport a MainWindow (pas de repli automatique si la
    // version HTTPS echoue, pas de suivi multi-onglets/rafale de popups) :
    // une seule page a la fois ici, donc rien a quoi rattacher ce suivi.
    // Vidlox/muvonix.shop est justement le cas reel qui a durci
    // NavigationHijackPolicy dans MainWindow (0.84.0.3) - meme raisonnement
    // ici : ce n'etait pas encore porte vers la fenetre d'application.
    private void Core_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        var blocker = _privacy.Get<NetworkBlockerModule>();
        var verdict = NavigationHijackPolicy.Decide(
            sender.Source,
            args.Uri,
            wasExplicitlyRequested: string.Equals(args.Uri, _app.Url, StringComparison.OrdinalIgnoreCase),
            isUserInitiated: args.IsUserInitiated,
            strictBlockEnabled: _uiSettings.StrictAdBlockEnabled && blocker?.IsEnabled == true,
            host => blocker?.IsBlocked(host) == true,
            host => blocker?.IsWhitelisted(host) == true);

        if (verdict != NavigationVerdict.Allow)
        {
            args.Cancel = true;
            var isParasite = verdict == NavigationVerdict.BlockParasite;
            _privacy.RecordManualBlock(
                isParasite ? "parasite-block" : "strict-ad-block",
                isParasite ? "Blocage des redirections parasites" : "Blocage des redirections publicitaires",
                args.Uri,
                sender.Source ?? string.Empty);
            RefreshShieldQuickCount();
            WinUiRuntimeTrace.Write($"App window navigation blocked ({verdict}): {args.Uri}");
            return;
        }

        var cleaned = _privacy.CleanUrl(args.Uri);
        if (cleaned is null || string.Equals(cleaned, args.Uri, StringComparison.OrdinalIgnoreCase)) return;

        args.Cancel = true;
        DispatcherQueue.TryEnqueue(() => sender.Navigate(cleaned));
    }

    // Popups (window.open / target=_blank) : meme politique pure que
    // MainWindow (PopupPolicy), simplifiee (pas de comptage de rafale par
    // geste, pas d'escalade "site sous pression" - une seule page ici, pas de
    // suivi multi-onglets). Sans ce handler, WebView2 ouvrait sa PROPRE
    // fenetre popup par defaut (bare, sans chrome Lumora ni protections) pour
    // TOUTE popup, y compris les pubs automatiques - constate reellement sur
    // Vidlox le 2026-07-26 (redirection vers tureenspappies.cfd).
    private void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        var blocker = _privacy.Get<NetworkBlockerModule>();
        var verdict = PopupPolicy.Decide(
            args.Uri,
            sender.Source,
            args.IsUserInitiated,
            blockerEnabled: blocker?.IsEnabled == true,
            host => blocker?.IsBlocked(host) == true,
            host => blocker?.IsWhitelisted(host) == true);

        if (verdict == PopupVerdict.Allow) return; // repli WebView2 par defaut, inchange

        args.Handled = true;
        _privacy.RecordManualBlock(
            "popup-blocker",
            "Bloqueur de popups",
            string.IsNullOrWhiteSpace(args.Uri) ? "about:blank" : args.Uri,
            sender.Source ?? string.Empty);
        RefreshShieldQuickCount();
        WinUiRuntimeTrace.Write($"App window popup blocked ({verdict}): {args.Uri}");
    }

    private async void Core_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess) return;

        if (_cosmeticFilter is { IsEnabled: true })
        {
            var script = _cosmeticFilter.BuildSiteInjectionScript(sender.Source);
            if (script is not null)
            {
                try { await sender.ExecuteScriptAsync(script); }
                catch (Exception ex) { WinUiRuntimeTrace.Write($"App window site cosmetic skipped: {ex.GetType().Name}"); }
            }
        }

        if (_consentModule is { IsEnabled: true })
        {
            try { await sender.ExecuteScriptAsync(_consentModule.BuildRetryScript()); }
            catch (Exception ex) { WinUiRuntimeTrace.Write($"App window consent retry skipped: {ex.GetType().Name}"); }
        }
    }

    // Meme defaut deja corrige pour MainWindow en 0.84.0.7 (voir
    // MainWindow.DownloadsIndicator.cs) : la boite de dialogue native
    // WebView2/Edge peut s'afficher detachee de la fenetre, y compris sur un
    // autre ecran - constate reellement ici le 2026-07-26 (site Vidlox).
    // Lumora prend le telechargement en charge lui-meme, l'enregistre dans
    // l'historique partage (visible aussi depuis le panneau Telechargements
    // de la fenetre principale) ET affiche desormais un indicateur dans
    // cette fenetre elle-meme (absence signalee comme un vrai manque, pas un
    // detail optionnel, apres un premier passage incomplet en 0.84.2.5-dev).
    private void Core_DownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
    {
        args.Handled = true;

        var entry = new DownloadEntry(args.DownloadOperation);
        entry.OnChanged += () => DispatcherQueue.TryEnqueue(() => _downloads?.Upsert(entry.ToHistoryEntry()));
        _downloads?.Upsert(entry.ToHistoryEntry());

        DownloadsQuickButton.Visibility = Visibility.Visible;
    }

    private void DownloadsQuickFlyout_Opening(object sender, object e)
    {
        DownloadsQuickPanel.Children.Clear();
        var recent = _downloads?.AllEntries().Take(5).ToList() ?? new List<DownloadHistoryEntry>();
        if (recent.Count == 0)
        {
            DownloadsQuickPanel.Children.Add(new TextBlock { Text = "Aucun téléchargement récent.", Opacity = 0.65, FontSize = 12 });
            return;
        }

        foreach (var dl in recent)
        {
            var row = new StackPanel { Spacing = 2 };
            row.Children.Add(new TextBlock { Text = dl.FileName, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            row.Children.Add(new TextBlock { Text = dl.StateLabel, FontSize = 11, Opacity = 0.65 });
            DownloadsQuickPanel.Children.Add(row);
        }
    }

    // ── Bouclier de confidentialite (lecture seule) ───────────────────────────

    private void ShieldQuickFlyout_Opening(object sender, object e)
    {
        var counters = _privacy.GlobalCounters;
        ShieldQuickCounterText.Text = counters.Total == 0
            ? "Aucun blocage depuis l'ouverture de cette application."
            : $"{counters.Total:N0} element(s) bloque(s) - {counters.Ads:N0} pub(s), {counters.Trackers:N0} tracker(s).";

        ShieldQuickModulesPanel.Children.Clear();
        ShieldQuickModulesPanel.Children.Add(BuildModuleStatusRow("Bloqueur de pubs et trackers", _uiSettings.NetworkBlockerEnabled));
        ShieldQuickModulesPanel.Children.Add(BuildModuleStatusRow("Anti-telemetrie", _uiSettings.TelemetryBlockerEnabled));
        ShieldQuickModulesPanel.Children.Add(BuildModuleStatusRow("Masquage visuel des pubs", _uiSettings.CosmeticFilterEnabled));
        ShieldQuickModulesPanel.Children.Add(BuildModuleStatusRow("Refus automatique des bannieres cookies", _uiSettings.ConsentManagerEnabled));
        ShieldQuickModulesPanel.Children.Add(BuildModuleStatusRow("Blocage des popups publicitaires", _uiSettings.PopupBlockerEnabled));
        ShieldQuickModulesPanel.Children.Add(BuildModuleStatusRow("Blocage des redirections publicitaires", _uiSettings.StrictAdBlockEnabled));
        ShieldQuickModulesPanel.Children.Add(BuildModuleStatusRow("Promotion HTTPS", _uiSettings.HttpsEnforcerEnabled));
        ShieldQuickModulesPanel.Children.Add(BuildModuleStatusRow("Nettoyage des parametres de tracking", _uiSettings.ParameterCleanerEnabled));
        ShieldQuickModulesPanel.Children.Add(BuildModuleStatusRow("Anti-camouflage CNAME", _uiSettings.CnameUncloakerEnabled));
    }

    private void RefreshShieldQuickCount()
    {
        var total = _privacy.GlobalCounters.Total;
        ShieldQuickCountText.Text = total > 999 ? "999+" : total.ToString();
    }

    // Icone neutre pour l'etat "off" (tiret, pas une croix) : signale par
    // l'utilisateur le 2026-07-26 - une croix a cote d'un volet qui se ferme
    // normalement en cliquant a cote pretait a confusion avec un bouton
    // fermer. Le succes (coche verte) reste explicite ; l'echec est un
    // simple tiret neutre, sans ambiguite.
    private static UIElement BuildModuleStatusRow(string label, bool enabled)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        FrameworkElement icon = enabled
            ? new FontIcon
            {
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                Glyph = "",
                FontSize = 13,
                Width = 16,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NovaSuccessBrush"]
            }
            : new TextBlock
            {
                Text = "–",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Width = 16,
                TextAlignment = TextAlignment.Center,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NovaControlMutedForegroundBrush"]
            };
        Grid.SetColumn(icon, 0);
        row.Children.Add(icon);

        var text = new TextBlock { Text = label, FontSize = 12, Opacity = enabled ? 1 : 0.55, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        return row;
    }


    // Confinement doux : jamais de blocage de navigation (casserait les
    // redirections de connexion), juste un signal visuel avec retour possible.
    private void UpdateExternalDomainBar(string currentUrl)
    {
        var within = WebAppUrlPolicy.IsWithinAppScope(_app.RootDomain, currentUrl);
        ExternalDomainBar.Visibility = within ? Visibility.Collapsed : Visibility.Visible;
        if (!within)
        {
            ExternalDomainText.Text = $"Hors de {_app.RootDomain} — actuellement sur {PublicSuffixService.HostOf(currentUrl)}";
        }
    }

    private void ExternalDomainReturnButton_Click(object sender, RoutedEventArgs e)
    {
        if (_view?.CoreWebView2 is { } core)
        {
            core.Navigate(_app.Url);
        }
    }
}
