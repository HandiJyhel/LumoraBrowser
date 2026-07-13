using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public sealed class FaviconQualityTests
{
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    private const string WebView2GenericGlobePngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAABXklEQVR4nKRT7VXCQBDcy/O/UIGxArACoAPtACqQDkgHSgXagbECoAKTCpJUYKwgzuQmeFz8x743L7ub3bnbj0sskq7rlsALUHV/8gW8AWkcnwSJEyZC/QCWct8LE2AOHBQzJuBPYBokrJxzNQF9A6T0AU43mpwJxHpC8FqnN0rsBfoRn4Ik0Lf4lsDOxEbmCtgDLbBQ3skuZaGbfer7zBslYnpXsqnW2sZSqgxTbA489vWwRlytUDnf7APsNsxWzRX8U9kkO5Cgg9MFgRd2RDKKTexKIUEdLUg7jOifEtrAZq+KG/Nd5eaVQXwGu4047pSYBXZPwG5yiRqdUAMzG48xNb8LlFtgbWy2WF/pRH82fAvQd9BXUQk8ZA9/rsVjP7cuqI8BR/MLxdE+aQPPIwMeWC4wxz/qfpU5czmcAmugf31BMsvjxv6YfxO9jOathMz8Rs7kbtSrfLjVIL8AAAD//7VmeZQAAAAGSURBVAMAPa7EYxzoGdAAAAAASUVORK5CYII=";

    [Fact]
    public void Accepts_NormalPng()
    {
        var png = Convert.FromBase64String(OnePixelPngBase64);

        Assert.True(FaviconQuality.IsUsablePng(png));
    }

    [Fact]
    public void Rejects_WebView2GenericGlobePng()
    {
        var png = Convert.FromBase64String(WebView2GenericGlobePngBase64);

        Assert.False(FaviconQuality.IsUsablePng(png));
    }

    [Fact]
    public void Rejects_WebView2GenericGlobeWhenWrappedAsIco()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"nova-generic-favicon-{Guid.NewGuid():N}.ico");
        try
        {
            var png = Convert.FromBase64String(WebView2GenericGlobePngBase64);
            File.WriteAllBytes(tempFile, IcoWriter.WrapPngAsIco(png));

            Assert.False(FaviconQuality.IsUsablePngBackedIcoFile(tempFile));
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void Accepts_NormalPngWhenWrappedAsIco()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"nova-good-favicon-{Guid.NewGuid():N}.ico");
        try
        {
            var png = Convert.FromBase64String(OnePixelPngBase64);
            File.WriteAllBytes(tempFile, IcoWriter.WrapPngAsIco(png));

            Assert.True(FaviconQuality.IsUsablePngBackedIcoFile(tempFile));
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }
}
