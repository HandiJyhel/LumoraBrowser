using Lumora.Privacy.CosmeticFilter;
using Xunit;

namespace Lumora.Tests;

public class CosmeticFilterModuleTests
{
    [Fact]
    public void La_seed_integree_est_active_des_la_construction()
    {
        var module = new CosmeticFilterModule();
        Assert.True(module.GenericRuleCount > 0);
    }

    [Fact]
    public void Le_script_generique_contient_les_selecteurs_de_la_seed()
    {
        var module = new CosmeticFilterModule();
        var script = module.BuildGenericInjectionScript();
        Assert.Contains("display:none", script);
        Assert.Contains("adsbygoogle", script);
    }

    [Fact]
    public void Aucune_regle_de_site_ne_produit_un_script_nul()
    {
        var module = new CosmeticFilterModule();
        Assert.Null(module.BuildSiteInjectionScript("https://site-sans-regle-specifique.example"));
    }

    [Fact]
    public async Task Chargement_sans_fichier_de_liste_sur_disque_ne_leve_pas_d_exception()
    {
        var module = new CosmeticFilterModule();
        await module.LoadAsync();
        Assert.Null(module.BuildSiteInjectionScript("https://exemple.com"));
    }

    [Fact]
    public void Le_script_de_suppression_cible_les_deux_identifiants_de_style()
    {
        var script = CosmeticFilterModule.BuildRemovalScript();
        Assert.Contains("__nova_cf_generic", script);
        Assert.Contains("__nova_cf_site", script);
    }
}
