using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// Ouverture d'un fichier téléchargé, partagée par MainWindow et la fenêtre
// Incognito : confirmation obligatoire pour les types dangereux (voir
// DownloadRisk), ouverture directe sinon.
internal static class DownloadOpenConfirmation
{
    public static async Task OpenAsync(string path, XamlRoot? xamlRoot)
    {
        if (DownloadRisk.IsDangerous(path))
        {
            if (xamlRoot is null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Ouvrir ce fichier ?",
                Content = $"« {Path.GetFileName(path)} » peut exécuter des programmes sur votre ordinateur.\n\n"
                          + "Ne l'ouvrez que si vous connaissez sa provenance et lui faites confiance.",
                PrimaryButtonText = "Ouvrir quand même",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
    }
}
