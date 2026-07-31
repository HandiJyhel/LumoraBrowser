using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public sealed class WheelScrollMathTests
{
    [Fact]
    public void Delta_positif_fait_remonter_le_contenu()
    {
        var changed = WheelScrollMath.TryComputeNextVerticalOffset(140d, 500d, 120, out var newOffset);

        Assert.True(changed);
        Assert.Equal(84d, newOffset);
    }

    [Fact]
    public void Delta_negatif_fait_descendre_le_contenu()
    {
        var changed = WheelScrollMath.TryComputeNextVerticalOffset(140d, 500d, -120, out var newOffset);

        Assert.True(changed);
        Assert.Equal(196d, newOffset);
    }

    [Fact]
    public void Le_defilement_est_borne_a_zero_et_a_la_hauteur_scrollable()
    {
        var changedUp = WheelScrollMath.TryComputeNextVerticalOffset(12d, 500d, 120, out var upOffset);
        var changedDown = WheelScrollMath.TryComputeNextVerticalOffset(492d, 500d, -120, out var downOffset);

        Assert.True(changedUp);
        Assert.Equal(0d, upOffset);
        Assert.True(changedDown);
        Assert.Equal(500d, downOffset);
    }

    [Fact]
    public void Aucun_defilement_si_delta_nul_ou_surface_non_scrollable()
    {
        var noDelta = WheelScrollMath.TryComputeNextVerticalOffset(140d, 500d, 0, out var noDeltaOffset);
        var noSurface = WheelScrollMath.TryComputeNextVerticalOffset(140d, 0d, -120, out var noSurfaceOffset);

        Assert.False(noDelta);
        Assert.Equal(140d, noDeltaOffset);
        Assert.False(noSurface);
        Assert.Equal(140d, noSurfaceOffset);
    }
}
