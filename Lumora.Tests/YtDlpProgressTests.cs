using Lumora.WinUI.VideoDownload;
using Xunit;

namespace Lumora.Tests;

public class YtDlpProgressTests
{
    [Fact]
    public void Parse_une_ligne_de_progression_avec_taille_MiB()
    {
        var ok = YtDlpProgress.TryParse(
            "[download]  45.2% of   10.00MiB at    1.20MiB/s ETA 00:07",
            out var percent, out var totalBytes);

        Assert.True(ok);
        Assert.Equal(45.2, percent, precision: 3);
        Assert.Equal((long)(10.00 * 1024 * 1024), totalBytes);
    }

    [Fact]
    public void Parse_une_ligne_avec_taille_GiB()
    {
        var ok = YtDlpProgress.TryParse(
            "[download]  12.0% of    1.50GiB at  900.00KiB/s ETA 00:30",
            out var percent, out var totalBytes);

        Assert.True(ok);
        Assert.Equal(12.0, percent, precision: 3);
        Assert.Equal((long)(1.50 * 1024 * 1024 * 1024), totalBytes);
    }

    [Fact]
    public void Parse_une_ligne_de_completion_a_100_pourcent()
    {
        var ok = YtDlpProgress.TryParse("[download] 100% of   10.00MiB in 00:08", out var percent, out var totalBytes);

        Assert.True(ok);
        Assert.Equal(100, percent, precision: 3);
        Assert.Equal((long)(10.00 * 1024 * 1024), totalBytes);
    }

    [Fact]
    public void Parse_un_pourcentage_sans_taille_connue()
    {
        var ok = YtDlpProgress.TryParse("[download]  33.3% of Unknown", out var percent, out var totalBytes);

        Assert.True(ok);
        Assert.Equal(33.3, percent, precision: 3);
        Assert.Equal(0, totalBytes);
    }

    [Fact]
    public void Ignore_une_ligne_qui_n_est_pas_une_ligne_de_progression()
    {
        var ok = YtDlpProgress.TryParse("[youtube] Extracting URL: https://www.youtube.com/watch?v=abc", out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void Ignore_une_ligne_vide_ou_nulle()
    {
        Assert.False(YtDlpProgress.TryParse(null, out _, out _));
        Assert.False(YtDlpProgress.TryParse("", out _, out _));
        Assert.False(YtDlpProgress.TryParse("   ", out _, out _));
    }
}
