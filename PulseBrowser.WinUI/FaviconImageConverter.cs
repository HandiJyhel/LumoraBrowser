using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PulseBrowser.WinUI;

// Normalise n'importe quelle image de favicon (ICO, BMP, JPEG...) en PNG réel,
// via les décodeurs WIC déjà embarqués dans Windows (aucune dépendance
// supplémentaire). Sans cette conversion, un favicon .ico téléchargé tel quel
// et enregistré sous un nom de fichier .png contient en réalité des octets ICO
// que les contrôles Image WinUI peuvent refuser de décoder.
internal static class FaviconImageConverter
{
    public static async Task<byte[]?> ToPngAsync(byte[] sourceBytes)
    {
        if (sourceBytes is null || sourceBytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var inputStream = new InMemoryRandomAccessStream();
            await inputStream.WriteAsync(sourceBytes.AsBuffer());
            inputStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(inputStream);

            // Icônes multi-résolution (.ico) : garder la plus grande frame disponible
            // plutôt que la première (souvent la plus petite, 16x16).
            var bestIndex = 0u;
            var bestArea = 0u;
            for (var i = 0u; i < decoder.FrameCount; i++)
            {
                var frame = await decoder.GetFrameAsync(i);
                var area = (uint)(frame.PixelWidth * frame.PixelHeight);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestIndex = i;
                }
            }

            var bitmap = await (await decoder.GetFrameAsync(bestIndex)).GetSoftwareBitmapAsync();

            using var outputStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();

            var result = new byte[outputStream.Size];
            outputStream.Seek(0);
            await outputStream.ReadAsync(result.AsBuffer(), (uint)result.Length, InputStreamOptions.None);
            return result;
        }
        catch
        {
            // Formats non décodables par WIC (ex. SVG) : pas de favicon plutôt qu'un
            // fichier corrompu — limite connue, documentée.
            return null;
        }
    }
}
