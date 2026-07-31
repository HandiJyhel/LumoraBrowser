using Xunit;

namespace Lumora.Tests;

// Verrouille le correctif du 2026-07-22 : trouve en usage reel, le panneau
// "Profils locaux" des Parametres exposait le chemin de chaque profil en
// clair et permettait d'ouvrir son dossier / le mettre en quarantaine sans
// mot de passe, et "Reinitialiser le profil" supprimait tout le dossier du
// profil actif sans la moindre verification - tout cela restait accessible
// depuis une session invite (_isGuestMode), qui ne change pas _profile.
public sealed class GuestModeProfileLockdownTests
{
    [Fact]
    public void Panneau_gestion_profils_est_cache_et_remplace_en_mode_invite()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        Assert.Contains("x:Name=\"ProfileManagementRestrictedPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ProfileGuestRestrictedNotice\"", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "ProfileManagementRestrictedPanel.Visibility = _isGuestMode ? Visibility.Collapsed : Visibility.Visible;",
            code, StringComparison.Ordinal);
        Assert.Contains("ProfileGuestRestrictedNotice.IsOpen = _isGuestMode;", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Actions_sensibles_de_profil_refusent_le_mode_invite_meme_hors_panneau()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");

        // Garde directe dans chaque methode (defense en profondeur), pas seulement
        // la visibilite du panneau - verifie en isolant chaque methode pour
        // s'assurer que la garde est bien DANS la bonne methode, pas ailleurs.
        var resetMethod = ExtractMethod(code, "private async void ResetProfileButton_Click");
        var quarantineMethod = ExtractMethod(code, "private async Task DeleteProfileAsync");
        var openDirMethod = ExtractMethod(code, "private async Task OpenProfileDirectory");

        Assert.Contains("if (_isGuestMode) return;", resetMethod, StringComparison.Ordinal);
        Assert.Contains("if (_isGuestMode) return;", quarantineMethod, StringComparison.Ordinal);
        Assert.Contains("if (_isGuestMode) return;", openDirMethod, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Signature introuvable : {signature}");

        var braceOpen = source.IndexOf('{', start);
        var depth = 0;
        var i = braceOpen;
        for (; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) break;
            }
        }

        return source[start..(i + 1)];
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
