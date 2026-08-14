using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// 2026-08-14 : logique de reordonnancement par glisser manuel (remplace
// CanDrag/DragStarting, non fonctionnel sur des Button en WinUI 3 - voir
// MEMORY.md). Le geste physique de glisser reste impossible a simuler dans
// le bac a sable de verification, mais ce calcul, lui, est une fonction pure
// verifiable directement.
public sealed class HorizontalDragReorderMathTests
{
    // Rangee de 3 elements : a [0-50], b [50-100], c [100-150].
    private static readonly (string Id, double Left, double Width)[] Row =
    {
        ("a", 0, 50),
        ("b", 50, 50),
        ("c", 100, 50),
    };

    [Fact]
    public void FindTargetId_retourne_l_element_dont_le_centre_est_le_plus_proche()
    {
        // Centre de "a" = 25, de "b" = 75, de "c" = 125.
        Assert.Equal("a", HorizontalDragReorderMath.FindTargetId(Row, "b", pointerX: 20));
        Assert.Equal("c", HorizontalDragReorderMath.FindTargetId(Row, "a", pointerX: 130));
    }

    [Fact]
    public void FindTargetId_exclut_toujours_l_element_glisse_lui_meme()
    {
        // Pointeur exactement sur le centre de "b", mais "b" est l'element
        // glisse : ne doit jamais se proposer lui-meme comme cible.
        var target = HorizontalDragReorderMath.FindTargetId(Row, draggedId: "b", pointerX: 75);
        Assert.NotEqual("b", target);
    }

    [Fact]
    public void FindTargetId_retourne_null_si_seul_l_element_glisse_existe()
    {
        var single = new[] { ("solo", 0d, 50d) };
        Assert.Null(HorizontalDragReorderMath.FindTargetId(single, "solo", pointerX: 25));
    }

    [Fact]
    public void FindTargetId_gere_une_liste_vide()
    {
        Assert.Null(HorizontalDragReorderMath.FindTargetId(Array.Empty<(string, double, double)>(), "x", pointerX: 0));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 8, false)]      // aucun mouvement
    [InlineData(0, 0, 5, 0, 8, false)]      // sous le seuil
    [InlineData(0, 0, 8, 0, 8, true)]       // exactement au seuil
    [InlineData(0, 0, 3, 6, 8, true)]       // diagonale, somme >= seuil
    [InlineData(0, 0, -20, 0, 8, true)]     // mouvement negatif (vers la gauche)
    public void ExceedsDragThreshold_bascule_au_bon_seuil(
        double startX, double startY, double currentX, double currentY, double threshold, bool expected)
    {
        Assert.Equal(expected, HorizontalDragReorderMath.ExceedsDragThreshold(startX, startY, currentX, currentY, threshold));
    }
}
