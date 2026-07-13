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

namespace Lumora.WinUI;

// Fenêtre de navigation privée : le moteur tourne sur un profil WebView2
// InPrivate (données en mémoire, purgées par le moteur à la libération du
// profil). Par construction, rien ne touche l'historique, le coffre, les
// favoris ou les favicons de Lumora : cette fenêtre n'est reliée à aucun store.
public sealed partial class LumoraPrivateWindow : Window
{
    // Nom de profil stable : toutes les fenêtres privées ouvertes en même temps
    // partagent la même session éphémère (comme les fenêtres InPrivate d'Edge).
    private const string PrivateProfileName = "lumora-prive";

    private readonly LumoraProfilePaths _profile;
    private readonly UiSettings _uiSettings;
    private readonly PrivacyEngine _privacy = new();
    private readonly string? _startUrl;
    private WebView2? _view;

    internal LumoraPrivateWindow(LumoraProfilePaths profile, string? startUrl = null)
    {
        _profile = profile;
        _startUrl = startUrl;
        _uiSettings = UiSettings.Load(profile.UiSettingsFile, profile.LegacyUiSettingsFile);

        InitializeComponent();
        InitPrivacyEngine();

        Closed += (_, _) =>
        {
            try { _view?.Close(); } catch { }
        };
    }

    // Mêmes protections réseau que les fenêtres d'application web (limite v1
    // partagée : pas de filtre cosmétique ni de refus automatique des bannières).
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

    public async void InitializeBrowserSurface()
    {
        var view = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _view = view;
        PrivateWebViewHost.Children.Add(view);

        try
        {
            // Environnement explicite sur le même dossier de données que le reste du
            // process (obligatoire : un seul navigateur Chromium par dossier), mais
            // profil InPrivate séparé : cookies, cache et stockages restent en mémoire.
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                string.Empty, _profile.BrowserDataDir, new CoreWebView2EnvironmentOptions());
            var controllerOptions = environment.CreateCoreWebView2ControllerOptions();
            controllerOptions.ProfileName = PrivateProfileName;
            controllerOptions.IsInPrivateModeEnabled = true;
            await view.EnsureCoreWebView2Async(environment, controllerOptions);
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Private window WebView2 creation failed: {ex.GetType().Name}");
            PrivateWebViewHost.Children.Clear();
            PrivateWebViewHost.Children.Add(new TextBlock
            {
                Text = $"Moteur web indisponible : {ex.Message}",
                Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var core = view.CoreWebView2;
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
            Title = string.IsNullOrWhiteSpace(title)
                ? "Navigation privee - Lumora"
                : $"{title} - Navigation privee";
        });
        core.NewWindowRequested += Core_NewWindowRequested;

        WinUiRuntimeTrace.Write("Private window WebView2 ready (InPrivate profile)");

        if (!string.IsNullOrWhiteSpace(_startUrl))
        {
            core.Navigate(_startUrl);
        }
        else
        {
            core.NavigateToString(WelcomeHtml());
            PrivateAddressBox.Focus(FocusState.Programmatic);
        }
    }

    private void Core_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (_privacy.ShouldBlock(args.Request.Uri, sender.Source ?? string.Empty))
        {
            args.Response = sender.Environment.CreateWebResourceResponse(null, 200, "OK", string.Empty);
        }
    }

    // Une page cible (target=_blank, window.open) ouvre une nouvelle fenêtre
    // privée : la navigation ne doit jamais s'échapper vers le profil normal.
    private void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        var window = new LumoraPrivateWindow(_profile, args.Uri);
        window.Activate();
        window.InitializeBrowserSurface();
    }

    private void UpdateChromeFromCore()
    {
        var core = _view?.CoreWebView2;
        if (core is null) return;

        var source = core.Source ?? string.Empty;
        PrivateAddressBox.Text = source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : source;
        PrivateBackButton.IsEnabled = core.CanGoBack;
        PrivateForwardButton.IsEnabled = core.CanGoForward;
    }

    private void PrivateAddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;

        var raw = PrivateAddressBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return;

        var url = AddressNormalizer.Normalize(raw, _uiSettings.SearchEngine);
        try { _view?.CoreWebView2?.Navigate(url); } catch { }
        _view?.Focus(FocusState.Programmatic);
    }

    private void PrivateBackButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _view?.CoreWebView2;
        if (core?.CanGoBack == true) core.GoBack();
    }

    private void PrivateForwardButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _view?.CoreWebView2;
        if (core?.CanGoForward == true) core.GoForward();
    }

    private void PrivateReloadButton_Click(object sender, RoutedEventArgs e) =>
        _view?.CoreWebView2?.Reload();

    private static string WelcomeHtml() => """
        <!doctype html><html lang="fr"><head><meta charset="utf-8">
        <title>Navigation privée</title>
        <style>
        body{font-family:'Segoe UI',system-ui,sans-serif;background:#1e1830;color:#e8e2f5;
             display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0}
        .card{max-width:560px;padding:0 32px;text-align:center}
        h1{font-size:26px;font-weight:600;margin:0 0 14px}
        p{opacity:.78;line-height:1.55;margin:0 0 10px}
        .badge{font-size:40px;margin-bottom:18px}
        </style></head><body><div class="card">
        <div class="badge">&#128373;&#65039;</div>
        <h1>Navigation privée</h1>
        <p>Cette fenêtre utilise une session éphémère : cookies, cache et stockages
        restent en mémoire et disparaissent à la fermeture de la dernière fenêtre privée.</p>
        <p>Rien n'est écrit dans l'historique, le coffre ou les favoris de Lumora.
        Les protections réseau (publicités, traqueurs, HTTPS) restent actives.</p>
        <p>Les fichiers que vous téléchargez volontairement sont, eux, conservés sur le disque.</p>
        </div></body></html>
        """;
}
