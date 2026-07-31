namespace Lumora.WinUI;

// Analyse des arguments de ligne de commande utilises pour lancer une session
// invite dans un process Windows dedie (voir GuestProcessLauncher et
// MainWindow.Profile.cs/SkipProfileButton_Click). Meme principe que
// IncognitoLaunchArgs/WebAppLaunchArgs : necessaire car WEBVIEW2_USER_DATA_FOLDER
// ne peut etre pose qu'une seule fois par process, avant toute creation de
// WebView2 - impossible de "devenir invite" a chaud dans le process de la
// fenetre de connexion deja demarree avec le profil par defaut.
internal static class GuestLaunchArgs
{
    public const string GuestFlag = "--guest";

    public static bool IsGuestLaunch(IEnumerable<string> args) =>
        args.Any(arg => arg is not null && arg.Trim().Equals(GuestFlag, StringComparison.OrdinalIgnoreCase));
}
