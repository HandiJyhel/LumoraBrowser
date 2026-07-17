using Lumora.WinUI.VideoDownload;
using Xunit;

namespace Lumora.Tests;

// Ne teste que l'absence de crash / la coherence du resultat : le contenu reel
// (present ou non) depend de la machine qui execute les tests.
public class FfmpegLocatorTests
{
    [Fact]
    public void FindLocalFfmpeg_ne_plante_pas_sans_ffmpeg_installe()
    {
        var previous = Environment.GetEnvironmentVariable("LUMORA_FFMPEG_PATH");
        Environment.SetEnvironmentVariable("LUMORA_FFMPEG_PATH", @"Z:\chemin\introuvable\ffmpeg.exe");
        try
        {
            var result = FfmpegLocator.FindLocalFfmpeg();
            Assert.True(result is null || System.IO.File.Exists(result));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LUMORA_FFMPEG_PATH", previous);
        }
    }

    [Fact]
    public void FindLocalFfmpeg_respecte_la_variable_d_environnement_quand_le_fichier_existe()
    {
        var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ffmpeg-test-{Guid.NewGuid():N}.exe");
        System.IO.File.WriteAllText(tempFile, "stub");
        var previous = Environment.GetEnvironmentVariable("LUMORA_FFMPEG_PATH");
        Environment.SetEnvironmentVariable("LUMORA_FFMPEG_PATH", tempFile);
        try
        {
            var result = FfmpegLocator.FindLocalFfmpeg();
            Assert.Equal(tempFile, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LUMORA_FFMPEG_PATH", previous);
            System.IO.File.Delete(tempFile);
        }
    }
}
