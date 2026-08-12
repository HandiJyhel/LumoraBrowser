namespace Lumora.WinUI;

// Vocabulaire stable des destinations du menu Demarrer (ModulesFlyout).
// Distinct de PinnedModuleIds (MainWindow.UsageMode.cs) : celui-la pilote la
// visibilite de boutons de la barre d'outils, une surface differente avec
// son propre vocabulaire d'ids - les deux ne doivent pas etre melanges.
public static class StartMenuTileIds
{
    public const string Favoris = "favoris";
    public const string Vault = "vault";
    public const string ReaderMode = "readerMode";
    public const string Notes = "notes";
    public const string Translate = "translate";
    public const string ReadingLens = "readingLens";
    public const string Passkeys = "passkeys";
    public const string Sessions = "sessions";
    public const string Wallet = "wallet";
    public const string History = "history";
    public const string Downloads = "downloads";
    public const string WebApps = "webApps";
    public const string Incognito = "incognito";
    public const string ReopenTab = "reopenTab";
    public const string TabGroups = "tabGroups";
    public const string SiteControl = "siteControl";
    public const string Settings = "settings";
    public const string Profiles = "profiles";
    public const string AllModules = "allModules";
    public const string About = "about";
    public const string Studio = "studio";
    // Decouvrabilite du bloqueur de pub (2026-08-08, retour utilisateur) :
    // seule protection de fond promue en tuile epinglable par defaut, avec
    // Coffre - voir UiSettings.DefaultPinnedStartMenuTileIds.
    public const string AdBlocker = "adBlocker";
}

// Une entree = l'etat d'usage d'une tuile donnee. Univers fixe (~21 ids
// possibles, jamais plus) : upsert par id, pas un journal illimite - voir
// PasskeyEntry.LastUsedAt (Models/Passkeys.cs) pour le meme motif.
public sealed record StartMenuTileUsage(string TileId, int OpenCount, DateTimeOffset LastOpenedAt)
{
    public StartMenuTileUsage RecordOpen(DateTimeOffset now) =>
        this with { OpenCount = OpenCount + 1, LastOpenedAt = now };
}

// Vue d'affichage d'une tuile pour la liaison XAML (DataTemplate non
// compile, {Binding} classique - meme motif que CommandPaletteItem). IsPinned
// pilote a la fois l'etat visuel eventuel et le libelle du menu contextuel
// epingler/desepingler.
// FamilyKey (2026-08-10) : "content"/"protection"/"tools"/null, resolu
// depuis la section de la tuile (StartMenuTileRegistry.ResolveFamilyKey).
// Reste une chaine simple ici (pas de Brush) pour que ce fichier continue de
// compiler dans Lumora.Tests, sans reference a Microsoft.UI.Xaml - le rendu
// en couleur reelle se fait cote XAML via
// Converters.StartMenuFamilyKeyToBrushConverter.
public sealed record StartMenuTileViewModel(string Id, string Title, string Subtitle, string Glyph, bool IsPinned, string? FamilyKey)
{
    public string PinActionLabel => IsPinned ? "Désépingler" : "Épingler";
}

public sealed record StartMenuSectionViewModel(string Title, string HeaderGlyph, IReadOnlyList<StartMenuTileViewModel> Tiles);

// Entree du rail de categories (colonne de gauche du menu Demarrer, refonte
// "maitre/detail" a la Windows 7 - Cle "Epingles" ou nom de section). La
// selection courante est portee par MainWindow (_startMenuSelectedCategoryKey),
// pas par ce record : IsSelected serait redondant avec ListView.SelectedItem.
// FamilyKey (2026-08-10, "choses a revoir") : meme principe que
// StartMenuTileViewModel ci-dessus - "Epingles" reste null (degrade neutre,
// ce n'est pas une famille de contenu), les autres categories reprennent la
// famille de leur section (StartMenuTileRegistry.ResolveFamilyKey). Avant
// cette date, StartMenuCategoryTemplate (MainWindow.xaml) recyclait le meme
// degrade pour toutes les categories - retour utilisateur : le rail semblait
// "moins quali" que les cartes Epingles juste a cote, qui avaient deja leur
// couleur par famille.
public sealed record StartMenuCategoryViewModel(string Key, string Title, string Glyph, string? FamilyKey);
