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
                Content = $"« {Path.GetFileName(path)} » peut exécuter des programmes sur ton ordinateur.\n\n"
                          + "Ne l'ouvre que si tu connais sa provenance et que tu lui fais confiance.",
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
