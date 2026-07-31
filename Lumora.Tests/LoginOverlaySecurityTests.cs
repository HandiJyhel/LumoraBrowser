using Xunit;

namespace Lumora.Tests;

// Trouve en usage reel le 2026-07-29 (captures a l'appui) :
// - LoginOverlay n'avait aucun Canvas.ZIndex explicite ("dernier enfant =
//   au-dessus de tout" ne tient pas des qu'un frere porte un ZIndex explicite,
//   ce qui gagne toujours sur l'ordre de declaration) - la colonne identitaire
//   et sa capsule (ZIndex 33-35) restaient cliquables PAR-DESSUS l'ecran de
//   connexion/verrouillage ;
// - aucun des trois points d'affichage de LoginOverlay (premier lancement,
//   changement de profil, verrouillage) n'arretait les navigations en cours,
//   donc une page deja en chargement (ou un onglet en arriere-plan) pouvait
//   continuer et declencher le bloqueur de pub, ecran verrouille affiche
//   par-dessus.
public sealed class LoginOverlaySecurityTests
{
    [Fact]
    public void LoginOverlay_a_un_zindex_explicite_au_dessus_de_la_colonne_identitaire()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        var loginIndex = xaml.IndexOf("x:Name=\"LoginOverlay\"", StringComparison.Ordinal);
        Assert.True(loginIndex >= 0, "LoginOverlay introuvable.");
        var loginDeclaration = xaml.Substring(loginIndex, Math.Min(200, xaml.Length - loginIndex));
        Assert.Contains("Canvas.ZIndex=\"90\"", loginDeclaration, StringComparison.Ordinal);

        var wizardIndex = xaml.IndexOf("x:Name=\"SetupWizardOverlay\"", StringComparison.Ordinal);
        Assert.True(wizardIndex >= 0, "SetupWizardOverlay introuvable.");
        var wizardDeclaration = xaml.Substring(wizardIndex, Math.Min(200, xaml.Length - wizardIndex));
        Assert.Contains("Canvas.ZIndex=\"95\"", wizardDeclaration, StringComparison.Ordinal);

        // Regression : tout ZIndex porte par un frere de LoginOverlay dans
        // RootShell (colonne identitaire, barres plein ecran...) doit rester
        // strictement sous 90.
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(xaml, "Canvas\\.ZIndex=\"(\\d+)\""))
        {
            var value = int.Parse(m.Groups[1].Value);
            Assert.True(value is 90 or 95 || value < 90, $"ZIndex inattendu >= 90 hors LoginOverlay/SetupWizardOverlay : {value}");
        }
    }

    [Fact]
    public void Affichage_de_l_ecran_de_connexion_arrete_les_navigations_en_cours()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.LoginSecurity.cs");

        var showChrome = ExtractMethod(code, "private void ShowLoginOverlayChrome()");
        Assert.Contains("StopAllTabNavigations();", showChrome, StringComparison.Ordinal);
        Assert.Contains("BrowserHost.IsHitTestVisible = false;", showChrome, StringComparison.Ordinal);
        Assert.Contains("LoginOverlay.Visibility = Visibility.Visible;", showChrome, StringComparison.Ordinal);

        var stopAll = ExtractMethod(code, "private void StopAllTabNavigations()");
        Assert.Contains("tab.View?.CoreWebView2?.Stop();", stopAll, StringComparison.Ordinal);

        // Les trois points d'entree historiques doivent tous passer par ce
        // meme chemin, pas dupliquer le bloc a la main (c'est exactement ce
        // qui avait laisse passer l'oubli du premier coup).
        var profileCode = ReadRepoFile("Lumora.WinUI", "MainWindow.Profile.cs");
        Assert.DoesNotContain(
            "BrowserHost.IsHitTestVisible = false;\n        LoginOverlay.Visibility = Visibility.Visible;\n        LoginOverlay.Focus(FocusState.Programmatic);",
            profileCode, StringComparison.Ordinal);

        var initOverlay = ExtractMethod(profileCode, "private async Task InitializeLoginOverlayAsync()");
        Assert.Contains("ShowLoginOverlayChrome();", initOverlay, StringComparison.Ordinal);

        var showProfileOverlay = ExtractMethod(profileCode, "private void ShowProfileOverlay()");
        Assert.Contains("ShowLoginOverlayChrome();", showProfileOverlay, StringComparison.Ordinal);

        var lockSession = ExtractMethod(profileCode, "private void LockSessionNow(string statusMessage)");
        Assert.Contains("ShowLoginOverlayChrome();", lockSession, StringComparison.Ordinal);
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
