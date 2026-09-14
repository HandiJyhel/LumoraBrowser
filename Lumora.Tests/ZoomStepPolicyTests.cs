using Lumora.WinUI.Accessibility;
using Xunit;

namespace Lumora.Tests;

public class ZoomStepPolicyTests
{
    [Fact]
    public void StepUp_depuis_100_va_a_110()
    {
        Assert.Equal(110, ZoomStepPolicy.StepUp(100));
    }

    [Fact]
    public void StepDown_depuis_100_va_a_90()
    {
        Assert.Equal(90, ZoomStepPolicy.StepDown(100));
    }

    [Fact]
    public void StepUp_au_palier_maximum_reste_au_maximum()
    {
        Assert.Equal(160, ZoomStepPolicy.StepUp(160));
    }

    [Fact]
    public void StepDown_au_palier_minimum_reste_au_minimum()
    {
        Assert.Equal(90, ZoomStepPolicy.StepDown(90));
    }

    [Fact]
    public void StepUp_depuis_une_valeur_hors_palier_retombe_sur_le_prochain_palier_connu()
    {
        // Une valeur qui n'existe dans aucun menu (ex. import externe) doit
        // quand meme retomber sur un palier que le ComboBox du Controle de
        // site sait afficher.
        Assert.Equal(125, ZoomStepPolicy.StepUp(115));
    }

    [Fact]
    public void StepDown_depuis_une_valeur_hors_palier_retombe_sur_le_palier_connu_precedent()
    {
        Assert.Equal(100, ZoomStepPolicy.StepDown(105));
    }

    [Fact]
    public void DefaultPercent_vaut_100()
    {
        Assert.Equal(100, ZoomStepPolicy.DefaultPercent);
    }
}
