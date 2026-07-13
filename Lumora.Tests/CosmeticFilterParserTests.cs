using Lumora.Privacy.CosmeticFilter;
using Xunit;

namespace Lumora.Tests;

public class CosmeticFilterParserTests
{
    private static List<CosmeticRule> Parse(params string[] lines) =>
        CosmeticFilterParser.ParseLines(lines).ToList();

    [Fact]
    public void Regle_generique_sans_domaine_est_reconnue()
    {
        var rule = Assert.Single(Parse("##.pub-banniere"));
        Assert.Null(rule.Domain);
        Assert.Equal(".pub-banniere", rule.Selector);
        Assert.False(rule.IsException);
    }

    [Fact]
    public void Regle_de_domaine_est_reconnue()
    {
        var rule = Assert.Single(Parse("exemple.com##.pub-banniere"));
        Assert.Equal("exemple.com", rule.Domain);
        Assert.Equal(".pub-banniere", rule.Selector);
    }

    [Fact]
    public void Regle_multi_domaines_produit_une_entree_par_domaine()
    {
        var rules = Parse("exemple.com,exemple.fr##.pub-banniere");
        Assert.Equal(2, rules.Count);
        Assert.Contains(rules, r => r.Domain == "exemple.com");
        Assert.Contains(rules, r => r.Domain == "exemple.fr");
    }

    [Fact]
    public void Exception_hash_arobase_hash_est_marquee_comme_telle()
    {
        var rule = Assert.Single(Parse("exemple.com#@#.pub-banniere"));
        Assert.True(rule.IsException);
    }

    [Fact]
    public void Domaine_exclu_par_tilde_est_ignore()
    {
        Assert.Empty(Parse("~exemple.com##.pub-banniere"));
    }

    [Theory]
    [InlineData("! ceci est un commentaire")]
    [InlineData("[Adblock Plus 2.0]")]
    [InlineData("||exemple.com^")]
    public void Lignes_non_cosmetiques_sont_ignorees(string line)
    {
        Assert.Empty(Parse(line));
    }

    [Theory]
    [InlineData("exemple.com##div:has-text(pub)")]
    [InlineData("exemple.com##.x:upward(3)")]
    [InlineData("exemple.com##+js(abort-on-property-read)")]
    public void Filtres_proceduraux_non_standards_sont_rejetes(string line)
    {
        Assert.Empty(Parse(line));
    }
}
