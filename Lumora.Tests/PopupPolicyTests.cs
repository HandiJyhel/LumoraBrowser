using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class PopupPolicyTests
{
    private const string Opener = "https://site-de-films.example/page";

    private static PopupVerdict Decide(
        string? popupUri,
        bool isUserInitiated,
        bool blockerEnabled = true,
        string[]? adHosts = null,
        string[]? whitelisted = null,
        string? opener = Opener)
    {
        var ads = adHosts ?? [];
        var allow = whitelisted ?? [];
        return PopupPolicy.Decide(
            popupUri,
            opener,
            isUserInitiated,
            blockerEnabled,
            host => ads.Contains(host, StringComparer.OrdinalIgnoreCase),
            host => allow.Contains(host, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void PopupAutomatique_Bloquee()
    {
        // Le popunder classique : window.open hors de tout geste utilisateur.
        Assert.Equal(PopupVerdict.BlockAutomatic, Decide("https://jeux-en-promo.example/", isUserInitiated: false));
    }

    [Fact]
    public void PopupAutomatique_AboutBlank_Bloquee() =>
        Assert.Equal(PopupVerdict.BlockAutomatic, Decide("about:blank", isUserInitiated: false));

    [Fact]
    public void ClicVersDomainePublicitaire_Bloque()
    {
        // Clic détourné : geste réel, mais destination répertoriée publicitaire.
        Assert.Equal(
            PopupVerdict.BlockAdDomain,
            Decide("https://ads.regie.example/jeu", isUserInitiated: true, adHosts: ["ads.regie.example"]));
    }

    [Fact]
    public void ClicVersSiteNormal_Autorise() =>
        Assert.Equal(PopupVerdict.Allow, Decide("https://wikipedia.org/", isUserInitiated: true));

    [Fact]
    public void ClicVersAboutBlank_Autorise() =>
        Assert.Equal(PopupVerdict.Allow, Decide("about:blank", isUserInitiated: true));

    [Theory]
    [InlineData("https://accounts.google.com/o/oauth2/auth")]
    [InlineData("https://login.microsoftonline.com/common")]
    [InlineData("https://exemple.fr/oauth/authorize")]
    [InlineData("https://sso.entreprise.fr/")]
    public void FenetreDauthentification_ToujoursAutorisee(string uri)
    {
        // Certains flux OAuth ouvrent leur fenêtre après un aller-retour réseau,
        // hors du handler de clic : jamais bloqués, même « automatiques ».
        Assert.Equal(PopupVerdict.Allow, Decide(uri, isUserInitiated: false));
    }

    [Fact]
    public void SiteWhitelisted_GardeSesPopups()
    {
        Assert.Equal(
            PopupVerdict.Allow,
            Decide("https://ads.regie.example/", isUserInitiated: false,
                adHosts: ["ads.regie.example"],
                whitelisted: ["site-de-films.example"]));
    }

    [Fact]
    public void BloqueurDesactive_ToutPasse() =>
        Assert.Equal(PopupVerdict.Allow, Decide("https://ads.regie.example/", isUserInitiated: false, blockerEnabled: false));

    [Fact]
    public void PopupPubliciaire_PrioritaireSurLeGeste()
    {
        // Même sans opener connu, un domaine listé reste bloqué sur clic.
        Assert.Equal(
            PopupVerdict.BlockAdDomain,
            Decide("https://ads.regie.example/", isUserInitiated: true, adHosts: ["ads.regie.example"], opener: null));
    }
}
