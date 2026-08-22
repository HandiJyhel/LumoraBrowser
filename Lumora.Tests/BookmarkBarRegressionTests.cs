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

    // 2026-08-22 : un import de plusieurs milliers de favoris (barre navigateur
    // pinnee sur des annees) rescannait toute la liste de noeuds a CHAQUE
    // favori ajoute (NextNodeId/NextPosition/detection de doublon), un cout
    // O(n^2) execute en synchrone sur le thread d'interface - l'application
    // devenait "Non repondant" et l'utilisateur a du forcer l'arret via le
    // gestionnaire de taches. Remplace par un etat accumulateur (ImportState)
    // mis a jour en O(1) amorti par favori.
    [Fact]
    public void Import_favoris_utilise_un_etat_accumulateur_au_lieu_de_rescanner_tous_les_noeuds()
    {
        var model = ReadRepoFile("Lumora.WinUI", "Models", "Bookmarks.cs");

        Assert.Contains("private sealed class ImportState", model, StringComparison.Ordinal);
        Assert.Contains("var state = ImportState.From(nodes);", model, StringComparison.Ordinal);
        Assert.Contains("!state.ExistingUrls.Add(item.Url)", model, StringComparison.Ordinal);
        Assert.Contains("state.NextId(\"bookmark\")", model, StringComparison.Ordinal);
        Assert.Contains("state.NextPosition(parentId)", model, StringComparison.Ordinal);
        Assert.DoesNotContain("nodes.Any(node => node.Kind == BookmarkKind.Url && node.Url == item.Url)", model, StringComparison.Ordinal);
    }

    // 2026-08-22 : la recuperation des icones lisait TOUTE la base Favicons du
    // navigateur source (l'historique de navigation complet, pas seulement les
    // favoris a importer), ce qui aggravait encore le gel ci-dessus. Filtree
    // desormais sur les seules URLs (et origines) des favoris importes.
    [Fact]
    public void CopyFaviconsAsync_ne_lit_que_les_icones_des_favoris_a_importer()
    {
        var model = ReadRepoFile("Lumora.WinUI", "Models", "Bookmarks.cs");

        Assert.Contains("CopyFaviconsAsync(string destinationDir, IReadOnlyCollection<string> wantedUrls)", model, StringComparison.Ordinal);
        Assert.Contains("wantedExact.Contains(pageUrl)", model, StringComparison.Ordinal);
        Assert.Contains("wantedOrigins.Contains(PublicSuffixService.OriginOf(pageUrl))", model, StringComparison.Ordinal);

        var caller = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");
        Assert.Contains("var wantedUrls = FlattenImportUrls(source.ReadTree()).ToList();", caller, StringComparison.Ordinal);
        Assert.Contains("CopyFaviconsAsync(_profile.FaviconsDir, wantedUrls)", caller, StringComparison.Ordinal);
    }

    // 2026-08-22 : la fusion (potentiellement des milliers de favoris) tournait
    // en synchrone sur le thread d'interface, et le bouton "Autres favoris" (ou
    // atterrit la majorite d'un import navigateur) reconstruisait TOUT son
    // arbre de menus contextuels a CHAQUE rendu de la barre - y compris a
    // chaque redimensionnement de fenetre, pas seulement a l'import. Les deux
    // sont desormais differes / deportes hors du thread d'interface.
    [Fact]
    public void Import_favoris_ne_bloque_plus_le_thread_d_interface()
    {
        var caller = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");
        var importExport = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksImportExport.cs");
        var flyouts = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksFlyouts.cs");

        Assert.Contains("private bool _bookmarkImportInProgress;", caller, StringComparison.Ordinal);
        Assert.Contains("await Task.Run(() => replaceExisting", caller, StringComparison.Ordinal);
        Assert.Contains("await Task.Run(() => _bookmarks.MergeImport(tree));", importExport, StringComparison.Ordinal);

        // CreateBookmarkFolderFlyout (bouton "Autres favoris" et tout dossier
        // de la barre) ne construit plus son contenu qu'a la premiere
        // ouverture (MenuFlyout.Opening), pas au rendu de la barre.
        Assert.Contains("flyout.Opening += (_, _) =>", flyouts, StringComparison.Ordinal);
    }

    // 2026-08-22 : trouve par relecture (pas de signalement precis) - le
    // panneau du rail lateral (position "gauche"/"droite" dans Studio Lumora,
    // voir UsesSideBookmarksRail) n'etait jamais cable pour le glisser-depose,
    // alors que chaque bouton de favori y declenche quand meme la capture du
    // pointeur (PointerPressed, cable sans condition de layout) : glisser un
    // favori dans ce mode ne le reordonnait jamais, en silence.
    [Fact]
    public void Glisser_depose_des_favoris_est_cable_sur_les_3_panneaux_possibles()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksDragDrop.cs");

        Assert.Contains(
            "new FrameworkElement?[] { BookmarksBarPanel, BookmarksBottomBarPanel, BookmarksSideBarPanel }",
            code,
            StringComparison.Ordinal);
    }

    // 2026-08-22 (suite) : 4 points signales par l'utilisateur avec captures
    // d'ecran (chevauchement boutons systeme, en-tete de dossier redondant,
    // raccourcis de sous-menu superflus, indicateur "deja en favori" peu
    // visible), maquette montree avant codage.
    [Fact]
    public void La_ligne_d_adresse_reserve_aussi_la_zone_des_boutons_systeme()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.WindowChrome.cs");

        // Avant ce correctif, seule BrowserTabs recevait la reserve
        // dynamique (safeRight) - la ligne d'adresse/outils (NavigationToolbarCapsule)
        // n'en avait aucune et son contenu pouvait passer SOUS les boutons
        // systeme des qu'elle etait assez remplie (modules epingles).
        Assert.Contains("NavigationToolbarCapsule.Margin = new Thickness(14, 6, safeRight + 14, 6);", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Ouverture_d_un_dossier_de_favoris_ne_repete_plus_son_nom()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksFlyouts.cs");

        Assert.Contains("private void AddBookmarkFolderCountHeader(IList<MenuFlyoutItemBase> items, string folderId)", code, StringComparison.Ordinal);
        Assert.Contains("AddBookmarkFolderCountHeader(flyout.Items, folder.Id);", code, StringComparison.Ordinal);
        // La fonction ne doit plus utiliser AddLumoraMenuHeader (qui redisait
        // le nom du dossier deja visible sur le bouton qu'on vient de cliquer).
        Assert.DoesNotContain(
            "AddLumoraMenuHeader(\n            flyout.Items,\n            BookmarkReadableTitle(folder),",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Sous_menus_de_dossiers_n_ont_plus_les_raccourcis_ouvrir_renommer_supprimer()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksFlyouts.cs");

        // Toujours disponibles au clic droit (ContextFlyout = CreateBookmarkContextFlyout),
        // mais plus repetes dans CHAQUE sous-dossier de la barre.
        Assert.DoesNotContain("Text = \"Ouvrir le dossier\",\n                    Tag = child", code, StringComparison.Ordinal);
        Assert.Contains("ContextFlyout = CreateBookmarkContextFlyout(child)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Etoile_favori_utilise_un_contour_quand_absent_des_favoris()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");

        // E735 (FavoriteStarFill) uniquement quand bookmarked, E734
        // (FavoriteStar, contour) sinon - avant ce correctif l'etoile restait
        // TOUJOURS pleine (E735), seule la couleur changeait.
        Assert.Contains("BookmarkStarIcon.Glyph = bookmarked ? \"\\uE735\" : \"\\uE734\";", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Action_retrouver_les_icones_manquantes_existe_et_est_cablee()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.BookmarksFaviconRefresh.cs");

        Assert.Contains("Click=\"RefreshMissingIconsButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private async void RefreshMissingIconsButton_Click(object sender, RoutedEventArgs e)", code, StringComparison.Ordinal);
        Assert.Contains("/favicon.ico", code, StringComparison.Ordinal);
        Assert.Contains("_bookmarks.SetIconForOrigin(origin, path);", code, StringComparison.Ordinal);
    }

    // 2026-08-22 (suite) : "»" apparaissait alors qu'il restait de la place
    // visible - l'estimation par nombre de caractères surevaluait la largeur
    // reelle des puces WinUI. Remplacee par une mesure reelle (Button.Measure()),
    // qui compte aussi le separateur "✦" et l'espacement du StackPanel
    // (jamais comptes avant).
    [Fact]
    public void Debordement_favoris_mesure_la_largeur_reelle_au_lieu_de_lestimer()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");

        Assert.DoesNotContain("EstimateBookmarkBarWidth", code, StringComparison.Ordinal);
        Assert.Contains("private double MeasureBookmarkBarButtonWidth(BookmarkNode node, UiDensityMetrics metrics)", code, StringComparison.Ordinal);
        Assert.Contains("probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));", code, StringComparison.Ordinal);
        Assert.Contains("private static double MeasureConstellationConnectorWidth()", code, StringComparison.Ordinal);
    }

    // 2026-08-22 (suite) : "la barre d'adresse doit rester visible en toute
    // circonstance" (demande explicite utilisateur, capture d'ecran a
    // l'appui) - rien ne l'empechait avant d'etre ecrasee a presque rien par
    // les modules epingles.
    [Fact]
    public void Barre_adresse_a_une_largeur_minimale_garantie()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("x:Name=\"AddressBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"220\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private void CollapseOverflowingToolbarModules(double toolbarWidth)", code, StringComparison.Ordinal);
        Assert.Contains("var budget = Math.Max(0, toolbarWidth - fixedWidth - AddressBox.MinWidth);", code, StringComparison.Ordinal);
        // Les 6 boutons "toujours dans la barre" ne font jamais partie de la
        // table des modules pingables - jamais masques par ce mecanisme.
        Assert.DoesNotContain("[AddBookmarkButton]", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Barre_adresse_passe_a_16px_par_defaut()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.SettingsTheme.cs");

        Assert.Contains("AddressBox.FontSize = largeText ? 18 : 16;", code, StringComparison.Ordinal);
    }

    // 2026-08-22 (suite) : "option A" choisie sur maquette - garder le motif
    // "✦" mais reellement visible et espace (6px/opacite 0.3 -> 16px/opacite
    // 0.85), meme traitement sur les modules epingles (5px -> 12px).
    [Fact]
    public void Espacement_barre_favoris_et_connecteur_sont_reellement_visibles()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");

        foreach (var panelName in new[] { "BookmarksBarPanel", "BookmarksBottomBarPanel" })
        {
            var nameIndex = xaml.IndexOf($"x:Name=\"{panelName}\"", StringComparison.Ordinal);
            Assert.True(nameIndex >= 0, $"{panelName} introuvable");
            var window = xaml.Substring(nameIndex, 220);
            Assert.Contains("Spacing=\"16\"", window, StringComparison.Ordinal);
        }

        Assert.Contains("FontSize = 9,", code, StringComparison.Ordinal);
        Assert.Contains("Opacity = 0.85,", code, StringComparison.Ordinal);
        Assert.Contains("const double panelSpacing = 16d;", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Espacement_barre_modules_est_augmente_et_compte_dans_le_repli()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("x:Name=\"ToolbarButtonsPanel\" Orientation=\"Horizontal\" Spacing=\"12\"", xaml, StringComparison.Ordinal);
        // L'espacement entre modules visibles doit compter dans le budget de
        // CollapseOverflowingToolbarModules, sinon la barre d'adresse peut a
        // nouveau passer sous son plancher malgre le mecanisme de repli.
        Assert.Contains("var withSpacing = used + (visibleCount > 0 ? ToolbarButtonsPanel.Spacing : 0) + w;", code, StringComparison.Ordinal);
    }

    // 2026-08-23 (session "petites corrections") : une quinzaine de favoris
    // icone-seule sans favicon retombaient tous sur la MEME icone generique
    // (maillon), illisibles cote a cote meme avec un espacement correct - pas
    // le meme bug que l'entree precedente (espacement). Repli : pastille
    // 1ere-lettre, couleur derivee du nom, dossiers inchanges. La construction
    // reelle des controles WinUI (Border/TextBlock) ne peut pas s'executer
    // hors de l'app (pas de DispatcherQueue dans l'hote xunit, meme
    // contrainte que le reste de ce fichier) - verifiee en reel sur l'app via
    // le skill verify a la place ; ces tests couvrent la logique source.
    [Fact]
    public void Un_lien_sans_favicon_utilisable_retombe_sur_une_pastille_lettre_pas_licone_generique()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");

        // La branche generique "Symbol.Link pour tout lien sans favicon" ne
        // doit plus exister : seul un dossier garde encore une icone fixe.
        Assert.DoesNotContain("Symbol.Folder : Symbol.Link", code, StringComparison.Ordinal);
        Assert.Contains("return BookmarkLetterAvatar(node, size);", code, StringComparison.Ordinal);
        Assert.Contains("return new SymbolIcon { Symbol = Symbol.Folder, Width = size, Height = size };", code, StringComparison.Ordinal);
    }

    [Fact]
    public void La_pastille_ne_plante_jamais_meme_sans_titre_ni_url_exploitable()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");

        // Favori "icone seule" : node.Title reste vide en base (IsIconOnlyTitle
        // masque juste l'affichage) - repli sur l'hote de l'URL, puis "?" en
        // tout dernier recours, jamais une chaine vide qui ferait planter
        // char.ToUpperInvariant(source[0]) dans BookmarkLetterAvatar.
        Assert.Contains("return \"?\";", code, StringComparison.Ordinal);
        var sourceMethod = code[code.IndexOf("private static string BookmarkAvatarSource", StringComparison.Ordinal)..];
        Assert.Contains("uri.Host", sourceMethod[..sourceMethod.IndexOf("private static int BookmarkAvatarHue", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact]
    public void La_teinte_de_la_pastille_est_deterministe_et_derivee_du_nom()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.Bookmarks.cs");

        // Meme favori => meme couleur a chaque rendu (hachage pur du nom, pas
        // Random/Guid) - sinon la pastille "clignoterait" de couleur a chaque
        // reouverture de la barre.
        Assert.Contains("hash = unchecked(hash * 31 + c);", code, StringComparison.Ordinal);
        Assert.Contains("return (int)(hash % 360);", code, StringComparison.Ordinal);
        Assert.DoesNotContain("new Random(", code, StringComparison.Ordinal);
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
