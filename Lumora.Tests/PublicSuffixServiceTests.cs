using Lumora.WinUI.Credentials;
using Xunit;

namespace Lumora.Tests;

// Vérifie la normalisation d'origine et le domaine enregistrable (Public Suffix List
// embarquée). Ces sorties pilotent le rapprochement des mots de passe et le cache
// de favicons : leur stabilité est critique.
public class PublicSuffixServiceTests
{
    [Theory]
    [InlineData("https://www.exemple.fr/page", "https://exemple.fr")]
    [InlineData("https://EXEMPLE.fr", "https://exemple.fr")]
    [InlineData("http://sous.exemple.fr", "http://sous.exemple.fr")]
    public void OriginOf_normalise_schema_et_hote(string input, string expected)
    {
        Assert.Equal(expected, PublicSuffixService.OriginOf(input));
    }

    [Theory]
    [InlineData("https://sous.exemple.fr", "exemple.fr")]
    [InlineData("https://a.b.exemple.co.uk", "exemple.co.uk")]   // suffixe multi-niveaux
    [InlineData("https://www.exemple.com", "exemple.com")]
    public void RootDomainOf_utilise_la_public_suffix_list(string input, string expected)
    {
        Assert.Equal(expected, PublicSuffixService.RootDomainOf(input));
    }

    [Fact]
    public void RootDomainOf_gere_les_adresses_ip()
    {
        Assert.Equal("192.168.1.10", PublicSuffixService.RootDomainOf("http://192.168.1.10:8080"));
    }

    [Fact]
    public void HostOf_retire_le_prefixe_www()
    {
        Assert.Equal("exemple.fr", PublicSuffixService.HostOf("https://www.exemple.fr/x"));
    }
}
