namespace Lumora.WinUI;

// Suppression recursive d'un dossier avec nouvelles tentatives : extrait en
// module pur (session "Compte et ouverture", 2026-08-14) suite a un bug reel
// trouve par l'utilisateur - le bouton "Reinitialiser ce profil..."
// (MainWindow.Profile.cs, ResetProfileButton_Click) supprimait le dossier de
// profil SANS aucune nouvelle tentative ni retour d'erreur, alors que le
// moteur WebView2 garde souvent ses fichiers (Cache, LevelDB...) verrouilles
// quelques instants apres sa fermeture - Directory.Delete echouait donc en
// silence, sans que l'utilisateur sache pourquoi rien ne s'etait passe.
//
// Reprend exactement la logique deja eprouvee de
// DeleteGuestSessionDirectoryWithRetry (MainWindow.xaml.cs, nettoyage du
// dossier ephemere invite) - centralisee ici pour que les deux appelants
// partagent un seul mecanisme teste, au lieu de deux copies informelles.
internal static class RetryDelete
{
    public static bool TryDeleteDirectory(string path, int maxAttempts, int delayMs, out Exception? lastError)
    {
        lastError = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                if (!Directory.Exists(path)) return true;
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                Thread.Sleep(delayMs);
            }
        }
        return !Directory.Exists(path);
    }
}
