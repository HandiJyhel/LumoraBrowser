using Xunit;

namespace Lumora.Tests;

// Meme style que IdentitySpineVisualIdentityTests/UiDensityVisualIdentityTests :
// assertions structurelles sur le source (pas d'execution UI). Couvre la
// fusion des onglets "Technique" et "Profil local" de l'ecran A propos en un
// seul onglet "Technique et profil" (demande utilisateur du 2026-08-05 : les
// deux dernieres rubriques etaient trop petites pour rester separees).
public sealed class AboutScreenTests
{
    [Fact]
    public void Onglets_Technique_et_Profil_local_sont_fusionnes()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.DoesNotContain("x:Name=\"AboutNavProfile\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"AboutSectionProfile\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Tag=\"profile\" GroupName=\"AboutSubNav\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AboutNavTechnical\" Content=\"Technique et profil\"", xaml, StringComparison.Ordinal);
        // Le contenu des deux anciennes rubriques reste present, juste regroupe
        // sous le meme x:Name AboutSectionTechnical (non-regression : rien
        // supprime, seulement reorganise).
        Assert.Contains("x:Name=\"AboutProfilePathText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Interface : C# + WinUI 3\"", xaml, StringComparison.Ordinal);

        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");
        Assert.DoesNotContain("AboutSectionProfile", code, StringComparison.Ordinal);
        // Le chemin du profil reste bien assigne au meme TextBlock qu'avant.
        Assert.Contains("AboutProfilePathText.Text = _profile.ProfileDir;", code, StringComparison.Ordinal);
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
