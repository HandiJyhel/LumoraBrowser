namespace Lumora.WinUI.VideoDownload;

// Recherche read-only d'un ffmpeg deja present sur la machine, pour permettre
// a yt-dlp de fusionner les flux video/audio separes que YouTube sert au-dela
// d'environ 720p. Aucune installation : si ffmpeg est absent, l'appelant doit
// se rabattre sur un format deja fusionne (qualite plus limitee).
internal static class FfmpegLocator
{
    public static string? FindLocalFfmpeg()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("LUMORA_FFMPEG_PATH") ?? string.Empty,
            Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Lumora", "tools", "ffmpeg.exe")
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch { }
        }

        return null;
    }
}
