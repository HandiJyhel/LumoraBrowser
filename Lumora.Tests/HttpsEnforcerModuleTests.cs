using Lumora.Privacy.HttpsEnforcer;
using Xunit;

namespace Lumora.Tests;

public class HttpsEnforcerModuleTests
{
    private readonly HttpsEnforcerModule _module = new();

    [Fact]
    public void Upgrade_une_url_http_vers_https()
    {
        Assert.Equal("https://exemple.com/page", _module.CleanUrl("http://exemple.com/page"));
    }

    [Fact]
    public void Ne_touche_pas_une_url_deja_https()
    {
        Assert.Null(_module.CleanUrl("https://exemple.com/page"));
    }

    [Theory]
    [InlineData("http://localhost:8080/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://192.168.1.1/")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://172.16.0.1/")]
    [InlineData("http://172.31.255.255/")]
    [InlineData("http://machine.local/")]
    public void Preserve_les_hotes_locaux(string uri)
    {
        Assert.Null(_module.CleanUrl(uri));
    }

    [Theory]
    [InlineData("http://172.15.0.1/")]
    [InlineData("http://172.32.0.1/")]
    public void N_exclut_pas_les_plages_172_hors_bloc_prive(string uri)
    {
        Assert.NotNull(_module.CleanUrl(uri));
    }

    [Fact]
    public void Respecte_l_autorisation_explicite_donnee_par_l_utilisateur_pour_un_hote()
    {
        _module.AllowHttp("intranet.exemple.com");
        Assert.Null(_module.CleanUrl("http://intranet.exemple.com/accueil"));
    }

    [Fact]
    public void L_autorisation_ne_s_applique_pas_a_un_autre_hote()
    {
        _module.AllowHttp("intranet.exemple.com");
        Assert.NotNull(_module.CleanUrl("http://autre-site.com/"));
    }

    [Fact]
    public void Url_invalide_ne_leve_pas_d_exception()
    {
        Assert.Null(_module.CleanUrl("http://"));
    }
}
