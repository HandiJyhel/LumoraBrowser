using Xunit;

namespace Lumora.Tests;

// Verrouille trois correctifs du 2026-07-22, trouves lors du tout premier
// test reel sur une machine remise a zero :
// - les raccourcis d'accessibilite Ctrl+Alt+chiffre/lettre interceptaient
//   les caracteres composes avec AltGr (touche Alt de droite, rapportee par
//   Windows comme Ctrl+Alt enfonces ensemble sur les claviers europeens),
//   ex. "@" (AltGr+0 en AZERTY francais) refuse dans le champ mot de passe ;
// - l'ecran de creation de profil n'avait aucun bouton Annuler (impossible
//   de revenir a sa session en cours) ni de lien mode invite (impossible de
//   tester Lumora sans compte au tout premier lancement, poste vierge).
public sealed class FirstLaunchAndAccessibilityAcceleratorTests
{
    [Theory]
    [InlineData("MainWindow.AccessibilityNavigation.cs")]
    [InlineData("MainWindow.AccessibilityRescue.cs")]
    [InlineData("MainWindow.AccessibilityQuickActions.cs")]
    [InlineData("MainWindow.AccessibilityContext.cs")]
    public void Raccourcis_ctrl_alt_laissent_passer_une_frappe_altgr(string fileName)
    {
        var source = ReadRepoFile("Lumora.WinUI", fileName);

        Assert.Contains("VirtualKeyModifiers.Control | VirtualKeyModifiers.Menu", source, StringComparison.Ordinal);
        Assert.Contains("if (IsRightAltKeyDown()) return;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void IsRightAltKeyDown_est_defini()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.CommandPalette.cs");

        Assert.Contains("private static bool IsRightAltKeyDown()", source, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.RightMenu", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ecran_creation_de_profil_propose_annuler_et_mode_invite_selon_le_contexte()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("x:Name=\"CreateProfileCancelButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CreateProfileGuestLink\"", xaml, StringComparison.Ordinal);

        Assert.Contains(
            "CreateProfileCancelButton.Visibility =\r\n                    (_userProfile is not null && !_vault.IsLocked) || _profileEntries.Count > 0",
            code, StringComparison.Ordinal);
        Assert.Contains(
            "CreateProfileGuestLink.Visibility =\r\n                    _userProfile is null ? Visibility.Visible : Visibility.Collapsed;",
            code, StringComparison.Ordinal);
        Assert.Contains("private void CreateProfileCancelButton_Click(object sender, RoutedEventArgs e)", code, StringComparison.Ordinal);
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
