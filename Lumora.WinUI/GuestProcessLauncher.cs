using System.Diagnostics;

namespace Lumora.WinUI;

// Lance une session invite dans un process Windows dedie, avec un dossier de
// profil ephemere (WebView2 compris) : voir LumoraProfilePaths.Default(), qui
// priorise la variable d'environnement LUMORA_PROFILE_DIR posee ici.
//
// Avant cette classe, choisir "Continuer sans profil" continuait dans le MEME
// process que l'ecran de connexion : WEBVIEW2_USER_DATA_FOLDER pointait deja
// vers le profil "par defaut" (fige tout au debut du process, avant meme le
// choix de l'utilisateur), donc cookies/cache/IndexedDB de WebView2
// survivaient reellement a la fermeture malgre le message affiche ("Aucune
// donnee persistante n'est gardee"). Impossible de corriger ca a chaud dans
// le meme process (meme contrainte que LumoraIncognitoWindow, voir son
// commentaire de classe) : la seule solution fiable est un nouveau process,
// lance AVANT toute creation de WebView2, avec la variable d'environnement
// deja positionnee vers un dossier temporaire.
internal static class GuestProcessLauncher
{
    private static string SessionRoot => Path.Combine(Path.GetTempPath(), "LumoraGuest");

    public static void Launch()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath)) return;

            var sessionDir = Path.Combine(SessionRoot, Guid.NewGuid().ToString("N"));

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(GuestLaunchArgs.GuestFlag);
            startInfo.EnvironmentVariables[LumoraProfilePaths.ProfileDirectoryEnvironmentVariable] = sessionDir;

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"GuestProcessLauncher.Launch failed: {ex.GetType().Name}");
        }
    }

    // Filet de securite : si une session invite precedente a plante ou a ete
    // tuee brutalement (Gestionnaire des taches, coupure de courant...), son
    // dossier temporaire a pu survivre. Nettoye au demarrage de la suivante -
    // meme principe que LumoraIncognitoWindow.CleanupStaleSessionFolders.
    public static void CleanupStaleSessionFolders()
    {
        try
        {
            if (!Directory.Exists(SessionRoot)) return;
            foreach (var dir in Directory.GetDirectories(SessionRoot))
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* encore verrouille par une autre session invite active */ }
            }
        }
        catch { }
    }
}
