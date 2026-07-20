using Lumora.Privacy.GeolocationSpoofing;
using Xunit;

namespace Lumora.Tests;

public class GeolocationSpoofScriptTests
{
    [Fact]
    public void Latitude_et_longitude_apparaissent_formatees_en_invariant()
    {
        var script = GeolocationSpoofScript.Build(48.8566, 2.3522, []);

        Assert.Contains("latitude:48.8566", script, StringComparison.Ordinal);
        Assert.Contains("longitude:2.3522", script, StringComparison.Ordinal);
    }

    [Fact]
    public void La_liste_dexemptions_apparait_en_json()
    {
        var script = GeolocationSpoofScript.Build(0, 0, ["example.com", "site.test"]);

        Assert.Contains("\"example.com\"", script, StringComparison.Ordinal);
        Assert.Contains("\"site.test\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_liste_dexemptions_vide_produit_un_tableau_json_vide()
    {
        var script = GeolocationSpoofScript.Build(0, 0, []);

        Assert.Contains("var exempt=[]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_script_remplace_bien_getCurrentPosition_et_watchPosition()
    {
        var script = GeolocationSpoofScript.Build(1.5, 2.5, []);

        Assert.Contains("navigator.geolocation.getCurrentPosition=function", script, StringComparison.Ordinal);
        Assert.Contains("navigator.geolocation.watchPosition=function", script, StringComparison.Ordinal);
        Assert.Contains("navigator.geolocation.clearWatch=function", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_script_verifie_le_domaine_avant_toute_exemption()
    {
        var script = GeolocationSpoofScript.Build(0, 0, ["trusted.example"]);

        Assert.Contains("host===root||host.endsWith('.'+root)", script, StringComparison.Ordinal);
    }
}
