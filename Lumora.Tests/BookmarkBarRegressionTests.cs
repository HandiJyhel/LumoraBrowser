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
        Assert.Contains("Nom invisible (icône seule dans la barre)", code, StringComparison.Ordinal);
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

    // 2026-08-13 : glisser-deposer direct des favoris dans la barre, meme
    // comportement que Chrome/Edge (aucun mode dedie a activer, contrairement
    // a la reorganisation de la barre d'outils). Verifie en regression sur le
    // texte source plutot qu'en instanciant BookmarkStore : Models/Bookmarks.cs
    // depend de Microsoft.UI.Xaml (Visibility) et Microsoft.Web.WebView2.Core,
    // absents du projet de test "pur" (voir le commentaire en tete de
    // Lumora.Tests.csproj) - meme contrainte que tous les autres tests de ce
    // fichier, qui verifient deja BookmarkStore par lecture de source.
    [Fact]
    public void ReorderNode_deplace_entre_freres_du_meme_parent_et_refuse_les_racines()
    {
        var model = ReadRepoFile("Lumora.WinUI", "Models", "Bookmarks.cs");

        Assert.Contains("public bool ReorderNode(string movedId, string? beforeId)", model, StringComparison.Ordinal);
        Assert.Contains("if (moved is null || moved.IsRoot)", model, StringComparison.Ordinal);
        Assert.Contains("!before.ParentId.Equals(moved.ParentId, StringComparison.Ordinal)", model, StringComparison.Ordinal);
        Assert.Contains("WriteNodes(nodes)", model, StringComparison.Ordinal);
    }

    // 2026-08-14 : CanDrag/DragStarting ne fonctionnaient pas du tout sur un
    // Button (confirme par la documentation officielle Microsoft), remplaces
    // par un suivi manuel du pointeur - voir HorizontalDragReorderMath.cs et
    // MainWindow.BookmarksDragDrop.cs.
    //
    // 2026-08-14 (2e correctif, meme jour) : le suivi manuel via += ne se
    // declenchait pas non plus - ButtonBase intercepte deja PointerPressed en
    // interne pour son propre Click (confirme par la documentation officielle
    // Microsoft), une souscription normale ne recoit jamais l'evenement.
    // Cable desormais via AddHandler(..., handledEventsToo: true), la seule
    // methode documentee pour recevoir l'evenement malgre cette interception.
    //
    // 2026-08-14 (3e correctif, meme jour) : meme apres ce 2e correctif, une
    // trace de diagnostic reelle a prouve que l'etat de glissement retombait
    // a false AVANT PointerReleased - preuve que PointerCaptureLost se
    // declenchait silencieusement en plein glissement, cause par la capture
    // du pointeur posee SUR LE BOUTON (un Button gere sa propre capture en
    // interne pour son etat visuel Pressed/Click et la relache des que le
    // pointeur sort de ses limites). Corrige en capturant sur le PANNEAU
    // (BookmarksBarPanel/BookmarksBottomBarPanel) : seul PointerPressed reste
    // cable par bouton, Moved/Released/CaptureLost passent sur les panneaux.
    [Fact]
    public void Boutons_de_la_barre_de_favoris_sont_glissables_sauf_les_dossiers_racine()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");
        var dragDrop = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksDragDrop.cs");

        Assert.Contains("if (!node.IsRoot)", code, StringComparison.Ordinal);
        Assert.Contains("button.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(BookmarkBarButton_PointerPressed), true);", code, StringComparison.Ordinal);
        // Moved/Released/CaptureLost ne sont plus cables par bouton (3e
        // correctif) : ils vivent desormais sur les panneaux, voir
        // WireBookmarkDragPanels ci-dessous.
        Assert.DoesNotContain("button.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(BookmarkBarButton_PointerMoved), true);", code, StringComparison.Ordinal);
        Assert.DoesNotContain("button.PointerPressed += BookmarkBarButton_PointerPressed;", code, StringComparison.Ordinal);
        Assert.DoesNotContain("button.CanDrag = true;", code, StringComparison.Ordinal);
        Assert.Contains("_bookmarks.ReorderNode(draggedId, targetId)", dragDrop, StringComparison.Ordinal);
        Assert.Contains("ReloadBookmarks();", dragDrop, StringComparison.Ordinal);
        Assert.Contains("HorizontalDragReorderMath.FindTargetId(", dragDrop, StringComparison.Ordinal);
    }

    // 2026-08-14 (3e correctif) : la capture doit se faire sur le panneau
    // (BookmarksBarPanel / BookmarksBottomBarPanel selon la position de la
    // barre), jamais sur le bouton - preuve reelle via trace de diagnostic,
    // voir MEMORY.md.
    [Fact]
    public void Capture_du_pointeur_favoris_se_fait_sur_le_panneau_pas_sur_le_bouton()
    {
        var dragDrop = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksDragDrop.cs");
        var xamlCs = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("panel.CapturePointer(e.Pointer);", dragDrop, StringComparison.Ordinal);
        Assert.DoesNotContain("btn.CapturePointer(e.Pointer);", dragDrop, StringComparison.Ordinal);
        Assert.Contains("private void WireBookmarkDragPanels()", dragDrop, StringComparison.Ordinal);
        Assert.Contains("panel.PointerMoved += BookmarkDragPanel_PointerMoved;", dragDrop, StringComparison.Ordinal);
        Assert.Contains("panel.PointerReleased += BookmarkDragPanel_PointerReleased;", dragDrop, StringComparison.Ordinal);
        Assert.Contains("panel.PointerCaptureLost += BookmarkDragPanel_PointerCaptureLost;", dragDrop, StringComparison.Ordinal);
        Assert.Contains("WireBookmarkDragPanels();", xamlCs, StringComparison.Ordinal);
    }

    // Le clic qui suit un vrai glissement ne doit pas aussi ouvrir le favori
    // qu'on vient de deplacer (BookmarkBarButton_Click, MainWindow.Bookmarks.cs).
    [Fact]
    public void Clic_est_ignore_immediatement_apres_un_glissement()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");

        Assert.Contains("if (_bookmarkSuppressNextClick)", code, StringComparison.Ordinal);
    }

    // 2026-08-14 : alternative au glisser demandee explicitement par
    // l'utilisateur apres 3 correctifs reels du glisser-depose qui echouaient
    // encore en conditions reelles ("un clic droit sur le favori que je veux
    // déplacer... ça évitera les fausses manipulations"). Deplace d'UN cran
    // via un simple clic, sans dependre d'aucun geste souris ni de capture de
    // pointeur - donc insensible au bug de capture qui touchait le glisser.
    [Fact]
    public void MoveNodeAdjacent_delegue_le_calcul_pur_et_appelle_ReorderNode_deja_teste()
    {
        var model = ReadRepoFile("Lumora.WinUI", "Models", "Bookmarks.cs");

        Assert.Contains("public bool MoveNodeAdjacent(string movedId, bool moveForward)", model, StringComparison.Ordinal);
        Assert.Contains("if (moved is null || moved.IsRoot)", model, StringComparison.Ordinal);
        Assert.Contains("AdjacentMoveMath.ComputeTarget(orderedIds, movedId, moveForward)", model, StringComparison.Ordinal);
        Assert.Contains("return canMove && ReorderNode(movedId, beforeId);", model, StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_contextuel_favori_propose_deplacer_avant_apres_desactive_aux_extremites()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksFlyouts.cs");

        // Pas d'accent dans les assertions : le fichier source encode "é" en
        // "é" litteral (convention deja en place dans ce fichier, voir
        // "Gérer les favoris" plus haut), pas en UTF-8 brut.
        Assert.Contains("moveBeforeItem", code, StringComparison.Ordinal);
        Assert.Contains("moveAfterItem", code, StringComparison.Ordinal);
        Assert.Contains("BookmarkContextMoveBefore_Click", code, StringComparison.Ordinal);
        Assert.Contains("BookmarkContextMoveAfter_Click", code, StringComparison.Ordinal);
        Assert.Contains("IsEnabled = !isFirst", code, StringComparison.Ordinal);
        Assert.Contains("IsEnabled = !isLast", code, StringComparison.Ordinal);
        Assert.Contains("_bookmarks.MoveNodeAdjacent(node.Id, moveForward: false)", code, StringComparison.Ordinal);
        Assert.Contains("_bookmarks.MoveNodeAdjacent(node.Id, moveForward: true)", code, StringComparison.Ordinal);
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
