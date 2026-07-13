using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public sealed class IcoWriterTests
{
    // PNG transparent 1x1, constante bien connue utilisée dans de nombreux tests.
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    [Fact]
    public void WrapsPngWithValidIcoHeader()
    {
        var png = Convert.FromBase64String(OnePixelPngBase64);
        var ico = IcoWriter.WrapPngAsIco(png);

        // ICONDIR : reserved(0), type(1=icone), count(1 image), little-endian.
        Assert.Equal(0, ico[0]);
        Assert.Equal(0, ico[1]);
        Assert.Equal(1, ico[2]);
        Assert.Equal(0, ico[3]);
        Assert.Equal(1, ico[4]);
        Assert.Equal(0, ico[5]);

        // ICONDIRENTRY : largeur/hauteur = 1x1 pour ce PNG.
        Assert.Equal(1, ico[6]);
        Assert.Equal(1, ico[7]);

        // Taille totale = en-tête (6+16) + PNG brut.
        Assert.Equal(6 + 16 + png.Length, ico.Length);
    }

    [Fact]
    public void EmbedsPngBytesUnmodifiedAtOffset22()
    {
        var png = Convert.FromBase64String(OnePixelPngBase64);
        var ico = IcoWriter.WrapPngAsIco(png);

        var embedded = ico[22..];
        Assert.Equal(png, embedded);
    }

    [Fact]
    public void Throws_ForInvalidData()
    {
        Assert.Throws<ArgumentException>(() => IcoWriter.WrapPngAsIco(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void Throws_ForNonPngSignature()
    {
        var fake = new byte[40];
        Assert.Throws<ArgumentException>(() => IcoWriter.WrapPngAsIco(fake));
    }
}
