using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class IncognitoWelcomeHtmlTests
{
    private const string SessionEphemereClaim = "Cette session ne sera jamais sauvegardee";
    private const string IpMasqueeClaim = "votre adresse IP reelle est masquee";
    private const string IpNonMasqueeClaim = "Votre adresse IP reelle N'est PAS masquee";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void La_promesse_de_session_ephemere_est_toujours_presente(bool torEnabled)
    {
        var html = IncognitoWelcomeHtml.Build(torEnabled);

        Assert.Contains(SessionEphemereClaim, html, StringComparison.Ordinal);
    }

    [Fact]
    public void Avec_tor_actif_le_message_ip_masquee_est_affiche()
    {
        var html = IncognitoWelcomeHtml.Build(torEnabled: true);

        Assert.Contains(IpMasqueeClaim, html, StringComparison.Ordinal);
        Assert.DoesNotContain(IpNonMasqueeClaim, html, StringComparison.Ordinal);
    }

    [Fact]
    public void Sans_tor_le_message_ip_non_masquee_est_affiche()
    {
        var html = IncognitoWelcomeHtml.Build(torEnabled: false);

        Assert.Contains(IpNonMasqueeClaim, html, StringComparison.Ordinal);
        Assert.DoesNotContain(IpMasqueeClaim, html, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_html_genere_est_bien_forme()
    {
        var html = IncognitoWelcomeHtml.Build(torEnabled: false);

        Assert.StartsWith("<!doctype html>", html, StringComparison.Ordinal);
        Assert.EndsWith("</html>", html, StringComparison.Ordinal);
    }

    // Sans balise <title>, WebView2 retombe sur l'URI data: brute comme
    // DocumentTitle - trouve en verification live le 2026-07-20 quand cette
    // valeur s'est mise a alimenter le titre de fenetre ET l'intitule de
    // l'onglet (nouveaute onglets). Bug preexistant a l'ajout des onglets,
    // corrige a la racine plutot que contourne cote C#.
    [Fact]
    public void Le_html_a_un_titre_pour_eviter_que_webview2_expose_l_uri_data_brute()
    {
        var html = IncognitoWelcomeHtml.Build(torEnabled: false);

        Assert.Contains("<title>Incognito</title>", html, StringComparison.Ordinal);
    }

    // Demande explicite utilisateur (2026-07-20) : le message doit expliquer
    // le PRINCIPE d'Incognito (protege l'historique local, pas le trafic
    // reseau), pas seulement des statuts bruts - point de confusion connu
    // (voir 0.83.24-dev, "le mode prive classique ne protege jamais du
    // reseau").
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Le_principe_reseau_vs_local_est_explique(bool torEnabled)
    {
        var html = IncognitoWelcomeHtml.Build(torEnabled);

        Assert.Contains("Ca protege votre historique local, pas votre trafic reseau", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_principe_du_bouton_tor_est_explique_quand_il_est_inactif()
    {
        var html = IncognitoWelcomeHtml.Build(torEnabled: false);

        Assert.Contains("faisant transiter votre trafic par le reseau Tor", html, StringComparison.Ordinal);
    }
}
