using Lumora.Privacy.NetworkBlocker;
using Xunit;

namespace Lumora.Tests;

// Verrouille le comportement du parseur de filtres Adblock/uBlock, en particulier
// la non-régression du bug de sur-blocage : une règle restreinte à des sites précis
// ($domain=...) ne doit JAMAIS devenir un blocage global (casse des avatars Google, etc.).
public class FilterParserTests
{
    private static List<ParsedRule> Parse(params string[] lines) =>
        FilterParser.ParseLines(lines).ToList();

    [Fact]
    public void Regle_domaine_simple_est_bloquee()
    {
        var rules = Parse("||doubleclick.net^");
        var rule = Assert.Single(rules);
        Assert.Equal(RuleAction.Block, rule.Action);
        Assert.Equal("doubleclick.net", rule.Domain);
    }

    [Fact]
    public void Regle_restreinte_par_domaine_est_ignoree_pas_globale()
    {
        // Le bug historique : cette règle bloquait lh3.googleusercontent.com PARTOUT.
        var rules = Parse("||lh3.googleusercontent.com^$domain=manytoon.com|freemanga.me");
        Assert.Empty(rules);
    }

    [Fact]
    public void Le_domaine_legitime_ne_finit_jamais_dans_les_blocages()
    {
        var rules = Parse(
            "||lh3.googleusercontent.com^$domain=manytoon.com",
            "||blogger.googleusercontent.com^$domain=ainzscans.net");
        Assert.DoesNotContain(rules, r => r.Domain.Contains("googleusercontent.com"));
    }

    [Fact]
    public void Option_third_party_est_conservee()
    {
        var rules = Parse("||tracker.example.com^$third-party");
        var rule = Assert.Single(rules);
        Assert.True(rule.ThirdPartyOnly);
        Assert.Equal("tracker.example.com", rule.Domain);
    }

    [Fact]
    public void Regle_avec_chemin_devient_sous_chaine()
    {
        var rules = Parse("||googleusercontent.com/tracker/");
        var rule = Assert.Single(rules);
        Assert.Equal(string.Empty, rule.Domain);
        Assert.Equal("googleusercontent.com/tracker/", rule.Pattern);
    }

    [Fact]
    public void Exception_produit_une_regle_allow()
    {
        var rules = Parse("@@||exemple.com^");
        var rule = Assert.Single(rules);
        Assert.Equal(RuleAction.Allow, rule.Action);
        Assert.Equal("exemple.com", rule.Domain);
    }

    [Theory]
    [InlineData("! commentaire")]
    [InlineData("[Adblock Plus 2.0]")]
    [InlineData("exemple.com##.banniere-pub")]      // règle cosmétique
    public void Lignes_non_bloquantes_sont_ignorees(string line)
    {
        Assert.Empty(Parse(line));
    }
}
