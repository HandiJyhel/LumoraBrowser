using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class StartMenuTileRegistryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Registre_naPasDIdDuplique()
    {
        var ids = StartMenuTileRegistry.All.Select(t => t.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Registre_naPasDeGlypheOuDeTitreVide()
    {
        Assert.All(StartMenuTileRegistry.All, tile =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tile.Title));
            Assert.False(string.IsNullOrWhiteSpace(tile.Glyph));
            Assert.False(string.IsNullOrWhiteSpace(tile.Section));
        });
    }

    [Fact]
    public void Find_retrouveUneTuileConnue()
    {
        var tile = StartMenuTileRegistry.Find(StartMenuTileIds.History);

        Assert.NotNull(tile);
        Assert.Equal("Historique", tile!.Title);
    }

    [Fact]
    public void Find_renvoieNullPourUnIdInconnu()
    {
        Assert.Null(StartMenuTileRegistry.Find("id-inexistant"));
    }

    [Fact]
    public void ScoreTile_requeteVide_renvoieUnScorePositif()
    {
        var tile = StartMenuTileRegistry.Find(StartMenuTileIds.Favoris)!;

        Assert.True(StartMenuTileRegistry.ScoreTile(tile, "") > 0);
        Assert.True(StartMenuTileRegistry.ScoreTile(tile, "   ") > 0);
    }

    [Fact]
    public void ScoreTile_correspondanceExacte_batLaCorrespondancePartielle()
    {
        var historique = StartMenuTileRegistry.Find(StartMenuTileIds.History)!;
        var favoris = StartMenuTileRegistry.Find(StartMenuTileIds.Favoris)!;

        var scoreExact = StartMenuTileRegistry.ScoreTile(historique, "Historique");
        var scoreAucun = StartMenuTileRegistry.ScoreTile(favoris, "Historique");

        Assert.True(scoreExact > 0);
        Assert.Equal(0, scoreAucun);
    }

    [Fact]
    public void ScoreTile_ignoreLaCasse()
    {
        var tile = StartMenuTileRegistry.Find(StartMenuTileIds.Downloads)!;

        Assert.True(StartMenuTileRegistry.ScoreTile(tile, "téléchargements") > 0);
        Assert.True(StartMenuTileRegistry.ScoreTile(tile, "TÉLÉCHARGEMENTS") > 0);
    }

    [Fact]
    public void UsageScore_frequencePlafonnee_auDelaDeVingtOuvertures()
    {
        var recent = Now.AddMinutes(-5);
        var vingt = new StartMenuTileUsage(StartMenuTileIds.Notes, 20, recent);
        var centVingt = new StartMenuTileUsage(StartMenuTileIds.Notes, 120, recent);

        Assert.Equal(
            StartMenuTileRegistry.UsageScore(vingt, Now),
            StartMenuTileRegistry.UsageScore(centVingt, Now));
    }

    [Fact]
    public void UsageScore_recenceDomineSurFrequenceAncienne()
    {
        var ouverteHier = new StartMenuTileUsage(StartMenuTileIds.Notes, 1, Now.AddHours(-1));
        var ouverteSouventIlYaUnMois = new StartMenuTileUsage(StartMenuTileIds.History, 5, Now.AddDays(-30));

        Assert.True(
            StartMenuTileRegistry.UsageScore(ouverteHier, Now) >
            StartMenuTileRegistry.UsageScore(ouverteSouventIlYaUnMois, Now));
    }

    [Fact]
    public void TopTiles_trieParScoreDecroissant()
    {
        var usage = new[]
        {
            new StartMenuTileUsage(StartMenuTileIds.Favoris, 1, Now.AddDays(-10)),
            new StartMenuTileUsage(StartMenuTileIds.Vault, 5, Now.AddMinutes(-1)),
            new StartMenuTileUsage(StartMenuTileIds.Notes, 2, Now.AddHours(-3)),
        };

        var top = StartMenuTileRegistry.TopTiles(usage, Now, count: 2);

        Assert.Equal(2, top.Count);
        Assert.Equal(StartMenuTileIds.Vault, top[0]);
    }

    [Fact]
    public void TopTiles_respecteLaLimiteDemandee()
    {
        var usage = StartMenuTileRegistry.All
            .Select((t, i) => new StartMenuTileUsage(t.Id, 1, Now.AddMinutes(-i)))
            .ToList();

        var top = StartMenuTileRegistry.TopTiles(usage, Now, count: 4);

        Assert.Equal(4, top.Count);
    }
}
