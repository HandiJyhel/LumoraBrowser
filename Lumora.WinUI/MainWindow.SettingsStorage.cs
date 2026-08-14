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

    // Le mot de passe de la sauvegarde EST le mot de passe du compte (2026-08-13,
    // idee de l'utilisateur : "a partir du moment ou tu crees un fichier
    // sauvegarde... on va te demander le mot de passe de cette application").
    // Plus de mot de passe dedie invente au moment de l'export - un mot de
    // passe utilise une fois tous les 6 mois a bien plus de chances d'etre
    // oublie que celui utilise au quotidien. Rendu possible sans regression de
    // securite par le renforcement de la regle du mot de passe de compte a 12
    // caracteres + 1 special (IsAccountPasswordStrongEnough, MainWindow.Profile.cs).
    private async void ExportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode) { StatusText.Text = "Indisponible en mode invité."; return; }
        if (_userProfile is null) { StatusText.Text = "Profil introuvable."; return; }

        // Profil sans mot de passe (2026-08-13, choix permanent pris a la
        // creation) : aucun mot de passe a reutiliser comme cle de sauvegarde,
        // sauvegarde impossible pour ce profil - voir UserProfile.HasAccountPassword.
        if (!_userProfile.HasAccountPassword)
        {
            StatusText.Text = "Ce profil n'a pas de mot de passe : aucune sauvegarde possible pour lui.";
            return;
        }

        var password = await PromptAccountPasswordAsync(
            "Confirmer votre mot de passe",
            "Entrez le mot de passe de votre compte Lumora pour créer la sauvegarde.");
        if (password is null) return;

        if (!await Task.Run(() => _userProfile.VerifyPassword(password)))
        { StatusText.Text = "Mot de passe incorrect."; return; }

        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = $"lumora-backup-{DateTime.Now:yyyyMMdd-HHmm}";
        picker.FileTypeChoices.Add("Sauvegarde Lumora", new List<string> { ".lumorabackup", ".novabackup" });

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            var result = LumoraBackup.Export(file.Path, password, _profile, _vault);
            StatusText.Text = result.CredentialCount > 0
                ? $"Sauvegarde exportée avec succès ({result.CredentialCount} mot(s) de passe inclus)."
                : "Sauvegarde exportée avec succès.";
        }
        catch (VaultLockedForBackupException ex)
        {
            StatusText.Text = ex.Message;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de l'export : {ex.Message}";
        }
    }

    private async void ImportBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (await RunImportBackupFlowAsync())
        {
            StorageImportBar.IsOpen = true;
            StatusText.Text = "Sauvegarde importée.";
        }
    }

    // Flux d'import partage entre le bouton Parametres > Stockage et l'etape
    // "Profil existant ?" de l'assistant de premier lancement
    // (MainWindow.SetupWizard.cs). Renvoie true si l'import a reussi ; met a
    // jour StatusText en cas d'echec/annulation, laisse l'appelant gerer la
    // suite en cas de succes (les deux appelants ont un apres-import different).
    // Le mot de passe demande est celui du COMPTE associe a la sauvegarde
    // (voir ExportBackupButton_Click) : tenter de dechiffrer avec ce mot de
    // passe EST la verification (pas de profil local a comparer au moment du
    // tout premier lancement) - succes du dechiffrement = bon mot de passe.
    private async Task<bool> RunImportBackupFlowAsync()
    {
        if (_isGuestMode) { StatusText.Text = "Indisponible en mode invité."; return false; }

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add(".lumorabackup");
        picker.FileTypeFilter.Add(".novabackup");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return false;

        var password = await PromptAccountPasswordAsync(
            "Importer une sauvegarde",
            "Entrez le mot de passe du compte associé à cette sauvegarde.");
        if (password is null) return false;

        try
        {
            LumoraBackup.Import(file.Path, password, _profile, _vault);
            ReloadBookmarks();
            return true;
        }
        catch (CryptographicException)
        {
            StatusText.Text = "Mot de passe incorrect ou sauvegarde corrompue.";
            return false;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de l'import : {ex.Message}";
            return false;
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

    // Champ unique (2026-08-13) : la sauvegarde n'utilise plus un mot de passe
    // dedie invente au moment de l'export, mais le mot de passe du compte -
    // plus besoin de le "confirmer" par une double saisie, il existe deja
    // (voir ExportBackupButton_Click/RunImportBackupFlowAsync pour ce que
    // chaque appelant fait ensuite de la valeur saisie).
    private async Task<string?> PromptAccountPasswordAsync(string title, string message)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            FontSize = 12
        });
        var pwBox = new PasswordBox { PlaceholderText = "Mot de passe de votre compte", MinWidth = 280 };
        panel.Children.Add(pwBox);

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
        return string.IsNullOrEmpty(pwBox.Password) ? null : pwBox.Password;
    }

}
