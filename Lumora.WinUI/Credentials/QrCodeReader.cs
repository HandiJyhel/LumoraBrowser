using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage;
using ZXing;
using ZXing.Common;

namespace Lumora.WinUI.Credentials;

// Lecture d'un QR code depuis un fichier image (capture d'écran du QR affiché
// par un site, photo transférée depuis un téléphone...) via ZXing.Net —
// managé pur, aucun runtime natif, aucune dépendance à PulseAuth. Choix
// délibéré face à une capture webcam en direct : plus simple, plus fiable
// (pas d'accès caméra/permissions à gérer), suffisant pour le cas d'usage
// réel (le QR d'activation 2FA est affiché à l'écran, une capture suffit à
// le récupérer). Le décodage WinRT (BitmapDecoder) reste local à l'appareil,
// comme tout le reste du coffre.
internal static class QrCodeReader
{
    public static async Task<string?> TryDecodeFileAsync(StorageFile file)
    {
        try
        {
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var width = softwareBitmap.PixelWidth;
            var height = softwareBitmap.PixelHeight;
            var buffer = new byte[4 * width * height];
            softwareBitmap.CopyToBuffer(buffer.AsBuffer());

            var luminanceSource = new RGBLuminanceSource(buffer, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = [BarcodeFormat.QR_CODE]
                }
            };

            return reader.Decode(luminanceSource)?.Text;
        }
        catch
        {
            // Fichier illisible, pas une image, ou aucun QR détecté : traité
            // comme "rien trouvé" par l'appelant, pas une erreur à remonter.
            return null;
        }
    }
}
