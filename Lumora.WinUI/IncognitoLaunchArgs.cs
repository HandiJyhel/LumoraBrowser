namespace Lumora.WinUI;

// Analyse des arguments de ligne de commande utilises pour lancer une fenetre
// Incognito dans un process Windows dedie (voir LumoraIncognitoWindow et
// IncognitoProcessLauncher). Meme principe que WebAppLaunchArgs pour les
// applications web epinglees.
internal static class IncognitoLaunchArgs
{
    public const string IncognitoFlag = "--incognito";
    public const string TorFlag = "--incognito-tor";
    public const string UrlPrefix = "--incognito-url=";
    // Pose par MainWindow quand elle se ferme pour laisser la place a
    // Incognito (voir MainWindow.Incognito.cs) : signale a la fenetre
    // Incognito qu'elle doit relancer une fenetre normale a sa propre
    // fermeture, pour ne jamais laisser l'utilisateur sans aucune fenetre
    // Lumora ouverte. Absent quand Incognito est ouvert en plus de
    // MainWindow (ce cas n'existe plus aujourd'hui, mais le flag reste
    // explicite plutot qu'implicite).
    public const string ReturnToMainFlag = "--incognito-return-to-main";

    public static bool IsIncognitoLaunch(
        IEnumerable<string> args, out bool torEnabled, out string? url, out bool returnToMain)
    {
        torEnabled = false;
        url = null;
        returnToMain = false;
        var found = false;

        foreach (var arg in args)
        {
            if (arg is null) continue;
            var trimmed = arg.Trim();

            if (trimmed.Equals(TorFlag, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                torEnabled = true;
            }
            else if (trimmed.Equals(ReturnToMainFlag, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                returnToMain = true;
            }
            else if (trimmed.Equals(IncognitoFlag, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
            }
            else if (trimmed.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var raw = trimmed[UrlPrefix.Length..].Trim().Trim('"');
                url = string.IsNullOrWhiteSpace(raw) ? null : raw;
            }
        }

        return found;
    }
}
