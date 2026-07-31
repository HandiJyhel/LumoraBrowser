using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// La politique anti-parasite 0.78.3.1 : les sites qui détournent les clics
// (chaque clic téléporte vers un site douteux, il faut revenir et recliquer)
// sont neutralisés silencieusement. Aucun comptage de clics : chaque tentative
// est bloquée, une par une.
//
// Depuis 0.84.0.3 : une navigation automatique (sans geste utilisateur) qui
// change de domaine est bloquée par défaut, même si le bouclier réseau n'a
// jamais reconnu le site comme publicitaire — un réseau absent des listes de
// filtres ne doit pas obtenir de laissez-passer (cas vidlox/muvonix.shop :
// chaîne de redirection entièrement invisible pour le bouclier).
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
            recentPopup);
    }

    [Fact]
    public void AdresseDemandeeExplicitement_JamaisBloquee()
    {
        // Même un domaine répertorié passe si l'utilisateur l'a tapé lui-même.
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://ads.regie.example/", wasExplicit: true, adHosts: ["ads.regie.example"]));
    }

    [Fact]
    public void DomaineRepertorie_ToujoursBloque()
    {
        // Le clic capturé vers une régie connue n'a pas d'excuse.
        Assert.Equal(
            NavigationVerdict.BlockAdDomain,
            Decide("https://ads.regie.example/promo", adHosts: ["ads.regie.example"]));
    }

    [Fact]
    public void RedirectionAutomatique_DomaineInconnuDesListes_BloqueParasite()
    {
        // Le cas central du durcissement 0.84.0.3 : aucun geste utilisateur,
        // et le domaine d'arrivée n'est reconnu par AUCUNE liste (ni ad host,
        // ni whitelist) — exactement le trou qui laissait passer vidlox ->
        // muvonix.shop, puisque l'ancienne règle n'agissait que si le bouclier
        // réseau avait déjà mesuré une pression publicitaire sur la page.
        Assert.Equal(
            NavigationVerdict.BlockParasite,
            Decide("https://preinvtive.muvonix.shop/"));
    }

    [Fact]
    public void TabUnder_ApresPopup_BloqueParasite()
    {
        // L'onglet vient d'ouvrir une popup et se redirige cross-domaine :
        // pattern tab-under.
        Assert.Equal(
            NavigationVerdict.BlockParasite,
            Decide("https://boutique-douteuse.example/", recentPopup: true));
    }

    [Fact]
    public void ClicUtilisateurCrossDomaine_Autorise()
    {
        // Régression 0.78.3.2 : depuis une page de résultats, les clics
        // légitimes vers un autre site étaient pris pour des redirections
        // parasites. Lumora doit bloquer les détournements automatiques, pas
        // un vrai clic — même si le domaine d'arrivée est totalement inconnu.
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide(
                "https://fr.wikipedia.org/wiki/Tintin",
                fromUri: "https://www.google.com/search?q=tintin",
                userInitiated: true));
    }

    [Fact]
    public void ClicUtilisateurVersDomainePublicitaire_EncoreBloque()
    {
        Assert.Equal(
            NavigationVerdict.BlockAdDomain,
            Decide(
                "https://ads.regie.example/promo",
                userInitiated: true,
                adHosts: ["ads.regie.example"]));
    }

    [Fact]
    public void TabUnderAvecGesteUtilisateur_EncoreBloque()
    {
        Assert.Equal(
            NavigationVerdict.BlockParasite,
            Decide(
                "https://boutique-douteuse.example/",
                userInitiated: true,
                recentPopup: true));
    }

    [Fact]
    public void MemeSiteRacine_ToujoursAutorise()
    {
        // Le site navigue chez lui (sous-domaine compris), même sans geste.
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://cdn.site-de-films.example/episode-2"));
    }

    [Fact]
    public void PremiereNavigationDunOnglet_Autorisee()
    {
        // Pas d'origine web (onglet neuf) : rien à détourner.
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://boutique-douteuse.example/", fromUri: null));
    }

    [Fact]
    public void Authentification_Autorisee_MemeApresPopup()
    {
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://accounts.google.com/o/oauth2/auth", recentPopup: true));
    }

    [Fact]
    public void SiteWhiteliste_GardeSesNavigations()
    {
        // Whitelist de l'origine OU de la destination : l'utilisateur a dit
        // « je fais confiance à ce site ».
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://boutique-douteuse.example/", whitelisted: ["site-de-films.example"]));
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://boutique-douteuse.example/", whitelisted: ["boutique-douteuse.example"]));
    }

    [Fact]
    public void BlocageStrictDesactive_ToutPasse()
    {
        Assert.Equal(
            NavigationVerdict.Allow,
            Decide("https://ads.regie.example/", strictEnabled: false, adHosts: ["ads.regie.example"]));
    }

    [Fact]
    public void AdresseNonWeb_Ignoree()
    {
        Assert.Equal(NavigationVerdict.Allow, Decide("lumora://accueil"));
    }
}
