using Lumora.WinUI.VideoDownload;
using Xunit;

namespace Lumora.Tests;

public class VideoDownloadFormatTests
{
    [Fact]
    public void Meilleure_qualite_avec_ffmpeg_fusionne_video_et_audio()
    {
        var selector = VideoDownloadFormat.BuildFormatSelector(VideoQuality.Best, hasFfmpeg: true);
        Assert.Equal("bv*+ba/b", selector);
    }

    [Fact]
    public void Meilleure_qualite_sans_ffmpeg_reste_sur_un_seul_fichier()
    {
        var selector = VideoDownloadFormat.BuildFormatSelector(VideoQuality.Best, hasFfmpeg: false);
        Assert.Equal("best[ext=mp4]/best", selector);
    }

    [Fact]
    public void Qualite_1080p_avec_ffmpeg_plafonne_la_hauteur()
    {
        var selector = VideoDownloadFormat.BuildFormatSelector(VideoQuality.Q1080, hasFfmpeg: true);
        Assert.Equal("bv*[height<=1080]+ba/b[height<=1080]", selector);
    }

    [Fact]
    public void Qualite_720p_sans_ffmpeg_plafonne_la_hauteur()
    {
        var selector = VideoDownloadFormat.BuildFormatSelector(VideoQuality.Q720, hasFfmpeg: false);
        Assert.Equal("best[height<=720][ext=mp4]/best[height<=720]", selector);
    }

    [Fact]
    public void Qualite_480p_avec_ffmpeg_plafonne_la_hauteur()
    {
        var selector = VideoDownloadFormat.BuildFormatSelector(VideoQuality.Q480, hasFfmpeg: true);
        Assert.Equal("bv*[height<=480]+ba/b[height<=480]", selector);
    }

    [Theory]
    [InlineData("Best", "Meilleure qualite disponible")]
    [InlineData("Q1080", "1080p")]
    [InlineData("Q720", "720p")]
    [InlineData("Q480", "480p")]
    public void Label_decrit_chaque_qualite(string qualityName, string expected)
    {
        var quality = Enum.Parse<VideoQuality>(qualityName);
        Assert.Equal(expected, VideoDownloadFormat.Label(quality));
    }
}
