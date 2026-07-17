using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Mot de passe du coffre ────────────────────────────────────────────────
    //
    // Le coffre est protégé par le mot de passe du profil (couplage automatique à
    // la création / au login). Il n'y a donc plus de « mot de passe maître »
    // séparé à activer : le fichier vault.lumora est portable et ne dépend que de
    // ce mot de passe. Le changement de mot de passe re-clé le coffre
    // (voir ChangeProfilePasswordButton_Click).

    private async Task<string?> PromptMasterPasswordAsync(string title, bool confirm)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = "Confirmer",
            CloseButtonText = "Annuler",
            XamlRoot = Content.XamlRoot
        };

        var panel = new StackPanel { Spacing = 10 };
        var pwBox = new PasswordBox { PlaceholderText = "Mot de passe", MinWidth = 260 };
        panel.Children.Add(pwBox);

        PasswordBox? pwBox2 = null;
        if (confirm)
        {
            pwBox2 = new PasswordBox { PlaceholderText = "Confirmer le mot de passe", MinWidth = 260 };
            panel.Children.Add(pwBox2);
        }

        dialog.Content = panel;
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;
        if (string.IsNullOrWhiteSpace(pwBox.Password)) return null;
        if (pwBox2 is not null && pwBox.Password != pwBox2.Password)
        {
            StatusText.Text = "Les mots de passe ne correspondent pas.";
            return null;
        }

        return pwBox.Password;
    }

    private async Task<bool> UnlockVaultIfNeededAsync()
    {
        if (!_vault.HasMasterPassword) return true;
        if (!_vault.IsLocked) return true;

        var pw = await PromptMasterPasswordAsync("Coffre verrouille - entrez le mot de passe du profil", confirm: false);
        if (string.IsNullOrWhiteSpace(pw)) return false;

        return _vault.Unlock(pw);
    }

    // Barrière d'accès au coffre : re-demande le PIN (si activé) sinon le mot de passe,
    // À CHAQUE ouverture — même déjà connecté. Le coffre est la zone la plus sensible.
    private async Task<bool> RequireVaultAccessAsync()
    {
        if (_userProfile is null) return false;

        if (_userProfile.HasPinLogin)
        {
            var pin = await PromptMasterPasswordAsync("Entrez votre code PIN pour ouvrir le coffre", confirm: false);
            if (string.IsNullOrWhiteSpace(pin) || !_userProfile.VerifyPin(pin)) return false;
            if (_vault.IsLocked) _vault.UnlockWithPin(pin);
            return true;
        }

        var pw = await PromptMasterPasswordAsync("Entrez votre mot de passe pour ouvrir le coffre", confirm: false);
        if (string.IsNullOrWhiteSpace(pw) || !_userProfile.VerifyPassword(pw)) return false;
        if (_vault.IsLocked) _vault.EnsureUnlockedWith(pw);
        return true;
    }

    private async void VaultMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode)
        {
            StatusText.Text = "Coffre indisponible en mode invité.";
            return;
        }

        if (!await RequireVaultAccessAsync())
        {
            StatusText.Text = "Acces au coffre refuse : code incorrect ou annule.";
            return;
        }

        ShowPanel(VaultPanel, "Gestionnaire de mots de passe");
        SyncFromBrowserStore();
        RefreshVaultPanel();
    }

    private async void ImportPasswordsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode)
        {
            StatusText.Text = "Import des mots de passe indisponible en mode invité.";
            return;
        }

        if (!await RequireVaultAccessAsync())
        {
            StatusText.Text = "Acces au coffre refuse : import annule.";
            return;
        }

        ShowPanel(VaultPanel, "Importer des mots de passe");
        SyncFromBrowserStore();
        RefreshVaultPanel();
        await ImportPasswordsAsync();
    }
}
