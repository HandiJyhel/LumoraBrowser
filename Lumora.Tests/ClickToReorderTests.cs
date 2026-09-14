using Lumora.WinUI.Accessibility;
using Xunit;

namespace Lumora.Tests;

public class ClickToReorderTests
{
    [Fact]
    public void Gap_juste_avant_lelement_decroche_nest_pas_une_cible()
    {
        Assert.False(ClickToReorder.IsDropTarget(gapIndex: 2, pickedIndex: 2));
    }

    [Fact]
    public void Gap_juste_apres_lelement_decroche_nest_pas_une_cible()
    {
        Assert.False(ClickToReorder.IsDropTarget(gapIndex: 3, pickedIndex: 2));
    }

    [Fact]
    public void Gap_eloigne_de_lelement_decroche_est_une_cible()
    {
        Assert.True(ClickToReorder.IsDropTarget(gapIndex: 0, pickedIndex: 2));
        Assert.True(ClickToReorder.IsDropTarget(gapIndex: 4, pickedIndex: 2));
    }

    [Fact]
    public void Aucun_element_decroche_aucune_cible()
    {
        Assert.False(ClickToReorder.IsDropTarget(gapIndex: 0, pickedIndex: -1));
    }

    [Fact]
    public void BeforeId_pour_un_gap_interieur_renvoie_lelement_a_cet_index()
    {
        var ids = new[] { "a", "b", "c" };
        Assert.Equal("b", ClickToReorder.BeforeIdForGap(ids, gapIndex: 1));
    }

    [Fact]
    public void BeforeId_pour_le_dernier_gap_renvoie_null()
    {
        var ids = new[] { "a", "b", "c" };
        Assert.Null(ClickToReorder.BeforeIdForGap(ids, gapIndex: 3));
    }

    [Fact]
    public void DisplayPosition_avant_lorigine_ne_decale_pas()
    {
        // Element decroche en position 3 (index 2), repose au tout debut
        // (gap 0) : reste 1re position affichee.
        Assert.Equal(1, ClickToReorder.DisplayPositionForGap(gapIndex: 0, pickedIndex: 2));
    }

    [Fact]
    public void DisplayPosition_apres_lorigine_se_decale_dun_cran()
    {
        // L'element decroche lui-meme est retire de la liste avant reinsertion :
        // un gap situe apres son ancienne position (index 4, ex. "avant E" dans
        // A,B,C(decroche),D,E) retombe en position 4 une fois C retire (A,B,D,E).
        Assert.Equal(4, ClickToReorder.DisplayPositionForGap(gapIndex: 4, pickedIndex: 2));
    }
}
