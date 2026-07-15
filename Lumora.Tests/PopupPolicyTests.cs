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
        string? opener = Opener,
        int popupsAlreadyOpenedForGesture = 0,
        bool openerUnderAdPressure = false)
    {
        var ads = adHosts ?? [];
        var allow = whitelisted ?? [];
        return PopupPolicy.Decide(
            popupUri,
            opener,
            isUserInitiated,
            blockerEnabled,
            host => ads.Contains(host, StringComparer.OrdinalIgnoreCase),
            host => allow.Contains(host, StringComparer.OrdinalIgnoreCase),
            popupsAlreadyOpenedForGesture,
            openerUnderAdPressure);
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
    [InlineData("https://appleid.apple.com/auth/authorize")]
    [InlineData("https://sso.entreprise.fr/")]
    [InlineData("https://login.banque.fr/espace-client")]
    public void FenetreDauthentification_ToujoursAutorisee(string uri)
    {
        // Fournisseurs d'identité connus et conventions d'hôte (login., sso.…) :
        // certains flux OAuth ouvrent leur fenêtre après un aller-retour réseau,
        // hors du handler de clic — jamais bloqués, même « automatiques ».
        Assert.Equal(PopupVerdict.Allow, Decide(uri, isUserInitiated: false));
    }

    [Fact]
    public void CheminLogin_SurGeste_Autorise()
    {
        // Le mot-clé de chemin reste accepté quand l'utilisateur a cliqué.
        Assert.Equal(PopupVerdict.Allow, Decide("https://exemple.fr/oauth/authorize", isUserInitiated: true));
    }

    [Fact]
    public void CheminLogin_SansGeste_Bloque()
    {
        // Durcissement 0.78.2 : une URL qui ne fait que RESSEMBLER à un login
        // (mot-clé dans le chemin, hôte quelconque) n'ouvre plus toute seule —
        // c'était la porte des popunders « pub.example/login/redirect ».
        Assert.Equal(PopupVerdict.BlockAutomatic, Decide("https://exemple.fr/oauth/authorize", isUserInitiated: false));
    }

    [Fact]
    public void CheminLogin_DomainePublicitaire_Bloque()
    {
        // Même sur geste : le domaine répertorié est éliminatoire AVANT
        // l'heuristique de login par mot-clé.
        Assert.Equal(
            PopupVerdict.BlockAdDomain,
            Decide("https://pub.example/login/redirect", isUserInitiated: true, adHosts: ["pub.example"]));
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

    [Fact]
    public void DeuxiemePopupDuMemeGeste_Bloquee()
    {
        // Aucun site légitime n'ouvre deux fenêtres pour un seul clic.
        Assert.Equal(
            PopupVerdict.BlockGestureFlood,
            Decide("https://jeux-en-promo.example/", isUserInitiated: true, popupsAlreadyOpenedForGesture: 1));
    }

    [Fact]
    public void RafaleDePopups_NeBloquePasLauthentification()
    {
        // Le plafond par geste ne s'applique jamais aux fenêtres de connexion.
        Assert.Equal(
            PopupVerdict.Allow,
            Decide("https://accounts.google.com/o/oauth2/auth", isUserInitiated: true, popupsAlreadyOpenedForGesture: 3));
    }

    [Fact]
    public void SiteSousPressionPublicitaire_PopupCrossDomaine_BloqueeNet()
    {
        // 0.78.3.1 : sur un site pris en flagrant délit publicitaire, le clic
        // détourné ne produit plus AUCUN onglet — blocage complet, silencieux.
        Assert.Equal(
            PopupVerdict.BlockUnderAdPressure,
            Decide("https://jeux-en-promo.example/", isUserInitiated: true, openerUnderAdPressure: true));
    }

    [Fact]
    public void SiteSousPressionPublicitaire_AboutBlank_BloqueeNet()
    {
        // window.open('about:blank') + document.write : l'astuce classique
        // pour servir la pub sans domaine — bloquée aussi sous pression.
        Assert.Equal(
            PopupVerdict.BlockUnderAdPressure,
            Decide("about:blank", isUserInitiated: true, openerUnderAdPressure: true));
    }

    [Fact]
    public void SiteSousPressionPublicitaire_PopupMemeSite_Autorisee()
    {
        // Le site garde le droit d'ouvrir SES propres pages (lecteur video,
        // page de detail...) : seul le cross-domaine est parasite.
        Assert.Equal(
            PopupVerdict.Allow,
            Decide("https://site-de-films.example/lecteur", isUserInitiated: true, openerUnderAdPressure: true));
    }

    [Fact]
    public void SiteSousPressionPublicitaire_Authentification_Autorisee()
    {
        Assert.Equal(
            PopupVerdict.Allow,
            Decide("https://accounts.google.com/o/oauth2/auth", isUserInitiated: true, openerUnderAdPressure: true));
    }

    [Fact]
    public void SitePropre_PopupAuPremierPlan()
    {
        Assert.Equal(
            PopupVerdict.Allow,
            Decide("https://wikipedia.org/", isUserInitiated: true, openerUnderAdPressure: false));
    }
}
