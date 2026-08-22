using Xunit;

namespace Lumora.Tests;

// Verifie par lecture de source plutot qu'en instanciant MainWindow :
// DisplayAddressForBar/SimplifyAddressForDisplay/AddressBox_GotFocus vivent
// dans MainWindow.xaml.cs/MainWindow.AddressSuggestions.cs, qui dependent de
// Microsoft.UI.Xaml, absents du projet de test "pur" - meme contrainte que
// tous les tests de BookmarkBarRegressionTests.cs (voir son en-tete).
//
// 2026-08-22 : demande explicite utilisateur, capture d'ecran a l'appui -
// une URL de tracking de 200+ caracteres (parametres publicitaires Amazon)
// restait illisible a n'importe quelle taille de police raisonnable, faute
// de hierarchie visuelle entre le domaine et le bruit des parametres. Deja
// verifie en direct sur amazon.fr : la taille de police seule (14->16px, cf.
// 4-points-favoris-visuel) ne suffisait pas.
public sealed class AddressBarSimplificationTests
{
    [Fact]
    public void Barre_adresse_au_repos_est_simplifiee_domaine_et_chemin()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml.cs");

        Assert.Contains("private static string SimplifyAddressForDisplay(string address)", code, StringComparison.Ordinal);
        Assert.Contains("return SimplifyAddressForDisplay(address);", code, StringComparison.Ordinal);
        // Domaine sans www., avec port si non standard, chemin joint par "›",
        // jamais la requete (?...) ni le schema (https://).
        Assert.Contains("host + \" › \" + string.Join(\" › \", segments);", code, StringComparison.Ordinal);
        Assert.Contains("host.StartsWith(\"www.\", StringComparison.OrdinalIgnoreCase)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Barre_adresse_bascule_sur_lurl_complete_au_focus()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.AddressSuggestions.cs");
        var xaml = ReadRepoFile("Lumora.WinUI", "MainWindow.xaml");

        Assert.Contains("GotFocus=\"AddressBox_GotFocus\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private void AddressBox_GotFocus(object sender, RoutedEventArgs e)", code, StringComparison.Ordinal);
        Assert.Contains("var rawAddress = CurrentTab()?.Address;", code, StringComparison.Ordinal);
        Assert.Contains("AddressBox.SelectAll();", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Barre_adresse_revient_au_repos_si_on_clique_ailleurs_sans_valider()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.AddressSuggestions.cs");

        Assert.Contains("private void RevertAddressBarToSimplifiedDisplay()", code, StringComparison.Ordinal);
        Assert.Contains("RevertAddressBarToSimplifiedDisplay();", code, StringComparison.Ordinal);
        // Jamais si un clic sur une suggestion est en cours (le focus part
        // avant que le clic ait pu s'executer).
        Assert.Contains("if (_addressSuggestionsPointerInside) return;", code, StringComparison.Ordinal);
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
