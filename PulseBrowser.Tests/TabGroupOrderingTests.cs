using PulseBrowser.WinUI;
using Xunit;

namespace PulseBrowser.Tests;

// Pas de vrai glisser-déposer d'onglets dans ce navigateur : rejoindre/quitter un
// groupe reordonne _tabs pour que le rail vertical affiche des grappes contiguës.
public sealed class TabGroupOrderingTests
{
    [Fact]
    public void ReorderForGroup_place_l_onglet_juste_apres_le_dernier_membre_du_groupe()
    {
        var order = new List<int> { 1, 4, 3, 2 };
        var groupByTabId = new Dictionary<int, int?> { [1] = 10, [4] = null, [3] = 10, [2] = null };

        var result = TabGroupOrdering.ReorderForGroup(order, movingTabId: 4, targetGroupId: 10, groupByTabId);

        Assert.Equal(new List<int> { 1, 3, 4, 2 }, result);
    }

    [Fact]
    public void ReorderForGroup_place_en_tete_si_le_groupe_n_a_aucun_autre_membre()
    {
        var order = new List<int> { 1, 2, 3 };
        var groupByTabId = new Dictionary<int, int?> { [1] = null, [2] = null, [3] = null };

        var result = TabGroupOrdering.ReorderForGroup(order, movingTabId: 3, targetGroupId: 99, groupByTabId);

        Assert.Equal(new List<int> { 3, 1, 2 }, result);
    }

    [Fact]
    public void ReorderForGroup_retour_a_null_conserve_la_position_d_origine()
    {
        var order = new List<int> { 1, 2, 3, 4 };
        var groupByTabId = new Dictionary<int, int?> { [1] = 10, [2] = 10, [3] = 10, [4] = null };

        var result = TabGroupOrdering.ReorderForGroup(order, movingTabId: 2, targetGroupId: null, groupByTabId);

        Assert.Equal(new List<int> { 1, 2, 3, 4 }, result);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(6, 0)]
    [InlineData(7, 1)]
    public void NextColorIndex_boucle_sur_la_taille_de_la_palette(int existingGroupCount, int expected)
    {
        Assert.Equal(expected, TabGroupOrdering.NextColorIndex(existingGroupCount, paletteSize: 6));
    }
}
