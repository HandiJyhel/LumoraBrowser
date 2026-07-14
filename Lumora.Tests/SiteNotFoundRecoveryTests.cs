using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class SiteNotFoundRecoveryTests
{
    // ── Requête de recherche ────────────────────────────────────────────────

    // Le nom du site SANS extension : c'est ce qui retrouve le mieux un site
    // qui a déménagé vers une autre extension.
    [Theory]
    [InlineData("https://www.monsite.fr/page?x=1", "monsite")]
    [InlineData("https://monsite.fr", "monsite")]
    [InlineData("http://WWW.MonSite.FR", "monsite")]
    [InlineData("https://www.zone-telechargement.win/", "zone-telechargement")]
    [InlineData("https://forum.monsite.fr/sujet", "monsite")]
    public void RequeteDeRecherche_NomSansExtension(string url, string attendu) =>
        Assert.Equal(attendu, SiteNotFoundRecovery.SearchQueryFor(url));

    [Fact]
    public void RequeteDeRecherche_NomTropCourt_RepliSurLHote() =>
        Assert.Equal("t.co", SiteNotFoundRecovery.SearchQueryFor("https://t.co/abc"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("lumora://accueil")]
    [InlineData("pas une url")]
    [InlineData("https://localhost/admin")]
    public void RequeteDeRecherche_NullHorsWeb(string? url) =>
        Assert.Null(SiteNotFoundRecovery.SearchQueryFor(url));

    // ── Variante www ────────────────────────────────────────────────────────

    [Fact]
    public void VarianteWww_AjouteLePrefixe()
    {
        var variante = SiteNotFoundRecovery.WwwVariantOf("https://monsite.fr/dossier/page");

        Assert.Equal("https://www.monsite.fr/dossier/page", variante);
    }

    [Fact]
    public void VarianteWww_RetireLePrefixe()
    {
        var variante = SiteNotFoundRecovery.WwwVariantOf("https://www.monsite.fr/");

        Assert.Equal("https://monsite.fr/", variante);
    }

    [Fact]
    public void VarianteWww_NullPourPageInterne() =>
        Assert.Null(SiteNotFoundRecovery.WwwVariantOf("lumora://accueil"));

    // ── Domaine proche déjà connu ───────────────────────────────────────────

    [Fact]
    public void DomaineConnu_FauteDeFrappeCorrigee()
    {
        var connus = new[] { "https://www.google.fr/recherche", "https://example.org" };

        var suggestion = SiteNotFoundRecovery.ClosestKnownUrl("https://gogle.fr", connus);

        Assert.Equal("https://www.google.fr", suggestion);
    }

    [Fact]
    public void DomaineConnu_MauvaiseExtensionCorrigee()
    {
        // « monsite.com » tapé alors que le favori connaît « monsite.fr » :
        // même étiquette enregistrable, seule l'extension diffère.
        var connus = new[] { "https://monsite.fr/accueil" };

        var suggestion = SiteNotFoundRecovery.ClosestKnownUrl("https://monsite.com", connus);

        Assert.Equal("https://monsite.fr", suggestion);
    }

    [Fact]
    public void DomaineConnu_HoteIdentiqueJamaisPropose()
    {
        // Le même domaine (avec ou sans www) échouerait pareil : pas de suggestion.
        var connus = new[] { "https://www.monsite.fr", "https://monsite.fr/page" };

        Assert.Null(SiteNotFoundRecovery.ClosestKnownUrl("https://monsite.fr", connus));
    }

    [Fact]
    public void DomaineConnu_TropEloigneIgnore()
    {
        var connus = new[] { "https://wikipedia.org" };

        Assert.Null(SiteNotFoundRecovery.ClosestKnownUrl("https://youtube.com", connus));
    }

    [Fact]
    public void DomaineConnu_LePlusProcheGagne()
    {
        var connus = new[]
        {
            "https://monsit.fr",     // distance 2 de "monsitee.fr"
            "https://monsite.fr"     // distance 1
        };

        var suggestion = SiteNotFoundRecovery.ClosestKnownUrl("https://monsitee.fr", connus);

        Assert.Equal("https://monsite.fr", suggestion);
    }

    [Fact]
    public void DomaineConnu_EntreesInvalidesIgnorees()
    {
        var connus = new[] { null, "", "lumora://accueil", "pas une url", "https://monsite.fr" };

        var suggestion = SiteNotFoundRecovery.ClosestKnownUrl("https://monsitee.fr", connus);

        Assert.Equal("https://monsite.fr", suggestion);
    }

    [Fact]
    public void DomaineConnu_UrlEnEchecInvalide()
    {
        var connus = new[] { "https://monsite.fr" };

        Assert.Null(SiteNotFoundRecovery.ClosestKnownUrl("lumora://accueil", connus));
        Assert.Null(SiteNotFoundRecovery.ClosestKnownUrl(null, connus));
    }
}
