using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Verrouille la logique pure de la barre des taches Lumora (2026-08-18,
// Lumora.WinUI/OpenPanelsTaskbar.cs) : ordre de la liste des modules
// "application" ouverts (Coffre, Favoris, Historique...), separee du rendu
// UI (MainWindow.OpenPanelsTaskbar.cs) - meme separation pure/UI que
// StartMenuTileRegistry.cs.
public sealed class OpenPanelsTaskbarTests
{
    [Fact]
    public void AddIfMissing_ajoute_un_id_absent_en_fin_de_liste()
    {
        var result = OpenPanelsTaskbar.AddIfMissing([StartMenuTileIds.Vault], StartMenuTileIds.Favoris);

        Assert.Equal([StartMenuTileIds.Vault, StartMenuTileIds.Favoris], result);
    }

    [Fact]
    public void AddIfMissing_ne_duplique_pas_et_ne_reordonne_pas_un_id_deja_present()
    {
        // Coeur du comportement "barre des taches Windows" : reactiver une
        // application deja ouverte ne deplace pas son icone.
        var open = new List<string> { StartMenuTileIds.Vault, StartMenuTileIds.Favoris, StartMenuTileIds.History };

        var result = OpenPanelsTaskbar.AddIfMissing(open, StartMenuTileIds.Vault);

        Assert.Equal(open, result);
    }

    [Fact]
    public void AddIfMissing_sur_une_liste_vide_cree_une_liste_a_un_element()
    {
        var result = OpenPanelsTaskbar.AddIfMissing([], StartMenuTileIds.Settings);

        Assert.Equal([StartMenuTileIds.Settings], result);
    }

    [Fact]
    public void Remove_retire_l_id_et_preserve_l_ordre_des_autres()
    {
        var open = new List<string> { StartMenuTileIds.Vault, StartMenuTileIds.Favoris, StartMenuTileIds.History };

        var result = OpenPanelsTaskbar.Remove(open, StartMenuTileIds.Favoris);

        Assert.Equal([StartMenuTileIds.Vault, StartMenuTileIds.History], result);
    }

    [Fact]
    public void Remove_d_un_id_absent_ne_change_rien()
    {
        var open = new List<string> { StartMenuTileIds.Vault };

        var result = OpenPanelsTaskbar.Remove(open, StartMenuTileIds.History);

        Assert.Equal(open, result);
    }

    [Fact]
    public void Remove_sur_une_liste_vide_reste_vide()
    {
        var result = OpenPanelsTaskbar.Remove([], StartMenuTileIds.Vault);

        Assert.Empty(result);
    }

    [Fact]
    public void Chaque_definition_suivie_a_un_titre_et_un_glyphe_non_vides()
    {
        // Filet de securite : un titre ou un glyphe vide se traduirait par un
        // bouton de barre des taches illisible ou muet pour un lecteur
        // d'ecran (AutomationProperties.Name construit a partir de Title,
        // voir MainWindow.OpenPanelsTaskbar.cs).
        foreach (var definition in OpenPanelsTaskbar.Definitions)
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Id));
            Assert.False(string.IsNullOrWhiteSpace(definition.Title));
            Assert.False(string.IsNullOrWhiteSpace(definition.Glyph));
        }
    }

    [Fact]
    public void Les_13_ids_suivis_sont_tous_distincts()
    {
        var ids = OpenPanelsTaskbar.Definitions.Select(d => d.Id).ToList();

        Assert.Equal(13, ids.Count);
        Assert.Equal(ids.Distinct(StringComparer.Ordinal).Count(), ids.Count);
    }

    [Fact]
    public void Find_retrouve_une_definition_par_id_et_rend_null_sinon()
    {
        var vault = OpenPanelsTaskbar.Find(StartMenuTileIds.Vault);
        var rss = OpenPanelsTaskbar.Find(OpenPanelsTaskbar.RssId);
        var inconnu = OpenPanelsTaskbar.Find("ceci-n-existe-pas");

        Assert.NotNull(vault);
        Assert.Equal("Coffre", vault!.Title);
        Assert.NotNull(rss);
        Assert.Equal("Flux RSS", rss!.Title);
        Assert.Null(inconnu);
    }
}
