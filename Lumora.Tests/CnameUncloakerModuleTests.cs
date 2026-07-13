using Lumora.Privacy.CnameUncloaker;
using Lumora.Privacy.NetworkBlocker;
using Xunit;

namespace Lumora.Tests;

// La confirmation d'un host cloaké dépend d'une vraie résolution DNS asynchrone
// (CnameResolver, P/Invoke DnsQuery_W) — non simulable en test unitaire. Ces tests
// couvrent donc uniquement les décisions synchrones documentées dans le code :
// la première requête vers un host tiers passe toujours (résolution en tâche de fond).
public class CnameUncloakerModuleTests
{
    [Fact]
    public void Premiere_requete_vers_un_host_tiers_inconnu_n_est_jamais_bloquee()
    {
        var uncloaker = new CnameUncloakerModule(new NetworkBlockerModule());
        Assert.False(uncloaker.ShouldBlock("https://cdn.metrics-tiers.example/px", "https://exemple.com"));
    }

    [Fact]
    public void Requete_vers_le_meme_site_n_est_pas_consideree_tierce()
    {
        var uncloaker = new CnameUncloakerModule(new NetworkBlockerModule());
        Assert.False(uncloaker.ShouldBlock("https://api.exemple.com/data", "https://www.exemple.com"));
    }

    [Fact]
    public void Host_deja_bloque_directement_par_le_bloqueur_reseau_n_est_pas_pris_en_charge_ici()
    {
        var networkBlocker = new NetworkBlockerModule();
        var uncloaker = new CnameUncloakerModule(networkBlocker);
        // doubleclick.net est déjà bloqué directement par la seed : pas besoin d'un
        // détour CNAME, ShouldBlock renvoie false ici (le blocage vient d'ailleurs).
        Assert.False(uncloaker.ShouldBlock("https://doubleclick.net/track", "https://exemple.com"));
    }

    [Fact]
    public void DetectedCount_demarre_a_zero()
    {
        var uncloaker = new CnameUncloakerModule(new NetworkBlockerModule());
        Assert.Equal(0, uncloaker.DetectedCount);
    }

    [Fact]
    public void Url_de_requete_invalide_ne_leve_pas_d_exception()
    {
        var uncloaker = new CnameUncloakerModule(new NetworkBlockerModule());
        Assert.False(uncloaker.ShouldBlock("pas-une-url", "https://exemple.com"));
    }
}
