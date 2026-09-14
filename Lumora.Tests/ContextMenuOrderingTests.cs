using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class ContextMenuOrderingTests
{
    [Fact]
    public void Sans_masque_ni_ordre_la_liste_est_inchangee()
    {
        var items = new[] { "copy", "cut", "paste" };
        var result = ContextMenuOrdering.Apply(items, n => n, hiddenNames: [], order: []);
        Assert.Equal(items, result);
    }

    [Fact]
    public void Un_element_masque_est_retire()
    {
        var items = new[] { "copy", "cut", "paste" };
        var result = ContextMenuOrdering.Apply(items, n => n, hiddenNames: ["cut"], order: []);
        Assert.Equal(["copy", "paste"], result);
    }

    [Fact]
    public void Separateur_sans_nom_nest_jamais_masque()
    {
        var items = new[] { "copy", "", "paste" };
        var result = ContextMenuOrdering.Apply(items, n => n, hiddenNames: [""], order: []);
        Assert.Equal(items, result);
    }

    [Fact]
    public void Ordre_choisi_passe_en_tete_le_reste_suit_dans_son_ordre_naturel()
    {
        var items = new[] { "copy", "cut", "paste", "selectAll" };
        var result = ContextMenuOrdering.Apply(items, n => n, hiddenNames: [], order: ["paste", "cut"]);
        Assert.Equal(["paste", "cut", "copy", "selectAll"], result);
    }

    [Fact]
    public void Masque_et_ordre_combines()
    {
        var items = new[] { "copy", "cut", "paste", "selectAll" };
        var result = ContextMenuOrdering.Apply(items, n => n, hiddenNames: ["copy"], order: ["selectAll"]);
        Assert.Equal(["selectAll", "cut", "paste"], result);
    }

    [Fact]
    public void Un_nom_dans_lordre_absent_de_la_liste_ne_plante_pas()
    {
        var items = new[] { "copy", "cut" };
        var result = ContextMenuOrdering.Apply(items, n => n, hiddenNames: [], order: ["inconnu", "cut"]);
        Assert.Equal(["cut", "copy"], result);
    }

    [Fact]
    public void Liste_vide_apres_masquage_ne_plante_pas()
    {
        var items = new[] { "copy" };
        var result = ContextMenuOrdering.Apply(items, n => n, hiddenNames: ["copy"], order: ["copy"]);
        Assert.Empty(result);
    }
}
