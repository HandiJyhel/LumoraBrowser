namespace Lumora.WinUI;

// Configuration process-wide de WebView2 (dossier de profil + anti-télémétrie
// moteur), à faire AVANT toute création de WebView2 dans le process. Partagée
// entre la fenêtre principale et les fenêtres d'application web : les deux
// doivent pointer vers le même profil pour partager cookies et sessions.
internal static class WebView2Bootstrap
{
    // Version du runtime WebView2 "Fixed Version" embarque avec l'app (dossier
    // Lumora.WinUI\FixedRuntime\<version>\, copie au build via Lumora.WinUI.csproj).
    // A synchroniser avec la valeur WebView2FixedVersion du csproj et avec le
    // hash SHA256 epingle dans scripts/prepare-webview2-fixedversion.ps1 -
    // les trois doivent toujours pointer vers le meme runtime telecharge. Voir
    // ce script pour la procedure de mise a jour (nouvelle version = nouveau
    // telechargement manuel + nouveau hash, comme pour TorTrustedRelease).
    private const string FixedRuntimeVersion = "REPLACE_WITH_DOWNLOADED_VERSION";

    private static bool _configured;

    public static void ConfigureOnce(string browserDataDir, bool webRtcLeakProtectionEnabled = true)
    {
        if (_configured) return;
        _configured = true;

        try
        {
            // Runtime WebView2 embarque (mode "Fixed Version") : l'app est
            // non empaquetee (WindowsPackageType=None), donc pas de
            // Package.Current.InstalledLocation - le dossier de base du
            // process (a cote de Lumora.WinUI.exe) est la bonne reference.
            // Sans cette variable, WebView2 chercherait le runtime Evergreen
            // installe sur la machine - exactement ce qu'on veut eviter pour
            // que l'installateur n'ait rien a telecharger.
            var fixedRuntimeDir = Path.Combine(AppContext.BaseDirectory, "FixedRuntime", FixedRuntimeVersion);
            if (Directory.Exists(fixedRuntimeDir))
            {
                Environment.SetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", fixedRuntimeDir);
            }
        }
        catch { }

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
