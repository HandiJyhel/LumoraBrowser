using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public sealed class AddressNormalizerTests
{
    [Theory]
    [InlineData("https://exemple.fr/page")]
    [InlineData("http://exemple.fr")]
    [InlineData("lumora://accueil")]
    [InlineData("file://C:/temp/page.html")]
    public void KnownProtocols_PassThroughUnchanged(string input)
    {
        Assert.Equal(input, AddressNormalizer.Normalize(input, "google"));
    }

    [Fact]
    public void EmptyInput_FallsBackToProvidedDefault()
    {
        Assert.Equal("lumora://accueil", AddressNormalizer.Normalize("  ", "google"));
        Assert.Equal("about:blank", AddressNormalizer.Normalize("", "google", emptyFallback: "about:blank"));
    }

    [Fact]
    public void DomainLikeInput_GetsHttps()
    {
        Assert.Equal("https://exemple.fr", AddressNormalizer.Normalize("exemple.fr", "google"));
    }

    [Theory]
    [InlineData("localhost", "http://localhost")]
    [InlineData("localhost:8080", "http://localhost:8080")]
    public void Localhost_StaysHttp(string input, string expected)
    {
        Assert.Equal(expected, AddressNormalizer.Normalize(input, "google"));
    }

    [Fact]
    public void QueryWithSpaces_BecomesSearch()
    {
        var url = AddressNormalizer.Normalize("recette tarte tatin", "google");
        Assert.StartsWith("https://www.google.com/search?q=recette%20tarte%20tatin", url);
    }

    [Fact]
    public void SingleWord_BecomesSearch()
    {
        var url = AddressNormalizer.Normalize("meteo", "duckduckgo");
        Assert.StartsWith("https://duckduckgo.com/?q=meteo", url);
    }

    [Theory]
    [InlineData("duckduckgo", "https://duckduckgo.com/")]
    [InlineData("brave", "https://search.brave.com/")]
    [InlineData("bing", "https://www.bing.com/")]
    [InlineData("google", "https://www.google.com/")]
    [InlineData("inconnu", "https://www.google.com/")]
    public void SearchUrl_RespectsConfiguredEngine(string engine, string expectedPrefix)
    {
        Assert.StartsWith(expectedPrefix, AddressNormalizer.SearchUrl("test", engine));
    }
}
