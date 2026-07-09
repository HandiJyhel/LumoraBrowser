using Microsoft.UI.Xaml;

namespace PulseBrowser.WinUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        WinUiRuntimeTrace.Write("App constructor start");
        InitializeComponent();
        WinUiRuntimeTrace.Write("App constructor after InitializeComponent");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        WinUiRuntimeTrace.Write("OnLaunched start");

        var appId = WebAppLaunchArgs.TryParseAppId(Environment.GetCommandLineArgs());
        if (appId is not null && TryLaunchWebApp(appId))
        {
            return;
        }

        _window = new MainWindow();
        WinUiRuntimeTrace.Write("MainWindow constructed");
        _window.Activate();
        WinUiRuntimeTrace.Write("MainWindow activated");
        ((MainWindow)_window).InitializeBrowserSurface();
        WinUiRuntimeTrace.Write("Browser surface initialized");
    }

    // Lancement direct en fenêtre d'application (raccourci Menu Démarrer/Bureau),
    // sans passer par la fenêtre principale. Retombe sur le navigateur normal si
    // l'application n'existe plus dans le registre local (ex. supprimée entre-temps).
    private bool TryLaunchWebApp(string appId)
    {
        try
        {
            var profile = PulseProfilePaths.Default();
            WebView2Bootstrap.ConfigureOnce(profile.BrowserDataDir);
            var store = new WebAppStore(profile.WebAppsFile);
            var app = store.Find(appId);
            if (app is null)
            {
                WinUiRuntimeTrace.Write($"Web app not found for id {appId}, falling back to MainWindow");
                return false;
            }

            var appWindow = new PulseAppWindow(app, profile);
            _window = appWindow;
            appWindow.Activate();
            appWindow.InitializeBrowserSurface();
            WinUiRuntimeTrace.Write($"PulseAppWindow activated for {appId}");
            return true;
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Web app launch failed: {ex.GetType().Name}");
            return false;
        }
    }
}
