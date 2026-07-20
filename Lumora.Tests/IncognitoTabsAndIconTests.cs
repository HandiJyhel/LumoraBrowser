using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Verrouille l'ajout des onglets dans la fenetre Incognito (2026-07-20, demande
// explicite utilisateur) et de l'icone de fenetre/barre des taches distincte.
// Avant cette version, Incognito etait volontairement mono-onglet (chaque
// target=_blank ouvrait un nouveau process) - voir le commentaire de classe de
// LumoraIncognitoWindow pour le detail de ce qui a change et pourquoi c'est
// sans risque (meme process, meme etat Tor/session deja garanti).
public sealed class IncognitoTabsAndIconTests
{
    [Fact]
    public void Fenetre_incognito_expose_une_barre_d_onglets_native()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml");

        Assert.Contains("<TabView x:Name=\"IncognitoTabs\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AddTabButtonClick=\"IncognitoTabs_AddTabButtonClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"IncognitoTabs_SelectionChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TabCloseRequested=\"IncognitoTabs_TabCloseRequested\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Fermer_le_dernier_onglet_ferme_toute_la_fenetre()
    {
        var source = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");

        Assert.Contains("IncognitoTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)", source, StringComparison.Ordinal);
        Assert.Contains("if (IncognitoTabs.TabItems.Count == 0)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ctrl_T_ouvre_un_nouvel_onglet_incognito()
    {
        var source = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");

        Assert.Contains("var newTabAccelerator = new KeyboardAccelerator", source, StringComparison.Ordinal);
        Assert.Contains("Key = VirtualKey.T,", source, StringComparison.Ordinal);
        Assert.Contains("newTabAccelerator.Invoked +=", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Lien_cible_ouvre_un_onglet_dans_la_meme_fenetre_pas_un_nouveau_process()
    {
        var source = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");

        Assert.Contains("_ = CreateTabAsync(args.Uri, select: true);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IncognitoProcessLauncher.Launch(args.Uri, IncognitoTorSwitch.IsOn);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Fenetre_incognito_applique_une_icone_dediee()
    {
        var source = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");
        var csproj = ReadRepoFile("Lumora.WinUI", "Lumora.WinUI.csproj");

        Assert.Contains("\"LumoraIncognito.ico\"", source, StringComparison.Ordinal);
        Assert.Contains("_appWindow.SetIcon(iconPath);", source, StringComparison.Ordinal);
        Assert.Contains("<Content Include=\"Assets\\LumoraIncognito.ico\" CopyToOutputDirectory=\"PreserveNewest\" />", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void Icone_incognito_est_un_ico_valide_encapsulant_un_png()
    {
        var path = FindRepoFile("Lumora.WinUI", "Assets", "LumoraIncognito.ico");

        Assert.True(FaviconQuality.IsUsablePngBackedIcoFile(path));
    }

    private static string ReadRepoFile(params string[] segments) => File.ReadAllText(FindRepoFile(segments));

    private static string FindRepoFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Fichier projet introuvable: {string.Join(Path.DirectorySeparatorChar, segments)}");
    }
}
