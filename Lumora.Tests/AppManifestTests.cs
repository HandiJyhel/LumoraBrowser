using Xunit;

namespace Lumora.Tests;

// Retour utilisateur (2026-08-08) : boutons/icones "pixellises". Cause reelle
// trouvee - app.manifest n'avait jamais declare dpiAwareness : sans elle,
// Windows peut traiter l'app en DPI non conscient et etirer toute la fenetre
// en bitmap pour matcher l'echelle d'affichage reelle (125%, 150%...), ce qui
// floute tout (boutons, icones, bordures), pas seulement tel ou tel controle.
// Reglage au niveau du PROCESSUS, pas du controle - couvre donc aussi
// LumoraIncognitoWindow (process separe mais meme executable/manifeste).
public sealed class AppManifestTests
{
    [Fact]
    public void App_manifest_declare_la_conscience_dpi_par_moniteur()
    {
        var manifest = ReadRepoFile("Lumora.WinUI", "app.manifest");

        Assert.Contains(
            "<dpiAwareness xmlns=\"http://schemas.microsoft.com/SMI/2016/WindowsSettings\">PerMonitorV2</dpiAwareness>",
            manifest, StringComparison.Ordinal);
        // Fallback legacy pour les OS qui ne comprennent pas la cle 2016.
        Assert.Contains(
            "<dpiAware xmlns=\"http://schemas.microsoft.com/SMI/2005/WindowsSettings\">true/pm</dpiAware>",
            manifest, StringComparison.Ordinal);
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
