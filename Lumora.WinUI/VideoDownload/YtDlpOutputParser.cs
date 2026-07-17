using System.Text.RegularExpressions;

namespace Lumora.WinUI.VideoDownload;

// Extraction pure du chemin de fichier final depuis la sortie texte de
// yt-dlp. Priorite a la ligne de fusion ffmpeg (le fichier reellement ecrit
// sur disque quand video et audio sont fusionnes), puis repli sur les lignes
// "Destination:" pour un telechargement direct sans fusion. Ne fait aucune
// IO : l'appelant decide quel chemin candidat verifier sur le disque.
internal static class YtDlpOutputParser
{
    private static readonly Regex MergerLine = new(
        """\[Merger\]\s+Merging formats into\s+"(?<path>.+)"$""",
        RegexOptions.Compiled);

    public static IEnumerable<string> CandidatePaths(string output)
    {
        var lines = output.Split('\n');

        foreach (var line in lines.Reverse())
        {
            var match = MergerLine.Match(line.TrimEnd('\r'));
            if (match.Success)
            {
                yield return match.Groups["path"].Value.Trim();
            }
        }

        const string marker = "Destination: ";
        foreach (var line in lines.Reverse())
        {
            var trimmed = line.TrimEnd('\r');
            var index = trimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                yield return trimmed[(index + marker.Length)..].Trim();
            }
        }
    }
}
