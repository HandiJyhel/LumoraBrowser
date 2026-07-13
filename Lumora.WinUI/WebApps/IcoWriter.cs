namespace Lumora.WinUI;

// Logique pure : encapsule un PNG existant (favicon déjà en cache local) dans
// un conteneur .ico minimal à une seule image. Windows (Vista et plus récent)
// accepte une image PNG brute dans un ICO, ce qui évite toute conversion de
// pixels ou dépendance externe pour générer l'icône d'un raccourci.
internal static class IcoWriter
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static byte[] WrapPngAsIco(byte[] pngBytes)
    {
        if (pngBytes is null || pngBytes.Length < 24 || !HasPngSignature(pngBytes))
        {
            throw new ArgumentException("Donnees PNG invalides.", nameof(pngBytes));
        }

        var (width, height) = ReadPngDimensions(pngBytes);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write((short)0); // reserved
            writer.Write((short)1); // type = icone
            writer.Write((short)1); // une seule image

            writer.Write((byte)(width  >= 256 ? 0 : width));
            writer.Write((byte)(height >= 256 ? 0 : height));
            writer.Write((byte)0);  // pas de palette
            writer.Write((byte)0);  // réservé
            writer.Write((short)1); // plans de couleur
            writer.Write((short)32); // bits par pixel
            writer.Write(pngBytes.Length); // taille des données image
            writer.Write(6 + 16);   // décalage : 1 ICONDIR (6) + 1 ICONDIRENTRY (16)

            writer.Write(pngBytes);
        }

        return stream.ToArray();
    }

    private static bool HasPngSignature(byte[] data)
    {
        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (data[i] != PngSignature[i]) return false;
        }

        return true;
    }

    private static (int Width, int Height) ReadPngDimensions(byte[] png)
    {
        // Signature (8 octets) puis chunk IHDR : longueur(4) + "IHDR"(4) + largeur(4) + hauteur(4), big-endian.
        var width  = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        var height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return (width, height);
    }
}
