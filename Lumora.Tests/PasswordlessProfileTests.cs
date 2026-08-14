using Xunit;

namespace Lumora.Tests;

// 2026-08-13 : profil sans mot de passe, choix explicite et permanent pris a
// la creation - "si un utilisateur cree un profil sans mot de passe, il doit
// en assumer les consequences" (mots de l'utilisateur). Verifie en regression
// sur le texte source (MainWindow.Profile.cs/MainWindow.SettingsStorage.cs
// dependent de WinUI, absent du projet de test "pur" - meme contrainte que
// les autres fichiers UI, voir BookmarkBarRegressionTests.cs). Le modele pur
// (UserProfile.HasAccountPassword) est verifie par de vrais tests
// comportementaux dans UserProfileTests.cs.
public sealed class PasswordlessProfileTests
{
    [Fact]
    public void Ecran_de_creation_propose_le_choix_avec_avertissement()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("x:Name=\"NoPasswordSwitch\" Header=\"Ne pas définir de mot de passe\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NoPasswordWarningBar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("définitif", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Creation_de_compte_saute_le_mot_de_passe_le_pin_et_la_cle_de_recuperation()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("if (NoPasswordSwitch.IsOn)", code, StringComparison.Ordinal);
        Assert.Contains("_pendingUserProfile = UserProfile.Create(name, null, null);", code, StringComparison.Ordinal);
    }

    // Les 4 points ou l'ecran de connexion/le verrouillage doivent sauter un
    // profil sans mot de passe : premier lancement, selecteur de profil,
    // verrouillage auto, et la verification d'identite pour agir sur le
    // profil d'un AUTRE utilisateur.
    [Fact]
    public void Ecran_de_connexion_et_verrouillage_sautent_un_profil_sans_mot_de_passe()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        var occurrences = CountOccurrences(code, "!_userProfile.HasAccountPassword");
        Assert.True(occurrences >= 3,
            $"Attendu au moins 3 verifications HasAccountPassword sur _userProfile (premier lancement, selecteur, verrouillage), trouve {occurrences}.");
        Assert.Contains("!targetProfile.HasAccountPassword", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Changer_le_mot_de_passe_et_cle_de_recuperation_masques_sans_mot_de_passe()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("ChangeProfilePasswordButton.Visibility = hasPassword ? Visibility.Visible : Visibility.Collapsed;", code, StringComparison.Ordinal);
        Assert.Contains("CreateRecoveryKeyButton.Visibility = hasPassword ? Visibility.Visible : Visibility.Collapsed;", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Sauvegarde_bloquee_avec_message_clair_sans_mot_de_passe()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsStorage.cs");

        Assert.Contains("if (!_userProfile.HasAccountPassword)", code, StringComparison.Ordinal);
        Assert.Contains("aucune sauvegarde possible pour lui", code, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
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
