using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Windows.System;
using WinRT.Interop;
using Lumora.Privacy;
using Lumora.Privacy.NetworkBlocker;
using Lumora.Privacy.TelemetryBlocker;
using Lumora.Privacy.ParameterCleaner;
using Lumora.Privacy.HttpsEnforcer;
using Lumora.Privacy.CnameUncloaker;
using Lumora.WinUI.Tor;

namespace Lumora.WinUI;

// Fenetre Incognito : fusion de l'ancienne navigation privee et de l'ancienne
// navigation anonyme (Tor). Session ephemere TOUJOURS active (dossier de
// donnees temporaire, supprime a la fermeture) ; Tor optionnel, desactive par
// defaut, bascule visible en permanence.
//
// Tourne dans un PROCESS Windows dedie, distinct de celui de MainWindow
// (lancee via App.xaml.cs + IncognitoLaunchArgs, exactement comme
// LumoraAppWindow pour les applications web epinglees). La configuration
// WebView2 passe par les variables d'environnement WEBVIEW2_USER_DATA_FOLDER
// et WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS + EnsureCoreWebView2Async() SANS
// argument, exactement comme MainWindow et LumoraAppWindow : creer un
// environnement WebView2 explicite (CoreWebView2Environment.CreateWithOptionsAsync
// + EnsureCoreWebView2Async(environment, ...)) s'est revele ne JAMAIS produire
// de CoreWebView2 exploitable sur au moins une installation, meme en process
// totalement neuf sans aucun autre moteur actif - cause plateforme exacte non
// confirmee, mais le chemin "sans argument" est le seul dont la fiabilite est
// prouvee dans ce projet.
//
// Consequence : le dossier de donnees et les arguments Chromium (dont le
// proxy Tor) sont figes UNE SEULE FOIS au demarrage du process, comme
// WebView2Bootstrap.ConfigureOnce le fait pour MainWindow. Basculer Tor ne
// peut donc pas se faire a chaud : ca ferme cette fenetre et en rouvre une
// neuve dans l'etat souhaite (process separe). Les onglets, eux, restent
// dans CE process (voir CreateTabAsync) : un deuxieme WebView2 dans le meme
// process fonctionne (c'est ce que fait deja MainWindow), seul un deuxieme
// ENVIRONNEMENT WebView2 explicite posait probleme.
public sealed partial class LumoraIncognitoWindow : Window
{
    private readonly LumoraProfilePaths _profile;
    private readonly UiSettings _uiSettings;
    private readonly PrivacyEngine _privacy = new();
    private readonly TorProcessManager _tor = new();
    private readonly bool _initialTorEnabled;
    private readonly string _sessionDataDir =
        Path.Combine(Path.GetTempPath(), "LumoraIncognito", Guid.NewGuid().ToString("N"));
    private string? _pendingStartUrl;
    private IncognitoTab? _currentTab;
    private bool _suppressToggleHandler;
    private bool _torEngineInstallInProgress;
    private Microsoft.UI.Windowing.AppWindow? _appWindow;
    private readonly bool _returnToMain;
    // Vrai juste avant un Close() qui relance immediatement une AUTRE fenetre
    // Incognito (bascule Tor, installation du moteur Tor) : dans ce cas le
    // Closed ci-dessous ne doit surtout pas relancer MainWindow, seulement le
    // vrai dernier Close() (l'utilisateur quitte Incognito) le doit.
    private bool _relaunchingIncognito;

    internal LumoraIncognitoWindow(
        LumoraProfilePaths profile, string? startUrl = null, bool initialTorEnabled = false, bool returnToMain = false)
    {
        _profile = profile;
        _pendingStartUrl = startUrl;
        _initialTorEnabled = initialTorEnabled;
        _returnToMain = returnToMain;
        _uiSettings = UiSettings.Load(profile.UiSettingsFile, profile.LegacyUiSettingsFile);

        InitializeComponent();
        InitPrivacyEngine();
        CleanupStaleSessionFolders();

        var hwnd = WindowNative.GetWindowHandle(this);
        var winId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(winId);
        LumoraTheme.ApplySecondaryWindowTheme(RootGrid, _appWindow, _uiSettings, LumoraWindowThemeRole.Incognito);
        ApplyIcon();
        ApplyWindowBorderColor(hwnd);

        var newTabAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.T,
            Modifiers = VirtualKeyModifiers.Control
        };
        newTabAccelerator.Invoked += (acceleratorSender, args) =>
        {
            _ = CreateTabAsync(null, select: true);
            args.Handled = true;
        };
        Content.KeyboardAccelerators.Add(newTabAccelerator);

        Closed += (_, _) =>
        {
            _tor.Dispose();
            foreach (var item in IncognitoTabs.TabItems.OfType<TabViewItem>())
            {
                if (item.Tag is IncognitoTab tab)
                {
                    try { tab.View.Close(); } catch { }
                }
            }
            try { if (Directory.Exists(_sessionDataDir)) Directory.Delete(_sessionDataDir, recursive: true); } catch { }

            // MainWindow s'est fermee pour laisser la place a cette fenetre
            // (voir MainWindow.Incognito.cs) : si on quitte vraiment Incognito
            // (pas juste une bascule Tor qui relance une autre fenetre
            // Incognito), on relance une fenetre normale pour ne jamais
            // laisser l'utilisateur sans aucune fenetre Lumora ouverte.
            if (_returnToMain && !_relaunchingIncognito)
            {
                IncognitoProcessLauncher.LaunchMainWindow();
            }
        };
    }

    private void ApplyIcon()
    {
        if (_appWindow is null) return;

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "LumoraIncognito.ico");
            if (File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
            }
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Incognito window icon apply failed: {ex.GetType().Name}");
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;

    // Windows 11 peint par defaut le lisere de fenetre avec la couleur
    // d'accentuation SYSTEME (reglage utilisateur hors de Lumora), pas avec
    // l'identite violette d'Incognito - meme constat et meme correctif que
    // MainWindow.WindowChrome.cs.ApplyWindowBorderColor (jamais branche ici
    // jusqu'a present), avec l'accent Incognito au lieu de l'ambre Nova pour
    // rester coherent avec le violet garde comme signature du mode prive.
    private void ApplyWindowBorderColor(nint hwnd)
    {
        try
        {
            var accent = RootGrid.Resources["LumoraWindowAccentBrush"] is SolidColorBrush accentBrush
                ? accentBrush.Color
                : LumoraTheme.UiColor(180, 138, 255);
            var accentRef = (accent.B << 16) | (accent.G << 8) | accent.R;
            DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref accentRef, sizeof(int));

            // Diagnostic (2026-08-01) : liseré exterieur corrige, mais une ligne
            // orange (couleur d'accent SYSTEME, PAS l'ambre Nova) restait visible
            // a la jonction barre de titre/contenu - hypothese testee ici : DWM
            // peint cette jonction via DWMWA_CAPTION_COLOR independamment de
            // AppWindow.TitleBar.BackgroundColor, jamais force explicitement.
            var chrome = RootGrid.Resources["LumoraWindowChromeBrush"] is SolidColorBrush chromeBrush
                ? chromeBrush.Color
                : LumoraTheme.UiColor(32, 26, 48);
            var chromeRef = (chrome.B << 16) | (chrome.G << 8) | chrome.R;
            DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref chromeRef, sizeof(int));
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Incognito window border color skipped: {ex.GetType().Name}");
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

        _ = network.LoadAsync();
    }

    // Filet de securite : si une fenetre Incognito precedente a plante ou a ete
    // tuee brutalement (Task Manager, coupure de courant...), son dossier
    // temporaire de session a pu survivre. Nettoye au demarrage de la
    // suivante, plutot que de laisser trainer indefiniment un residu de
    // navigation cense etre ephemere.
    private static void CleanupStaleSessionFolders()
    {
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "LumoraIncognito");
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.GetDirectories(root))
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* encore verrouille par une autre fenetre Incognito active */ }
            }
        }
        catch { }
    }

    // Point d'entree appele juste apres Activate().
    public async void InitializeWindow()
    {
        if (_initialTorEnabled)
        {
            if (!TorProcessManager.IsEngineInstalled(_profile))
            {
                SetTorSwitchSilently(false);
                ShowStartupError("Moteur Tor non installé.", offerInstall: true);
                return;
            }

            IncognitoTorSwitch.IsEnabled = false;
            IncognitoTorStatusText.Text = "Connexion au réseau Tor...";

            var connected = await ConnectTorAsync();
            if (!connected)
            {
                ShowStartupError(_tor.StatusMessage, offerInstall: true);
                return;
            }

            IncognitoTorStatusText.Text = "IP masquée : oui";
            SetTorIndicatorActive(true);
            // Le ToggleSwitch demarre a IsOn=false (valeur par defaut XAML) meme
            // quand Tor est actif des l'ouverture (_initialTorEnabled) : sans ce
            // rattrapage, l'interrupteur restait visuellement/reellement "eteint"
            // pendant toute la session Tor active, et cliquer dessus pour
            // desactiver Tor le faisait au contraire passer a On (rouvrait une
            // NOUVELLE fenetre... avec Tor actif), rendant Tor impossible a
            // desactiver depuis l'interrupteur. SetTorSwitchSilently pour ne pas
            // redeclencher IncognitoTorSwitch_Toggled (deja en cours d'execution
            // de ce meme scenario Tor).
            SetTorSwitchSilently(true);
            IncognitoTorSwitch.IsEnabled = true;
            IncognitoNewCircuitButton.Visibility = Visibility.Visible;
        }

        ConfigureProcessWebView(_initialTorEnabled);
        // Sur la page d'accueil (pas d'URL de depart), le champ "autofocus" de
        // IncognitoWelcomeHtml recoit deja le focus clavier cote page - focus
        // programmatique de IncognitoAddressBox retire ici (2026-08-01) car il
        // volait ce focus a la barre de recherche integree juste ajoutee,
        // rendant son autofocus inoperant. La barre d'adresse reste bien sur
        // utilisable au clic/Tab comme avant, juste plus focus par defaut.
        await CreateTabAsync(_pendingStartUrl, select: true);
    }

    private void SetTorSwitchSilently(bool isOn)
    {
        _suppressToggleHandler = true;
        IncognitoTorSwitch.IsOn = isOn;
        _suppressToggleHandler = false;
    }

    // Pastille rouge/vert a cote du texte de statut : rouge tant que l'IP reelle
    // n'est pas confirmee masquee (desactive, en cours de connexion, echec),
    // vert seulement une fois la connexion Tor reellement etablie. Purement
    // additif a IncognitoTorStatusText (deja le signal accessible principal) -
    // jamais la couleur seule comme unique indicateur d'etat.
    private void SetTorIndicatorActive(bool active) =>
        IncognitoTorIndicatorDot.Fill = RootGrid.Resources[
            active ? "LumoraWindowSuccessBrush" : "LumoraWindowDangerBrush"] as Brush
            ?? new SolidColorBrush(active
                ? LumoraTheme.UiColor(61, 214, 136)
                : LumoraTheme.UiColor(229, 72, 77));

    // Le proxy Tor est fige au demarrage du process (voir commentaire de
    // classe) : basculer Tor ne peut pas se faire a chaud. On rouvre une
    // fenetre Incognito neuve dans l'etat souhaite et on ferme celle-ci,
    // plutot qu'une migration impossible entre deux profils differents. Les
    // autres onglets ouverts sont perdus (comportement deja present avant les
    // onglets : la bascule Tor ne conservait deja pas la page courante).
    // Demande explicite utilisateur (2026-08-01) : plutot que de faire semblant
    // d'etre un interrupteur instantane, une ContentDialog explique la
    // consequence reelle (fermeture/reouverture, onglets perdus) avant de
    // continuer - meme pattern de confirmation que MainWindow.WebApps.cs. Un
    // Annuler restaure le switch et le texte de statut a leur etat d'avant.
    private async void IncognitoTorSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleHandler) return;

        var wantsTor = IncognitoTorSwitch.IsOn;
        if (wantsTor && !TorProcessManager.IsEngineInstalled(_profile))
        {
            SetTorSwitchSilently(false);
            IncognitoTorStatusText.Text = "Moteur Tor non installé.";
            IncognitoTorInstallButton.Visibility = Visibility.Visible;
            return;
        }

        var previousStatus = IncognitoTorStatusText.Text;
        var confirm = new ContentDialog
        {
            Title = wantsTor ? "Activer Tor ?" : "Désactiver Tor ?",
            Content = "Cette fenêtre Incognito va se fermer et une nouvelle va s'ouvrir avec Tor "
                + (wantsTor ? "activé" : "désactivé")
                + ". Les onglets ouverts seront perdus. Vous resterez en Incognito.",
            PrimaryButtonText = wantsTor ? "Activer Tor" : "Désactiver Tor",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            SetTorSwitchSilently(!wantsTor);
            IncognitoTorStatusText.Text = previousStatus;
            return;
        }

        IncognitoTorSwitch.IsEnabled = false;
        IncognitoTorStatusText.Text = wantsTor ? "Ouverture d'une session Tor..." : "Fermeture de la session Tor...";
        _relaunchingIncognito = true;
        IncognitoProcessLauncher.Launch(torEnabled: wantsTor, returnToMain: _returnToMain);
        Close();
    }

    // Le port SOCKS par defaut de Tor (9050) convient a une seule fenetre,
    // mais deux fenetres Incognito+Tor ouvertes en meme temps (deux process
    // distincts) s'y battraient. Le systeme d'exploitation attribue un port
    // loopback libre a l'ouverture d'un socket sur le port 0 ; on le relache
    // aussitot pour que tor.exe puisse s'y lier a son tour (petite fenetre de
    // course acceptable : usage local, jamais expose au reseau).
    private static int FindFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task<bool> ConnectTorAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        void Handler(TorEngineState state, string message)
        {
            if (state == TorEngineState.Connected)
            {
                tcs.TrySetResult(true);
            }
            else if (state is TorEngineState.Error or TorEngineState.Stopped)
            {
                tcs.TrySetResult(false);
            }
            else
            {
                DispatcherQueue.TryEnqueue(() => IncognitoTorStatusText.Text = message);
            }
        }

        _tor.StateChanged += Handler;
        try
        {
            var started = await _tor.StartAsync(_profile, FindFreeTcpPort(), FindFreeTcpPort());
            if (!started)
            {
                return false;
            }
            return await tcs.Task;
        }
        finally
        {
            _tor.StateChanged -= Handler;
        }
    }

    // "Nouveau circuit" : echappatoire pour un noeud de sortie Tor mal note
    // aupres d'un site (captcha qui boucle indefiniment malgre une reponse
    // correcte - comportement connu du reseau Tor, pas un bug Lumora). Ne
    // ferme pas la fenetre (contrairement a la bascule Tor elle-meme, qui
    // change de port SOCKS fige au demarrage du process) : demande de
    // nouveaux circuits sur le control port puis recharge l'onglet courant
    // pour que la prochaine connexion emprunte un circuit different. Les
    // connexions deja ouvertes peuvent garder l'ancien circuit jusqu'a leur
    // fermeture naturelle - meme limite honnete que le "New Identity" de Tor
    // Browser, jamais presentee comme une garantie instantanee.
    private async void IncognitoNewCircuitButton_Click(object sender, RoutedEventArgs e)
    {
        IncognitoNewCircuitButton.IsEnabled = false;
        var previousStatus = IncognitoTorStatusText.Text;

        var (success, message) = await _tor.RequestNewCircuitAsync();
        IncognitoTorStatusText.Text = message;

        if (success)
        {
            _currentTab?.View.CoreWebView2?.Reload();
        }

        await Task.Delay(TorProcessManager.NewCircuitCooldown);
        IncognitoNewCircuitButton.IsEnabled = true;
        if (success)
        {
            IncognitoTorStatusText.Text = "IP masquée : oui";
        }
        else if (IncognitoTorStatusText.Text == message)
        {
            IncognitoTorStatusText.Text = previousStatus;
        }
    }

    // Pose le dossier de session et les arguments Chromium (dont le proxy
    // Tor) une seule fois pour tout le process, avant le premier onglet -
    // exactement comme avant l'ajout des onglets (voir commentaire de
    // classe). Les onglets suivants reutilisent automatiquement le meme
    // environnement WebView2 implicite via EnsureCoreWebView2Async() sans
    // argument.
    private void ConfigureProcessWebView(bool torEnabled)
    {
        var flags = "--disable-crash-reporter --disable-breakpad --disable-domain-reliability --no-pings";
        if (_uiSettings.WebRtcLeakProtectionEnabled)
        {
            flags += " --force-webrtc-ip-handling-policy=disable_non_proxied_udp";
        }
        if (torEnabled)
        {
            flags += $" --proxy-server=socks5://127.0.0.1:{_tor.SocksPort}";
        }
        try { Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", _sessionDataDir); } catch { }
        try { Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", flags); } catch { }
    }

    private void IncognitoTabs_AddTabButtonClick(TabView sender, object args) =>
        _ = CreateTabAsync(null, select: true);

    private void IncognitoTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IncognitoTabs.SelectedItem is not TabViewItem item || item.Tag is not IncognitoTab tab)
        {
            return;
        }

        _currentTab = tab;

        // Rend l'onglet visible SANS recharger sa page : chaque onglet garde
        // son propre WebView2 dans IncognitoWebViewHost (toujours present
        // dans l'arbre visuel), le changement d'onglet est un simple
        // basculement de visibilite - meme mecanisme que MainWindow.ActivateTab.
        foreach (var other in IncognitoTabs.TabItems.OfType<TabViewItem>())
        {
            if (other.Tag is IncognitoTab otherTab)
            {
                otherTab.View.Visibility = ReferenceEquals(otherTab, tab) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        UpdateTabFromCore(tab);
        Title = WindowTitleFor(tab.Title);
        // FocusState.Pointer et non Programmatic (2026-08-02, meme correctif que
        // MainWindow.ActivateTab) : Programmatic reste au niveau de l'enveloppe
        // XAML sans se propager jusqu'a Chromium, la molette restait muette
        // apres un changement d'onglet tant qu'on n'avait pas clique dans la page.
        tab.View.Focus(FocusState.Pointer);
    }

    // "Nouvel onglet" (onglet tout juste cree) et "Incognito" (titre de la
    // page d'accueil, voir IncognitoWelcomeHtml) sont tous deux des etats
    // neutres, sans vraie page chargee : garder le titre de marque plutot
    // que produire "Incognito - Incognito".
    private static string WindowTitleFor(string tabTitle) =>
        tabTitle is "Nouvel onglet" or "Incognito" ? "Incognito - Lumora" : $"{tabTitle} - Incognito";

    // Fermer le dernier onglet ferme toute la fenetre, comme un navigateur
    // classique - la session ephemere est de toute facon liee a la fenetre,
    // pas a un onglet individuel.
    private void IncognitoTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is not IncognitoTab tab) return;

        IncognitoTabs.TabItems.Remove(args.Tab);
        IncognitoWebViewHost.Children.Remove(tab.View);
        try { tab.View.Close(); } catch { }

        if (IncognitoTabs.TabItems.Count == 0)
        {
            Close();
        }
    }

    // Cree un nouvel onglet dans CETTE fenetre/process (voir commentaire de
    // classe : un deuxieme WebView2 dans le meme process fonctionne, seul un
    // deuxieme environnement explicite posait probleme). Tous les onglets
    // d'une fenetre partagent donc automatiquement le meme etat Tor/session.
    // Le WebView2 va dans IncognitoWebViewHost (partage, toujours dans
    // l'arbre visuel), jamais en TabViewItem.Content : verifie en conditions
    // reelles que WebView2 place directement en contenu d'un TabViewItem ne
    // s'affiche pas (fond vide malgre une navigation reussie).
    private async Task<IncognitoTab?> CreateTabAsync(string? url, bool select)
    {
        var view = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Visibility = select ? Visibility.Visible : Visibility.Collapsed
        };
        // FocusState.Pointer et non Programmatic (2026-08-02, meme correctif que
        // MainWindow) : Programmatic ne se propage pas jusqu'a Chromium, ce
        // survol est justement le mecanisme principal de recuperation du focus.
        view.PointerEntered += (_, _) => view.Focus(FocusState.Pointer);
        var item = new TabViewItem
        {
            Header = "Nouvel onglet",
            IsClosable = true
        };
        var tab = new IncognitoTab { Item = item, View = view };
        item.Tag = tab;

        IncognitoWebViewHost.Children.Add(view);
        IncognitoTabs.TabItems.Add(item);
        if (select)
        {
            IncognitoTabs.SelectedItem = item;
        }

        CoreWebView2? core;
        try
        {
            await view.EnsureCoreWebView2Async();
            core = view.CoreWebView2;
        }
        catch (Exception ex)
        {
            FailTab(item, tab, $"Moteur web indisponible : {ex.Message}");
            return null;
        }

        if (core is null)
        {
            FailTab(item, tab, "Moteur web indisponible : la session n'a pas pu démarrer. Fermez et rouvrez la fenêtre Incognito.");
            return null;
        }

        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        try { core.Settings.IsReputationCheckingRequired = _uiSettings.SmartScreenEnabled; } catch { }

        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += Core_WebResourceRequested;
        core.SourceChanged += (_, _) => DispatcherQueue.TryEnqueue(() => UpdateTabFromCore(tab));
        core.HistoryChanged += (_, _) => DispatcherQueue.TryEnqueue(() => UpdateTabFromCore(tab));
        core.DocumentTitleChanged += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            var title = core.DocumentTitle;
            tab.Title = string.IsNullOrWhiteSpace(title) ? "Nouvel onglet" : title;
            tab.Item.Header = tab.Title;
            if (ReferenceEquals(tab, _currentTab))
            {
                Title = WindowTitleFor(tab.Title);
            }
        });
        // Une page cible (target=_blank, window.open) ouvre un nouvel onglet
        // dans cette meme fenetre plutot qu'une fenetre Incognito separee :
        // meme etat Tor/session deja garanti, pas besoin d'un nouveau process.
        core.NewWindowRequested += (coreSender, args) =>
        {
            args.Handled = true;
            _ = CreateTabAsync(args.Uri, select: true);
        };

        WinUiRuntimeTrace.Write($"Incognito tab WebView2 ready (tor={_initialTorEnabled})");

        if (!string.IsNullOrWhiteSpace(url))
        {
            core.Navigate(url);
        }
        else
        {
            core.NavigateToString(IncognitoWelcomeHtml.Build(_initialTorEnabled));
        }

        return tab;
    }

    // Echec de creation d'un onglet : si c'est le seul onglet (typiquement le
    // tout premier, au demarrage), on retombe sur l'ecran d'erreur plein cadre
    // existant. Sinon, l'onglet rate est simplement retire - les autres
    // restent utilisables.
    private void FailTab(TabViewItem item, IncognitoTab tab, string message)
    {
        WinUiRuntimeTrace.Write($"Incognito window startup failed: {message}");
        var wasOnlyTab = IncognitoTabs.TabItems.Count == 1;

        IncognitoTabs.TabItems.Remove(item);
        IncognitoWebViewHost.Children.Remove(tab.View);
        try { tab.View.Close(); } catch { }

        if (wasOnlyTab)
        {
            ShowStartupError(message);
        }
    }

    private void ShowStartupError(string message, bool offerInstall = false)
    {
        WinUiRuntimeTrace.Write($"Incognito window startup failed: {message}");
        IncognitoTorSwitch.IsEnabled = true;
        IncognitoTabs.Visibility = Visibility.Collapsed;
        IncognitoWebViewHost.Visibility = Visibility.Collapsed;
        IncognitoErrorHost.Children.Clear();
        IncognitoErrorHost.Visibility = Visibility.Visible;

        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        if (offerInstall)
        {
            var statusText = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
            var installButton = new Button { Content = "Installer le moteur Tor", HorizontalAlignment = HorizontalAlignment.Left };
            installButton.Click += async (_, _) =>
                await RunTorEngineInstallAsync(installButton, text => statusText.Text = text);
            panel.Children.Add(installButton);
            panel.Children.Add(statusText);
        }

        IncognitoErrorHost.Children.Add(panel);
    }

    private async void IncognitoTorInstallButton_Click(object sender, RoutedEventArgs e)
    {
        await RunTorEngineInstallAsync(IncognitoTorInstallButton, text => IncognitoTorStatusText.Text = text);
    }

    // Logique d'installation partagee entre le bouton de l'en-tete (bascule)
    // et celui de l'ecran d'echec au demarrage. Jamais declenchee
    // automatiquement : uniquement sur clic explicite. En cas de succes,
    // rouvre une fenetre neuve avec Tor active (le proxy est fige au
    // demarrage du process, cf. commentaire de classe) et ferme celle-ci.
    private async Task RunTorEngineInstallAsync(Button triggerButton, Action<string> report)
    {
        if (_torEngineInstallInProgress)
        {
            return;
        }

        _torEngineInstallInProgress = true;
        triggerButton.IsEnabled = false;
        try
        {
            await TorEngineProvider.DownloadEngineAsync(
                TorProcessManager.ExpectedExecutablePath(_profile), new Progress<string>(report));
            report("Moteur Tor installé. Réouverture avec Tor activé...");
            _relaunchingIncognito = true;
            IncognitoProcessLauncher.Launch(torEnabled: true, returnToMain: _returnToMain);
            Close();
        }
        catch (Exception ex)
        {
            report($"Installation du moteur Tor impossible : {ex.Message}");
            triggerButton.IsEnabled = true;
        }
        finally
        {
            _torEngineInstallInProgress = false;
        }
    }

    // La navigation principale (Document) n'est jamais remplacee par une reponse
    // vide, meme si son URL matche une regle EasyList/EasyPrivacy : sinon un
    // faux positif sur le domaine cible (frequent avec les redirecteurs
    // publicitaires) rend la page entiere blanche au lieu de ne bloquer que ses
    // sous-ressources. Meme garde-fou que MainWindow.Privacy.cs.
    private void Core_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (args.ResourceContext == CoreWebView2WebResourceContext.Document)
        {
            return;
        }

        if (_privacy.ShouldBlock(args.Request.Uri, sender.Source ?? string.Empty))
        {
            args.Response = sender.Environment.CreateWebResourceResponse(null, 200, "OK", string.Empty);
        }
    }

    // Met a jour l'adresse memorisee de l'onglet, et - seulement si c'est
    // l'onglet actuellement visible - la barre d'adresse et les boutons
    // precedent/suivant partages par toute la fenetre.
    private void UpdateTabFromCore(IncognitoTab tab)
    {
        var core = tab.View.CoreWebView2;
        if (core is null) return;

        tab.Address = core.Source ?? string.Empty;

        if (!ReferenceEquals(tab, _currentTab)) return;

        IncognitoAddressBox.Text = tab.Address.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : tab.Address;
        IncognitoBackButton.IsEnabled = core.CanGoBack;
        IncognitoForwardButton.IsEnabled = core.CanGoForward;
    }

    // Moteur de recherche fixe pour Incognito, independant du reglage global
    // (_uiSettings.SearchEngine) : diagnostique en conditions reelles le
    // 2026-07-20, Google bloque parfois des plages entieres de noeuds de
    // sortie Tor pour la recherche, avec une boucle de captcha qui ne se
    // resout jamais quel que soit le nombre de circuits Tor demandes -
    // comportement cote serveur Google, pas un bug reseau. DuckDuckGo est
    // nettement plus tolerant envers Tor (c'est aussi le choix par defaut de
    // Tor Browser). Fixe plutot que configurable : meme logique que les
    // autres garanties Incognito (session ephemere, etc.), une garantie
    // simple et previsible plutot qu'un reglage de plus a expliquer.
    private const string IncognitoSearchEngine = "duckduckgo";

    private void IncognitoAddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;

        var raw = IncognitoAddressBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return;

        var url = AddressNormalizer.Normalize(raw, IncognitoSearchEngine);
        try { _currentTab?.View.CoreWebView2?.Navigate(url); } catch { }
        // FocusState.Pointer et non Programmatic (2026-08-02, meme correctif que
        // MainWindow) : Programmatic ne se propage pas jusqu'a Chromium.
        _currentTab?.View.Focus(FocusState.Pointer);
    }

    private void IncognitoBackButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _currentTab?.View.CoreWebView2;
        if (core?.CanGoBack == true) core.GoBack();
    }

    private void IncognitoForwardButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _currentTab?.View.CoreWebView2;
        if (core?.CanGoForward == true) core.GoForward();
    }

    private void IncognitoReloadButton_Click(object sender, RoutedEventArgs e) =>
        _currentTab?.View.CoreWebView2?.Reload();
}
