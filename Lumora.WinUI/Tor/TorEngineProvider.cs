using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Lumora.WinUI.Tor;

internal sealed record TorEngineReceipt(string Version, string TorExeSha256);

internal sealed record TorUpdateCheck(
    string? InstalledVersion, string? LatestVersion, bool UpdateAvailable, string? Problem);

// Telechargement, verification et installation du moteur Tor a la demande.
// Rien n'est jamais telecharge en arriere-plan : DownloadEngineAsync et
// CheckForUpdateAsync ne sont appeles que sur une action explicite de
// l'utilisateur (installer Tor, ou activer Tor dans LumoraIncognitoWindow) ou
// une case cochee dans l'installateur (scripts/installer).
//
// Depuis 1.0.1, la version n'est plus epinglee : on lit la derniere version
// stable sur dist.torproject.org et on ne l'installe que si sa signature
// OpenPGP est valide pour la cle de signature officielle epinglee (voir
// TorTrustedRelease et TorSignatureVerifier). Les SHA256 de l'archive et de
// tor.exe sont ensuite consignes dans un recu local, reverifie a chaque
// lancement par TorProcessManager.
//
// Volontairement decouple de LumoraProfilePaths (prend un chemin de
// destination direct) : ce fichier est aussi copie tel quel dans
// l'installateur autonome (scripts/build-installer.ps1), qui ne depend pas
// du reste de l'app WinUI et n'a pas de profil au moment ou il tourne.
internal static class TorEngineProvider
{
    public const string ReceiptFileName = "engine-receipt.json";

    // Nombre de versions les plus recentes essayees avant d'abandonner : un
    // dossier de version peut apparaitre sur le miroir avant que ses fichiers
    // y soient tous publies (404 transitoire sur la toute derniere).
    private const int MaxCandidateVersions = 3;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders = { { "User-Agent", "Lumora/1.0 (Tor engine installer)" } }
    };

    public static string ReceiptPath(string exePath) =>
        Path.Combine(Path.GetDirectoryName(exePath) ?? ".", ReceiptFileName);

    // ---- Recu local -------------------------------------------------------

    public static TorEngineReceipt? ReadReceipt(string exePath)
    {
        try
        {
            var path = ReceiptPath(exePath);
            if (!File.Exists(path))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var version = doc.RootElement.GetProperty("version").GetString();
            var hash = doc.RootElement.GetProperty("torExeSha256").GetString();
            return string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(hash)
                ? null
                : new TorEngineReceipt(version, hash.ToLowerInvariant());
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException
            or InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void WriteReceipt(string exePath, string version, string archiveSha256, string exeSha256)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["version"] = version,
            ["torExeSha256"] = exeSha256,
            ["archiveSha256"] = archiveSha256,
            ["installedUtc"] = DateTime.UtcNow.ToString("O"),
        }, new JsonSerializerOptions { WriteIndented = true });

        var path = ReceiptPath(exePath);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    // Version de Tor reellement installee : celle du recu, ou - pour une
    // installation de la 1.0.0 sans recu - la version historique si l'empreinte
    // de tor.exe est exactement celle verifiee a la main. null = inconnue.
    public static async Task<string?> GetInstalledVersionAsync(string exePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(exePath))
        {
            return null;
        }

        var exeHash = await ComputeSha256Async(exePath, cancellationToken);
        var receipt = ReadReceipt(exePath);
        if (receipt is not null && string.Equals(receipt.TorExeSha256, exeHash, StringComparison.OrdinalIgnoreCase))
        {
            return receipt.Version;
        }

        return TorTrustedRelease.LegacyTrustedFileHashes.TryGetValue("tor.exe", out var legacyHash)
            && string.Equals(legacyHash, exeHash, StringComparison.OrdinalIgnoreCase)
                ? TorTrustedRelease.LegacyVersion
                : null;
    }

    // ---- Recherche de mise a jour ----------------------------------------

    // Ne leve jamais : sans reseau, le moteur deja installe reste utilisable
    // (Problem decrit alors pourquoi la verification n'a pas eu lieu).
    public static async Task<TorUpdateCheck> CheckForUpdateAsync(
        string exePath, CancellationToken cancellationToken = default)
    {
        string? installed = null;
        try
        {
            installed = await GetInstalledVersionAsync(exePath, cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var versions = await FetchStableVersionsAsync(timeout.Token);
            var latest = versions.FirstOrDefault(v => v.Value >= TorTrustedRelease.MinimumVersion);
            if (latest.Text is null)
            {
                return new TorUpdateCheck(installed, null, false,
                    "Aucune version stable de Tor vérifiable n'est publiée sur le miroir officiel.");
            }

            // installed == null alors que tor.exe existe : binaire inconnu (altere,
            // ou version qui n'est ni celle d'un recu ni la version historique) ->
            // il ne demarrerait de toute facon pas, le reinstaller le remplace.
            var needsUpdate = installed is null
                || (TorReleaseFeed.TryParse(installed, out var installedValue) && latest.Value > installedValue);
            return new TorUpdateCheck(installed, latest.Text, needsUpdate, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            or OperationCanceledException or IOException or InvalidOperationException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new TorUpdateCheck(installed, null, false,
                $"Vérification de mise à jour impossible : {ex.Message}");
        }
    }

    private static async Task<IReadOnlyList<TorReleaseVersion>> FetchStableVersionsAsync(CancellationToken cancellationToken)
    {
        var html = await Http.GetStringAsync(TorTrustedRelease.MirrorBaseUrl, cancellationToken);
        return TorReleaseFeed.ParseStableVersions(html);
    }

    // ---- Installation / mise a jour --------------------------------------

    // destinationExePath : chemin complet ou ecrire tor.exe (le dossier parent
    // est cree s'il n'existe pas). L'appelant decide de l'emplacement -
    // TorProcessManager.ExpectedExecutablePath(profile) depuis l'app,
    // %LocalAppData%\Lumora\profiles\default\tor\tor.exe calcule directement
    // depuis l'installateur (voir Program.cs.template, EnsureTorEngine).
    // Installe la derniere version stable verifiee, en remplacant l'eventuelle
    // version precedente. Retourne la version installee.
    public static async Task<string> DownloadEngineAsync(
        string destinationExePath, IProgress<string>? progress, CancellationToken cancellationToken = default)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "LumoraTorInstall", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            progress?.Report("Recherche de la dernière version de Tor (dist.torproject.org)...");
            var candidates = (await FetchStableVersionsAsync(cancellationToken))
                .Where(v => v.Value >= TorTrustedRelease.MinimumVersion)
                .Take(MaxCandidateVersions)
                .ToList();
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    "Aucune version stable de Tor vérifiable n'est publiée sur le miroir officiel dist.torproject.org.");
            }

            string? archivePath = null;
            TorReleaseVersion chosen = default;
            foreach (var candidate in candidates)
            {
                progress?.Report($"Téléchargement du moteur Tor {candidate.Text} (dist.torproject.org)...");
                var path = Path.Combine(workDir, TorTrustedRelease.ArchiveFileName(candidate.Text));
                if (await TryDownloadAsync(TorTrustedRelease.ArchiveUrl(candidate.Text), path, cancellationToken))
                {
                    archivePath = path;
                    chosen = candidate;
                    break;
                }
            }

            if (archivePath is null)
            {
                throw new InvalidOperationException(
                    "Les dernières versions de Tor listées sur le miroir officiel ne sont pas téléchargeables pour le moment. Réessaie plus tard.");
            }

            var installedVersion = await GetInstalledVersionAsync(destinationExePath, cancellationToken);
            if (installedVersion is not null
                && TorReleaseFeed.TryParse(installedVersion, out var installedValue)
                && chosen.Value < installedValue)
            {
                throw new InvalidOperationException(
                    $"Le miroir propose Tor {chosen.Text}, plus ancien que la version déjà installée ({installedVersion}). Installation refusée (anti-retour-arrière).");
            }

            progress?.Report("Vérification de la signature officielle de Tor...");
            var signature = await Http.GetStringAsync(TorTrustedRelease.SignatureUrl(chosen.Text), cancellationToken);
            await VerifyArchiveSignatureAsync(archivePath, signature, progress, cancellationToken);

            var archiveHash = await ComputeSha256Async(archivePath, cancellationToken);

            progress?.Report("Extraction du moteur Tor...");
            var extractedExePath = Path.Combine(workDir, "tor.exe");
            if (!TryExtractTorExecutable(archivePath, extractedExePath))
            {
                throw new InvalidOperationException("tor.exe introuvable dans l'archive officielle.");
            }

            var exeHash = await ComputeSha256Async(extractedExePath, cancellationToken);

            var torDir = Path.GetDirectoryName(destinationExePath)
                ?? throw new InvalidOperationException("Chemin de destination du moteur Tor invalide.");
            Directory.CreateDirectory(torDir);
            ReplaceExecutable(extractedExePath, destinationExePath);
            WriteReceipt(destinationExePath, chosen.Text, archiveHash, exeHash);
            progress?.Report($"Moteur Tor {chosen.Text} installé.");
            return chosen.Text;
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { }
        }
    }

    private static async Task VerifyArchiveSignatureAsync(
        string archivePath, string armoredSignature, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var result = VerifyFile(archivePath, armoredSignature, TorSigningKey.ArmoredPublicKeyBlock);

        if (result.Outcome == TorSignatureOutcome.SignerKeyUnknown)
        {
            // Rotation de la sous-cle de signature depuis la publication de cette
            // version de Lumora : un bloc de cle a jour est recupere, puis soumis
            // exactement aux memes verifications (cle principale epinglee,
            // liaison de la sous-cle, revocation, expiration).
            progress?.Report("Mise à jour de la clé de signature de Tor...");
            var freshKeyBlock = await Http.GetStringAsync(TorTrustedRelease.SigningKeyRefreshUrl, cancellationToken);
            result = VerifyFile(archivePath, armoredSignature, freshKeyBlock);
        }

        if (result.Outcome != TorSignatureOutcome.Valid)
        {
            throw new InvalidOperationException(
                $"La signature officielle de l'archive Tor n'est pas valide ({result.Message}) Installation annulée par sécurité.");
        }
    }

    private static TorSignatureResult VerifyFile(string path, string armoredSignature, string armoredKeyBlock)
    {
        using var stream = File.OpenRead(path);
        return TorSignatureVerifier.VerifyDetached(
            stream, armoredSignature, armoredKeyBlock, TorTrustedRelease.PinnedSigningKeyFingerprint);
    }

    // Ecrit a cote puis remplace d'un coup : jamais de tor.exe a moitie ecrit.
    // Echoue proprement si tor.exe est verrouille (une autre fenetre
    // Incognito avec Tor l'execute), plutot que de laisser une installation
    // incoherente.
    private static void ReplaceExecutable(string extractedExePath, string destinationExePath)
    {
        var staged = destinationExePath + ".new";
        try
        {
            File.Copy(extractedExePath, staged, overwrite: true);
            File.Move(staged, destinationExePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(staged); } catch { }
            throw new InvalidOperationException(
                "Le moteur Tor est en cours d'utilisation (une autre fenêtre Incognito avec Tor est probablement ouverte). Ferme-la puis réessaie.");
        }
    }

    // false = 404 (fichier absent du miroir) ; toute autre erreur HTTP leve.
    private static async Task<bool> TryDownloadAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destinationPath);
        await input.CopyToAsync(output, cancellationToken);
        return true;
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
