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
        new(StartMenuTileIds.Vault, "Coffre", StartMenuGlyphs.Padlock),
        new(StartMenuTileIds.Favoris, "Favoris", StartMenuGlyphs.Star),
        new(StartMenuTileIds.History, "Historique", StartMenuGlyphs.Clock),
        new(StartMenuTileIds.Settings, "Param\u00E8tres", StartMenuGlyphs.Sliders),
        new(StartMenuTileIds.Notes, "Notes", StartMenuGlyphs.Notes),
        new(RssId, "Flux RSS", StartMenuGlyphs.Rss),
        new(StartMenuTileIds.Downloads, "T\u00E9l\u00E9chargements", StartMenuGlyphs.Download),
        new(StartMenuTileIds.TabGroups, "Groupes d'onglets", StartMenuGlyphs.TabStack),
        new(StartMenuTileIds.Sessions, "Sessions", StartMenuGlyphs.Sessions),
        new(StartMenuTileIds.Wallet, "Portefeuille", StartMenuGlyphs.Wallet),
        new(StartMenuTileIds.WebApps, "Applis web", StartMenuGlyphs.Pin),
        new(StartMenuTileIds.ReadingLens, "Loupe de lecture", StartMenuGlyphs.ReadingLens),
        new(StartMenuTileIds.AllModules, "Modules", StartMenuGlyphs.Modules),
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
