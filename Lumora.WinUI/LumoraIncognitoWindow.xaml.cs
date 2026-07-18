using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Windows.System;
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
// neuve dans l'etat souhaite (process separe).
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
    private WebView2? _view;
    private bool _suppressToggleHandler;

    internal LumoraIncognitoWindow(LumoraProfilePaths profile, string? startUrl = null, bool initialTorEnabled = false)
    {
        _profile = profile;
        _pendingStartUrl = startUrl;
        _initialTorEnabled = initialTorEnabled;
        _uiSettings = UiSettings.Load(profile.UiSettingsFile, profile.LegacyUiSettingsFile);

        InitializeComponent();
        InitPrivacyEngine();
        CleanupStaleSessionFolders();

        Closed += (_, _) =>
        {
            _tor.Dispose();
            try { _view?.Close(); } catch { }
            try { if (Directory.Exists(_sessionDataDir)) Directory.Delete(_sessionDataDir, recursive: true); } catch { }
        };
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
                ShowStartupError("Moteur Tor non installe.");
                return;
            }

            IncognitoTorSwitch.IsEnabled = false;
            IncognitoTorStatusText.Text = "Connexion au reseau Tor...";

            var connected = await ConnectTorAsync();
            if (!connected)
            {
                ShowStartupError(_tor.StatusMessage);
                return;
            }

            IncognitoTorStatusText.Text = "IP masquee : oui";
            IncognitoTorSwitch.IsEnabled = true;
        }

        await BuildBrowserSurfaceAsync(_initialTorEnabled);
    }

    private void SetTorSwitchSilently(bool isOn)
    {
        _suppressToggleHandler = true;
        IncognitoTorSwitch.IsOn = isOn;
        _suppressToggleHandler = false;
    }

    // Le proxy Tor est fige au demarrage du process (voir commentaire de
    // classe) : basculer Tor ne peut pas se faire a chaud. On rouvre une
    // fenetre Incognito neuve dans l'etat souhaite et on ferme celle-ci,
    // plutot qu'une migration impossible entre deux profils differents.
    private void IncognitoTorSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleHandler) return;

        var wantsTor = IncognitoTorSwitch.IsOn;
        if (wantsTor && !TorProcessManager.IsEngineInstalled(_profile))
        {
            SetTorSwitchSilently(false);
            IncognitoTorStatusText.Text = "Moteur Tor non installe.";
            return;
        }

        IncognitoTorSwitch.IsEnabled = false;
        IncognitoTorStatusText.Text = wantsTor ? "Ouverture d'une session Tor..." : "Fermeture de la session Tor...";
        IncognitoProcessLauncher.Launch(torEnabled: wantsTor);
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
            var started = await _tor.StartAsync(_profile, FindFreeTcpPort());
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

    // Construit la surface WebView2, une seule fois pour toute la duree de vie
    // du process : configuration par variables d'environnement (dossier
    // temporaire de session + arguments Chromium, dont le proxy Tor) suivie de
    // EnsureCoreWebView2Async() sans argument - seul chemin fiable dans ce
    // projet (voir commentaire de classe).
    private async Task BuildBrowserSurfaceAsync(bool torEnabled)
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

        var view = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        IncognitoWebViewHost.Children.Add(view);

        CoreWebView2? core;
        try
        {
            await view.EnsureCoreWebView2Async();
            core = view.CoreWebView2;
        }
        catch (Exception ex)
        {
            ShowStartupError($"Moteur web indisponible : {ex.Message}");
            return;
        }

        if (core is null)
        {
            ShowStartupError("Moteur web indisponible : la session n'a pas pu demarrer. Fermez et rouvrez la fenetre Incognito.");
            return;
        }

        _view = view;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        try { core.Settings.IsReputationCheckingRequired = _uiSettings.SmartScreenEnabled; } catch { }

        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += Core_WebResourceRequested;
        core.SourceChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateChromeFromCore);
        core.HistoryChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateChromeFromCore);
        core.DocumentTitleChanged += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            var title = core.DocumentTitle;
            Title = string.IsNullOrWhiteSpace(title) ? "Incognito - Lumora" : $"{title} - Incognito";
        });
        core.NewWindowRequested += Core_NewWindowRequested;

        WinUiRuntimeTrace.Write($"Incognito window WebView2 ready (tor={torEnabled})");

        if (!string.IsNullOrWhiteSpace(_pendingStartUrl))
        {
            core.Navigate(_pendingStartUrl);
        }
        else
        {
            core.NavigateToString(WelcomeHtml(torEnabled));
            IncognitoAddressBox.Focus(FocusState.Programmatic);
        }
    }

    private void ShowStartupError(string message)
    {
        WinUiRuntimeTrace.Write($"Incognito window startup failed: {message}");
        IncognitoTorSwitch.IsEnabled = true;
        IncognitoWebViewHost.Children.Clear();
        IncognitoWebViewHost.Children.Add(new TextBlock
        {
            Text = message,
            Margin = new Thickness(24),
            TextWrapping = TextWrapping.Wrap
        });
    }

    private void Core_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (_privacy.ShouldBlock(args.Request.Uri, sender.Source ?? string.Empty))
        {
            args.Response = sender.Environment.CreateWebResourceResponse(null, 200, "OK", string.Empty);
        }
    }

    // Une page cible (target=_blank, window.open) ouvre une nouvelle fenetre
    // Incognito qui herite de l'etat Tor courant : jamais de retour silencieux
    // vers une session non anonymisee si l'utilisateur avait active Tor ici.
    // Nouveau process (voir commentaire de classe), pas une fenetre
    // in-process : un deuxieme moteur WebView2 dans CE process reproduirait
    // le probleme que le process dedie contourne.
    private void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        IncognitoProcessLauncher.Launch(args.Uri, IncognitoTorSwitch.IsOn);
    }

    private void UpdateChromeFromCore()
    {
        var core = _view?.CoreWebView2;
        if (core is null) return;

        var source = core.Source ?? string.Empty;
        IncognitoAddressBox.Text = source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : source;
        IncognitoBackButton.IsEnabled = core.CanGoBack;
        IncognitoForwardButton.IsEnabled = core.CanGoForward;
    }

    private void IncognitoAddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;

        var raw = IncognitoAddressBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return;

        var url = AddressNormalizer.Normalize(raw, _uiSettings.SearchEngine);
        try { _view?.CoreWebView2?.Navigate(url); } catch { }
        _view?.Focus(FocusState.Programmatic);
    }

    private void IncognitoBackButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _view?.CoreWebView2;
        if (core?.CanGoBack == true) core.GoBack();
    }

    private void IncognitoForwardButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _view?.CoreWebView2;
        if (core?.CanGoForward == true) core.GoForward();
    }

    private void IncognitoReloadButton_Click(object sender, RoutedEventArgs e) =>
        _view?.CoreWebView2?.Reload();

    private static string WelcomeHtml(bool torEnabled) => $$"""
        <!doctype html><html lang="fr"><head><meta charset="utf-8">
        <style>
        body{font-family:'Segoe UI',system-ui,sans-serif;background:radial-gradient(circle at 50% 28%,#8e7bd633,transparent 25%),linear-gradient(180deg,#17131f,#1c1628);color:#fff8ea;
             display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0}
        .card{max-width:560px;padding:0 32px;text-align:center}
        h1{font-size:26px;font-weight:600;margin:0 0 14px}
        p{opacity:.78;line-height:1.55;margin:0 0 10px}
        .badge{font-size:40px;margin-bottom:18px;color:#8e7bd6}
        .claim{font-weight:600;opacity:1}
        </style></head><body><div class="card">
        <div class="badge">&#128373;&#65039;</div>
        <h1>Incognito</h1>
        <p class="claim">Cette session ne sera jamais sauvegardee : a la fermeture de cette
        fenetre, rien n'est ecrit dans l'historique, le coffre, les favoris ou le disque
        - cookies, cache et stockage restent dans un dossier temporaire supprime a la
        fermeture.</p>
        <p class="claim">{{(torEnabled
            ? "Votre adresse IP reelle est masquee : elle n'est visible ni du site visite ni d'un relais Tor unique."
            : "Votre adresse IP reelle N'est PAS masquee : le site visite et votre reseau la voient normalement. Activez Tor (bouton en haut a droite) pour la masquer.")}}</p>
        <p>Les fichiers que vous telechargez volontairement sont, eux, conserves sur le disque.</p>
        </div></body></html>
        """;
}
