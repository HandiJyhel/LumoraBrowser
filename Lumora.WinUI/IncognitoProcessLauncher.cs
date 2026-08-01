using System.Diagnostics;

namespace Lumora.WinUI;

// Lance une nouvelle fenetre Incognito dans un process Windows dedie, distinct
// du process appelant (MainWindow ou une autre fenetre Incognito). Voir
// LumoraIncognitoWindow pour le pourquoi : un deuxieme environnement WebView2
// explicite dans le meme process que l'appelant s'est revele peu fiable sur au
// moins une installation.
internal static class IncognitoProcessLauncher
{
    // Variables WebView2 que chaque process Lumora doit toujours recalculer
    // lui-meme (voir WebView2Bootstrap.ConfigureOnce / LumoraIncognitoWindow.
    // ConfigureProcessWebView) - jamais les heriter d'un process parent. Un
    // nouveau process Windows herite par defaut de TOUT l'environnement de
    // son parent : sans ce nettoyage, une fenetre Incognito+Tor qui relance
    // MainWindow (ou une autre fenetre Incognito) lui transmettait son propre
    // WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS (dont le --proxy-server Tor) et
    // son WEBVIEW2_USER_DATA_FOLDER - le nouveau process demarrait alors avec
    // le proxy Tor d'un tor.exe qui ne tournait plus dans CE process,
    // ERR_PROXY_CONNECTION_FAILED sur toute navigation, meme en mode normal.
    private static readonly string[] InheritedWebView2Vars =
    {
        "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
        "WEBVIEW2_USER_DATA_FOLDER",
    };

    public static void Launch(string? url = null, bool torEnabled = false, bool returnToMain = false)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath)) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
            };
            foreach (var arg in BuildArguments(url, torEnabled, returnToMain))
            {
                startInfo.ArgumentList.Add(arg);
            }
            foreach (var name in InheritedWebView2Vars)
            {
                startInfo.EnvironmentVariables.Remove(name);
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"IncognitoProcessLauncher.Launch failed: {ex.GetType().Name}");
        }
    }

    // Relance une fenetre Lumora normale (aucun argument Incognito) : utilise
    // quand la derniere fenetre Incognito se ferme apres avoir remplace
    // MainWindow (voir MainWindow.Incognito.cs / LumoraIncognitoWindow), pour
    // ne jamais laisser l'utilisateur sans aucune fenetre Lumora ouverte. La
    // session (onglets) est restauree normalement au demarrage, comme pour
    // tout lancement de Lumora.
    public static void LaunchMainWindow()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath)) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
            };
            foreach (var name in InheritedWebView2Vars)
            {
                startInfo.EnvironmentVariables.Remove(name);
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"IncognitoProcessLauncher.LaunchMainWindow failed: {ex.GetType().Name}");
        }
    }

    // Logique pure (testable) de construction des arguments : separee de Launch,
    // qui demarre un vrai process et n'est pas testable sans effet de bord.
    internal static IReadOnlyList<string> BuildArguments(string? url, bool torEnabled, bool returnToMain = false)
    {
        var args = new List<string> { IncognitoLaunchArgs.IncognitoFlag };
        if (torEnabled)
        {
            args.Add(IncognitoLaunchArgs.TorFlag);
        }
        if (returnToMain)
        {
            args.Add(IncognitoLaunchArgs.ReturnToMainFlag);
        }
        if (!string.IsNullOrWhiteSpace(url))
        {
            args.Add($"{IncognitoLaunchArgs.UrlPrefix}{url}");
        }

        return args;
    }
}
