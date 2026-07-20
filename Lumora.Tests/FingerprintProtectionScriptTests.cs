using Lumora.Privacy.FingerprintProtection;
using Xunit;

namespace Lumora.Tests;

public class FingerprintProtectionScriptTests
{
    [Fact]
    public void La_graine_fournie_apparait_dans_le_script_genere()
    {
        var script = FingerprintProtectionScript.Build(123456789L);

        Assert.Contains("var seed=123456789;", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Deux_graines_differentes_produisent_des_scripts_differents()
    {
        var scriptA = FingerprintProtectionScript.Build(1L);
        var scriptB = FingerprintProtectionScript.Build(2L);

        Assert.NotEqual(scriptA, scriptB);
    }

    [Fact]
    public void Aucun_espace_reserve_de_graine_ne_subsiste()
    {
        var script = FingerprintProtectionScript.Build(42L);

        Assert.DoesNotContain("__SEED__", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_script_couvre_bien_canvas_webgl_et_audio()
    {
        var script = FingerprintProtectionScript.Build(1L);

        Assert.Contains("getImageData", script, StringComparison.Ordinal);
        Assert.Contains("toDataURL", script, StringComparison.Ordinal);
        Assert.Contains("toBlob", script, StringComparison.Ordinal);
        Assert.Contains("getParameter", script, StringComparison.Ordinal);
        Assert.Contains("getChannelData", script, StringComparison.Ordinal);
        Assert.Contains("hardwareConcurrency", script, StringComparison.Ordinal);
        Assert.Contains("deviceMemory", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_meme_graine_produit_toujours_le_meme_script()
    {
        var scriptA = FingerprintProtectionScript.Build(999L);
        var scriptB = FingerprintProtectionScript.Build(999L);

        Assert.Equal(scriptA, scriptB);
    }
}
