using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Incognito (Ctrl+Shift+N) ─────────────────────────────────────────────
    // Fusion de l'ancienne navigation privee et de l'ancienne navigation
    // anonyme (Tor) en un seul mode. Lancee dans un process Windows dedie
    // (IncognitoProcessLauncher), pas une fenetre in-process : voir
    // LumoraIncognitoWindow pour le pourquoi (fiabilite WebView2).

    private void IncognitoWindowMenu_Click(object sender, RoutedEventArgs e) =>
        OpenIncognitoWindow();

    // Entree du selecteur de mode (Neutre/Equilibre/Focus/.../Incognito) : place
    // a cote des autres modes pour l'ergonomie, mais n'en est pas un au sens
    // strict - ca n'appelle jamais ApplyUsageModeFromUi, ca ouvre juste la
    // fenetre a part comme le ferait le menu ou le raccourci clavier.
    private void UsageModeIncognitoButton_Click(object sender, RoutedEventArgs e)
    {
        UsageModeFlyout.Hide();
        OpenIncognitoWindow();
    }

    private void IncognitoWindowAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OpenIncognitoWindow();
    }

    // Incognito remplace la fenetre normale plutot que de s'ouvrir a cote
    // (demande explicite utilisateur, 2026-07-20) : MainWindow se ferme des
    // qu'Incognito s'ouvre. Pour ne jamais laisser l'utilisateur sans aucune
    // fenetre Lumora, la fenetre Incognito relance automatiquement une
    // fenetre normale (session/onglets restaures normalement) quand elle se
    // ferme a son tour - voir IncognitoLaunchArgs.ReturnToMainFlag et le
    // Closed de LumoraIncognitoWindow.
    private void OpenIncognitoWindow(string? startUrl = null)
    {
        // Pas d'Incognito tant que la session profil n'est pas ouverte : la
        // fenetre s'appuie sur les reglages (moteur de recherche, protections)
        // du profil actif.
        if (LoginOverlay.Visibility == Visibility.Visible ||
            SetupWizardOverlay.Visibility == Visibility.Visible)
        {
            return;
        }

        IncognitoProcessLauncher.Launch(startUrl, returnToMain: true);
        Close();
    }
}
