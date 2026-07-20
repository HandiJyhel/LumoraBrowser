using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Teste uniquement BuildArguments (logique pure) : Launch demarre un vrai
// process Windows et est volontairement exclu des tests automatises, meme
// principe que YtDlpEngineProvider.DownloadEngineAsync.
public class IncognitoProcessLauncherTests
{
    [Fact]
    public void Sans_option_seul_le_drapeau_incognito_est_present()
    {
        var args = IncognitoProcessLauncher.BuildArguments(url: null, torEnabled: false);

        Assert.Equal(["--incognito"], args);
    }

    [Fact]
    public void Avec_tor_le_drapeau_tor_est_ajoute()
    {
        var args = IncognitoProcessLauncher.BuildArguments(url: null, torEnabled: true);

        Assert.Equal(["--incognito", "--incognito-tor"], args);
    }

    [Fact]
    public void Avec_une_url_le_prefixe_est_ajoute()
    {
        var args = IncognitoProcessLauncher.BuildArguments(url: "https://example.com", torEnabled: false);

        Assert.Equal(["--incognito", "--incognito-url=https://example.com"], args);
    }

    [Fact]
    public void Avec_tor_et_url_les_deux_sont_presents_dans_lordre()
    {
        var args = IncognitoProcessLauncher.BuildArguments(url: "https://example.com", torEnabled: true);

        Assert.Equal(["--incognito", "--incognito-tor", "--incognito-url=https://example.com"], args);
    }

    [Fact]
    public void Une_url_vide_ou_blanche_nest_pas_ajoutee()
    {
        var args = IncognitoProcessLauncher.BuildArguments(url: "   ", torEnabled: false);

        Assert.Equal(["--incognito"], args);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "https://example.com")]
    [InlineData(true, "https://example.com")]
    public void Les_arguments_construits_sont_relus_a_lidentique_par_IncognitoLaunchArgs(bool torEnabled, string? url)
    {
        var args = IncognitoProcessLauncher.BuildArguments(url, torEnabled);

        var found = IncognitoLaunchArgs.IsIncognitoLaunch(args, out var parsedTor, out var parsedUrl, out _);

        Assert.True(found);
        Assert.Equal(torEnabled, parsedTor);
        Assert.Equal(url, parsedUrl);
    }

    [Fact]
    public void Avec_retour_au_navigateur_de_base_le_drapeau_est_ajoute_et_relu()
    {
        var args = IncognitoProcessLauncher.BuildArguments(url: null, torEnabled: false, returnToMain: true);

        Assert.Equal(["--incognito", "--incognito-return-to-main"], args);

        var found = IncognitoLaunchArgs.IsIncognitoLaunch(args, out _, out _, out var returnToMain);
        Assert.True(found);
        Assert.True(returnToMain);
    }
}
