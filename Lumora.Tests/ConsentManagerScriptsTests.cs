using Lumora.Privacy.ConsentManager;
using Xunit;

namespace Lumora.Tests;

public class ConsentManagerScriptsTests
{
    [Fact]
    public void Le_garde_fou_ne_lit_plus_le_texte_de_toute_la_page()
    {
        var script = ConsentManagerScripts.BuildInjectionScript([]);

        // Le bug corrigé : le boilerplate RGPD générique était lu sur document.body
        // entier, ce qui désactivait le refus auto sur la quasi-totalité des sites.
        // Seul loginRiskDetected() peut encore lire la page entière (document +
        // shadow roots ouverts depuis le 2026-07-29, voir collectRoots()), et
        // seulement pour des phrases explicites de casse de connexion (LOGRISK),
        // pas le boilerplate.
        Assert.DoesNotContain("candidates.push(document.body)", script, StringComparison.Ordinal);
        Assert.Contains("roots[r].body || roots[r]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_selecteurs_couvrent_des_cmp_supplementaires()
    {
        var script = ConsentManagerScripts.BuildInjectionScript([]);

        Assert.Contains("#cmplz-deny", script, StringComparison.Ordinal);
        Assert.Contains("#cky-btn-reject", script, StringComparison.Ordinal);
        Assert.Contains(".osano-cm-deny", script, StringComparison.Ordinal);
        Assert.Contains("#shopify-pc__banner__btn-decline", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_repli_panneau_detaille_decoche_les_cases_non_obligatoires()
    {
        var script = ConsentManagerScripts.BuildInjectionScript([]);

        Assert.Contains("uncheckToggles", script, StringComparison.Ordinal);
        Assert.Contains("input[type=\"checkbox\"]:checked:not([disabled])", script, StringComparison.Ordinal);
        Assert.Contains("__lumoraConsentPanelOpened", script, StringComparison.Ordinal);
    }

    [Fact]
    public void La_liste_de_compatibilite_connexion_apparait_en_json()
    {
        var script = ConsentManagerScripts.BuildInjectionScript(["exemple.fr", "site.test"]);

        Assert.Contains("'exemple.fr'", script, StringComparison.Ordinal);
        Assert.Contains("'site.test'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_script_de_rattrapage_reutilise_le_meme_moteur()
    {
        var script = ConsentManagerScripts.BuildRetryScript([]);

        Assert.Contains("runConsentEngine()", script, StringComparison.Ordinal);
        Assert.Contains("uncheckToggles", script, StringComparison.Ordinal);
    }
}
