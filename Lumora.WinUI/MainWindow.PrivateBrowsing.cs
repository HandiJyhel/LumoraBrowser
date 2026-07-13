using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Navigation privée (Ctrl+Shift+N) ─────────────────────────────────────
    // La fenêtre privée vit sa vie : elle n'est pas suivie par MainWindow (même
    // modèle que les fenêtres d'application web) et son profil InPrivate est
    // purgé par le moteur à la fermeture.

    private void PrivateWindowMenu_Click(object sender, RoutedEventArgs e) =>
        OpenPrivateWindow();

    private void PrivateWindowAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OpenPrivateWindow();
    }

    private void OpenPrivateWindow(string? startUrl = null)
    {
        // Pas de navigation privée tant que la session profil n'est pas ouverte :
        // la fenêtre s'appuie sur les réglages (moteur de recherche, protections)
        // du profil actif.
        if (LoginOverlay.Visibility == Visibility.Visible ||
            SetupWizardOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        var window = new LumoraPrivateWindow(_profile, startUrl);
        window.Activate();
        window.InitializeBrowserSurface();
        StatusText.Text = "Fenetre de navigation privee ouverte.";
    }
}
