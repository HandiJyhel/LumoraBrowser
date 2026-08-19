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
    // Marge tres large au-dessus d'une vraie photo/capture d'ecran (~64 megapixels) :
    // sert seulement a rejeter une image demesuree/forgee avant l'allocation du
    // buffer, jamais atteinte par un usage reel.
    private const long MaxPixelCount = 64_000_000;

    public static async Task<string?> TryDecodeFileAsync(StorageFile file)
    {
        // Ouverture du fichier volontairement HORS du try/catch générique
        // ci-dessous : une IOException/UnauthorizedAccessException (fichier
        // verrouillé par un autre programme, permissions insuffisantes) est une
        // vraie erreur d'accès, pas "aucun QR trouvé" - avant ce correctif, les
        // deux cas produisaient le même message "aucun QR reconnu" trompeur
        // (bug réel trouvé en audit le 2026-08-19). L'appelant peut distinguer
        // les deux avec un catch (IOException or UnauthorizedAccessException).
        using var stream = await file.OpenAsync(FileAccessMode.Read);

        try
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var width = softwareBitmap.PixelWidth;
            var height = softwareBitmap.PixelHeight;
            // Calcul en long avant l'allocation : width*height en int pouvait
            // deborder pour une image demesuree (>~46000x46000), produisant une
            // taille de tableau invalide au lieu d'un rejet propre (bug réel
            // trouvé en audit le 2026-08-19).
            var pixelCount = (long)width * height;
            if (pixelCount <= 0 || pixelCount > MaxPixelCount)
            {
                return null;
            }

            var buffer = new byte[4 * pixelCount];
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
            // Pas une image, format non supporté, ou aucun QR détecté : traité
            // comme "rien trouvé" par l'appelant, pas une erreur à remonter.
            return null;
        }
    }
}
