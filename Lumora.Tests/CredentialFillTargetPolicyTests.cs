using Lumora.WinUI.Credentials;
using Xunit;

namespace Lumora.Tests;

// Faille corrigée le 2026-09-24 (audit de sécurité) : le remplissage envoyait
// le mot de passe à toutes les iframes de la page, y compris tierces.
public class CredentialFillTargetPolicyTests
{
    private const string Origin = "https://exemple.fr";

    [Theory]
    [InlineData("https://exemple.fr/connexion")]
    [InlineData("https://www.exemple.fr/")]
    [InlineData("https://comptes.exemple.fr/login")]
    public void Meme_site_autorise(string target) =>
        Assert.True(CredentialFillTargetPolicy.IsAllowedTarget(target, Origin, null));

    [Theory]
    [InlineData("https://regie-pub.example/iframe")]
    [InlineData("https://exemple.fr.pirate.example/")]
    [InlineData("https://exemple-fr.example/")]
    public void Iframe_tierce_refusee(string target) =>
        Assert.False(CredentialFillTargetPolicy.IsAllowedTarget(target, Origin, null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("about:blank")]
    [InlineData("about:srcdoc")]
    [InlineData("data:text/html,<form>")]
    [InlineData("javascript:alert(1)")]
    public void Adresse_inconnue_ou_non_web_refusee(string? target) =>
        Assert.False(CredentialFillTargetPolicy.IsAllowedTarget(target, Origin, null));

    [Fact]
    public void Identifiant_https_jamais_rempli_sur_page_http() =>
        Assert.False(CredentialFillTargetPolicy.IsAllowedTarget("http://exemple.fr/connexion", Origin, null));

    [Fact]
    public void Identifiant_http_rempli_sur_http() =>
        Assert.True(CredentialFillTargetPolicy.IsAllowedTarget("http://exemple.fr/connexion", "http://exemple.fr", null));

    [Fact]
    public void Origine_importee_sans_schema_verifie_quand_meme_le_domaine()
    {
        Assert.True(CredentialFillTargetPolicy.IsAllowedTarget("https://exemple.fr/", "exemple.fr", null));
        Assert.False(CredentialFillTargetPolicy.IsAllowedTarget("https://autre.example/", "exemple.fr", null));
    }

    [Fact]
    public void Adresse_de_connexion_enregistree_compte_aussi()
    {
        // Compte google.com dont la connexion se fait sur accounts.google.com :
        // même site ; un domaine de connexion distinct enregistré est accepté.
        Assert.True(CredentialFillTargetPolicy.IsAllowedTarget("https://login.fournisseur.example/", Origin, "https://login.fournisseur.example/sso"));
    }

    [Fact]
    public void Sous_domaines_de_proprietaires_differents_sur_suffixe_public()
    {
        // github.io est un suffixe public : deux sites distincts.
        Assert.False(CredentialFillTargetPolicy.IsAllowedTarget("https://pirate.github.io/", "https://moi.github.io", null));
    }

    [Theory]
    [InlineData("https://www.exemple.fr/inscription", true)]
    [InlineData("https://widget.example/form", false)]
    [InlineData("about:blank", false)]
    public void Mot_de_passe_genere_iframe_meme_site_seulement(string frame, bool expected) =>
        Assert.Equal(expected, CredentialFillTargetPolicy.IsSameSiteFrame(frame, "https://exemple.fr/inscription"));
}
