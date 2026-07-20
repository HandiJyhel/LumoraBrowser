using Xunit;

namespace Lumora.Tests;

public sealed class BookmarkBarRegressionTests
{
    [Fact]
    public void ToolbarRendering_ne_limite_pas_les_favoris_visibles_a_un_nombre_arbitraire()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");

        Assert.DoesNotContain(".Take(18)", source, StringComparison.Ordinal);
        Assert.Contains("RevealBookmarkInBar(saved.Id)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Barre_de_favoris_utilise_un_menu_de_debordement_plutot_qu_un_ascenseur()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");

        Assert.DoesNotContain("BookmarksBarScrollViewer", xaml, StringComparison.Ordinal);
        Assert.Contains("CreateBookmarksOverflowButton", code, StringComparison.Ordinal);
        Assert.Contains("CreateBookmarksOverflowFlyout", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Gestionnaire_de_favoris_expose_selection_multiple_et_suppression_massive()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");

        Assert.Contains("SelectionMode=\"Extended\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectAllBookmarksButton_Click", code, StringComparison.Ordinal);
        Assert.Contains("ClearBookmarksButton_Click", code, StringComparison.Ordinal);
        Assert.Contains("RemoveNodes(selected.Select(node => node.Id))", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Titres_invisibles_sont_preserves_pour_les_favoris_icone_seule()
    {
        var model = ReadRepoFile("Lumora.WinUI", "Models", "Bookmarks.cs");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksDialogs.cs");

        Assert.Contains("public const string InvisibleTitle = \"\\u200B\";", model, StringComparison.Ordinal);
        Assert.Contains("Nom invisible (icone seule dans la barre)", code, StringComparison.Ordinal);
        Assert.Contains("BookmarkStore.InvisibleTitle", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_favoris_fusionne_les_dossiers_homonymes_au_lieu_de_les_doubler()
    {
        var model = ReadRepoFile("Lumora.WinUI", "Models", "Bookmarks.cs");

        Assert.Contains("public int RepairImportedFolderDuplicates()", model, StringComparison.Ordinal);
        Assert.Contains("FindExistingImportFolder(nodes, parentId, folderTitle)", model, StringComparison.Ordinal);
        Assert.Contains("MergeSiblingImportFolders(nodes);", model, StringComparison.Ordinal);
        Assert.Contains("SameImportFolderTitle(node.Title, title)", model, StringComparison.Ordinal);
        Assert.Contains("createdFolder && childImported == 0", model, StringComparison.Ordinal);
    }

    [Fact]
    public void Demarrage_repare_les_doublons_de_favoris_existants()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("_bookmarks.RepairImportedFolderDuplicates()", code, StringComparison.Ordinal);
        Assert.Contains("Bookmark duplicate repair changed", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_propre_publie_l_application_autonome()
    {
        var script = ReadRepoFile("scripts", "build-clean-test-artifact.ps1");

        // Le "/t:Publish" litteral a ete remplace par -Target "Publish" passe a
        // la fonction partagee Invoke-WinUiTarget (winui-build-common.ps1) lors
        // d'un refactor legitime : le comportement (publish autonome complet)
        // est inchange, seule la forme d'ecriture de la cible MSBuild a bouge.
        Assert.Contains("-Target \"Publish\"", script, StringComparison.Ordinal);
        Assert.Contains("/p:SelfContained=true", script, StringComparison.Ordinal);
        Assert.Contains("/p:PublishSelfContained=true", script, StringComparison.Ordinal);
        Assert.Contains("/p:PublishDir=$appDir\\\"", script, StringComparison.Ordinal);
        Assert.Contains("[switch]$NoRestore", script, StringComparison.Ordinal);
        Assert.Contains("App.xbf", script, StringComparison.Ordinal);
        Assert.Contains("MainWindow.xbf", script, StringComparison.Ordinal);
        Assert.Contains("LumoraAppWindow.xbf", script, StringComparison.Ordinal);
        Assert.Contains("Fichier XAML compile introuvable apres publish", script, StringComparison.Ordinal);
        Assert.Contains("Lumora.WinUI.pri", script, StringComparison.Ordinal);
        Assert.Contains("Fichier de ressources WinUI introuvable apres publish", script, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Fichier projet introuvable: {string.Join(Path.DirectorySeparatorChar, segments)}");
    }
}
