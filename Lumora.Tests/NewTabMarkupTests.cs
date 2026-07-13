using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Échappement HTML/JS de la page nouvel onglet — les titres et URL de raccourcis
// viennent de l'utilisateur et sont injectés dans du HTML/JS généré ; une faille
// ici est une XSS locale (onclick, href, texte).
public sealed class NewTabMarkupTests
{
    [Fact]
    public void HtmlText_echappe_les_caracteres_html()
    {
        Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt;", NewTabMarkup.HtmlText("<script>alert(1)</script>"));
    }

    [Fact]
    public void HtmlText_retombe_sur_Lumora_si_vide()
    {
        Assert.Equal("Lumora", NewTabMarkup.HtmlText(null));
        Assert.Equal("Lumora", NewTabMarkup.HtmlText("   "));
    }

    [Theory]
    [InlineData("Pulse")]
    [InlineData("Pulse Browser")]
    [InlineData("Nova")]
    [InlineData("Nova Browser")]
    public void HtmlText_migre_les_anciens_noms_de_marque(string legacyTitle)
    {
        Assert.Equal("Lumora", NewTabMarkup.HtmlText(legacyTitle));
    }

    [Fact]
    public void HtmlAttribute_echappe_les_guillemets()
    {
        Assert.Equal("&quot;onmouseover=alert(1)&quot;", NewTabMarkup.HtmlAttribute("\"onmouseover=alert(1)\""));
    }

    [Fact]
    public void JsString_echappe_antislash_et_apostrophe_pour_bloquer_l_injection_dans_onclick()
    {
        Assert.Equal("a\\'); alert(1); //", NewTabMarkup.JsString("a'); alert(1); //"));
        Assert.Equal("chemin\\\\windows", NewTabMarkup.JsString("chemin\\windows"));
    }

    [Fact]
    public void NormalizeShortcutUrl_laisse_les_schemas_connus_intacts()
    {
        Assert.Equal("https://example.com", NewTabMarkup.NormalizeShortcutUrl("https://example.com"));
        Assert.Equal("http://example.com", NewTabMarkup.NormalizeShortcutUrl("http://example.com"));
        Assert.Equal("lumora://settings", NewTabMarkup.NormalizeShortcutUrl("lumora://settings"));
    }

    [Fact]
    public void NormalizeShortcutUrl_ajoute_https_si_aucun_schema()
    {
        Assert.Equal("https://example.com", NewTabMarkup.NormalizeShortcutUrl("example.com"));
        Assert.Equal("https://example.com", NewTabMarkup.NormalizeShortcutUrl("  example.com  "));
    }

    [Fact]
    public void NormalizeShortcutUrl_ne_laisse_pas_javascript_url_traverser_tel_quel()
    {
        // Pas de schéma reconnu explicitement -> préfixé en https, donc jamais exécutable
        // comme javascript: dans un attribut href.
        Assert.StartsWith("https://", NewTabMarkup.NormalizeShortcutUrl("javascript:alert(1)"));
    }

    [Fact]
    public void ShortcutInitial_prend_la_premiere_lettre_en_majuscule()
    {
        Assert.Equal("G", NewTabMarkup.ShortcutInitial("gmail"));
        Assert.Equal("É", NewTabMarkup.ShortcutInitial("étoile"));
    }

    [Fact]
    public void ShortcutInitial_retombe_sur_un_point_si_vide()
    {
        Assert.Equal("•", NewTabMarkup.ShortcutInitial("   "));
    }
}
