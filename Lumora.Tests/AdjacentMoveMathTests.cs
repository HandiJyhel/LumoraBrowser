using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Tests comportementaux reels (pas de regression sur texte source) de la
// logique pure de deplacement "d'un cran" des favoris - voir
// AdjacentMoveMath.cs pour le contexte complet.
public sealed class AdjacentMoveMathTests
{
    private static readonly string[] Order = ["a", "b", "c", "d"];

    [Fact]
    public void Deplacer_avant_prend_la_place_du_frere_precedent()
    {
        var (canMove, beforeId) = AdjacentMoveMath.ComputeTarget(Order, "c", moveForward: false);

        Assert.True(canMove);
        Assert.Equal("b", beforeId);
    }

    [Fact]
    public void Deplacer_avant_refuse_si_deja_premier()
    {
        var (canMove, beforeId) = AdjacentMoveMath.ComputeTarget(Order, "a", moveForward: false);

        Assert.False(canMove);
        Assert.Null(beforeId);
    }

    [Fact]
    public void Deplacer_apres_saute_le_frere_suivant()
    {
        // a,b,c,d ; deplacer b apres -> doit se placer avant d (apres c)
        var (canMove, beforeId) = AdjacentMoveMath.ComputeTarget(Order, "b", moveForward: true);

        Assert.True(canMove);
        Assert.Equal("d", beforeId);
    }

    [Fact]
    public void Deplacer_apres_avant_dernier_place_en_dernier_beforeId_null()
    {
        // a,b,c,d ; deplacer c apres -> d etait le dernier, donc c prend sa
        // place finale (beforeId=null = "en dernier" pour ReorderNode).
        var (canMove, beforeId) = AdjacentMoveMath.ComputeTarget(Order, "c", moveForward: true);

        Assert.True(canMove);
        Assert.Null(beforeId);
    }

    [Fact]
    public void Deplacer_apres_refuse_si_deja_dernier()
    {
        var (canMove, beforeId) = AdjacentMoveMath.ComputeTarget(Order, "d", moveForward: true);

        Assert.False(canMove);
        Assert.Null(beforeId);
    }

    [Fact]
    public void Id_absent_de_la_liste_refuse_sans_planter()
    {
        var (canMove, beforeId) = AdjacentMoveMath.ComputeTarget(Order, "inconnu", moveForward: true);

        Assert.False(canMove);
        Assert.Null(beforeId);
    }

    [Fact]
    public void Liste_a_un_seul_element_refuse_les_deux_directions()
    {
        var single = new[] { "solo" };

        Assert.False(AdjacentMoveMath.ComputeTarget(single, "solo", moveForward: true).CanMove);
        Assert.False(AdjacentMoveMath.ComputeTarget(single, "solo", moveForward: false).CanMove);
    }
}
