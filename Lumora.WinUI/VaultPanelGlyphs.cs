namespace Lumora.WinUI;

// Refonte organique "Encre chaude" (2026-09-12, suite - "liste/detail du
// Coffre" signale comme chantier restant dans le bilan de la session
// precedente). MainWindow.VaultPanel.cs utilisait `SymbolIcon` (glyphes
// systeme Segoe MDL2 Assets, meme famille que les FontIcon deja remplaces
// ailleurs) pour les actions de la fiche d'un identifiant (copier/afficher/
// modifier/renommer/supprimer...) et un glyphe brut pour le favicon de
// secours. Memes principes que StartMenuGlyphs.cs/BookmarkGlyphs (Fill
// uniquement, EvenOdd implicite pour les trous), fichier separe car ce
// n'est PAS le Menu Demarrer - portee differente, pas de raison de
// regrouper dans StartMenuGlyphs.cs.
public static class VaultPanelGlyphs
{
    // 2 cadres qui se chevauchent (anneaux EvenOdd) - "Copier" (identifiant,
    // mot de passe, code TOTP : meme action partout, une seule geometrie).
    public const string Copy =
        "M4,2 L16,2 L16,14 L4,14 Z M6,4 L14,4 L14,12 L6,12 Z " +
        "M8,8 L20,8 L20,20 L8,20 Z M10,10 L18,10 L18,18 L10,18 Z";

    // Triangle plein - "Ouvrir la page de connexion".
    public const string Forward = "M8,4 L18,12 L8,20 Z";

    // Crayon (corps + pointe) - "Modifier le mot de passe" (distinct du tag
    // ci-dessous : "Renommer" et "Modifier" apparaissent l'un pres de
    // l'autre dans la meme fiche, meme raisonnement que la refonte
    // bouclier/cle de la Toolbar plus tot cette session - jamais 2 actions
    // voisines avec la meme silhouette).
    public const string Pencil =
        "M4,20 L4,17 L15,6 L18,9 L7,20 Z " +
        "M15,6 L17,4 L20,7 L18,9 Z";

    // Etiquette (silhouette + oeillet en trou EvenOdd) - "Renommer".
    public const string Tag =
        "M4,4 L16,4 L20,10 L16,16 L4,16 Z " +
        "M8,10 A1.6,1.6 0 1,1 4.8,10 A1.6,1.6 0 1,1 8,10 Z";

    // Corbeille (couvercle + poignee + corps a 2 lignes en trou EvenOdd) -
    // "Supprimer" (identifiant entier, ou code TOTP seul).
    public const string Trash =
        "M9,3 L15,3 L15,5.5 L9,5.5 Z " +
        "M4,6 L20,6 L20,8 L4,8 Z " +
        "M6,9 L18,9 L18,21 L6,21 Z " +
        "M11,11 L12.2,11 L12.2,19 L11,19 Z " +
        "M13.8,11 L15,11 L15,19 L13.8,19 Z";

    // Silhouette (tete + epaules) - "Identifiant" (champ nom d'utilisateur) :
    // MEME geometrie que StartMenuGlyphs.Person, recopiee ici (fichier sans
    // dependance croisee, meme raisonnement que StartMenuGlyphs.Notes).
    public const string Person =
        "M16,8 A4,4 0 1,1 8,8 A4,4 0 1,1 16,8 Z " +
        "M4,21 C4,15 8,13 12,13 C16,13 20,15 20,21 Z";

    // Croix pleine - "Ajouter un code TOTP".
    public const string Plus =
        "M11,4 L13,4 L13,11 L20,11 L20,13 L13,13 L13,20 L11,20 L11,13 L4,13 L4,11 L11,11 Z";
}
