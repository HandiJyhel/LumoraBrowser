using Xunit;

namespace Lumora.Tests;

// 2026-08-31, session "gestion des favoris" : jusqu'ici le gestionnaire ne
// permettait que créer/renommer un dossier et déplacer un favori d'UN cran
// parmi ses frères - aucun moyen de ranger un favori DANS un dossier
// n'existait ("gestion des favoris à chier", retour direct utilisateur).
// Trois ajouts : glisser-déposer réel vers un dossier, "Déplacer vers…" au
// menu contextuel, modes d'affichage Icônes/Liste/Détails. Même contrainte
// que BookmarkBarRegressionTests.cs : Models/Bookmarks.cs et MainWindow.*.cs
// dépendent de Microsoft.UI.Xaml/WebView2, absents du projet de test "pur" -
// vérifiés en régression sur le texte source, comportement réel confirmé via
// le skill verify.
public sealed class BookmarkManagementRegressionTests
{
    [Fact]
    public void MoveNode_deplace_dans_un_dossier_different_et_refuse_les_cycles()
    {
        var model = ReadRepoFile("Lumora.WinUI", "Models", "Bookmarks.cs");

        Assert.Contains("public bool MoveNode(string movedId, string newParentId)", model, StringComparison.Ordinal);
        Assert.Contains("if (moved is null || moved.IsRoot)", model, StringComparison.Ordinal);
        Assert.Contains("newParent is null || newParent.Kind != BookmarkKind.Folder || newParent.Id == movedId", model, StringComparison.Ordinal);
        Assert.Contains("IsDescendantOfMoved(nodes, newParentId, movedId)", model, StringComparison.Ordinal);
        Assert.Contains("Position = NextPosition(nodes, newParentId)", model, StringComparison.Ordinal);
        Assert.Contains("WriteNodes(nodes);", model, StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_contextuel_favori_propose_deplacer_vers_en_plus_d_avant_apres()
    {
        var flyouts = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksFlyouts.cs");

        // Pas d'accent dans les assertions : ce fichier encode "é"/"…" en
        // é/… litteraux (convention deja en place, voir
        // BookmarkBarRegressionTests.cs).
        Assert.Contains("var moveToItem = new MenuFlyoutItem", flyouts, StringComparison.Ordinal);
        Assert.Contains("Icon = new SymbolIcon(Symbol.MoveToFolder)", flyouts, StringComparison.Ordinal);
        Assert.Contains("moveToItem.Click += BookmarkContextMoveTo_Click;", flyouts, StringComparison.Ordinal);
        Assert.Contains("private async void BookmarkContextMoveTo_Click(object sender, RoutedEventArgs e)", flyouts, StringComparison.Ordinal);
        Assert.Contains("await MoveBookmarkNodesAsync(new[] { node });", flyouts, StringComparison.Ordinal);
    }

    [Fact]
    public void Deplacer_vers_reutilise_le_picker_dossier_deja_teste_et_exclut_les_cycles()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksMoveTo.cs");
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("private async void MoveSelectionButton_Click(object sender, RoutedEventArgs e)", code, StringComparison.Ordinal);
        Assert.Contains("private async Task MoveBookmarkNodesAsync(IReadOnlyList<BookmarkNode> nodes)", code, StringComparison.Ordinal);
        Assert.Contains("BuildBookmarkFolderChoices().Where(choice => !excludedIds.Contains(choice.Id))", code, StringComparison.Ordinal);
        Assert.Contains("IsDescendantOf(node.Id, candidate.Id)", code, StringComparison.Ordinal);
        Assert.Contains("_bookmarks.MoveNode(node.Id, target.Id)", code, StringComparison.Ordinal);
        Assert.Contains("Click=\"MoveSelectionButton_Click\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Glisser_depose_de_la_grille_capture_sur_le_panneau_pas_sur_le_conteneur()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksGridDragDrop.cs");

        // Meme piege documente que la barre (MainWindow.BookmarksDragDrop.cs,
        // voir MEMORY.md) : un ListViewItem gere sa propre capture pour son
        // etat visuel Pressed/selection, capturer directement dessus la
        // ferait perdre en plein glissement.
        Assert.Contains("panel.CapturePointer(e.Pointer);", code, StringComparison.Ordinal);
        Assert.DoesNotContain("container.CapturePointer(e.Pointer);", code, StringComparison.Ordinal);
        Assert.Contains(
            "container.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(BookmarkGridItem_PointerPressed), true);",
            code, StringComparison.Ordinal);
        Assert.Contains("BookmarkDropMath.ComputeGridDropTarget(", code, StringComparison.Ordinal);
        Assert.Contains("_bookmarks.MoveNode(draggedId, targetId)", code, StringComparison.Ordinal);
        Assert.Contains("_bookmarks.ReorderNode(draggedId, targetId)", code, StringComparison.Ordinal);
        Assert.Contains("if (item.Node.IsRoot) return;", code, StringComparison.Ordinal);

        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        Assert.Contains("ContainerContentChanging=\"BookmarksList_ContainerContentChanging\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Modes_affichage_favoris_permutent_gabarit_et_persistent_le_choix()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksViewMode.cs");
        var settingsModel = ReadRepoFile("Lumora.WinUI", "Models", "UiSettings.cs");
        var settingsCode = ReadRepoFile("Lumora.WinUI", "MainWindow.Settings.cs");
        var layoutCode = ReadRepoFile("Lumora.WinUI", "MainWindow.LayoutStudio.cs");
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("public string BookmarkViewMode { get; set; } = \"icons\";", settingsModel, StringComparison.Ordinal);
        Assert.Contains("_bookmarkViewMode = NormalizeBookmarkViewMode(_uiSettings.BookmarkViewMode);", settingsCode, StringComparison.Ordinal);
        Assert.Contains("_uiSettings.BookmarkViewMode = _bookmarkViewMode;", layoutCode, StringComparison.Ordinal);

        Assert.Contains("private void ApplyBookmarkViewMode()", code, StringComparison.Ordinal);
        Assert.Contains("BookmarksList.ItemsPanel = (ItemsPanelTemplate)RootShell.Resources[panelKey];", code, StringComparison.Ordinal);
        Assert.Contains("BookmarksList.ItemTemplate = (DataTemplate)RootShell.Resources[templateKey];", code, StringComparison.Ordinal);

        Assert.Contains("x:Key=\"BookmarkIconsItemTemplate\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"BookmarkListItemTemplate\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"BookmarkDetailsItemTemplate\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BookmarkDetailsHeaderRow\"", xaml, StringComparison.Ordinal);
        Assert.Contains("GroupName=\"BookmarkViewMode\"", xaml, StringComparison.Ordinal);
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
