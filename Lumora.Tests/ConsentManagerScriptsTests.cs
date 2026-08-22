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
    public void Les_liens_sans_href_reconnu_sont_consideres_cliquables()
    {
        var script = ConsentManagerScripts.BuildInjectionScript([]);

        // Bug réel 2026-08-19 (leboncoin.fr) : le lien "Continuer sans accepter"
        // n'a ni href="#" ni href="" - un CLICKABLE restreint à ces deux motifs le
        // rate complètement malgré un texte déjà reconnu (TXT).
        Assert.Contains(
            "var CLICKABLE = 'button, [role=\"button\"], input[type=\"button\"], input[type=\"submit\"], a';",
            script,
            StringComparison.Ordinal);
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

    // 2026-08-22 : deux bugs réels signalés par l'utilisateur ("ça ne marche
    // pas encore très bien sur certains sites"), trouvés par relecture puis
    // vérifiés en exécutant le VRAI script JS extrait de l'assembly compilée
    // dans un harnais Node fait main (vm + objets factices, même technique que
    // documentée dans MEMORY.md "vérifier un script JS sans jsdom") - pas
    // seulement une recherche de sous-chaîne dans le code source.
    [Fact]
    public void Le_texte_des_boutons_est_normalise_avant_comparaison()
    {
        var script = ConsentManagerScripts.BuildInjectionScript([]);

        // Espace insécable (très courant dans le HTML des bandeaux cookies) et
        // texte réparti sur plusieurs lignes dans le HTML source empêchaient une
        // correspondance EXACTE avec la liste de phrases reconnues, même quand
        // le texte visible était le bon.
        Assert.Contains("function normalizeText(s) {", script, StringComparison.Ordinal);
        Assert.Contains("s.replace(/\\s+/g, ' ').trim().toLowerCase();", script, StringComparison.Ordinal);
        Assert.Contains("return normalizeText(el.getAttribute('aria-label')", script, StringComparison.Ordinal);
    }

    [Fact]
    public void La_visibilite_detecte_aussi_visibility_hidden()
    {
        var script = ConsentManagerScripts.BuildInjectionScript([]);

        // offsetWidth/offsetHeight restent positifs pour visibility:hidden (mise
        // en page conservée, juste invisible) : un bouton "Refuser" caché de
        // cette façon (variante desktop/mobile dupliquée) était cliqué sans
        // effet réel, laissant le bandeau ouvert.
        Assert.Contains("typeof el.checkVisibility === 'function'", script, StringComparison.Ordinal);
        Assert.Contains("checkVisibility({checkOpacity: true, checkVisibilityCSS: true})", script, StringComparison.Ordinal);
    }

    [Fact]
    public void La_liste_de_refus_inclut_non_merci()
    {
        var script = ConsentManagerScripts.BuildInjectionScript([]);

        Assert.Contains("'non merci'", script, StringComparison.Ordinal);
    }
}
