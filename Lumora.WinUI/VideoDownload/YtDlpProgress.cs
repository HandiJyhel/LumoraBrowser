using System.Globalization;
using System.Text.RegularExpressions;

namespace Lumora.WinUI.VideoDownload;

// Parsing pur d'une ligne de progression yt-dlp (avec --newline), du style
// "[download]  45.2% of   10.00MiB at  1.20MiB/s ETA 00:07". Ne fait aucune IO :
// juste de l'extraction de texte, testable sans lancer de processus.
internal static class YtDlpProgress
{
    private static readonly Regex ProgressLine = new(
        @"^\[download\]\s+(?<percent>[\d.]+)%(?:\s+of\s+~?\s*(?<size>[\d.]+)\s*(?<unit>[KMGT]i)?B)?",
        RegexOptions.Compiled);

    public static bool TryParse(string? line, out double percent, out long totalBytes)
    {
        percent = 0;
        totalBytes = 0;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = ProgressLine.Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (!double.TryParse(match.Groups["percent"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out percent))
        {
            return false;
        }

        var sizeGroup = match.Groups["size"];
        if (sizeGroup.Success && double.TryParse(sizeGroup.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
        {
            var multiplier = match.Groups["unit"].Value switch
            {
                "Ki" => 1024L,
                "Mi" => 1024L * 1024,
                "Gi" => 1024L * 1024 * 1024,
                "Ti" => 1024L * 1024 * 1024 * 1024,
                _ => 1L
            };
            totalBytes = (long)(size * multiplier);
        }

        return true;
    }
}
