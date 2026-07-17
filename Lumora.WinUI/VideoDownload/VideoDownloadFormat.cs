namespace Lumora.WinUI.VideoDownload;

internal enum VideoQuality
{
    Best,
    Q1080,
    Q720,
    Q480
}

// Construction pure du selecteur de format (-f) yt-dlp pour une qualite
// demandee par l'utilisateur. Le filtre height<=X laisse yt-dlp se rabattre
// automatiquement sur la resolution disponible la plus proche si la video
// n'existe pas dans la qualite demandee. Aucune IO : testable independamment
// du processus yt-dlp.
internal static class VideoDownloadFormat
{
    public static string BuildFormatSelector(VideoQuality quality, bool hasFfmpeg)
    {
        var heightFilter = quality switch
        {
            VideoQuality.Q1080 => "[height<=1080]",
            VideoQuality.Q720 => "[height<=720]",
            VideoQuality.Q480 => "[height<=480]",
            _ => string.Empty
        };

        return hasFfmpeg
            ? $"bv*{heightFilter}+ba/b{heightFilter}"
            : $"best{heightFilter}[ext=mp4]/best{heightFilter}";
    }

    public static string Label(VideoQuality quality) => quality switch
    {
        VideoQuality.Q1080 => "1080p",
        VideoQuality.Q720 => "720p",
        VideoQuality.Q480 => "480p",
        _ => "Meilleure qualite disponible"
    };
}
