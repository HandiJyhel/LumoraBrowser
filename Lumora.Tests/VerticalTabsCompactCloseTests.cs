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
// Troisieme version, actuelle : croix large et centree (30x30 dans une
// tuile 44x38), meme reglage d'opacite 0.6/1 deja prouve suffisant pour la
// decouvrabilite, juste une cible bien plus grande - validee via maquette
// HTML avant implementation.
public sealed class VerticalTabsCompactCloseTests
{
    [Fact]
    public void Rail_compact_expose_une_croix_de_fermeture_toujours_visible()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.TabGroups.cs");
        var compactBranch = ExtractCompactBranch(code);

        Assert.Contains("var compactClose = new Button", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Child = new SymbolIcon(Symbol.Cancel)", compactBranch, StringComparison.Ordinal);
        Assert.Contains("compactClose.Click += VerticalTabCloseButton_Click;", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Tag = tab.Id,", compactBranch, StringComparison.Ordinal);

        // Toujours visible (pas de Visibility.Collapsed sur le bouton lui-meme) :
        // seule l'opacite bouge au survol, jamais la visibilite.
        Assert.DoesNotContain("compactClose.Visibility", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Opacity = 0.6", compactBranch, StringComparison.Ordinal);
        Assert.Contains("compactClose.Opacity = 1; icon.Opacity = 0.3;", compactBranch, StringComparison.Ordinal);
        Assert.Contains("compactClose.Opacity = 0.6; icon.Opacity = 1;", compactBranch, StringComparison.Ordinal);

        // Cible agrandie (2026-08-08) : 24x24 au lieu des 20x20 d'origine
        // ("trop serree"), redescendue depuis un premier essai a 30x30
        // ("trop imposant" en usage reel le meme jour).
        Assert.Contains("Width = 24,", compactBranch, StringComparison.Ordinal);
        Assert.Contains("Height = 24,", compactBranch, StringComparison.Ordinal);
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
    }

    private static string ExtractCompactBranch(string source)
    {
        var marker = "if (_verticalTabsCompact || tab.Pinned)";
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
