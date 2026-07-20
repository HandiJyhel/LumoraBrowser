using Xunit;

namespace Lumora.Tests;

// Verrouille le remplacement de MainWindow par Incognito (2026-07-20, demande
// explicite utilisateur, avec clarification : la fenetre normale doit se
// rouvrir automatiquement a la fermeture d'Incognito plutot que de laisser
// l'utilisateur sans aucune fenetre Lumora). Verifie uniquement le cablage
// source (memes contraintes que les autres tests de regression du projet :
// pas de vrai process demarre depuis les tests). La verification live du
// cycle complet (fermer Incognito -> nouveau process MainWindow) a ete faite
// manuellement via UIA, voir MEMORY.md 0.83.49-dev.
public sealed class IncognitoReturnToMainTests
{
    [Fact]
    public void MainWindow_se_ferme_et_demande_le_retour_au_navigateur_de_base()
    {
        var source = ReadRepoFile("Lumora.WinUI", "MainWindow.Incognito.cs");

        Assert.Contains("IncognitoProcessLauncher.Launch(startUrl, returnToMain: true);", source, StringComparison.Ordinal);
        Assert.Contains("Close();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Fermer_incognito_relance_mainwindow_seulement_si_ce_nest_pas_une_bascule_interne()
    {
        var source = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");

        Assert.Contains("if (_returnToMain && !_relaunchingIncognito)", source, StringComparison.Ordinal);
        Assert.Contains("IncognitoProcessLauncher.LaunchMainWindow();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bascule_tor_et_installation_marquent_bien_relaunchingIncognito_avant_de_fermer()
    {
        var source = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");

        // Les deux points ou Incognito se relance elle-meme (bascule Tor,
        // installation du moteur) doivent poser le flag AVANT Close(), sinon
        // le Closed relancerait MainWindow en plus de la nouvelle fenetre
        // Incognito (double fenetre).
        var occurrences = CountOccurrences(source, "_relaunchingIncognito = true;");
        Assert.Equal(2, occurrences);
        Assert.Contains("IncognitoProcessLauncher.Launch(torEnabled: wantsTor, returnToMain: _returnToMain);", source, StringComparison.Ordinal);
        Assert.Contains("IncognitoProcessLauncher.Launch(torEnabled: true, returnToMain: _returnToMain);", source, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
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
