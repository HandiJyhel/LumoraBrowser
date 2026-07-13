using Lumora.Privacy.NetworkBlocker;
using Xunit;

namespace Lumora.Tests;

// Ne teste que le comportement basé sur la seed intégrée (aucun appel à LoadAsync) :
// LoadAsync télécharge/lit de vraies listes depuis le disque utilisateur réel et peut
// déclencher une mise à jour réseau en arrière-plan — inadapté à un test unitaire.
public class NetworkBlockerModuleTests
{
    [Fact]
    public void Bloque_un_domaine_present_dans_la_seed_integree()
    {
        var blocker = new NetworkBlockerModule();
        Assert.True(blocker.ShouldBlock("https://doubleclick.net/track", "https://exemple.com"));
    }

    [Fact]
    public void Bloque_un_sous_domaine_d_un_domaine_de_la_seed()
    {
        var blocker = new NetworkBlockerModule();
        Assert.True(blocker.ShouldBlock("https://ads.doubleclick.net/track", "https://exemple.com"));
    }

    [Fact]
    public void Ne_bloque_pas_un_domaine_inconnu()
    {
        var blocker = new NetworkBlockerModule();
        Assert.False(blocker.ShouldBlock("https://exemple.com/script.js", "https://exemple.com"));
    }

    [Fact]
    public void La_liste_blanche_utilisateur_prevaut_sur_la_seed()
    {
        var blocker = new NetworkBlockerModule();
        blocker.SetUserWhitelist(new[] { "doubleclick.net" });
        Assert.False(blocker.ShouldBlock("https://doubleclick.net/track", "https://exemple.com"));
    }

    [Fact]
    public void Module_desactive_ne_bloque_plus_rien()
    {
        var blocker = new NetworkBlockerModule { IsEnabled = false };
        Assert.False(blocker.ShouldBlock("https://doubleclick.net/track", "https://exemple.com"));
    }

    [Fact]
    public void IsBlocked_reflete_la_seed_independamment_de_ShouldBlock()
    {
        var blocker = new NetworkBlockerModule();
        Assert.True(blocker.IsBlocked("doubleclick.net"));
        Assert.False(blocker.IsBlocked("exemple.com"));
    }

    [Fact]
    public void Url_de_requete_invalide_ne_leve_pas_d_exception()
    {
        var blocker = new NetworkBlockerModule();
        Assert.False(blocker.ShouldBlock("pas-une-url", "https://exemple.com"));
    }
}
