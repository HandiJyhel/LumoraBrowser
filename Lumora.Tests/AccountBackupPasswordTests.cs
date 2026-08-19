using Xunit;

namespace Lumora.Tests;

// 2026-08-13 : le mot de passe de compte sert desormais aussi de cle pour les
// sauvegardes .lumorabackup (idee de l'utilisateur, voir MEMORY.md) - la regle
// "8 caracteres, aucune complexite" etait insuffisante pour proteger un
// fichier qui peut contenir tous les mots de passe/cartes. Verifie en
// regression sur le texte source (MainWindow.Profile.cs depend de
// Microsoft.UI.Xaml, absent du projet de test "pur" - meme contrainte que les
// autres fichiers WinUI, voir BookmarkBarRegressionTests.cs).
public sealed class AccountBackupPasswordTests
{
    [Fact]
    public void Regle_de_mot_de_passe_renforcee_a_12_caracteres_plus_special()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("private static bool IsAccountPasswordStrongEnough(string password, out string error)", code, StringComparison.Ordinal);
        Assert.Contains("password.Length < 12", code, StringComparison.Ordinal);
        Assert.Contains("!password.Any(c => !char.IsLetterOrDigit(c))", code, StringComparison.Ordinal);
        Assert.DoesNotContain("password.Length < 8", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_3_points_de_creation_changement_recuperation_utilisent_la_regle_renforcee()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("if (!IsAccountPasswordStrongEnough(pw, out var pwError))", code, StringComparison.Ordinal);
        // newPassword (variable locale) et non plus newBox.Password directement depuis
        // le 2026-08-14 : lire .Password sur le thread UI avant Task.Run, voir
        // Aucun_Task_Run_ne_lit_Password_directement_sur_un_controle_XAML.
        Assert.Contains("if (!IsAccountPasswordStrongEnough(newPassword, out var changePwError))", code, StringComparison.Ordinal);
        Assert.Contains("if (!IsAccountPasswordStrongEnough(newBox.Password, out var recoveryPwError))", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Sauvegarde_reutilise_le_mot_de_passe_du_compte_au_lieu_d_un_mot_de_passe_dedie()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsStorage.cs");

        // Export : verifie le mot de passe contre le compte (pas de nouveau
        // mot de passe invente), puis le reutilise tel quel pour chiffrer.
        Assert.Contains("_userProfile.VerifyPassword(password)", code, StringComparison.Ordinal);
        Assert.Contains("LumoraBackup.Export(file.Path, password, _profile, _vault)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("PromptBackupPasswordAsync", code, StringComparison.Ordinal);

        // Import : meme champ, mais aucune verification locale possible avant
        // dechiffrement (premier lancement = aucun profil existant encore).
        Assert.Contains("LumoraBackup.Import(file.Path, password, _profile, _vault)", code, StringComparison.Ordinal);
        Assert.Contains("private async Task<string?> PromptAccountPasswordAsync(string title, string message)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Coffre_verrouille_bloque_l_export_avec_un_message_clair()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsStorage.cs");

        Assert.Contains("catch (VaultLockedForBackupException ex)", code, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Fichier projet introuvable: {string.Join(Path.DirectorySeparatorChar, segments)}");
    }
}
