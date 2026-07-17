using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// La politique anti-parasite 0.78.3.1 : les sites qui détournent les clics
// (chaque clic téléporte vers un site douteux, il faut revenir et recliquer)
// sont neutralisés silencieusement. Aucun comptage de clics : chaque tentative
// est bloquée, une par une.
public class NavigationHijackPolicyTests
{
    private const string From = "https://site-de-films.example/page";

    private static NavigationVerdict Decide(
        string toUri,
        string? fromUri = From,
        bool wasExplicit = false,
        bool userInitiated = false,
        bool strictEnabled = true,
        string[]? adHosts = null,
        string[]? whitelisted = null,
        bool pressure = false,
        bool recentPopup = false)
    {
        var ads = adHosts ?? [];
        var allow = whitelisted ?? [];
        return NavigationHijackPolicy.Decide(
            fromUri,
            toUri,
            wasExplicit,
            userInitiated,
            strictEnabled,
            host => ads.Contains(host, StringComparer.OrdinalIgnoreCase),
            host => allow.Contains(host, StringComparer.OrdinalIgnoreCase),
            pressure,
            recentPopup);
    }

    [Fact]
    public void AdresseDemandeeExplicitement_JamaisBloquee()
    {
        // Même un domaine répertorié passe si l'utilisateur l'a tapé lui-même.
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://ads.regie.example/", wasExplicit: true, adHosts: ["ads.regie.example"], pressure: true));
    }

    [Fact]
    public void DomaineRepertorie_Bloque_MemeSansPression()
    {
        // Le clic capturé vers une régie connue n'a pas d'excuse.
        Assert.Equal(
            NavigationVerdict.BlockAdDomain,
            Decide("https://ads.regie.example/promo", adHosts: ["ads.regie.example"]));
    }

    [Fact]
    public void ClicDetourne_SousPression_BloqueParasite()
    {
        // Redirection automatique cross-domaine depuis un site déjà pris en
        // flagrant délit publicitaire → parasite, bloqué net.
        Assert.Equal(
            NavigationVerdict.BlockParasite,
            Decide("https://boutique-douteuse.example/promo", pressure: true));
    }

    [Fact]
    public void TabUnder_ApresPopup_BloqueParasite()
    {
        // L'onglet vient d'ouvrir une popup et se redirige cross-domaine :
        // pattern tab-under, même sans pression réseau mesurée.
        Assert.Equal(
            NavigationVerdict.BlockParasite,
            Decide("https://boutique-douteuse.example/", recentPopup: true));
    }

    [Fact]
    public void NavigationCrossDomaine_SiteSain_Autorisee()
    {
        // Pas de pression, pas de popup récente : un lien externe normal.
        Assert.Equal(NavigationVerdict.Allow, Decide("https://wikipedia.org/article"));
    }

    [Fact]
    public void ClicUtilisateurCrossDomaine_SousPression_Autorise()
    {
        // Régression 0.78.3.2 : depuis une page de résultats ou une page chargée
        // en pubs, les clics légitimes vers un autre site étaient pris pour des
        // redirections parasites. Lumora doit bloquer les détournements, pas la
        // navigation normale.
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide(
                "https://fr.wikipedia.org/wiki/Tintin",
                fromUri: "https://www.google.com/search?q=tintin",
                userInitiated: true,
                pressure: true));
    }

    [Fact]
    public void ClicUtilisateurVersDomainePublicitaire_EncoreBloque()
    {
        Assert.Equal(
            NavigationVerdict.BlockAdDomain,
            Decide(
                "https://ads.regie.example/promo",
                userInitiated: true,
                adHosts: ["ads.regie.example"],
                pressure: true));
    }

    [Fact]
    public void TabUnderAvecGesteUtilisateur_EncoreBloque()
    {
        Assert.Equal(
            NavigationVerdict.BlockParasite,
            Decide(
                "https://boutique-douteuse.example/",
                userInitiated: true,
                pressure: true,
                recentPopup: true));
    }

    [Fact]
    public void MemeSiteRacine_ToujoursAutorise()
    {
        // Le site navigue chez lui (sous-domaine compris), même sous pression.
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://cdn.site-de-films.example/episode-2", pressure: true));
    }

    [Fact]
    public void PremiereNavigationDunOnglet_Autorisee()
    {
        // Pas d'origine web (onglet neuf) : rien à détourner.
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://boutique-douteuse.example/", fromUri: null, pressure: true));
    }

    [Fact]
    public void Authentification_Autorisee_MemeSousPression()
    {
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://accounts.google.com/o/oauth2/auth", pressure: true, recentPopup: true));
    }

    [Fact]
    public void SiteWhiteliste_GardeSesNavigations()
    {
        // Whitelist de l'origine OU de la destination : l'utilisateur a dit
        // « je fais confiance à ce site ».
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://boutique-douteuse.example/", whitelisted: ["site-de-films.example"], pressure: true));
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://boutique-douteuse.example/", whitelisted: ["boutique-douteuse.example"], pressure: true));
    }

    [Fact]
    public void BlocageStrictDesactive_ToutPasse()
    {
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://ads.regie.example/", strictEnabled: false, adHosts: ["ads.regie.example"], pressure: true));
    }

    [Fact]
    public void AdresseNonWeb_Ignoree()
    {
        Assert.Equal(NavigationVerdict.Allow, Decide("lumora://accueil", pressure: true));
    }
}
