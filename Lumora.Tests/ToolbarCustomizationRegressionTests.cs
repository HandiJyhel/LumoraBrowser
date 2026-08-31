using Xunit;

namespace Lumora.Tests;

// Historique du glisser-deposer de la barre d'outils (CanDrag -> suivi
// manuel du pointeur -> AddHandler handledEventsToo -> capture sur le
// panneau -> reordonnancement differe au relachement) : 4 correctifs reels
// successifs en une seule session, chacun confirme par documentation ou
// trace de diagnostic, sans jamais aboutir a un resultat fiable en
// conditions reelles ("je suis toujours bloque au 3e bloc"). Voir MEMORY.md
// pour le detail complet de chaque tentative.
//
// 2026-08-14 (retrait complet) : demande explicite de l'utilisateur - le
// clic droit "ne sert a rien puisque le glisser ne fonctionne pas". Tout le
// mecanisme (mode edition, capture de pointeur, suivi de seuil, glisser en
// direct/differe) est retire. Remplace par un deplacement au clic (Monter/
// Descendre) integre au menu Modules existant (epingler/desepingler) -
// meme principe deja livre pour les favoris (BookmarkStore.MoveNodeAdjacent),
// aucune capture de pointeur en jeu, donc aucun risque de la perdre.
public sealed class ToolbarCustomizationRegressionTests
{
    [Fact]
    public void Aucun_mecanisme_de_glisser_ne_subsiste()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var toolbarCode = ReadRepoFile("Lumora.WinUI", "MainWindow.ToolbarCustomization.cs");
        var serviceCode = ReadRepoFile("Lumora.WinUI", "ToolbarCustomizationService.cs");
        var xamlCsCode = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        // Le texte du menu peut subsister dans un commentaire explicatif
        // (historique du retrait) - seul le VRAI declencheur (Click=) compte.
        Assert.DoesNotContain("Click=\"ToolbarReorganizeMenuItem_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolbarReorganizeMenuItem_Click", toolbarCode, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"ToolbarEditModeOverlay\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDrag=\"True\"", xaml, StringComparison.Ordinal);

        Assert.DoesNotContain("CapturePointer(", toolbarCode, StringComparison.Ordinal);
        Assert.DoesNotContain("PointerPressed", toolbarCode, StringComparison.Ordinal);
        Assert.DoesNotContain("PointerMoved", toolbarCode, StringComparison.Ordinal);
        Assert.DoesNotContain("IsInEditMode", serviceCode, StringComparison.Ordinal);
        Assert.DoesNotContain("OnDropped(", serviceCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolbarButton_PointerPressed", xamlCsCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ToolbarButtonsPanel.PointerMoved", xamlCsCode, StringComparison.Ordinal);
    }

    [Fact]
    public void MoveButtonAdjacent_utilise_la_logique_pure_partagee_avec_les_favoris()
    {
        var code = ReadRepoFile("Lumora.WinUI", "ToolbarCustomizationService.cs");

        Assert.Contains("public bool MoveButtonAdjacent(string buttonId, bool moveForward)", code, StringComparison.Ordinal);
        Assert.Contains("AdjacentMoveMath.ComputeTarget(visibleOrder, buttonId, moveForward)", code, StringComparison.Ordinal);
        Assert.Contains("SaveOrder();", code, StringComparison.Ordinal);
    }

    // 2026-08-14 : deja corrige une fois sur l'ancien mecanisme de glisser
    // ("je ne peux que deplacer le dernier bloc" - les modules non epingles,
    // Visibility=Collapsed, polluaient le calcul de cible). Le nouveau
    // mecanisme au clic doit repartir de la MEME contrainte des le depart :
    // seuls les boutons VISIBLES (epingles) sont des voisins valides.
    [Fact]
    public void GetVisibleButtonOrder_exclut_les_boutons_masques()
    {
        var code = ReadRepoFile("Lumora.WinUI", "ToolbarCustomizationService.cs");

        Assert.Contains("public IReadOnlyList<string> GetVisibleButtonOrder()", code, StringComparison.Ordinal);
        Assert.Contains("_buttonOrder.Where(IsButtonVisible).ToList();", code, StringComparison.Ordinal);
        Assert.Contains("element.Visibility == Visibility.Visible", code, StringComparison.Ordinal);
    }

    // 2026-08-14 (2e passe, meme jour) : "je ne peux que deplacer le dernier
    // bloc... tu fais quoi du bouton des favoris du coffre et de tout le
    // reste ? Ca compte." La 1re version ne couvrait que les 16 boutons de
    // ModulesQuickBar - favoris/incognito/bouclier/coffre/historique/
    // telechargements etaient a des Grid.Column fixes, hors de portee.
    // Deplaces dans ToolbarButtonsPanel pour devenir reordonnables comme les
    // modules.
    [Fact]
    public void Favoris_incognito_bouclier_coffre_historique_telechargements_sont_reordonnables()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var serviceCode = ReadRepoFile("Lumora.WinUI", "ToolbarCustomizationService.cs");
        var xamlCsCode = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        var toolbarPanelIndex = xaml.IndexOf("x:Name=\"ToolbarButtonsPanel\"", StringComparison.Ordinal);
        Assert.True(toolbarPanelIndex >= 0, "ToolbarButtonsPanel introuvable.");

        foreach (var buttonId in new[]
                 {
                     "AddBookmarkButton", "IncognitoToolbarButton", "ShieldButton",
                     "VaultQuickAccessButton", "HistoryToolbarButton", "DownloadsIndicatorButton"
                 })
        {
            // Declares APRES ToolbarButtonsPanel (donc a l'interieur, plus a
            // leur ancien Grid.Column fixe qui les plaçait avant) ; figurent
            // dans l'ordre par defaut du service et dans la map de boutons
            // du code-behind, comme les modules deja reordonnables.
            var buttonIndex = xaml.IndexOf($"x:Name=\"{buttonId}\"", StringComparison.Ordinal);
            Assert.True(buttonIndex > toolbarPanelIndex, $"{buttonId} n'est pas déclaré à l'intérieur de ToolbarButtonsPanel.");
            Assert.Contains($"\"{buttonId}\",", serviceCode, StringComparison.Ordinal);
            Assert.Contains($"[\"{buttonId}\"] = {buttonId},", xamlCsCode, StringComparison.Ordinal);
        }
    }

    // 2026-08-31 : piege reel trouve en verifiant en direct - un bouton
    // declare dans ToolbarButtonsPanel (XAML) mais absent de
    // GetDefaultButtonOrder()/toolbarButtonMap disparaissait silencieusement
    // au demarrage (ApplyOrderToToolbar vide puis reconstruit le panneau
    // UNIQUEMENT depuis ces deux listes). LockNowButton (verrouillage manuel,
    // MainWindow.TabSuspension.cs) sert de cas concret pour ne pas regresser.
    [Fact]
    public void LockNowButton_est_enregistre_dans_le_service_de_reorganisation()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");
        var serviceCode = ReadRepoFile("Lumora.WinUI", "ToolbarCustomizationService.cs");
        var xamlCsCode = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        var toolbarPanelIndex = xaml.IndexOf("x:Name=\"ToolbarButtonsPanel\"", StringComparison.Ordinal);
        var buttonIndex = xaml.IndexOf("x:Name=\"LockNowButton\"", StringComparison.Ordinal);
        Assert.True(buttonIndex > toolbarPanelIndex, "LockNowButton n'est pas déclaré à l'intérieur de ToolbarButtonsPanel.");
        Assert.Contains("\"LockNowButton\",", serviceCode, StringComparison.Ordinal);
        Assert.Contains("[\"LockNowButton\"] = LockNowButton,", xamlCsCode, StringComparison.Ordinal);
    }

    // 2026-08-14 (2e passe) : la petite liste integree au menu Modules ne
    // tenait plus une fois etendue a toute la barre - remplacee par une
    // fenetre dediee (overlay modal), ouverte via un lien au fond du meme
    // menu. 2 sections : "Toujours dans la barre" (tout ce qui est visible
    // sans epinglage) et "Modules epingles" (le sous-ensemble optionnel) -
    // un module desepingle disparait des 2 (demande explicite deja actee).
    [Fact]
    public void Fenetre_de_reorganisation_groupe_toujours_visible_et_modules_epingles()
    {
        var toolbarCode = ReadRepoFile("Lumora.WinUI", "MainWindow.ToolbarCustomization.cs");
        var usageModeCode = ReadRepoFile("Lumora.WinUI", "MainWindow.UsageMode.cs");
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("private void RefreshToolbarReorganizeSections()", toolbarCode, StringComparison.Ordinal);
        Assert.Contains("_toolbarCustomization.GetVisibleButtonOrder();", toolbarCode, StringComparison.Ordinal);
        Assert.Contains("!OptionalPinnedButtonIds.Contains(id)", toolbarCode, StringComparison.Ordinal);
        Assert.Contains("OptionalPinnedButtonIds.Contains(id)", toolbarCode, StringComparison.Ordinal);
        Assert.Contains("RefreshToolbarReorganizeSections();", usageModeCode, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"ToolbarReorganizeOverlay\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ToolbarReorganizeAlwaysSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ToolbarReorganizeModulesSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Réorganiser toute la barre…\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"ToolbarReorganizeMenuLink_Click\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Monter_descendre_sont_actionnables_au_clic_et_desactives_aux_extremites()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.ToolbarCustomization.cs");

        Assert.Contains("private void ToolbarReorganizeUpButton_Click(object sender, RoutedEventArgs e)", code, StringComparison.Ordinal);
        Assert.Contains("private void ToolbarReorganizeDownButton_Click(object sender, RoutedEventArgs e)", code, StringComparison.Ordinal);
        Assert.Contains("MoveButtonAdjacent(buttonId, moveForward: false)", code, StringComparison.Ordinal);
        Assert.Contains("MoveButtonAdjacent(buttonId, moveForward: true)", code, StringComparison.Ordinal);
        Assert.Contains("IsEnabled = !isFirst", code, StringComparison.Ordinal);
        Assert.Contains("IsEnabled = !isLast", code, StringComparison.Ordinal);
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
