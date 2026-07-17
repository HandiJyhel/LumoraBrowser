using Lumora.WinUI.VideoDownload;
using Xunit;

namespace Lumora.Tests;

public class YtDlpOutputParserTests
{
    [Fact]
    public void Priorise_la_ligne_de_fusion_sur_les_destinations_intermediaires()
    {
        var output = string.Join('\n', new[]
        {
            "[youtube] Extracting URL: https://www.youtube.com/watch?v=abc",
            "[download] Destination: Titre [abc].f299.mp4",
            "[download] 100% of 50.00MiB in 00:10",
            "[download] Destination: Titre [abc].f251.webm",
            "[download] 100% of 5.00MiB in 00:02",
            "[Merger] Merging formats into \"C:\\Users\\test\\Downloads\\Titre [abc].mp4\""
        });

        var candidates = YtDlpOutputParser.CandidatePaths(output).ToList();

        Assert.Equal(@"C:\Users\test\Downloads\Titre [abc].mp4", candidates[0]);
    }

    [Fact]
    public void Retombe_sur_la_ligne_destination_quand_il_n_y_a_pas_de_fusion()
    {
        var output = string.Join('\n', new[]
        {
            "[youtube] Extracting URL: https://www.youtube.com/watch?v=abc",
            "[download] Destination: C:\\Users\\test\\Downloads\\Titre [abc].mp4",
            "[download] 100% of 20.00MiB in 00:05"
        });

        var candidates = YtDlpOutputParser.CandidatePaths(output).ToList();

        Assert.Equal(@"C:\Users\test\Downloads\Titre [abc].mp4", candidates[0]);
    }

    [Fact]
    public void Gere_les_fins_de_ligne_CRLF()
    {
        var output = "[download] Destination: C:\\Downloads\\video.mp4\r\n[download] 100% of 10.00MiB in 00:03\r\n";

        var candidates = YtDlpOutputParser.CandidatePaths(output).ToList();

        Assert.Equal(@"C:\Downloads\video.mp4", candidates[0]);
    }

    [Fact]
    public void Ne_retourne_rien_pour_une_sortie_sans_chemin()
    {
        var output = "[youtube] Extracting URL: https://www.youtube.com/watch?v=abc\n[download] 12.0% of 1.00GiB";

        var candidates = YtDlpOutputParser.CandidatePaths(output).ToList();

        Assert.Empty(candidates);
    }
}
