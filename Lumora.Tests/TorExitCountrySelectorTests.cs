using Lumora.WinUI.Tor;
using Xunit;

namespace Lumora.Tests;

// Teste TorExitCountrySelector : la liste de pays (bien formee, pas de
// doublon, pas de Pays-Bas par defaut - voir commentaire dans le fichier
// source) et le refus honnete d'ApplyAsync quand Tor n'est pas connecte.
// Meme principe que TorProcessManagerTests : jamais de vrai processus tor.exe
// ni de vrai control port dans ces tests.
public class TorExitCountrySelectorTests
{
    [Fact]
    public void Options_nest_pas_vide_et_ne_contient_aucun_doublon_de_code()
    {
        var options = TorExitCountrySelector.Options;

        Assert.NotEmpty(options);

        var distinctCodes = new HashSet<string>(
            options.Select(o => o.Code), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(options.Count, distinctCodes.Count);
    }

    [Fact]
    public void Options_utilise_des_codes_iso_a_deux_lettres_majuscules_avec_un_nom_affiche()
    {
        foreach (var option in TorExitCountrySelector.Options)
        {
            Assert.Matches("^[A-Z]{2}$", option.Code);
            Assert.False(string.IsNullOrWhiteSpace(option.DisplayName));
        }
    }

    [Fact]
    public void Options_ne_propose_pas_les_Pays_Bas_par_defaut()
    {
        // Le pays sur lequel les utilisateurs retombent presque toujours sans
        // rien choisir (voir commentaire de TorExitCountrySelector) : le
        // proposer dans la liste courte n'apporterait aucun choix reel.
        Assert.DoesNotContain(TorExitCountrySelector.Options, o => o.Code == "NL");
    }

    [Fact]
    public async Task ApplyAsync_refuse_honnetement_si_le_moteur_n_est_pas_connecte()
    {
        using var manager = new TorProcessManager();

        var (success, message) = await TorExitCountrySelector.ApplyAsync(manager, "FR");

        Assert.False(success);
        Assert.Equal("Le moteur Tor n'est pas connecte.", message);
    }

    [Fact]
    public async Task ApplyAsync_refuse_honnetement_pour_le_retour_a_automatique_aussi()
    {
        using var manager = new TorProcessManager();

        var (success, message) = await TorExitCountrySelector.ApplyAsync(manager, null);

        Assert.False(success);
        Assert.Equal("Le moteur Tor n'est pas connecte.", message);
    }
}
