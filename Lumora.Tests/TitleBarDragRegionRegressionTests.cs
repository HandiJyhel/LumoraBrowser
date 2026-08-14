using Xunit;

namespace Lumora.Tests;

// 2026-08-14 : bug reel trouve via trace de diagnostic (winui-runtime-trace.log
// fourni par l'utilisateur, voir MEMORY.md) - en layout aplati (theme
// Classique + onglets verticaux), la zone de "drag" de la fenetre
// (UpdateTitleBarDragRegion, MainWindow.WindowChrome.cs) tombait DANS
// ModulesQuickBar (barre d'outils). Windows captait alors le pointeur pour
// deplacer la fenetre des qu'un glissement de bouton depassait quelques
// pixels, rendant tout glisser/reorganisation de la barre d'outils
// impossible - independant du bug de capture deja corrige (panneau vs
// bouton), un tout autre mecanisme (non-client Win32, pas WinUI).
public sealed class TitleBarDragRegionRegressionTests
{
    [Fact]
    public void Zone_de_drag_de_la_fenetre_exclut_ModulesQuickBar()
    {
        var code = ReadRepoFile("Lumora.WinUI", "MainWindow.WindowChrome.cs");

        Assert.Contains("ModulesQuickBar.TransformToVisual(RootShell)", code, StringComparison.Ordinal);
        Assert.Contains("if (modulesBounds.Top < rowHeight)", code, StringComparison.Ordinal);
        Assert.Contains("dragLeft = Math.Max(dragLeft, modulesBounds.Right);", code, StringComparison.Ordinal);
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
