using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Lumora.WinUI;

public partial class App : Application
{
    private Window? _window;
    // Capturee sur le thread UI au tout debut de OnLaunched (2026-08-13,
    // mono-instance) : l'evenement AppInstance.Activated d'une relance
    // redirigee arrive potentiellement sur un thread hors UI, il faut donc
    // la DispatcherQueue du thread UI deja connue a l'avance plutot que
    // GetForCurrentThread() (qui renverrait null depuis ce thread-la).
    private Microsoft.UI.Dispatching.DispatcherQueue? _uiDispatcherQueue;

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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        WinUiRuntimeTrace.Write("OnLaunched start");
        _uiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        var commandLineArgs = Environment.GetCommandLineArgs();
        var appId = WebAppLaunchArgs.TryParseAppId(commandLineArgs);
        var webApp = appId is not null ? TryFindWebApp(appId) : null;
        var startInGuestMode = GuestLaunchArgs.IsGuestLaunch(commandLineArgs);
        var isIncognitoLaunch = IncognitoLaunchArgs.IsIncognitoLaunch(commandLineArgs, out _, out _, out _);

        // Doit être posé AVANT toute création de fenêtre : c'est ce qui évite
        // que le navigateur principal et chaque application web (même exe,
        // Lumora.WinUI.exe) se retrouvent regroupés sous une seule icône dans
        // la barre des tâches / les épinglages Windows.
        ApplyExplicitAppUserModelId(webApp is not null
            ? WebAppIdentity.AppUserModelId(webApp.Id)
            : WebAppIdentity.BrowserAppUserModelId);

        // Mono-instance (2026-08-13, retour utilisateur) : relancer l'icône
        // Lumora pendant qu'une fenêtre normale tourne déjà lançait un
        // DEUXIÈME process Windows complet - celui-ci se comportait comme un
        // "vrai" démarrage et repurgeait les sessions non approuvées (voir
        // MainWindow.Sessions.cs), même si l'utilisateur n'avait jamais
        // fermé Lumora. Seul le lancement NORMAL (ni Incognito, ni Invité,
        // ni Appli web - chacun déjà son propre process par choix
        // d'isolation) redirige vers un process déjà actif du même profil,
        // exactement comme Chrome/Edge/Firefox : le process existant reçoit
        // la relance et ouvre une nouvelle fenêtre (MainWindow.NewWindow.cs),
        // au lieu qu'un nouveau process démarre pour de vrai.
        if (!isIncognitoLaunch && webApp is null && !startInGuestMode)
        {
            if (await TryRedirectToExistingInstanceAsync())
            {
                return;
            }
        }

        if (isIncognitoLaunch)
        {
            IncognitoLaunchArgs.IsIncognitoLaunch(commandLineArgs, out var torEnabled, out var incognitoUrl, out var returnToMain);
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

        _window = new MainWindow(startInGuestMode);
        WinUiRuntimeTrace.Write("MainWindow constructed");
        _window.Activate();
        WinUiRuntimeTrace.Write("MainWindow activated");
        ((MainWindow)_window).InitializeBrowserSurface();
        WinUiRuntimeTrace.Write("Browser surface initialized");
    }

    // Enregistre ce process comme LE process normal pour ce profil. Si un
    // autre process porte déjà cette clé, cette relance lui est redirigée et
    // CE process s'arrête sans jamais créer de fenêtre/WebView2 - retourne
    // true dans ce cas (l'appelant doit alors arrêter OnLaunched). Si c'est
    // ce process qui gagne l'enregistrement, s'abonne à Activated pour
    // ouvrir une nouvelle fenêtre à chaque future relance redirigée ici, et
    // retourne false (l'appelant continue le lancement normal).
    private async Task<bool> TryRedirectToExistingInstanceAsync()
    {
        try
        {
            var profileDir = LumoraProfilePaths.Default().ProfileDir;
            var keyHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(profileDir.ToLowerInvariant())))[..24];
            var instanceKey = "Lumora-" + keyHash;

            var keyedInstance = AppInstance.FindOrRegisterForKey(instanceKey);
            if (!keyedInstance.IsCurrent)
            {
                var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                await keyedInstance.RedirectActivationToAsync(activatedArgs);
                WinUiRuntimeTrace.Write("Launch redirected to existing Lumora instance");
                Application.Current.Exit();
                return true;
            }

            keyedInstance.Activated += OnExistingInstanceActivated;
            return false;
        }
        catch (Exception ex)
        {
            // Échec du mécanisme mono-instance (API indisponible, etc.) : on
            // repart sur un lancement normal plutôt que de bloquer l'app -
            // pire cas identique au comportement d'avant cette fonctionnalité.
            WinUiRuntimeTrace.Write($"Single-instance check skipped: {ex.GetType().Name}");
            return false;
        }
    }

    // Peut être appelé sur un thread hors UI (marshaling WinRT de
    // AppInstance.Activated) : toujours rebondir sur _uiDispatcherQueue avant
    // de toucher la moindre fenêtre.
    private void OnExistingInstanceActivated(object? sender, AppActivationArguments args)
    {
        _uiDispatcherQueue?.TryEnqueue(() =>
        {
            WinUiRuntimeTrace.Write("Redirected activation received, opening a new window");
            MainWindow.OpenNewWindowFromExternalActivation();
        });
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
