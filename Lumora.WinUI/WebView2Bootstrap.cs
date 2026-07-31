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
            var flags = "--disable-crash-reporter --disable-breakpad --disable-domain-reliability --no-pings";
            // Privacy Sandbox (Topics API + Protected Audience/FLEDGE) : ce sont les
            // API de ciblage publicitaire de Chromium, actives par defaut. Coupees
            // ici car elles n'ont pas leur place dans un navigateur qui promet
            // l'absence de tracking - constate le 2026-07-29 via les fichiers
            // InterestGroups/BrowsingTopicsSiteData crees dans le profil meme sans
            // usage. Effet secondaire utile : ces fichiers ne sont plus crees.
            flags += " --disable-features=BrowsingTopics,InterestGroupStorage,AdInterestGroupAPI,Fledge,PrivacySandboxSettings4,PrivacySandboxAdsAPIsOverride";
            // Anti-fuite WebRTC : interdit les candidats ICE UDP non proxifies, qui
            // reveleraient sinon l'IP locale/publique reelle a n'importe quel site
            // utilisant WebRTC (appel video ou simple fingerprinting).
            if (webRtcLeakProtectionEnabled)
            {
                flags += " --force-webrtc-ip-handling-policy=disable_non_proxied_udp";
            }
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
                string.IsNullOrWhiteSpace(external) ? flags : $"{flags} {external.Trim()}");
        }
        catch { }
    }
}
