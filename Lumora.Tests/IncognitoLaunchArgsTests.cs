using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class IncognitoLaunchArgsTests
{
    [Fact]
    public void Aucun_argument_ne_declenche_pas_incognito()
    {
        var found = IncognitoLaunchArgs.IsIncognitoLaunch([], out var torEnabled, out var url, out var returnToMain);

        Assert.False(found);
        Assert.False(torEnabled);
        Assert.Null(url);
        Assert.False(returnToMain);
    }

    [Fact]
    public void Le_drapeau_incognito_seul_est_detecte_sans_tor()
    {
        var found = IncognitoLaunchArgs.IsIncognitoLaunch(["--incognito"], out var torEnabled, out var url, out var returnToMain);

        Assert.True(found);
        Assert.False(torEnabled);
        Assert.Null(url);
        Assert.False(returnToMain);
    }

    [Fact]
    public void Le_drapeau_tor_active_incognito_et_tor()
    {
        var found = IncognitoLaunchArgs.IsIncognitoLaunch(["--incognito-tor"], out var torEnabled, out var url, out _);

        Assert.True(found);
        Assert.True(torEnabled);
    }

    [Fact]
    public void Le_drapeau_retour_au_navigateur_de_base_est_detecte()
    {
        var found = IncognitoLaunchArgs.IsIncognitoLaunch(
            ["--incognito", "--incognito-return-to-main"], out _, out _, out var returnToMain);

        Assert.True(found);
        Assert.True(returnToMain);
    }

    [Fact]
    public void Sans_le_drapeau_retour_au_navigateur_de_base_reste_absent()
    {
        IncognitoLaunchArgs.IsIncognitoLaunch(["--incognito"], out _, out _, out var returnToMain);

        Assert.False(returnToMain);
    }

    [Fact]
    public void Lurl_est_extraite_du_prefixe()
    {
        var found = IncognitoLaunchArgs.IsIncognitoLaunch(
            ["--incognito", "--incognito-url=https://example.com"], out _, out var url, out _);

        Assert.True(found);
        Assert.Equal("https://example.com", url);
    }

    [Fact]
    public void Lurl_entre_guillemets_est_nettoyee()
    {
        IncognitoLaunchArgs.IsIncognitoLaunch(
            ["--incognito", "--incognito-url=\"https://example.com\""], out _, out var url, out _);

        Assert.Equal("https://example.com", url);
    }

    [Fact]
    public void Une_url_vide_apres_le_prefixe_reste_null()
    {
        IncognitoLaunchArgs.IsIncognitoLaunch(
            ["--incognito", "--incognito-url="], out _, out var url, out _);

        Assert.Null(url);
    }

    [Fact]
    public void La_detection_est_insensible_a_la_casse_et_aux_espaces()
    {
        var found = IncognitoLaunchArgs.IsIncognitoLaunch(["  --INCOGNITO-TOR  "], out var torEnabled, out _, out _);

        Assert.True(found);
        Assert.True(torEnabled);
    }

    [Fact]
    public void Un_argument_null_dans_la_liste_nest_pas_un_probleme()
    {
        var found = IncognitoLaunchArgs.IsIncognitoLaunch([null!, "--incognito"], out _, out _, out _);

        Assert.True(found);
    }

    [Fact]
    public void Un_argument_sans_rapport_ne_declenche_rien()
    {
        var found = IncognitoLaunchArgs.IsIncognitoLaunch(["--app=mon-app"], out var torEnabled, out var url, out var returnToMain);

        Assert.False(found);
        Assert.False(torEnabled);
        Assert.Null(url);
        Assert.False(returnToMain);
    }
}
