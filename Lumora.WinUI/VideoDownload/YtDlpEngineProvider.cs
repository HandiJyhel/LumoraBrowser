using System.Net.Http;
using System.Security.Cryptography;

namespace Lumora.WinUI.VideoDownload;

// Recherche et installation à la demande du moteur yt-dlp (open source, licence
// Unlicense) utilisé par le module de téléchargement vidéo. Rien n'est jamais
// téléchargé automatiquement : DownloadEngineAsync n'est appelé que sur un clic
// explicite de l'utilisateur. Contrairement à FilterListManager/TranslationService
// (fichiers passifs), on vérifie ici le SHA256 publié par yt-dlp avant d'écrire le
// binaire sur disque, car ce fichier sera ensuite exécuté.
internal static class YtDlpEngineProvider
{
    private const string LatestExeUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string LatestChecksumsUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS";

    private static readonly string InstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lumora", "tools");

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders = { { "User-Agent", "Lumora/1.0 (yt-dlp engine installer)" } }
    };

    public static string? FindLocalEngine()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("LUMORA_YTDLP_PATH") ?? string.Empty,
            Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp.exe"),
            Path.Combine(InstallDir, "yt-dlp.exe")
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
                var candidate = Path.Combine(dir.Trim(), "yt-dlp.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch { }
        }

        return null;
    }

    // Télécharge yt-dlp.exe depuis la release GitHub officielle, vérifie son SHA256
    // face à SHA2-256SUMS (publié par le même release), puis l'installe dans
    // %LOCALAPPDATA%\Lumora\tools. Lève une exception avec un message explicite
    // en cas d'échec (réseau, hash invalide) sans jamais installer un fichier
    // non vérifié.
    public static async Task<string> DownloadEngineAsync(IProgress<string>? progress, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(InstallDir);
        var destination = Path.Combine(InstallDir, "yt-dlp.exe");
        var tempPath = destination + ".part";

        progress?.Report("Verification du hash officiel (SHA2-256SUMS)...");
        var expectedHash = await FetchExpectedHashAsync(cancellationToken);

        progress?.Report("Telechargement de yt-dlp.exe (github.com/yt-dlp/yt-dlp)...");
        using (var response = await Http.GetAsync(LatestExeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(tempPath);
            await input.CopyToAsync(output, cancellationToken);
        }

        progress?.Report("Vérification de l'intégrité du fichier téléchargé...");
        var actualHash = await ComputeSha256Async(tempPath, cancellationToken);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(tempPath);
            throw new InvalidOperationException(
                "Le fichier téléchargé ne correspond pas au hash officiel publié par yt-dlp. Installation annulée par sécurité.");
        }

        File.Move(tempPath, destination, overwrite: true);
        return destination;
    }

    private static async Task<string> FetchExpectedHashAsync(CancellationToken cancellationToken)
    {
        var sums = await Http.GetStringAsync(LatestChecksumsUrl, cancellationToken);
        return ParseExpectedHash(sums, "yt-dlp.exe")
            ?? throw new InvalidOperationException(
                "Impossible de trouver le hash officiel de yt-dlp.exe dans SHA2-256SUMS.");
    }

    internal static string? ParseExpectedHash(string sha256SumsContent, string fileName)
    {
        foreach (var rawLine in sha256SumsContent.Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var parts = trimmed.Split((char[]?)[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var name = parts[^1].TrimStart('*');
            if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                return parts[0];
            }
        }

        return null;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
