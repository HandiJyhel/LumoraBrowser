using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Lumora.WinUI.Tor;
using Xunit;

namespace Lumora.Tests;

// Teste uniquement la logique pure d'extraction (TryExtractTorExecutable) sur
// des archives tar.gz synthetiques construites en memoire. Le telechargement
// reel (DownloadEngineAsync) tape le reseau dist.torproject.org et est
// volontairement exclu des tests automatises, meme principe que
// YtDlpEngineProviderTests pour DownloadEngineAsync.
public class TorEngineProviderTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "LumoraTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string CreateSyntheticArchive(string directory, params (string Name, string Content)[] entries)
    {
        var archivePath = Path.Combine(directory, "fake-bundle.tar.gz");
        using (var fileStream = File.Create(archivePath))
        using (var gzip = new GZipStream(fileStream, CompressionLevel.Fastest))
        using (var writer = new TarWriter(gzip))
        {
            foreach (var (name, content) in entries)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content))
                };
                writer.WriteEntry(entry);
            }
        }

        return archivePath;
    }

    [Fact]
    public void Extrait_lentree_tor_tor_exe_et_ignore_les_autres_fichiers()
    {
        var dir = CreateTempDir();
        var archivePath = CreateSyntheticArchive(
            dir,
            ("tor/tor-gencert.exe", "contenu-decoy-gencert"),
            ("tor/tor.exe", "contenu-tor-exe-de-test"),
            ("tor/pluggable_transports/lyrebird.exe", "contenu-decoy-lyrebird"));
        var destPath = Path.Combine(dir, "extracted-tor.exe");

        var found = TorEngineProvider.TryExtractTorExecutable(archivePath, destPath);

        Assert.True(found);
        Assert.Equal("contenu-tor-exe-de-test", File.ReadAllText(destPath));
    }

    [Fact]
    public void Renvoie_false_si_aucune_entree_tor_tor_exe_dans_larchive()
    {
        var dir = CreateTempDir();
        var archivePath = CreateSyntheticArchive(
            dir,
            ("tor/tor-gencert.exe", "contenu-decoy-gencert"));
        var destPath = Path.Combine(dir, "extracted-tor.exe");

        var found = TorEngineProvider.TryExtractTorExecutable(archivePath, destPath);

        Assert.False(found);
        Assert.False(File.Exists(destPath));
    }

    [Fact]
    public void Ignore_une_entree_de_meme_nom_dans_un_autre_dossier()
    {
        var dir = CreateTempDir();
        var archivePath = CreateSyntheticArchive(
            dir,
            ("autre-dossier/tor.exe", "contenu-piege"));
        var destPath = Path.Combine(dir, "extracted-tor.exe");

        var found = TorEngineProvider.TryExtractTorExecutable(archivePath, destPath);

        Assert.False(found);
    }

    [Fact]
    public void ArchiveSha256_est_un_hash_hex_minuscule_de_64_caracteres()
    {
        Assert.Equal(64, TorTrustedRelease.ArchiveSha256.Length);
        Assert.Matches("^[0-9a-f]{64}$", TorTrustedRelease.ArchiveSha256);
    }

    [Fact]
    public void ArchiveUrl_pointe_vers_dist_torproject_org_et_contient_la_version()
    {
        Assert.StartsWith("https://dist.torproject.org/", TorTrustedRelease.ArchiveUrl);
        Assert.Contains(TorTrustedRelease.Version, TorTrustedRelease.ArchiveUrl);
        Assert.EndsWith(TorTrustedRelease.ArchiveFileName, TorTrustedRelease.ArchiveUrl);
    }
}
