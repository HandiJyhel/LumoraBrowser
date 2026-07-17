using Lumora.WinUI.VideoDownload;
using Xunit;

namespace Lumora.Tests;

// Ne teste que la logique pure de parsing du hash (ParseExpectedHash) : le
// telechargement reel (DownloadEngineAsync) tape le reseau GitHub et est
// volontairement exclu des tests automatises, meme principe que
// NetworkBlockerModuleTests pour FilterListManager.
public class YtDlpEngineProviderTests
{
    private const string SampleSums = """
        2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7a  yt-dlp.exe
        d4735e3a265e16eee03f59718b9b5d03019c07d8b6c51f90da3a666eec13ab35  yt-dlp
        9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08  yt-dlp.tar.gz
        """;

    [Fact]
    public void Trouve_le_hash_correspondant_au_nom_de_fichier()
    {
        var hash = YtDlpEngineProvider.ParseExpectedHash(SampleSums, "yt-dlp.exe");
        Assert.Equal("2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7a", hash);
    }

    [Fact]
    public void Ignore_la_casse_du_nom_de_fichier()
    {
        var hash = YtDlpEngineProvider.ParseExpectedHash(SampleSums, "YT-DLP.EXE");
        Assert.Equal("2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7a", hash);
    }

    [Fact]
    public void Renvoie_null_si_le_fichier_est_absent_du_sums()
    {
        var hash = YtDlpEngineProvider.ParseExpectedHash(SampleSums, "yt-dlp.zip");
        Assert.Null(hash);
    }

    [Fact]
    public void Renvoie_null_sur_un_contenu_vide()
    {
        Assert.Null(YtDlpEngineProvider.ParseExpectedHash(string.Empty, "yt-dlp.exe"));
    }

    [Fact]
    public void Supporte_le_prefixe_binaire_etoile_du_format_sha256sum()
    {
        var sums = "2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7a *yt-dlp.exe";
        var hash = YtDlpEngineProvider.ParseExpectedHash(sums, "yt-dlp.exe");
        Assert.Equal("2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7a", hash);
    }

    [Fact]
    public void FindLocalEngine_ne_plante_pas_sans_moteur_installe()
    {
        var previous = Environment.GetEnvironmentVariable("LUMORA_YTDLP_PATH");
        Environment.SetEnvironmentVariable("LUMORA_YTDLP_PATH", @"Z:\chemin\introuvable\yt-dlp.exe");
        try
        {
            var result = YtDlpEngineProvider.FindLocalEngine();
            Assert.True(result is null || System.IO.File.Exists(result));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LUMORA_YTDLP_PATH", previous);
        }
    }
}
