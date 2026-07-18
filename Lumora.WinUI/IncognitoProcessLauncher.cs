using System.Diagnostics;

namespace Lumora.WinUI;

// Lance une nouvelle fenetre Incognito dans un process Windows dedie, distinct
// du process appelant (MainWindow ou une autre fenetre Incognito). Voir
// LumoraIncognitoWindow pour le pourquoi : un deuxieme environnement WebView2
// explicite dans le meme process que l'appelant s'est revele peu fiable sur au
// moins une installation.
internal static class IncognitoProcessLauncher
{
    public static void Launch(string? url = null, bool torEnabled = false)
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
            startInfo.ArgumentList.Add(IncognitoLaunchArgs.IncognitoFlag);
            if (torEnabled)
            {
                startInfo.ArgumentList.Add(IncognitoLaunchArgs.TorFlag);
            }
            if (!string.IsNullOrWhiteSpace(url))
            {
                startInfo.ArgumentList.Add($"{IncognitoLaunchArgs.UrlPrefix}{url}");
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"IncognitoProcessLauncher.Launch failed: {ex.GetType().Name}");
        }
    }
}
