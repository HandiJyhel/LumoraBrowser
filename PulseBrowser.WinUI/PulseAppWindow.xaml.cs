using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Microsoft.Web.WebView2.Core;
using WinRT.Interop;
using PulseBrowser.Privacy;
using PulseBrowser.Privacy.NetworkBlocker;
using PulseBrowser.Privacy.TelemetryBlocker;
using PulseBrowser.Privacy.ParameterCleaner;
using PulseBrowser.Privacy.HttpsEnforcer;
using PulseBrowser.Privacy.CnameUncloaker;
using PulseBrowser.WinUI.Credentials;

namespace PulseBrowser.WinUI;

// Fenêtre d'application web : un site épinglé, sans onglets ni barre d'adresse,
// partageant le profil (cookies/sessions) de la fenêtre principale. Protections
// réseau actives (bloqueur pubs/trackers, anti-télémétrie, HTTPS, CNAME) ; le
// filtre cosmétique et le refus automatique des bannières cookies restent pour
// l'instant réservés à la fenêtre principale (limite documentée v1).
public sealed partial class PulseAppWindow : Window
{
    private readonly PulseWebApp _app;
    private readonly PulseProfilePaths _profile;
    private readonly UiSettings _uiSettings;
    private readonly PrivacyEngine _privacy = new();
    private WebView2? _view;
    private Microsoft.UI.Windowing.AppWindow? _appWindow;

    internal PulseAppWindow(PulseWebApp app, PulseProfilePaths profile)
    {
        _app = app;
        _profile = profile;
        _uiSettings = UiSettings.Load(profile.UiSettingsFile, profile.LegacyUiSettingsFile);

        InitializeComponent();
        Title = $"{_app.Title} — Pulse Browser";

        var hwnd = WindowNative.GetWindowHandle(this);
        var winId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(winId);
        ApplyIcon();
        ApplyAlwaysOnTop();

        InitPrivacyEngine();
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
            var custom = Path.Combine(_profile.WebAppIconsDir, _app.IconFile);
            if (FaviconQuality.IsUsablePngBackedIcoFile(custom)) return custom;
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "Assets", "PulseBrowser.ico");
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
        core.SourceChanged += (_, _) => UpdateExternalDomainBar(core.Source);
        core.DocumentTitleChanged += (_, _) => { /* titre de fenêtre volontairement stable (nom de l'app) */ };

        view.Source = new Uri(_app.Url);
    }

    private void Core_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (_privacy.ShouldBlock(args.Request.Uri, sender.Source ?? string.Empty))
        {
            args.Response = sender.Environment.CreateWebResourceResponse(null, 200, "OK", string.Empty);
        }
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
