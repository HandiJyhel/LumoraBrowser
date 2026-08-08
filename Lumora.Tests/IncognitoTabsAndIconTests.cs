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

    // Demande utilisateur du 2026-08-07 ("fais exactement ce que t'as fait
    // avec le mode normal" pour les onglets verticaux) - meme principe que
    // VerticalTabsSwitch/VerticalTabsRail de MainWindow : IncognitoTabs
    // (TabView) reste la source de verite, seule sa bande est masquee au
    // profit d'un rail rendu a la main. Version simplifiee (pas de reglage
    // persiste, pas de groupes/epingles - IncognitoTab n'en a pas).
    [Fact]
    public void Fenetre_incognito_propose_une_bascule_onglets_verticaux()
    {
        var xaml = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml");
        var code = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");

        Assert.Contains("x:Name=\"IncognitoVerticalTabsToggleButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"IncognitoVerticalTabsToggleButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IncognitoVerticalTabsColumn\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IncognitoVerticalTabsRail\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"IncognitoVerticalTabsPanelItems\"", xaml, StringComparison.Ordinal);

        Assert.Contains("private bool _verticalTabsEnabled;", code, StringComparison.Ordinal);
        Assert.Contains("private void IncognitoVerticalTabsToggleButton_Click(object sender, RoutedEventArgs e)", code, StringComparison.Ordinal);
        Assert.Contains("private void ApplyVerticalTabsLayout()", code, StringComparison.Ordinal);
        Assert.Contains("private void RenderVerticalTabs()", code, StringComparison.Ordinal);
        Assert.Contains("IncognitoTabs.Visibility = _verticalTabsEnabled ? Visibility.Collapsed : Visibility.Visible;", code, StringComparison.Ordinal);
        Assert.Contains("IncognitoVerticalTabsRail.Visibility = _verticalTabsEnabled ? Visibility.Visible : Visibility.Collapsed;", code, StringComparison.Ordinal);
    }

    // Le rail doit rester synchronise avec IncognitoTabs.TabItems (source de
    // verite unique) a chaque point qui la modifie - sinon le rail affiche
    // un etat perime des qu'un onglet est ajoute/ferme/renomme pendant que
    // la vue verticale est active.
    [Fact]
    public void Rail_vertical_incognito_reste_synchronise_avec_les_onglets()
    {
        var code = ReadRepoFile("Lumora.WinUI", "LumoraIncognitoWindow.xaml.cs");
        var createTab = ExtractMethod(code, "private async Task<IncognitoTab?> CreateTabAsync(string? url, bool select)");
        var closeTab = ExtractMethod(code, "private void CloseIncognitoTab(TabViewItem item, IncognitoTab tab)");
        var selectionChanged = ExtractMethod(code, "private void IncognitoTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)");

        Assert.Contains("RenderVerticalTabs();", createTab, StringComparison.Ordinal);
        Assert.Contains("RenderVerticalTabs();", closeTab, StringComparison.Ordinal);
        Assert.Contains("RenderVerticalTabs();", selectionChanged, StringComparison.Ordinal);

        // Renommage (DocumentTitleChanged) : le lambda est anonyme (pas de
        // signature a extraire), on verrouille juste que RenderVerticalTabs()
        // apparait bien APRES la mise a jour du titre dans ce bloc-la.
        var titleChangedIndex = code.IndexOf("core.DocumentTitleChanged +=", StringComparison.Ordinal);
        Assert.True(titleChangedIndex >= 0, "DocumentTitleChanged introuvable.");
        var nextRenderIndex = code.IndexOf("RenderVerticalTabs();", titleChangedIndex, StringComparison.Ordinal);
        var blockEndIndex = code.IndexOf("});", titleChangedIndex, StringComparison.Ordinal);
        Assert.True(nextRenderIndex >= 0 && nextRenderIndex < blockEndIndex,
            "RenderVerticalTabs() doit etre appele dans le bloc DocumentTitleChanged.");
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
