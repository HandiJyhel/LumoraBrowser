using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// 2026-08-31 : logique du glisser-deposer "vers un dossier" du gestionnaire
// de favoris (session "gestion des favoris") - meme principe que
// HorizontalDragReorderMathTests.cs, fonction pure verifiable directement,
// le geste physique reste hors de portee du bac a sable de verification.
public sealed class BookmarkDropMathTests
{
    // Grille 2 colonnes x 2 rangees : "folder" [0,0]-[100,100] (dossier),
    // "link" [100,0]-[200,100] (favori).
    private static readonly BookmarkDropMath.DropItem[] Grid =
    {
        new("folder", IsFolder: true, Left: 0, Top: 0, Width: 100, Height: 100),
        new("link", IsFolder: false, Left: 100, Top: 0, Width: 100, Height: 100),
    };

    [Fact]
    public void ComputeGridDropTarget_survol_au_centre_d_un_dossier_propose_d_y_ranger()
    {
        var (targetId, into) = BookmarkDropMath.ComputeGridDropTarget(Grid, draggedId: "link", pointerX: 50, pointerY: 50);

        Assert.Equal("folder", targetId);
        Assert.True(into);
    }

    [Fact]
    public void ComputeGridDropTarget_survol_du_bord_d_un_dossier_reordonne_au_lieu_de_ranger_dedans()
    {
        // Bord gauche de "folder" (Left=0), largement hors de la marge
        // interieure (22%) : doit rester un simple reordonnancement.
        var (targetId, into) = BookmarkDropMath.ComputeGridDropTarget(Grid, draggedId: "link", pointerX: 2, pointerY: 50);

        Assert.Equal("folder", targetId);
        Assert.False(into);
    }

    [Fact]
    public void ComputeGridDropTarget_ne_propose_jamais_de_ranger_dans_un_favori_simple()
    {
        // Meme au centre exact de "link" (pas un dossier), IntoFolder doit
        // rester faux - seul un reordonnancement est possible sur un favori.
        var (targetId, into) = BookmarkDropMath.ComputeGridDropTarget(Grid, draggedId: "folder", pointerX: 150, pointerY: 50);

        Assert.Equal("link", targetId);
        Assert.False(into);
    }

    [Fact]
    public void ComputeGridDropTarget_exclut_toujours_l_element_glisse_lui_meme()
    {
        var (targetId, _) = BookmarkDropMath.ComputeGridDropTarget(Grid, draggedId: "folder", pointerX: 50, pointerY: 50);

        Assert.NotEqual("folder", targetId);
    }

    [Fact]
    public void ComputeGridDropTarget_retourne_null_si_seul_l_element_glisse_existe()
    {
        var single = new[] { new BookmarkDropMath.DropItem("solo", true, 0, 0, 100, 100) };

        var (targetId, into) = BookmarkDropMath.ComputeGridDropTarget(single, draggedId: "solo", pointerX: 50, pointerY: 50);

        Assert.Null(targetId);
        Assert.False(into);
    }

    [Fact]
    public void FindTreeRowAt_retourne_la_ligne_qui_contient_verticalement_le_pointeur()
    {
        var rows = new (string Id, double Top, double Height)[]
        {
            ("racine", 0, 32),
            ("travail", 32, 32),
            ("voyages", 64, 32),
        };

        Assert.Equal("racine", BookmarkDropMath.FindTreeRowAt(rows, pointerY: 10));
        Assert.Equal("travail", BookmarkDropMath.FindTreeRowAt(rows, pointerY: 40));
        Assert.Equal("voyages", BookmarkDropMath.FindTreeRowAt(rows, pointerY: 80));
    }

    [Fact]
    public void FindTreeRowAt_retourne_null_hors_de_toute_ligne()
    {
        var rows = new (string Id, double Top, double Height)[] { ("seule", 0, 32) };

        Assert.Null(BookmarkDropMath.FindTreeRowAt(rows, pointerY: 100));
    }
}
