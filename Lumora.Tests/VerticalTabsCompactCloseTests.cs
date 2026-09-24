using Xunit;

namespace Lumora.Tests;

// Bug reel signale par l'utilisateur avec capture d'ecran (2026-08-07) : en
// rail vertical compact (icone seule, _verticalTabsCompact ou tab.Pinned),
// aucun bouton de fermeture visible - seule la ligne "etendue" en avait un.
// Fermer restait techniquement possible (clic droit > "Fermer l'onglet",
// Ctrl+W) mais sans affordance visible, ce qui revenait a une impossibilite
// pour la plupart des utilisateurs.
//
// Premiere version (croix reveleee au survol) jugee insuffisante par
// l'utilisateur en conditions reelles - capture d'ecran montrant le rail
// sans aucun indice visible au repos, donc rien a decouvrir sans deja savoir
// qu'il faut survoler. Deuxieme version (pastille 16x16 dans le coin de
// l'icone) jugee trop serree (2026-08-08, Google Chrome cite en reference
// pour la TAILLE de cible - pas pour son "hover uniquement", deja rejete).
// Troisieme version : croix large et centree (24x24 dans une tuile 44x38),
// meme reglage d'opacite 0.6/1 deja prouve suffisant pour la decouvrabilite,
// juste une cible bien plus grande - validee via maquette HTML avant
// implementation.
//
// Quatrieme version, actuelle (round 3, 2026-08-12) : tuile et croix
// reduites une 2e fois (44x38 -> 30x28, croix 24x24 -> 18x18) suite a un
// retour utilisateur avec capture d'ecran d'un rail Edge reel a l'appui -
// avec ~15 onglets/session, meme la tuile 44x38 restait trop large. Taille
// reduite PROPORTIONNELLEMENT a la tuile (~30%), pas re-arbitree a part :
// le debat "24 vs 30 vs 20" ci-dessus reste tranche, seule l'echelle change.
//
// Cinquieme version (2026-09-24) : bug reel signale par l'utilisateur - la
// croix centree couvrait presque toute la tuile, vouloir changer d'onglet
// fermait le site. Croix visible uniquement sur l'onglet ACTIF (regle de
// Chrome quand ses onglets deviennent trop etroits), la ou cliquer ne change
// rien d'autre ; clic molette ou clic droit pour les inactifs, dont
// l'infobulle le dit. Garde anti double-clic : un clic sur la croix juste
// apres l'activation de la tuile est ignore.
//
// Sixieme version, actuelle (2026-09-25) : bug reel signale par
// l'utilisateur - la regle ci-dessus suppose que cliquer l'onglet actif ne
// fait jamais rien, faux depuis un panneau interne (Reglages, Coffre,
// Historique...) ou cliquer cette tuile ramene au site
// (VerticalTabButton_Click -> ReturnToBrowserIfHidden). La croix y couvrait
// la cible : le clic fermait l'onglet au lieu d'y revenir, et comme
// CloseTab n'affiche pas forcement ce panneau navigateur, ca passait pour
// une appli qui ne repond plus. Croix desormais conditionnee a
// BrowserPanel.Visibility ; repli sur un repere visuel "retour"
// (Symbol.Back, IsHitTestVisible=false) visible au repos - la tuile entiere
// reste le bouton, pas de nouvelle hitbox etroite.
public sealed class VerticalTabsCompactCloseTests
{
    [Fact]
    public void Rail_compact_expose_une_croix_de_fermeture_sur_l_onglet_actif()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.TabGroups.cs");
        var compactBranch = ExtractCompactBranch(code);

        Assert.Contains("var compactClose = new Button", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Child = new SymbolIcon(Symbol.Cancel)", compactBranch, StringComparison.Ordinal);
        Assert.Contains("compactClose.Click += VerticalCompactTabCloseButton_Click;", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Tag = tab.Id,", compactBranch, StringComparison.Ordinal);

        // Sur l'onglet actif, visible au repos (pas de Visibility.Collapsed,
        // decouvrabilite exigee le 2026-08-07) : seule l'opacite bouge au survol.
        // Sur les autres, absente : la tuile entiere selectionne l'onglet.
        // Depuis le 2026-09-25, conditionnee aussi a la visibilite du panneau
        // navigateur (sinon cliquer l'onglet actif = revenir au site, pas fermer).
        Assert.Contains("var browserPanelVisible = BrowserPanel.Visibility == Visibility.Visible;", compactBranch, StringComparison.Ordinal);
        Assert.Contains("var showCompactClose = current?.Id == tab.Id && browserPanelVisible;", compactBranch, StringComparison.Ordinal);
        Assert.Contains("if (showCompactClose)", compactBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("compactClose.Visibility", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Opacity = 0.6", compactBranch, StringComparison.Ordinal);
        Assert.Contains("compactClose.Opacity = 1; icon.Opacity = 0.3;", compactBranch, StringComparison.Ordinal);
        Assert.Contains("compactClose.Opacity = 0.6; icon.Opacity = iconRestOpacity;", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Clic molette ou clic droit pour fermer", compactBranch, StringComparison.Ordinal);

        // Cible reduite proportionnellement (round 3, 2026-08-12) : 18x18 par
        // defaut, suite au resserrement de la tuile elle-meme (44x38 -> 30x28)
        // - voir le commentaire d'en-tete pour l'historique complet des
        // tailles. Depuis le 2026-09-12, cette taille de repli (18) devient
        // elle-meme le "sinon" d'un ternaire sur CompactModeEnabled (Interface
        // compacte descend a CompactVerticalTabCompactCloseSize) - le repli
        // reste inchange.
        Assert.Contains("_compactModeEnabled ? CompactVerticalTabCompactCloseSize : 18", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Width = compactCloseSize,", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Height = compactCloseSize,", compactBranch, StringComparison.Ordinal);
    }

    // Bug reel signale par l'utilisateur (2026-09-25) : depuis un panneau
    // interne, l'onglet actif du rail compact est la tuile qui ramene au
    // site - la croix ne doit plus y capturer le clic. A la place, un repere
    // purement visuel (non cliquable en soi) garde la decouvrabilite au repos.
    [Fact]
    public void Rail_compact_remplace_la_croix_par_un_repere_retour_hors_navigateur()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.TabGroups.cs");
        var compactBranch = ExtractCompactBranch(code);

        Assert.Contains("var showReturnHint = current?.Id == tab.Id && !browserPanelVisible;", compactBranch, StringComparison.Ordinal);
        Assert.Contains("if (showReturnHint)", compactBranch, StringComparison.Ordinal);

        // Purement decoratif : IsHitTestVisible=false garantit que le clic
        // traverse jusqu'au bouton de la tuile (ActivateTab ->
        // ReturnToBrowserIfHidden), jamais une nouvelle hitbox etroite comme
        // celle qui a cause le bug de la croix.
        Assert.Contains("IsHitTestVisible = false,", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Child = new SymbolIcon(Symbol.Back)", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Foreground = (Brush)RootShell.Resources[\"NovaAccentBrush\"]", compactBranch, StringComparison.Ordinal);

        Assert.Contains("ToolTipService.SetToolTip(button, $\"{tab.Title}\\nRevenir au site\");", compactBranch, StringComparison.Ordinal);
    }

    // Verrouille que le bouton reutilise bien le meme gestionnaire que la
    // croix de la ligne etendue (pas de logique de fermeture dupliquee).
    [Fact]
    public void Croix_compacte_reutilise_le_meme_gestionnaire_que_la_ligne_etendue()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.TabGroups.cs");

        Assert.Contains("private void VerticalTabCloseButton_Click(object sender, RoutedEventArgs e)", code, StringComparison.Ordinal);
        var handler = ExtractMethod(code, "private void VerticalTabCloseButton_Click(object sender, RoutedEventArgs e)");
        Assert.Contains("sender is Button { Tag: int id }", handler, StringComparison.Ordinal);
        Assert.Contains("CloseTab(tab);", handler, StringComparison.Ordinal);

        var guarded = ExtractMethod(code, "private void VerticalCompactTabCloseButton_Click(object sender, RoutedEventArgs e)");
        Assert.Contains("VerticalCompactCloseGuard", guarded, StringComparison.Ordinal);
        Assert.Contains("VerticalTabCloseButton_Click(sender, e);", guarded, StringComparison.Ordinal);
    }

    [Fact]
    public void Clic_molette_ferme_un_onglet_du_rail()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.TabGroups.cs");
        Assert.Contains("button.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(VerticalTabButton_PointerPressed), true);", code, StringComparison.Ordinal);
        var handler = ExtractMethod(code, "private void VerticalTabButton_PointerPressed(object sender, PointerRoutedEventArgs e)");
        Assert.Contains("IsMiddleButtonPressed", handler, StringComparison.Ordinal);
        Assert.Contains("CloseTab(tab);", handler, StringComparison.Ordinal);
    }

    private static string ExtractCompactBranch(string source)
    {
        // Extrait via booleen nomme depuis le 2026-09-10 (session "interface",
        // reutilise aussi par l'indicateur d'onglet actif) - meme condition,
        // juste factorisee: "var isCompactTile = _verticalTabsCompact || tab.Pinned;".
        var marker = "if (isCompactTile)";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Signature introuvable : {marker}");

        var braceOpen = source.IndexOf('{', start);
        var depth = 0;
        var i = braceOpen;
        for (; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) break;
            }
        }

        return source[start..(i + 1)];
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Signature introuvable : {signature}");

        var braceOpen = source.IndexOf('{', start);
        var depth = 0;
        var i = braceOpen;
        for (; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) break;
            }
        }

        return source[start..(i + 1)];
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
