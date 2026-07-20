using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace Lumora.WinUI.Tor;

// Telechargement et installation du moteur Tor a la demande. Rien n'est
// jamais telecharge automatiquement : DownloadEngineAsync n'est appele que
// sur un clic explicite de l'utilisateur (LumoraIncognitoWindow). Meme
// philosophie que YtDlpEngineProvider : double verification (archive puis
// binaire extrait) avant d'ecrire quoi que ce soit dans le profil, car ce
// fichier sera ensuite execute par TorProcessManager.
internal static class TorEngineProvider
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders = { { "User-Agent", "Lumora/1.0 (Tor engine installer)" } }
    };

    public static async Task DownloadEngineAsync(
        LumoraProfilePaths profile, IProgress<string>? progress, CancellationToken cancellationToken = default)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "LumoraTorInstall", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            var archivePath = Path.Combine(workDir, TorTrustedRelease.ArchiveFileName);

            progress?.Report($"Telechargement du moteur Tor {TorTrustedRelease.Version} (dist.torproject.org)...");
            using (var response = await Http.GetAsync(
                TorTrustedRelease.ArchiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(archivePath);
                await input.CopyToAsync(output, cancellationToken);
            }

            progress?.Report("Verification de l'integrite de l'archive...");
            var archiveHash = await ComputeSha256Async(archivePath, cancellationToken);
            if (!string.Equals(archiveHash, TorTrustedRelease.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "L'archive telechargee ne correspond pas au hash officiel epingle. Installation annulee par securite.");
            }

            progress?.Report("Extraction du moteur Tor...");
            var extractedExePath = Path.Combine(workDir, "tor.exe");
            if (!TryExtractTorExecutable(archivePath, extractedExePath))
            {
                throw new InvalidOperationException("tor.exe introuvable dans l'archive officielle.");
            }

            progress?.Report("Verification du binaire extrait...");
            var exeHash = await ComputeSha256Async(extractedExePath, cancellationToken);
            if (!TorTrustedRelease.TrustedFileHashes.TryGetValue("tor.exe", out var expectedExeHash)
                || !string.Equals(exeHash, expectedExeHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Le moteur Tor extrait ne correspond pas a la version verifiee. Installation annulee par securite.");
            }

            var torDir = TorProcessManager.ExpectedDirectory(profile);
            Directory.CreateDirectory(torDir);
            File.Copy(extractedExePath, TorProcessManager.ExpectedExecutablePath(profile), overwrite: true);
            progress?.Report("Moteur Tor installe.");
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { }
        }
    }

    // Extrait uniquement l'entree "tor/tor.exe" du tar.gz, en ignorant le
    // reste (transports enfichables, tor-gencert.exe) : non references par
    // TorProcessManager, donc pas installes.
    internal static bool TryExtractTorExecutable(string archivePath, string destinationPath)
    {
        using var fileStream = File.OpenRead(archivePath);
        using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzip);

        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            if (entry.EntryType == TarEntryType.RegularFile &&
                string.Equals(entry.Name, "tor/tor.exe", StringComparison.Ordinal))
            {
                entry.ExtractToFile(destinationPath, overwrite: true);
                return true;
            }
        }

        return false;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
