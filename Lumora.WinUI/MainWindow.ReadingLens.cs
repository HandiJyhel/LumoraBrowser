using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

// ── Loupe de lecture (accessibilite) ─────────────────────────────────────
// Capture d'ecran de la page (CoreWebView2.CapturePreviewAsync) affichee
// dans un panneau zoomable (ScrollViewer.ZoomMode). Fonctionne meme sur un
// contenu d'iframe cross-origin (captcha reCAPTCHA/hCaptcha) puisque c'est
// une image du rendu, pas une lecture du DOM. Lumora ne resout jamais un
// defi a la place de l'utilisateur : elle l'aide seulement a le lire.
// Image locale uniquement, jamais transmise a un service externe.
public sealed partial class MainWindow
{
    private void UpdateReadingLensButtonVisibility()
    {
        if (ReadingLensButton is null) return;
        ReadingLensButton.Opacity = _uiSettings.AccessibilityReadingLensEnabled ? 1 : 0.72;
    }

    private async void ReadingLensButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiSettings.AccessibilityReadingLensEnabled)
        {
            StatusText.Text = "Activez la loupe de lecture dans Reglages > Confort pour l'utiliser.";
            return;
        }

        await CaptureReadingLensAsync();
    }

    private async void ReadingLensRefreshButton_Click(object sender, RoutedEventArgs e) =>
        await CaptureReadingLensAsync();

    private async Task CaptureReadingLensAsync()
    {
        var tab = CurrentTab();
        var core = tab?.View?.CoreWebView2;
        var address = tab?.View?.Source?.ToString() ?? tab?.Address ?? string.Empty;
        if (core is null || !BookmarkStore.IsWebUrl(address))
        {
            StatusText.Text = "Ouvrez une page web pour utiliser la loupe de lecture.";
            return;
        }

        ShowPanel(ReadingLensPanel, "Loupe de lecture : capture en cours...");

        try
        {
            using var memoryStream = new MemoryStream();
            var randomAccessStream = memoryStream.AsRandomAccessStream();
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, randomAccessStream);
            randomAccessStream.Seek(0);

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(randomAccessStream);
            ReadingLensImage.Source = bitmap;
            ReadingLensEmptyText.Visibility = Visibility.Collapsed;
            StatusText.Text = "Loupe de lecture : molette + Ctrl (ou pincer) pour zoomer.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Loupe de lecture indisponible : {ex.Message}";
        }
    }
}
