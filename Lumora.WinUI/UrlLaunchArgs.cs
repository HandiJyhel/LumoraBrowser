using System.Text;

namespace Lumora.WinUI;

// Logique pure : extrait une URL http(s) depuis les arguments de lancement.
// Utilisé quand Windows invoque Lumora comme navigateur par défaut (clic sur
// un lien depuis une autre app, "Ouvrir avec"...) - la commande enregistrée
// est "<exe>" "%1" (voir RegisterAsDefaultBrowserCandidate dans
// scripts/installer/Program.cs.template), donc Windows lance le process avec
// l'URL comme argument brut. Sans cette lecture, Lumora s'ouvrirait sur sa
// page de démarrage habituelle en ignorant totalement le lien cliqué (bug
// signalé par l'utilisateur le 2026-08-28 - voir MEMORY.md).
internal static class UrlLaunchArgs
{
    public static string? TryParseUrl(IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            if (arg is null) continue;
            var trimmed = arg.Trim().Trim('"');
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
        }

        return null;
    }

    // Variante pour une relance redirigée vers une instance déjà active
    // (App.xaml.cs/OnExistingInstanceActivated) : contrairement à
    // Environment.GetCommandLineArgs() (tableau), l'API de réactivation livre
    // tous les arguments comme UNE seule chaîne
    // (Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs.Arguments).
    // Découpage minimal ci-dessous, suffisant ici : un seul argument utile
    // (l'URL, entre guillemets) est attendu dans ce cas précis, jamais
    // d'échappement complexe à gérer.
    public static string? TryParseUrl(string? rawArguments)
    {
        if (string.IsNullOrWhiteSpace(rawArguments)) return null;
        return TryParseUrl(SplitArguments(rawArguments));
    }

    private static IEnumerable<string> SplitArguments(string commandLine)
    {
        var current = new StringBuilder();
        var inQuotes = false;
        foreach (var c in commandLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0) yield return current.ToString();
    }
}
