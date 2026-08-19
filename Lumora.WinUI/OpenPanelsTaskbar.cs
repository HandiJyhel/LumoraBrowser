namespace Lumora.WinUI;

// Definition statique d'un panneau "application" suivi par la barre des
// taches (StatusBarRow, MainWindow.xaml). Meme esprit que
// StartMenuTileDefinition/StartMenuTileRegistry.cs : classe pure sans
// dependance UI, testable dans Lumora.Tests - l'execution reelle
// (rattachement au FrameworkElement, rendu des boutons, cliques) reste en
// code-behind (MainWindow.OpenPanelsTaskbar.cs).
public sealed record TrackedPanelDefinition(string Id, string Title, string Glyph);

public static class OpenPanelsTaskbar
{
    // Le Flux RSS n'a pas de tuile StartMenuTileRegistry (il ne vit que comme
    // bouton de toolbar epinglable, RssModuleButton dans MainWindow.xaml) donc
    // pas de constante partagee dans StartMenuTileIds. Reprend la chaine
    // "rss" deja utilisee par le systeme d'epinglage existant
    // (IsPinned("rss"), MainWindow.UsageMode.cs) pour rester coherent plutot
    // que d'inventer un nouvel identifiant.
    public const string RssId = "rss";

    // Les 13 panneaux "application" suivis (decision utilisateur du
    // 2026-08-18 : tout module qui a vraiment besoin d'une fenetre complete
    // rejoint la barre des taches des qu'il s'ouvre, disparait quand il se
    // ferme - rien a epingler, le menu Modules sert deja de lanceur). A
    // propos, l'assistant d'import et le Centre du site actuel sont
    // volontairement absents (ecran statique, etape transitoire, panneau
    // contextuel au site visite - voir MainWindow.OpenPanelsTaskbar.cs pour
    // le rattachement aux FrameworkElement reels). Glyphe/titre recopies
    // depuis StartMenuTileRegistry.All pour rester coherents avec le menu
    // Demarrer ; RssId est la seule exception (source : RssModuleButton).
    //
    // Glyphes stockes en echappement C# litteral ("\uXXXX" visible dans le
    // code source), jamais en caractere Unicode brut - voir le correctif
    // documente dans StartMenuTileRegistry.cs (glyphe invisible a
    // l'inspection texte, incident du 2026-08-12).
    public static readonly IReadOnlyList<TrackedPanelDefinition> Definitions =
    [
        new(StartMenuTileIds.Vault, "Coffre", "\uE72E"),
        new(StartMenuTileIds.Favoris, "Favoris", "\uE735"),
        new(StartMenuTileIds.History, "Historique", "\uE81C"),
        new(StartMenuTileIds.Settings, "Param\u00E8tres", "\uE713"),
        new(StartMenuTileIds.Notes, "Notes", "\uE70B"),
        new(RssId, "Flux RSS", "\uE7C1"),
        new(StartMenuTileIds.Downloads, "T\u00E9l\u00E9chargements", "\uE896"),
        new(StartMenuTileIds.TabGroups, "Groupes d'onglets", "\uE8FD"),
        new(StartMenuTileIds.Sessions, "Sessions", "\uE7F4"),
        new(StartMenuTileIds.Wallet, "Portefeuille", "\uE8C7"),
        new(StartMenuTileIds.WebApps, "Applis web", "\uE71D"),
        new(StartMenuTileIds.ReadingLens, "Loupe de lecture", "\uE721"),
        new(StartMenuTileIds.AllModules, "Modules", "\uE8A9"),
    ];

    public static TrackedPanelDefinition? Find(string id) =>
        Definitions.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal));

    // Ajoute id en fin de liste s'il n'y est pas deja. Ordre = ordre
    // d'ouverture, jamais reordonne au reaffichage - meme convention que la
    // vraie barre des taches Windows : reactiver une appli deja ouverte ne
    // deplace pas son icone.
    public static IReadOnlyList<string> AddIfMissing(IReadOnlyList<string> openIds, string id)
    {
        if (openIds.Contains(id))
        {
            return openIds;
        }

        var next = new List<string>(openIds) { id };
        return next;
    }

    public static IReadOnlyList<string> Remove(IReadOnlyList<string> openIds, string id)
    {
        if (!openIds.Contains(id))
        {
            return openIds;
        }

        return openIds.Where(existing => existing != id).ToList();
    }
}
