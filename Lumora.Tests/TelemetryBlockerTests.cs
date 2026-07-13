using Lumora.Privacy.TelemetryBlocker;
using Xunit;

namespace Lumora.Tests;

// Verrouille le comportement du module anti-télémétrie : blocage des endpoints
// dédiés (seed), respect de la whitelist utilisateur, et surtout AUCUN faux
// positif sur des domaines légitimes (principe : ne jamais sur-bloquer).
public class TelemetryBlockerTests
{
    private static TelemetryBlockerModule NewModule() => new() { IsEnabled = true };

    [Fact]
    public void Endpoint_telemetrie_connu_est_bloque()
    {
        var module = NewModule();
        Assert.True(module.ShouldBlock(
            "https://api.mixpanel.com/track", "https://exemple.com/"));
        Assert.True(module.ShouldBlock(
            "https://script.hotjar.com/modules.js", "https://exemple.com/"));
        Assert.True(module.ShouldBlock(
            "https://browser-intake-datadoghq.com/api/v2/rum", "https://exemple.com/"));
    }

    [Fact]
    public void Sous_domaine_d_un_endpoint_seed_est_bloque()
    {
        var module = NewModule();
        // o123456.ingest.sentry.io → parent ingest.sentry.io présent dans la seed
        Assert.True(module.ShouldBlock(
            "https://o123456.ingest.sentry.io/api/1/envelope/", "https://exemple.com/"));
        Assert.True(module.ShouldBlock(
            "https://bam.nr-data.net/events/1/abc", "https://exemple.com/"));
    }

    [Fact]
    public void Domaine_legitime_n_est_jamais_bloque()
    {
        var module = NewModule();
        Assert.False(module.ShouldBlock("https://www.google.com/search?q=x", "https://exemple.com/"));
        Assert.False(module.ShouldBlock("https://lh3.googleusercontent.com/avatar.png", "https://exemple.com/"));
        Assert.False(module.ShouldBlock("https://cdn.jsdelivr.net/npm/lib.js", "https://exemple.com/"));
        Assert.False(module.ShouldBlock("https://github.com/user/repo", "https://exemple.com/"));
        // sentry.io (site corporate) n'est pas dans la seed — seul ingest.sentry.io l'est
        Assert.False(module.ShouldBlock("https://sentry.io/welcome/", "https://exemple.com/"));
        // mixpanel.com (site corporate) non plus — seuls les endpoints api.* le sont
        Assert.False(module.ShouldBlock("https://mixpanel.com/pricing/", "https://exemple.com/"));
    }

    [Fact]
    public void Suffixe_ressemblant_n_est_pas_confondu_avec_le_domaine_seed()
    {
        var module = NewModule();
        // "notclarity.ms" ne doit pas matcher "clarity.ms" (comparaison par labels)
        Assert.False(module.ShouldBlock("https://notclarity.ms/page", "https://exemple.com/"));
    }

    [Fact]
    public void Whitelist_utilisateur_desactive_le_blocage()
    {
        var module = NewModule();
        module.SetUserWhitelist(new[] { "api.mixpanel.com" });
        Assert.False(module.ShouldBlock(
            "https://api.mixpanel.com/track", "https://exemple.com/"));
        // Les autres endpoints restent bloqués
        Assert.True(module.ShouldBlock(
            "https://script.hotjar.com/modules.js", "https://exemple.com/"));
    }

    [Fact]
    public void Whitelist_couvre_les_sous_domaines()
    {
        var module = NewModule();
        module.SetUserWhitelist(new[] { "sentry.io" });
        Assert.False(module.ShouldBlock(
            "https://o123456.ingest.sentry.io/api/1/envelope/", "https://exemple.com/"));
    }

    [Fact]
    public void Module_desactive_ne_bloque_rien()
    {
        var module = NewModule();
        module.IsEnabled = false;
        Assert.False(module.ShouldBlock(
            "https://api.mixpanel.com/track", "https://exemple.com/"));
        Assert.Equal(0, module.BlockedCount);
    }

    [Fact]
    public void Compteurs_incrementes_au_blocage_et_reset_par_page()
    {
        var module = NewModule();
        module.ShouldBlock("https://api.mixpanel.com/track", "https://exemple.com/");
        module.ShouldBlock("https://api.amplitude.com/2/httpapi", "https://exemple.com/");
        Assert.Equal(2, module.BlockedCount);
        Assert.Equal(2, module.PageBlockedCount);

        module.ResetPageBlockedCount();
        Assert.Equal(0, module.PageBlockedCount);
        Assert.Equal(2, module.BlockedCount); // le total global ne bouge pas
    }

    [Fact]
    public void Uri_invalide_ou_sans_schema_est_ignoree()
    {
        var module = NewModule();
        Assert.False(module.ShouldBlock("pas-une-url", "https://exemple.com/"));
        Assert.False(module.ShouldBlock(string.Empty, "https://exemple.com/"));
    }

    [Fact]
    public void La_seed_ne_contient_que_des_domaines_normalises()
    {
        // Filet anti-typo : minuscules, pas d'espace, pas de schéma, pas de chemin.
        foreach (var domain in TelemetrySeedList.Domains)
        {
            Assert.Equal(domain, domain.Trim().ToLowerInvariant());
            Assert.DoesNotContain("://", domain);
            Assert.DoesNotContain("/", domain);
            Assert.Contains(".", domain);
        }
    }
}
