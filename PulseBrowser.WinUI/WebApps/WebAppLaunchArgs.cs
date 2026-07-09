namespace PulseBrowser.WinUI;

// Logique pure : extrait l'identifiant d'application web depuis les arguments
// de ligne de commande (--app=<id>), utilisés par le raccourci Menu Démarrer.
internal static class WebAppLaunchArgs
{
    private const string Prefix = "--app=";

    public static string? TryParseAppId(IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            if (arg is null) continue;
            var trimmed = arg.Trim();
            if (trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                var id = trimmed[Prefix.Length..].Trim().Trim('"');
                return string.IsNullOrWhiteSpace(id) ? null : id;
            }
        }

        return null;
    }
}
