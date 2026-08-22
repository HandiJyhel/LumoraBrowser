using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace Lumora.WinUI.Tor;

// Telechargement et installation du moteur Tor a la demande. Rien n'est
// jamais telecharge automatiquement : DownloadEngineAsync n'est appele que
// sur un clic explicite de l'utilisateur (LumoraIncognitoWindow) ou une case
// cochee explicitement dans l'installateur (scripts/installer). Meme
// philosophie que YtDlpEngineProvider : double verification (archive puis
// binaire extrait) avant d'ecrire quoi que ce soit sur le disque, car ce
// fichier sera ensuite execute par TorProcessManager.
//
// Volontairement decouple de LumoraProfilePaths (prend un chemin de
// destination direct) : ce fichier est aussi copie tel quel dans
// l'installateur autonome (scripts/build-installer.ps1), qui ne depend pas
// du reste de l'app WinUI et n'a pas de profil au moment ou il tourne.
internal static class TorEngineProvider
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders = { { "User-Agent", "Lumora/1.0 (Tor engine installer)" } }
    };

    // destinationExePath : chemin complet ou ecrire tor.exe (le dossier parent
    // est cree s'il n'existe pas). L'appelant decide de l'emplacement -
    // TorProcessManager.ExpectedExecutablePath(profile) depuis l'app,
    // %LocalAppData%\Lumora\profiles\default\tor\tor.exe calcule directement
    // depuis l'installateur (voir Program.cs.template, EnsureTorEngine).
    public static async Task DownloadEngineAsync(
        string destinationExePath, IProgress<string>? progress, CancellationToken cancellationToken = default)
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
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // dist.torproject.org ne conserve qu'un nombre limite de versions en
                    // ligne : la version epinglee dans TorTrustedRelease (verifiee a la
                    // main par signature GPG lors de sa mise en place) finit par en etre
                    // retiree - deja constate deux fois (15.0.18 le 2026-07-22, 15.0.19 le
                    // 2026-08-21). Un 404 brut ("Response status code does not indicate
                    // success") ne dit pas ca a l'utilisateur ; message explicite a la place.
                    throw new InvalidOperationException(
                        $"Le moteur Tor {TorTrustedRelease.Version} n'est plus disponible sur le miroir officiel dist.torproject.org (version retiree). " +
                        "Une mise à jour de Lumora est nécessaire pour pointer vers une version plus récente et vérifiée.");
                }

                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(archivePath);
                await input.CopyToAsync(output, cancellationToken);
            }

            progress?.Report("Vérification de l'intégrité de l'archive...");
            var archiveHash = await ComputeSha256Async(archivePath, cancellationToken);
            if (!string.Equals(archiveHash, TorTrustedRelease.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "L'archive téléchargée ne correspond pas au hash officiel épinglé. Installation annulée par sécurité.");
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
                    "Le moteur Tor extrait ne correspond pas à la version vérifiée. Installation annulée par sécurité.");
            }

            var torDir = Path.GetDirectoryName(destinationExePath)
                ?? throw new InvalidOperationException("Chemin de destination du moteur Tor invalide.");
            Directory.CreateDirectory(torDir);
            File.Copy(extractedExePath, destinationExePath, overwrite: true);
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
