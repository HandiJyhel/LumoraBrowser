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

// Fenetre de navigation anonyme (Tor) : masque l'adresse IP reelle de CETTE
// fenetre en routant son trafic via un proxy SOCKS5 local (--proxy-server),
// fourni par un processus tor.exe gere par TorProcessManager. Le moteur n'est
// jamais telecharge automatiquement (etape separee, pas encore cablee) : tant
// qu'il est absent, la fenetre reste sur un ecran d'etat, sans onglet de
// navigation casse pointant vers un proxy qui n'existe pas.
public sealed partial class LumoraTorWindow : Window
{
    private readonly LumoraProfilePaths _profile;
    private readonly UiSettings _uiSettings;
    private readonly PrivacyEngine _privacy = new();
    private readonly TorProcessManager _tor = new();
    private WebView2? _view;

    internal LumoraTorWindow(LumoraProfilePaths profile)
    {
        _profile = profile;
        _uiSettings = UiSettings.Load(profile.UiSettingsFile, profile.LegacyUiSettingsFile);

        InitializeComponent();
        InitPrivacyEngine();

        _tor.StateChanged += OnTorStateChanged;
        Closed += (_, _) =>
        {
            try { _view?.Close(); } catch { }
            _tor.Dispose();
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

    // Point d'entree appele juste apres Activate(). Ne cree jamais le WebView2
    // tant que le moteur Tor n'est pas confirme connecte : naviguer vers un
    // proxy SOCKS5 qui n'ecoute pas se solderait par des pages qui ne chargent
    // jamais, sans aucune explication pour l'utilisateur.
    public async void InitializeWindow()
    {
        if (!TorProcessManager.IsEngineInstalled(_profile))
        {
            TorStatusText.Text = "Moteur Tor non installe.";
            return;
        }

        TorInstallButton.IsEnabled = false;
        TorStatusText.Text = "Demarrage du moteur Tor...";
        var started = await _tor.StartAsync(_profile);
        if (!started)
        {
            TorInstallButton.IsEnabled = true;
        }
    }

    private void OnTorStateChanged(TorEngineState state, string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TorStatusText.Text = message;
            if (state == TorEngineState.Connected && _view is null)
            {
                _ = InitializeBrowserSurfaceAsync();
            }
            else if (state is TorEngineState.Stopped or TorEngineState.Error)
            {
                TorInstallButton.IsEnabled = true;
            }
        });
    }

    private void TorInstallButton_Click(object sender, RoutedEventArgs e)
    {
        // Le telechargement du moteur (Tor Expert Bundle, torproject.org) est
        // une etape volontairement separee : aucune action reseau ici.
        TorStatusText.Text = "Telechargement du moteur Tor : pas encore disponible dans cette version.";
    }

    private async Task InitializeBrowserSurfaceAsync()
    {
        var view = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _view = view;
        TorWebViewHost.Children.Add(view);

        try
        {
            // Dossier de donnees dedie : un utilisateur-data-folder distinct force
            // WebView2 a lancer un processus Chromium separe, seul moyen d'avoir
            // une configuration proxy propre a cette fenetre sans affecter le
            // reste de la navigation.
            var torDataDir = System.IO.Path.Combine(TorProcessManager.ExpectedDirectory(_profile), "webview2");
            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = $"--proxy-server=socks5://127.0.0.1:{_tor.SocksPort}"
            };
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(string.Empty, torDataDir, options);
            await view.EnsureCoreWebView2Async(environment);
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Tor window WebView2 creation failed: {ex.GetType().Name}");
            TorWebViewHost.Children.Clear();
            TorStatusText.Text = $"Moteur web indisponible : {ex.Message}";
            TorInstallButton.IsEnabled = true;
            return;
        }

        var core = view.CoreWebView2;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;

        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += Core_WebResourceRequested;
        core.SourceChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateChromeFromCore);
        core.HistoryChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateChromeFromCore);
        core.DocumentTitleChanged += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            var title = core.DocumentTitle;
            Title = string.IsNullOrWhiteSpace(title)
                ? "Navigation anonyme - Lumora"
                : $"{title} - Navigation anonyme";
        });

        SetupPanel.Visibility = Visibility.Collapsed;
        TorWebViewHost.Visibility = Visibility.Visible;
        core.NavigateToString(WelcomeHtml());
        TorAddressBox.Focus(FocusState.Programmatic);

        WinUiRuntimeTrace.Write("Tor window WebView2 ready (proxied profile)");
    }

    private void Core_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (_privacy.ShouldBlock(args.Request.Uri, sender.Source ?? string.Empty))
        {
            args.Response = sender.Environment.CreateWebResourceResponse(null, 200, "OK", string.Empty);
        }
    }

    private void UpdateChromeFromCore()
    {
        var core = _view?.CoreWebView2;
        if (core is null) return;

        var source = core.Source ?? string.Empty;
        TorAddressBox.Text = source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : source;
        TorBackButton.IsEnabled = core.CanGoBack;
        TorForwardButton.IsEnabled = core.CanGoForward;
    }

    private void TorAddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;

        var raw = TorAddressBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return;

        var url = AddressNormalizer.Normalize(raw, _uiSettings.SearchEngine);
        try { _view?.CoreWebView2?.Navigate(url); } catch { }
        _view?.Focus(FocusState.Programmatic);
    }

    private void TorBackButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _view?.CoreWebView2;
        if (core?.CanGoBack == true) core.GoBack();
    }

    private void TorForwardButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _view?.CoreWebView2;
        if (core?.CanGoForward == true) core.GoForward();
    }

    private void TorReloadButton_Click(object sender, RoutedEventArgs e) =>
        _view?.CoreWebView2?.Reload();

    private static string WelcomeHtml() => """
        <!doctype html><html lang="fr"><head><meta charset="utf-8">
        <title>Navigation anonyme</title>
        <style>
        body{font-family:'Segoe UI',system-ui,sans-serif;background:radial-gradient(circle at 50% 28%,#8e7bd633,transparent 25%),linear-gradient(180deg,#17131f,#1c1628);color:#fff8ea;
             display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0}
        .card{max-width:560px;padding:0 32px;text-align:center}
        h1{font-size:26px;font-weight:600;margin:0 0 14px}
        p{opacity:.78;line-height:1.55;margin:0 0 10px}
        .badge{font-size:40px;margin-bottom:18px;color:#8e7bd6}
        </style></head><body><div class="card">
        <div class="badge">&#128373;&#65039;</div>
        <h1>Navigation anonyme</h1>
        <p>Cette fenetre est connectee au reseau Tor : le site visite et les
        relais intermediaires ne voient jamais votre adresse IP reelle en
        meme temps.</p>
        <p>Rien n'est ecrit dans l'historique, le coffre ou les favoris de
        Lumora depuis cette fenetre.</p>
        </div></body></html>
        """;
}
