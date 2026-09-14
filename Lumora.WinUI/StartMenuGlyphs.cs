namespace Lumora.WinUI;

// Refonte organique "Encre chaude" (2026-09-12, suite - "glyphes du Menu
// Demarrer" signales comme chantier restant dans le bilan de la session
// precedente). StartMenuTileRegistry.cs et OpenPanelsTaskbar.cs portaient
// chacun leur propre jeu de glyphes Segoe MDL2 Assets pour les MEMES tuiles
// (recopies manuellement d'un fichier a l'autre, cause du "Glyphe corrige -
// dupliquait E72E" documente historiquement dans StartMenuTileRegistry.cs) :
// ce fichier devient la source UNIQUE des geometries Path.Data utilisees par
// les deux, pour ne plus jamais avoir a les resynchroniser a la main.
//
// Toutes les geometries sont dessinees sur une grille ~24x24 (mini-langage
// XAML Path.Data), remplies en Fill (jamais de Stroke - coherent avec le
// reste de la refonte "icones pleines/silhouettes"), sans FillRule explicite
// (EvenOdd par defaut en WinUI3 - une sous-figure imbriquee dans une autre
// devient un trou, technique deja utilisee pour la cle de VaultQuickAccessButton
// et la page generique de BookmarkGlyphs.Link).
//
// Limite honnete assumee : le rendu pixel exact n'est pas verifiable dans cet
// environnement (pas de capture d'ecran fiable, deja documente MEMORY.md) -
// ces geometries visent "distinctes et non cassees", pas un polish visuel
// confirme a l'oeil.
public static class StartMenuGlyphs
{
    // Grille de 4 carres pleins - "Tous les modules".
    public const string Modules =
        "M4,4 L10,4 L10,10 L4,10 Z " +
        "M14,4 L20,4 L20,10 L14,10 Z " +
        "M4,14 L10,14 L10,20 L4,20 Z " +
        "M14,14 L20,14 L20,20 L14,20 Z";

    // Livre ouvert (2 "pages" trapezoidales) - "Mode lecture".
    public const string ReaderMode =
        "M3,5 L11,7 L11,19 L3,17 Z " +
        "M21,5 L13,7 L13,19 L21,17 Z";

    // "Notes" reprend la MEME silhouette de page generique que l'icone de
    // secours des favoris/historique - c'est ICI la source (fichier sans
    // aucune dependance, compile dans Lumora.Tests, voir
    // PathMiniLanguageTests.cs) : BookmarkGlyphs.Link (Models/Bookmarks.cs,
    // qui depend de types WinUI et n'est PAS compile dans les tests)
    // reference cette constante plutot que de la recopier (nettoyage
    // 2026-09-14, doublon reel corrige - la reference ne peut se faire que
    // dans ce sens, jamais l'inverse).
    public const string Notes =
        "M6,3 L14,3 L19,8 L19,21 L6,21 Z " +
        "M9,12 L16,12 L16,13.4 L9,13.4 Z " +
        "M9,15.5 L16,15.5 L16,16.9 L9,16.9 Z";

    // 2 fleches opposees (echange) - "Traduction".
    public const string Translate =
        "M2,6 L14,6 L14,3.5 L20,7.5 L14,11.5 L14,9 L2,9 Z " +
        "M22,18 L10,18 L10,20.5 L4,16.5 L10,12.5 L10,15 L22,15 Z";

    // Loupe (anneau + poignee diagonale) - "Loupe de lecture".
    public const string ReadingLens =
        "M16,10 A6,6 0 1,1 4,10 A6,6 0 1,1 16,10 Z " +
        "M14.3,10 A4.3,4.3 0 1,1 5.7,10 A4.3,4.3 0 1,1 14.3,10 Z " +
        "M14.2,15.8 L15.8,14.2 L21.8,20.2 L20.2,21.8 Z";

    // Cadenas (corps + anse en arc) - "Coffre" (distinct du bouclier
    // Confidentialite/AdBlocker ci-dessous, memes 2 concepts jamais cote a
    // cote dans le Menu Demarrer - Coffre et Bloqueur de pub restent 2
    // sections "Confidentialite" mais 2 symboles differents).
    public const string Padlock =
        "M6,11 L18,11 L18,20 L6,20 Z " +
        "M8,11 L8,7.5 A4,4 0 0,1 16,7.5 L16,11 Z";

    // Bouclier - "Bloqueur de pub" : MEME geometrie que Confidentialite du
    // site (ShieldButton, Toolbar) et les autres occurrences deja etablies
    // dans l'app (Reglages, Centre Lumora) - reutilisation volontaire, pas
    // une geometrie inventee de plus.
    public const string Shield =
        "M12,3.2 L18.6,5.9 L18.6,11 C18.6,15.6 15.9,19.1 12,20.8 " +
        "C8.1,19.1 5.4,15.6 5.4,11 L5.4,5.9 Z";

    // Cle - "Passkeys" : MEME geometrie que VaultQuickAccessButton (Toolbar,
    // deja verifiee fonctionnelle cette session) - identifiants/authentification
    // sans mot de passe, meme famille visuelle que le Coffre.
    public const string Key =
        "M12,12 A5,5 0 1,1 2,12 A5,5 0 1,1 12,12 Z " +
        "M9.3,12 A2.3,2.3 0 1,1 4.7,12 A2.3,2.3 0 1,1 9.3,12 Z " +
        "M12,10.8 L20,10.8 L20,13.2 L12,13.2 Z " +
        "M15.4,13.2 L16.6,13.2 L16.6,15.6 L15.4,15.6 Z " +
        "M18.2,13.2 L19.6,13.2 L19.6,16.8 L18.2,16.8 Z";

    // 2 cercles pleins qui se chevauchent (connexions) - "Sessions".
    public const string Sessions =
        "M13.5,12 A5.5,5.5 0 1,1 2.5,12 A5.5,5.5 0 1,1 13.5,12 Z " +
        "M20.5,12 A5.5,5.5 0 1,1 9.5,12 A5.5,5.5 0 1,1 20.5,12 Z";

    // Carte avec bande (trou EvenOdd) - "Portefeuille".
    public const string Wallet =
        "M3,6 L21,6 L21,18 L3,18 Z " +
        "M3,9.5 L21,9.5 L21,11.5 L3,11.5 Z";

    // Oeil en amande (2 courbes + pupille en trou) - "Incognito". Silhouette
    // pleine equivalente a l'oeil trace en Stroke de IncognitoToolbarButton
    // (Toolbar), necessaire ici en un seul Path (gabarit de tuile a 1 element).
    public const string Eye =
        "M2,12 C6,4 18,4 22,12 C18,20 6,20 2,12 Z " +
        "M15.2,12 A3.2,3.2 0 1,1 8.8,12 A3.2,3.2 0 1,1 15.2,12 Z";

    // Cible/bullseye (anneau + point central) - "Site actuel".
    public const string Target =
        "M21,12 A9,9 0 1,1 3,12 A9,9 0 1,1 21,12 Z " +
        "M17,12 A5,5 0 1,1 7,12 A5,5 0 1,1 17,12 Z " +
        "M14,12 A2,2 0 1,1 10,12 A2,2 0 1,1 14,12 Z";

    // Fenetre + fleche de coin - "Nouvelle fenêtre".
    public const string NewWindow =
        "M4,7 L18,7 L18,19 L4,19 Z " +
        "M16,4 L22,4 L22,10 L19,7 Z";

    // Etoile pleine - "Favoris" : MEME geometrie que BookmarkStarIcon
    // (Toolbar), version pleine (pas de bascule contour/plein necessaire ici).
    public const string Star =
        "M12,4 L14.2,9.6 L20.2,10.1 L15.6,14 L17,20 L12,16.8 " +
        "L7,20 L8.4,14 L3.8,10.1 L9.8,9.6 Z";

    // Horloge (disque + aiguilles en trous EvenOdd) - "Historique".
    public const string Clock =
        "M21,12 A9,9 0 1,1 3,12 A9,9 0 1,1 21,12 Z " +
        "M11.3,6 L12.7,6 L12.7,12 L11.3,12 Z " +
        "M12,11.3 L17,10.3 L17,11.6 L12,12.6 Z";

    // Fleche vers le bas + socle - "Téléchargements".
    public const string Download =
        "M11,3 L13,3 L13,12 L16,12 L12,17 L8,12 L11,12 Z " +
        "M5,19 L19,19 L19,21 L5,21 Z";

    // Epingle (silhouette + trou de tete) - "Applis web" (distinct de
    // "Nouvelle fenêtre" ci-dessus - 2 concepts "fenetre" auraient recree le
    // meme probleme de ressemblance que le bouclier/cle de la Toolbar).
    public const string Pin =
        "M12,3 C16,3 19,6 19,10 C19,15 12,21 12,21 C12,21 5,15 5,10 C5,6 8,3 12,3 Z " +
        "M14.2,10 A2.2,2.2 0 1,1 9.8,10 A2.2,2.2 0 1,1 14.2,10 Z";

    // Anneau ouvert (camembert) + fleche - "Onglet fermé" (restaurer/undo,
    // meme famille visuelle que le bouton Recharger de la Toolbar sans en
    // reprendre exactement la geometrie).
    public const string Restore =
        "M17,7 A8,8 0 1,1 7,17 Z " +
        "M20,4 L20,10 L15,7 Z";

    // 3 carres empiles en escalier - "Groupes d'onglets".
    public const string TabStack =
        "M4,6 L12,6 L12,12 L4,12 Z " +
        "M8,10 L16,10 L16,16 L8,16 Z " +
        "M12,14 L20,14 L20,20 L12,20 Z";

    // 2 curseurs de reglage (lignes + ronds) - "Paramètres", plus simple et
    // plus sur a construire sans verification visuelle qu'un engrenage.
    public const string Sliders =
        "M4,7 L20,7 L20,8.6 L4,8.6 Z " +
        "M16.6,7.8 A2.6,2.6 0 1,1 11.4,7.8 A2.6,2.6 0 1,1 16.6,7.8 Z " +
        "M4,15.4 L20,15.4 L20,17 L4,17 Z " +
        "M11.6,16.2 A2.6,2.6 0 1,1 6.4,16.2 A2.6,2.6 0 1,1 11.6,16.2 Z";

    // Silhouette (tete + epaules) - "Profils locaux".
    public const string Person =
        "M16,8 A4,4 0 1,1 8,8 A4,4 0 1,1 16,8 Z " +
        "M4,21 C4,15 8,13 12,13 C16,13 20,15 20,21 Z";

    // Disque + "i" en trous EvenOdd - "À propos".
    public const string Info =
        "M21,12 A9,9 0 1,1 3,12 A9,9 0 1,1 21,12 Z " +
        "M13.3,7.5 A1.3,1.3 0 1,1 10.7,7.5 A1.3,1.3 0 1,1 13.3,7.5 Z " +
        "M11,10.5 L13,10.5 L13,17 L11,17 Z";

    // Cadre + bande laterale pleine (disposition/sidebar) - "Studio"
    // ("position des onglets/favoris").
    public const string Layout =
        "M4,5 L20,5 L20,19 L4,19 Z " +
        "M9,7 L18,7 L18,17 L9,17 Z";

    // Point + 2 arcs concentriques - "Flux RSS" (OpenPanelsTaskbar
    // uniquement, pas de tuile StartMenuTileRegistry pour ce module).
    public const string Rss =
        "M8.2,18 A2.2,2.2 0 1,1 3.8,18 A2.2,2.2 0 1,1 8.2,18 Z " +
        "M4,12 A8,8 0 0,1 12,20 L9,20 A5,5 0 0,0 4,15 Z " +
        "M4,6 A14,14 0 0,1 18,20 L15,20 A11,11 0 0,0 4,9 Z";
}
