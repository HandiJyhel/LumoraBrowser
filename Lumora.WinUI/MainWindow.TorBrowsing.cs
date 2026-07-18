using Microsoft.UI.Xaml;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Navigation anonyme (Tor) ──────────────────────────────────────────────
    // Fenetre independante, non suivie par MainWindow (meme modele que la
    // navigation privee) : son cycle de vie et son profil WebView2 propre
    // vivent dans LumoraTorWindow.

    private void TorWindowMenu_Click(object sender, RoutedEventArgs e) =>
        OpenTorWindow();

    private void OpenTorWindow()
    {
        if (LoginOverlay.Visibility == Visibility.Visible ||
            SetupWizardOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        var window = new LumoraTorWindow(_profile);
        window.Activate();
        window.InitializeWindow();
        StatusText.Text = "Fenetre de navigation anonyme ouverte.";
    }
}
