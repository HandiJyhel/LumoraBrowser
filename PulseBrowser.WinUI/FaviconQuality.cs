using System.Security.Cryptography;

namespace PulseBrowser.WinUI;

internal static class FaviconQuality
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // Favicon generique renvoye par WebView2 quand Chromium n'a pas encore de vraie
    // icone de site. Il ne doit jamais etre memorise comme favicon utilisateur.
    private static readonly byte[] WebView2GenericFaviconHash =
    {
        0x95, 0x9A, 0x80, 0xAA, 0x9A, 0x16, 0xAD, 0x7B,
        0x30, 0x6D, 0x78, 0x95, 0xB3, 0x40, 0x83, 0xF3,
        0x81, 0x7C, 0xC6, 0x1F, 0xB6, 0xE8, 0xB6, 0x76,
        0xB0, 0x5D, 0x5D, 0xD5, 0x9A, 0xC8, 0x9F, 0x15
    };

    public static bool IsUsablePng(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length < 24 || !HasPngSignature(pngBytes))
            return false;

        var hash = SHA256.HashData(pngBytes);
        return !hash.SequenceEqual(WebView2GenericFaviconHash);
    }

    public static bool IsUsablePngFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            return IsUsablePng(File.ReadAllBytes(path));
        }
        catch
        {
            return false;
        }
    }

    public static bool IsUsablePngBackedIcoFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var ico = File.ReadAllBytes(path);
            if (ico.Length < 22) return false;

            var reserved = BitConverter.ToUInt16(ico, 0);
            var type = BitConverter.ToUInt16(ico, 2);
            var count = BitConverter.ToUInt16(ico, 4);
            if (reserved != 0 || type != 1 || count < 1) return false;

            var imageSize = BitConverter.ToInt32(ico, 14);
            var imageOffset = BitConverter.ToInt32(ico, 18);
            if (imageSize <= 0 || imageOffset < 0 || imageOffset + imageSize > ico.Length)
                return false;

            var png = new byte[imageSize];
            Buffer.BlockCopy(ico, imageOffset, png, 0, imageSize);
            return IsUsablePng(png);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasPngSignature(byte[] data)
    {
        if (data.Length < PngSignature.Length) return false;

        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (data[i] != PngSignature[i]) return false;
        }

        return true;
    }
}
