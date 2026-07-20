using Xunit;

namespace Lumora.Tests;

// Verrouille le choix de moteur de recherche fixe pour Incognito (DuckDuckGo),
// independant du reglage global _uiSettings.SearchEngine. Diagnostique le
// 2026-07-20 : Google bloque parfois des plages entieres de noeuds de sortie
// Tor pour la recherche, avec une boucle de captcha qui ne se resout jamais
// quel que soit le nombre de circuits Tor demandes - comportement cote
// serveur Google, pas un bug reseau ni un bug Lumora. DuckDuckGo est
// nettement plus tolerant envers Tor.
public sealed class IncognitoSearchEngineTests
{
    [Fact]
    public void Barre_adresse_incognito_utilise_duckduckgo_fixe_pas_le_reglage_global()
    {
        var source = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");

        Assert.Contains("private const string IncognitoSearchEngine = \"duckduckgo\";", source, StringComparison.Ordinal);
        Assert.Contains("AddressNormalizer.Normalize(raw, IncognitoSearchEngine)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddressNormalizer.Normalize(raw, _uiSettings.SearchEngine)", source, StringComparison.Ordinal);
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
