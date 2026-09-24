using System.Diagnostics;

namespace Lumora.WinUI;

// Ouvre une adresse web dans le navigateur Lumora principal depuis une fenêtre
// d'application web. Même chemin que Windows quand Lumora est le navigateur
// par défaut : relance de l'exe avec l'URL en argument brut (UrlLaunchArgs),
// redirigée vers l'instance principale déjà active (App.OnLaunched,
// TryRedirectToExistingInstanceAsync) ou démarrant le navigateur sinon.
// L'adresse arrive ainsi dans un onglet normal, avec toutes les protections
// (bloqueur, anti-empreinte, Coffre...), jamais dans une fenêtre WebView2 brute.
internal static class MainBrowserLauncher
{
    // Mêmes variables que IncognitoProcessLauncher : héritées, elles
    // imposeraient au navigateur principal les réglages WebView2 de CE process.
    private static readonly string[] InheritedWebView2Vars =
    {
        "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
        "WEBVIEW2_USER_DATA_FOLDER",
    };

    public static bool OpenUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        if (MainWindow.TryOpenUrlInExistingWindow(parsed.AbsoluteUri))
        {
            return true;
        }

        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath)) return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(parsed.AbsoluteUri);
            foreach (var name in InheritedWebView2Vars)
            {
                startInfo.EnvironmentVariables.Remove(name);
            }

            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"MainBrowserLauncher.OpenUrl failed: {ex.GetType().Name}");
            return false;
        }
    }
}
