namespace Lumora.WinUI;

// Configuration process-wide de WebView2 (dossier de profil + anti-télémétrie
// moteur), à faire AVANT toute création de WebView2 dans le process. Partagée
// entre la fenêtre principale et les fenêtres d'application web : les deux
// doivent pointer vers le même profil pour partager cookies et sessions.
internal static class WebView2Bootstrap
{
    private static bool _configured;

    public static void ConfigureOnce(string browserDataDir, bool webRtcLeakProtectionEnabled = true)
    {
        if (_configured) return;
        _configured = true;

        try { Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", browserDataDir); } catch { }

        try
        {
            // Les arguments déjà posés dans l'environnement (diagnostic,
            // vérification pilotée) sont CONSERVÉS : on ajoute nos drapeaux
            // anti-télémétrie au lieu d'écraser la variable.
            var external = Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS");
            var antiTelemetry = "--disable-crash-reporter --disable-breakpad --disable-domain-reliability --no-pings";
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
                string.IsNullOrWhiteSpace(external) ? antiTelemetry : $"{antiTelemetry} {external.Trim()}");
        }
        catch { }
    }
}
