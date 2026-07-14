using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class AddressSuggestionEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private static AddressSuggestionCandidate History(
        string url, string title, int visits = 1, DateTimeOffset? lastVisit = null) =>
        new(AddressSuggestionKind.History, title, url, VisitCount: visits, LastVisit: lastVisit ?? Now.AddDays(-1));

    [Fact]
    public void RequeteVide_AucuneSuggestion()
    {
        var candidates = new[] { History("https://www.youtube.com", "YouTube") };

        Assert.Empty(AddressSuggestionEngine.Suggest("", candidates, Now));
        Assert.Empty(AddressSuggestionEngine.Suggest("   ", candidates, Now));
        Assert.Empty(AddressSuggestionEngine.Suggest(null, candidates, Now));
    }

    [Fact]
    public void PrefixeDeDomaine_PasseDevantLeTitre()
    {
        var candidates = new[]
        {
            History("https://example.org/article", "Pourquoi YouTube change"),
            History("https://www.youtube.com", "YouTube")
        };

        var results = AddressSuggestionEngine.Suggest("you", candidates, Now);

        Assert.Equal(2, results.Count);
        Assert.Equal("https://www.youtube.com", results[0].Url);
    }

    [Fact]
    public void AccentsIgnores_MeteoSansAccentTrouveLaPageAccentuee()
    {
        var candidates = new[] { History("https://meteofrance.com", "Météo France") };

        var results = AddressSuggestionEngine.Suggest("meteo", candidates, Now);

        Assert.Single(results);
        Assert.Equal("Météo France", results[0].Title);
    }

    [Fact]
    public void MemeUrl_OngletOuvertGagneSurFavoriEtHistorique()
    {
        var candidates = new[]
        {
            History("https://github.com/lumora", "Lumora - GitHub", visits: 5),
            new AddressSuggestionCandidate(AddressSuggestionKind.Bookmark, "Lumora", "https://github.com/lumora"),
            new AddressSuggestionCandidate(AddressSuggestionKind.OpenTab, "Lumora", "https://github.com/lumora", TabId: 7)
        };

        var results = AddressSuggestionEngine.Suggest("lumora", candidates, Now);

        Assert.Single(results);
        Assert.Equal(AddressSuggestionKind.OpenTab, results[0].Kind);
        Assert.Equal(7, results[0].TabId);
    }

    [Fact]
    public void UrlsInternesLumora_JamaisSuggerees()
    {
        var candidates = new[]
        {
            History("lumora://accueil", "Accueil Lumora"),
            History("https://accueil-jardinage.fr", "Accueil jardinage")
        };

        var results = AddressSuggestionEngine.Suggest("accueil", candidates, Now);

        Assert.Single(results);
        Assert.Equal("https://accueil-jardinage.fr", results[0].Url);
    }

    [Fact]
    public void PlusieursMots_TousDoiventCorrespondre()
    {
        var candidates = new[]
        {
            History("https://docs.example.com/guide", "Guide complet WinUI"),
            History("https://docs.example.com/autre", "Guide complet Rust")
        };

        var results = AddressSuggestionEngine.Suggest("guide winui", candidates, Now);

        Assert.Single(results);
        Assert.Equal("https://docs.example.com/guide", results[0].Url);
    }

    [Fact]
    public void PageTresVisitee_PasseDevantPageVisiteeUneFois()
    {
        var candidates = new[]
        {
            History("https://mail.example.com", "Courrier", visits: 1),
            History("https://courrier.example.org", "Courrier", visits: 9)
        };

        var results = AddressSuggestionEngine.Suggest("courrier", candidates, Now);

        Assert.Equal("https://courrier.example.org", results[0].Url);
    }

    [Fact]
    public void NombreDeResultats_Plafonne()
    {
        var candidates = Enumerable.Range(0, 20)
            .Select(i => History($"https://site{i}.example.com", $"Site {i}"))
            .ToArray();

        var results = AddressSuggestionEngine.Suggest("site", candidates, Now);

        Assert.Equal(AddressSuggestionEngine.DefaultMaxResults, results.Count);
    }

    [Fact]
    public void AggregateHistory_RegroupeParUrlEtCompteLesVisites()
    {
        var visits = new[]
        {
            ("https://a.example.com", "Ancien titre", Now.AddDays(-3)),
            ("https://a.example.com", "Nouveau titre", Now.AddHours(-1)),
            ("https://b.example.com", "Autre page", Now.AddDays(-1))
        };

        var candidates = AddressSuggestionEngine.AggregateHistory(visits);

        Assert.Equal(2, candidates.Count);
        var pageA = candidates.Single(c => c.Url == "https://a.example.com");
        Assert.Equal(2, pageA.VisitCount);
        Assert.Equal("Nouveau titre", pageA.Title);
        Assert.Equal(Now.AddHours(-1), pageA.LastVisit);
    }

    [Fact]
    public void TitreVide_RemplaceParUrl()
    {
        var candidates = new[] { History("https://sans-titre.example.com", "") };

        var results = AddressSuggestionEngine.Suggest("sans-titre", candidates, Now);

        Assert.Single(results);
        Assert.Equal("https://sans-titre.example.com", results[0].Title);
    }

    [Fact]
    public void HttpEtHttps_MemePageDedoublonnee()
    {
        var candidates = new[]
        {
            History("http://exemple.fr/page", "Page", visits: 2),
            History("https://exemple.fr/page/", "Page", visits: 3)
        };

        var results = AddressSuggestionEngine.Suggest("exemple", candidates, Now);

        Assert.Single(results);
    }
}
