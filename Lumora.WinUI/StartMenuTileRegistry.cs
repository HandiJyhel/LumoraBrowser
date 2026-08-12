namespace Lumora.WinUI;

// Definition statique d'une tuile du menu Demarrer (ModulesFlyout). Classe
// pure sans dependance UI - testable dans Lumora.Tests comme
// AddressSuggestionEngine.cs. L'execution reelle (navigation) reste en
// code-behind (MainWindow.StartMenu.cs) : ce registre ne connait que l'id,
// le libelle et la section, jamais le handler cible.
public sealed record StartMenuTileDefinition(
    string Id,
    string Section,
    string Title,
    string Subtitle,
    string Glyph);

public static class StartMenuTileRegistry
{
    // Une seule entree par id (elimine les doublons de l'ancien XAML statique,
    // ou "Mode lecture"/"Coffre"/"Portefeuille"/"Favoris"/"Parametres"
    // apparaissaient chacun dans 2 sections). "Lecture et contenu" reprend le
    // nom de categorie deja utilise par l'utilisateur avant cette refonte.
    //
    // Categorie "Onglets" supprimee (0.93.5.0-dev, retour utilisateur sur le
    // rail de categories : "on sait pas ce que ca fait la" - 2 de ses 4
    // elements n'avaient rien a voir avec la gestion d'onglets). Incognito et
    // Site actuel (permissions/reglages du site visite) rejoignent
    // Confidentialite et securite - ce sont des fonctions de confidentialite,
    // pas des onglets. Onglet ferme et Groupes d'onglets rejoignent
    // Navigation - ce sont des actions de navigation/historique.
    public static readonly IReadOnlyList<StartMenuTileDefinition> All =
    [
        // Sortie de "Lumora et profil" vers sa propre categorie de premier
        // niveau (2026-08-08, suite du plan "identifier et lister les
        // problemes de code" - retour utilisateur d'origine : "je n'ai pas
        // trouve le bouton pour activer ou desactiver des modules"). Placee
        // en tete de liste pour apparaitre juste apres "Epingles" dans le
        // rail du Menu Demarrer, plutot que noyee au milieu de 5 autres
        // tuiles sous "Lumora et profil".
        new(StartMenuTileIds.AllModules, "Modules", "Tous les modules", "Activer ou désactiver", "\uE8A9"),

        new(StartMenuTileIds.ReaderMode, "Lecture et contenu", "Mode lecture", "Sans distraction", "\uE736"),
        new(StartMenuTileIds.Notes, "Lecture et contenu", "Notes", "Locales", "\uE70B"),
        new(StartMenuTileIds.Translate, "Lecture et contenu", "Traduction", "Hors-ligne", "\uF2B7"),
        new(StartMenuTileIds.ReadingLens, "Lecture et contenu", "Loupe de lecture", "Confort visuel", "\uE721"),

        new(StartMenuTileIds.Vault, "Confidentialité", "Coffre", "Mots de passe", "\uE72E"),
        // Seule protection de fond promue en tuile (2026-08-08, retour
        // utilisateur : "par defaut, ce que l'utilisateur voit quoi qu'il
        // arrive, c'est le bloqueur de pub et le coffre") - epinglee par
        // defaut avec Coffre, voir UiSettings.DefaultPinnedStartMenuTileIds.
        // Ouvre directement Confidentialite > Publicites et traceurs
        // (OpenAdBlockerSettings, MainWindow.xaml.cs), pas la vue
        // d'ensemble de Confidentialite.
        // Glyphe corrige (2026-08-12, retour utilisateur) : dupliquait E72E
        // (cadenas) avec Coffre - stocke comme caractere Unicode brut au lieu
        // de l'echappement C# "\uXXXX" utilise partout ailleurs dans ce
        // fichier, ce qui le rendait invisible a l'inspection texte (zone
        // privee Unicode, aucun glyphe hors police Segoe MDL2). EA18, meme
        // bouclier deja verifie sur ShieldButton (barre d'outils).
        new(StartMenuTileIds.AdBlocker, "Confidentialité", "Bloqueur de pub", "Publicités et traceurs", "\uEA18"),
        new(StartMenuTileIds.Passkeys, "Confidentialité", "Passkeys", "Sans mot de passe", "\uE8D7"),
        new(StartMenuTileIds.Sessions, "Confidentialité", "Sessions", "Connexions actives", "\uE7F4"),
        new(StartMenuTileIds.Wallet, "Confidentialité", "Portefeuille", "Cartes locales", "\uE8C7"),
        // Glyphe corrige (2026-08-12, retour utilisateur) : dupliquait aussi
        // E72E (cadenas) avec Coffre. E890, deja verifie dans ce depot
        // (icone "oeil", AccessibilityQuickVisionButton) - distinct du
        // cadenas.
        new(StartMenuTileIds.Incognito, "Confidentialité", "Incognito", "Nouvelle fenêtre privée", "\uE890"),
        new(StartMenuTileIds.SiteControl, "Confidentialité", "Site actuel", "Centre de contrôle", "\uE774"),

        new(StartMenuTileIds.Favoris, "Navigation", "Favoris", "Enregistrés", "\uE735"),
        new(StartMenuTileIds.History, "Navigation", "Historique", "Recherche sémantique", "\uE81C"),
        new(StartMenuTileIds.Downloads, "Navigation", "Téléchargements", "Fichiers reçus", "\uE896"),
        new(StartMenuTileIds.WebApps, "Navigation", "Applis web", "Épinglées", "\uE71D"),
        new(StartMenuTileIds.ReopenTab, "Navigation", "Onglet fermé", "Rouvrir le dernier", "\uE7A7"),
        new(StartMenuTileIds.TabGroups, "Navigation", "Groupes d'onglets", "Enregistrés", "\uE8FD"),

        new(StartMenuTileIds.Settings, "Lumora et profil", "Paramètres", "Centre Lumora", "\uE713"),
        new(StartMenuTileIds.Profiles, "Lumora et profil", "Profils locaux", "PIN, verrouillage", "\uE77B"),
        new(StartMenuTileIds.About, "Lumora et profil", "À propos", "Version, licences", "\uE946"),
        new(StartMenuTileIds.Studio, "Lumora et profil", "Studio", "Position des onglets/favoris", "\uE82D"),
    ];

    public static StartMenuTileDefinition? Find(string id) =>
        All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));

    // Famille de sens d'une section (chantier identite visuelle, 2026-08-10) :
    // "content" = ce qui est a vous (Navigation : favoris, historique,
    // telechargements...), "protection" = ce qui vous protege
    // (Confidentialite), "tools" = ce qui vous aide a agir sur le contenu
    // (Lecture et contenu, Modules), "identity" = Lumora lui-meme (Lumora et
    // profil). "Epingles" (categorie synthetique, pas une section du
    // registre) reste seule sur le degrade neutre - chaque tuile y garde sa
    // propre famille (Parametres suit desormais "identity" comme partout
    // ailleurs). Fonction pure : le rendu (resolution en Brush) reste cote UI
    // (MainWindow.StartMenu.cs), voir la meme separation que ScoreTile.
    //
    // "Lumora et profil" corrige (2026-08-12, retour utilisateur : "Paramètres"
    // epingle et la categorie elle-meme restaient au degrade neutre terne,
    // contraste net avec les 3 familles vives ci-dessus) - violet dedie
    // (NovaTileFamilyIdentityBrush), plutot que de laisser cette categorie
    // hors du systeme de couleur introduit le 10/08.
    public static string? ResolveFamilyKey(string section) => section switch
    {
        "Navigation" => "content",
        "Confidentialité" => "protection",
        "Lecture et contenu" => "tools",
        "Modules" => "tools",
        "Lumora et profil" => "identity",
        _ => null,
    };

    // Meme forme que ScoreCommandPaletteItem (MainWindow.CommandPalette.cs) :
    // requete vide -> tout passe (ordre du registre) ; sinon score par
    // correspondance exacte/prefixe/contenu sur titre, sous-titre, section.
    public static int ScoreTile(StartMenuTileDefinition tile, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return 1;

        var q = query.Trim();
        var score = 0;
        if (tile.Title.Equals(q, StringComparison.CurrentCultureIgnoreCase)) score += 150;
        if (tile.Title.StartsWith(q, StringComparison.CurrentCultureIgnoreCase)) score += 100;
        if (tile.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase)) score += 65;
        if (tile.Subtitle.Contains(q, StringComparison.CurrentCultureIgnoreCase)) score += 45;
        if (tile.Section.Contains(q, StringComparison.CurrentCultureIgnoreCase)) score += 25;
        return score;
    }

    // Frequence plafonnee + paliers de recence, meme forme que
    // AddressSuggestionEngine.MatchScore mais seuils resserres : l'usage du
    // menu Demarrer est bien moins frequent que la frappe dans la barre
    // d'adresse, la recence doit dominer plus vite.
    public static int UsageScore(StartMenuTileUsage usage, DateTimeOffset now)
    {
        var score = Math.Min(usage.OpenCount, 20) * 3;
        var age = now - usage.LastOpenedAt;
        score += age switch
        {
            _ when age <= TimeSpan.FromHours(2) => 40,
            _ when age <= TimeSpan.FromDays(2) => 25,
            _ when age <= TimeSpan.FromDays(14) => 10,
            _ => 0
        };
        return score;
    }

    public static IReadOnlyList<string> TopTiles(IEnumerable<StartMenuTileUsage> usage, DateTimeOffset now, int count) =>
        usage
            .OrderByDescending(u => UsageScore(u, now))
            .ThenByDescending(u => u.LastOpenedAt)
            .Take(count)
            .Select(u => u.TileId)
            .ToList();
}
