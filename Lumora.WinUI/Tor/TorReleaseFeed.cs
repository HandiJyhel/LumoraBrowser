using System.Text.RegularExpressions;

namespace Lumora.WinUI.Tor;

internal readonly record struct TorReleaseVersion(string Text, Version Value);

// Lecture de l'index (autoindex Apache) de https://dist.torproject.org/torbrowser/
// pour en tirer les versions STABLES publiees, de la plus recente a la plus
// ancienne. Logique pure (texte -> versions), sans reseau : testable avec des
// extraits reels de l'index.
internal static partial class TorReleaseFeed
{
    // Une version stable est "16.0.3" ou "16.0" : jamais "16.0a14" (alpha) ni
    // "16.0b1" (beta), jamais d'autre dossier de l'index.
    [GeneratedRegex(@"href=""(?<v>\d+\.\d+(?:\.\d+)?)/""", RegexOptions.CultureInvariant)]
    private static partial Regex StableVersionLink();

    public static IReadOnlyList<TorReleaseVersion> ParseStableVersions(string indexHtml)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var versions = new List<TorReleaseVersion>();
        foreach (Match match in StableVersionLink().Matches(indexHtml))
        {
            var text = match.Groups["v"].Value;
            if (seen.Add(text) && TryParse(text, out var version))
            {
                versions.Add(new TorReleaseVersion(text, version));
            }
        }

        versions.Sort((a, b) => b.Value.CompareTo(a.Value));
        return versions;
    }

    public static bool TryParse(string text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (!Version.TryParse(text, out var parsed))
        {
            return false;
        }

        // "16.0" et "16.0.0" sont la meme version : normalise a 3 composantes
        // pour que la comparaison avec la version installee soit exacte.
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
        return true;
    }
}
