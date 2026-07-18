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

    public static bool IsIncognitoLaunch(IEnumerable<string> args, out bool torEnabled, out string? url)
    {
        torEnabled = false;
        url = null;
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
