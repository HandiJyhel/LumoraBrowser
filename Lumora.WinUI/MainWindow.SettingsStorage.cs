using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lumora.WinUI;

// Deplacement/export/import du profil et effacement des donnees de
// navigation. Extrait de MainWindow.Settings.cs (god file) : ne touche que
// _profile/_isGuestMode/StatusText, aucun changement de comportement.
public sealed partial class MainWindow
{
    // ── Stockage ──────────────────────────────────────────────────────────────

    private async void ChangeFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode) { StatusText.Text = "Indisponible en mode invité."; return; }

        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        var newPath = folder.Path;
        if (string.Equals(newPath, _profile.ProfileDir, StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "Ce dossier est déjà le dossier actuel.";
            return;
        }

        try
        {
            CopyProfileTo(newPath);
            var config = LumoraConfig.Load();
            config.CustomProfilePath = newPath;
            config.Save();
            StorageCurrentFolderText.Text = newPath;
            StorageRestartBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de la copie : {ex.Message}";
        }
    }

    // Trouve en usage reel le 2026-07-22 : ce chemin etait fige sur
    // "profiles/default", alors que chaque profil a desormais un dossier
    // nomme d'apres son utilisateur (y compris le tout premier, voir
    // CreateProfileButton_Click) - copiait donc le mauvais dossier (ou rien,
    // silencieusement, faute d'existence) des qu'un second utilisateur
    // existait. Utilise desormais le vrai dossier du profil actif.
    private void CopyProfileTo(string destDir)
    {
        var srcDir = new DirectoryInfo(_profile.ProfileDir);
        if (!srcDir.Exists) return;

        foreach (var srcFile in srcDir.GetFiles("*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(srcDir.FullName, srcFile.FullName);
            var destFile = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            srcFile.CopyTo(destFile, overwrite: true);
        }
    }

    private async void ExportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode) { StatusText.Text = "Indisponible en mode invité."; return; }

        var password = await PromptBackupPasswordAsync("Exporter une sauvegarde", confirm: true);
        if (password is null) return;

        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = $"lumora-backup-{DateTime.Now:yyyyMMdd-HHmm}";
        picker.FileTypeChoices.Add("Sauvegarde Lumora", new List<string> { ".lumorabackup", ".novabackup" });

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            LumoraBackup.Export(file.Path, password, _profile);
            StatusText.Text = "Sauvegarde exportée avec succès.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de l'export : {ex.Message}";
        }
    }

    private async void ImportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode) { StatusText.Text = "Indisponible en mode invité."; return; }

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add(".lumorabackup");
        picker.FileTypeFilter.Add(".novabackup");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var password = await PromptBackupPasswordAsync("Importer une sauvegarde", confirm: false);
        if (password is null) return;

        try
        {
            LumoraBackup.Import(file.Path, password, _profile);
            ReloadBookmarks();
            StorageImportBar.IsOpen = true;
            StatusText.Text = "Sauvegarde importée.";
        }
        catch (CryptographicException)
        {
            StatusText.Text = "Mot de passe incorrect ou sauvegarde corrompue.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de l'import : {ex.Message}";
        }
    }

    private void StorageRestartCloseButton_Click(object sender, RoutedEventArgs e) =>
        Application.Current.Exit();

    private async void ClearBrowsingDataButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _browserView?.CoreWebView2;
        if (core is null) { StatusText.Text = "Moteur web non initialisé."; return; }
        try
        {
            await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite);
            StatusText.Text = "Données de navigation supprimées (cookies, cache, sessions).";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de la suppression : {ex.Message}";
        }
    }

    private async Task<string?> PromptBackupPasswordAsync(string title, bool confirm)
    {
        var panel = new StackPanel { Spacing = 8 };
        var pwBox = new PasswordBox { PlaceholderText = "Mot de passe de la sauvegarde", MinWidth = 280 };
        panel.Children.Add(pwBox);

        PasswordBox? pwBox2 = null;
        if (confirm)
        {
            var hint = new TextBlock
            {
                Text = "Conservez ce mot de passe : il sera nécessaire pour restaurer vos données.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                FontSize = 12
            };
            panel.Children.Add(hint);
            pwBox2 = new PasswordBox { PlaceholderText = "Confirmer le mot de passe", MinWidth = 280 };
            panel.Children.Add(pwBox2);
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "Confirmer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;
        if (string.IsNullOrEmpty(pwBox.Password)) return null;
        if (pwBox2 is not null && pwBox.Password != pwBox2.Password)
        {
            StatusText.Text = "Les mots de passe ne correspondent pas.";
            return null;
        }
        return pwBox.Password;
    }

}
