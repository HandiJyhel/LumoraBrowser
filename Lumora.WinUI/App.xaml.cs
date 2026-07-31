using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Lumora.WinUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        WinUiRuntimeTrace.Write("App constructor start");
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            WinUiRuntimeTrace.Write($"UNHANDLED: {e.Message} :: {e.Exception}");
        };
        WinUiRuntimeTrace.Write("App constructor after InitializeComponent");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        WinUiRuntimeTrace.Write("OnLaunched start");

        var commandLineArgs = Environment.GetCommandLineArgs();
        var appId = WebAppLaunchArgs.TryParseAppId(commandLineArgs);
        var webApp = appId is not null ? TryFindWebApp(appId) : null;

        // Doit être posé AVANT toute création de fenêtre : c'est ce qui évite
        // que le navigateur principal et chaque application web (même exe,
        // Lumora.WinUI.exe) se retrouvent regroupés sous une seule icône dans
        // la barre des tâches / les épinglages Windows.
        ApplyExplicitAppUserModelId(webApp is not null
            ? WebAppIdentity.AppUserModelId(webApp.Id)
            : WebAppIdentity.BrowserAppUserModelId);

        if (IncognitoLaunchArgs.IsIncognitoLaunch(commandLineArgs, out var torEnabled, out var incognitoUrl, out var returnToMain))
        {
            var profile = LumoraProfilePaths.Default();
            var incognitoWindow = new LumoraIncognitoWindow(profile, incognitoUrl, torEnabled, returnToMain);
            _window = incognitoWindow;
            incognitoWindow.Activate();
            incognitoWindow.InitializeWindow();
            WinUiRuntimeTrace.Write("Standalone Incognito window activated");
            return;
        }

        if (webApp is not null && TryLaunchWebApp(webApp))
        {
            return;
        }

        var startInGuestMode = GuestLaunchArgs.IsGuestLaunch(commandLineArgs);
        _window = new MainWindow(startInGuestMode);
        WinUiRuntimeTrace.Write("MainWindow constructed");
        _window.Activate();
        WinUiRuntimeTrace.Write("MainWindow activated");
        ((MainWindow)_window).InitializeBrowserSurface();
        WinUiRuntimeTrace.Write("Browser surface initialized");
    }

    private static LumoraWebApp? TryFindWebApp(string appId)
    {
        try
        {
            var profile = LumoraProfilePaths.Default();
            return new WebAppStore(profile.WebAppsFile).Find(appId);
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Web app lookup failed: {ex.GetType().Name}");
            return null;
        }
    }

    // Lancement direct en fenêtre d'application (raccourci Menu Démarrer/Bureau),
    // sans passer par la fenêtre principale. Retombe sur le navigateur normal si
    // l'application n'existe plus dans le registre local (ex. supprimée entre-temps).
    private bool TryLaunchWebApp(LumoraWebApp app)
    {
        try
        {
            var profile = LumoraProfilePaths.Default();
            WebView2Bootstrap.ConfigureOnce(profile.BrowserDataDir);

            var appWindow = new LumoraAppWindow(app, profile);
            _window = appWindow;
            appWindow.Activate();
            appWindow.InitializeBrowserSurface();
            WinUiRuntimeTrace.Write($"LumoraAppWindow activated for {app.Id}");
            return true;
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Web app launch failed: {ex.GetType().Name}");
            return false;
        }
    }

    private static void ApplyExplicitAppUserModelId(string appUserModelId)
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(appUserModelId);
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"AppUserModelID set failed: {ex.GetType().Name}");
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
