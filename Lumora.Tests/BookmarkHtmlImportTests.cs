using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// 2026-10-25 : l'import HTML aplatissait tout dans un seul dossier (balayage des
// <A> seulement) - l'utilisateur voulait garder l'arborescence de son navigateur.
public sealed class BookmarkHtmlImportTests
{
    private const string ChromeExport = """
        <!DOCTYPE NETSCAPE-Bookmark-file-1>
        <META HTTP-EQUIV="Content-Type" CONTENT="text/html; charset=UTF-8">
        <TITLE>Bookmarks</TITLE>
        <H1>Bookmarks</H1>
        <DL><p>
            <DT><H3 ADD_DATE="1" LAST_MODIFIED="2" PERSONAL_TOOLBAR_FOLDER="true">Barre de favoris</H3>
            <DL><p>
                <DT><A HREF="https://exemple.fr/" ADD_DATE="1">Accueil</A>
                <DT><H3 ADD_DATE="1">T.C.G.</H3>
                <DL><p>
                    <DT><A HREF="https://ludotrotter.com/" ADD_DATE="1">Ludotrotter</A>
                    <DT><H3 ADD_DATE="1">Pokémon</H3>
                    <DL><p>
                        <DT><A HREF="https://pokemon.com/?a=1&amp;b=2">Pok&eacute;mon &amp; co</A>
                    </DL><p>
                </DL><p>
                <DT><H3 ADD_DATE="1">Dossier vide</H3>
                <DL><p>
                </DL><p>
            </DL><p>
            <DT><H3 ADD_DATE="1">Autres favoris</H3>
            <DL><p>
                <DT><A HREF="https://autre.org/">Autre</A>
                <DT><H3 ADD_DATE="1">Lectures</H3>
                <DL><p>
                    <DT><A HREF="https://lire.net/">Lire</A>
                </DL><p>
            </DL><p>
        </DL><p>
        """;

    [Fact]
    public void Garde_les_dossiers_et_sous_dossiers_de_la_barre_de_favoris()
    {
        var tree = BookmarkImportTree.FromHtml(ChromeExport, "export");

        Assert.Equal(3, tree.Toolbar.Count);
        Assert.Equal("Accueil", tree.Toolbar[0].Title);
        Assert.Equal("https://exemple.fr/", tree.Toolbar[0].Url);

        var tcg = tree.Toolbar[1];
        Assert.Null(tcg.Url);
        Assert.Equal("T.C.G.", tcg.Title);
        Assert.Equal(2, tcg.Children.Count);
        Assert.Equal("Ludotrotter", tcg.Children[0].Title);

        var pokemon = tcg.Children[1];
        Assert.Equal("Pokémon", pokemon.Title);
        var lien = Assert.Single(pokemon.Children);
        Assert.Equal("https://pokemon.com/?a=1&b=2", lien.Url);
        Assert.Equal("Pokémon & co", lien.Title);

        Assert.Equal("Dossier vide", tree.Toolbar[2].Title);
        Assert.Empty(tree.Toolbar[2].Children);
    }

    [Fact]
    public void Le_contenu_de_Autres_favoris_va_directement_dans_Autres_favoris_de_Lumora()
    {
        var tree = BookmarkImportTree.FromHtml(ChromeExport, "export");

        Assert.Equal(2, tree.Other.Count);
        Assert.Equal("Autre", tree.Other[0].Title);
        Assert.Equal("Lectures", tree.Other[1].Title);
        Assert.Equal("https://lire.net/", Assert.Single(tree.Other[1].Children).Url);
        Assert.DoesNotContain(tree.Other, item => item.Title == "Autres favoris");
    }

    [Fact]
    public void Export_Firefox_reconnait_la_barre_personnelle_et_les_marque_pages_non_classes()
    {
        const string firefox = """
            <DL><p>
                <DT><H3 PERSONAL_TOOLBAR_FOLDER="true">Barre personnelle</H3>
                <DL><p>
                    <DT><A HREF="https://a.example/">A</A>
                </DL><p>
                <DT><H3 UNFILED_BOOKMARKS_FOLDER="true">Étiquettes diverses</H3>
                <DL><p>
                    <DT><A HREF="https://b.example/">B</A>
                </DL><p>
                <DT><A HREF="https://menu.example/">Menu</A>
            </DL><p>
            """;

        var tree = BookmarkImportTree.FromHtml(firefox, "ff");

        Assert.Equal("A", Assert.Single(tree.Toolbar).Title);
        Assert.Equal(new[] { "B", "Menu" }, tree.Other.Select(item => item.Title));
    }

    [Fact]
    public void Un_fichier_sans_aucun_dossier_reste_regroupe_dans_un_dossier_nomme_d_apres_le_fichier()
    {
        const string plat = """
            <DL><p>
                <DT><A HREF="https://a.example/">A</A>
                <DT><A HREF="https://b.example/">B</A>
            </DL><p>
            """;

        var tree = BookmarkImportTree.FromHtml(plat, "mes-favoris");

        Assert.Empty(tree.Toolbar);
        var dossier = Assert.Single(tree.Other);
        Assert.Equal("Import HTML mes-favoris", dossier.Title);
        Assert.Equal(2, dossier.Children.Count);
    }

    [Fact]
    public void Ignore_les_liens_qui_ne_sont_pas_des_pages_web()
    {
        const string html = """
            <DL><p>
                <DT><H3 PERSONAL_TOOLBAR_FOLDER="true">Barre</H3>
                <DL><p>
                    <DT><A HREF="javascript:alert(1)">Script</A>
                    <DT><A HREF="chrome://settings">Réglages</A>
                    <DT><A HREF="https://ok.example/">Ok</A>
                </DL><p>
            </DL><p>
            """;

        var tree = BookmarkImportTree.FromHtml(html, "x");

        Assert.Equal("Ok", Assert.Single(tree.Toolbar).Title);
    }

    [Fact]
    public void Un_lien_apres_un_dossier_vide_sans_liste_n_est_pas_range_dans_ce_dossier()
    {
        const string html = """
            <DL><p>
                <DT><H3>Dossier sans liste</H3>
                <DT><A HREF="https://a.example/">A</A>
            </DL><p>
            """;

        var tree = BookmarkImportTree.FromHtml(html, "x");

        Assert.Equal(2, tree.Other.Count);
        Assert.Empty(tree.Other[0].Children);
        Assert.Equal("A", tree.Other[1].Title);
    }
}
