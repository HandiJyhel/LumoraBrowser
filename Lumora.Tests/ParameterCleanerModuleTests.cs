using Lumora.Privacy.ParameterCleaner;
using Xunit;

namespace Lumora.Tests;

public class ParameterCleanerModuleTests
{
    private readonly ParameterCleanerModule _module = new();

    [Theory]
    [InlineData("https://exemple.com/page?utm_source=news&utm_medium=email", "https://exemple.com/page")]
    [InlineData("https://exemple.com/page?gclid=abc123", "https://exemple.com/page")]
    [InlineData("https://exemple.com/page?fbclid=xyz", "https://exemple.com/page")]
    public void Supprime_les_parametres_de_tracking_connus(string input, string expected)
    {
        Assert.Equal(expected, _module.CleanUrl(input));
    }

    [Fact]
    public void Conserve_les_parametres_legitimes_a_cote_des_parametres_de_tracking()
    {
        var result = _module.CleanUrl("https://exemple.com/page?id=42&utm_source=news");
        Assert.Equal("https://exemple.com/page?id=42", result);
    }

    [Fact]
    public void Ne_touche_pas_une_url_sans_parametre_de_tracking()
    {
        Assert.Null(_module.CleanUrl("https://exemple.com/page?id=42"));
    }

    [Fact]
    public void Ne_touche_pas_une_url_sans_query_ni_fragment()
    {
        Assert.Null(_module.CleanUrl("https://exemple.com/page"));
    }

    [Fact]
    public void Url_invalide_ne_leve_pas_d_exception()
    {
        Assert.Null(_module.CleanUrl("pas-une-url?utm_source=x"));
    }

    [Fact]
    public void Supprime_plusieurs_parametres_de_familles_differentes_a_la_fois()
    {
        var result = _module.CleanUrl("https://exemple.com/?utm_source=a&msclkid=b&mc_cid=c&keep=1");
        Assert.Equal("https://exemple.com/?keep=1", result);
    }
}
