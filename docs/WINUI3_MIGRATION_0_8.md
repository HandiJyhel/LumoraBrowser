# Migration WinUI 3 0.8

## Decision

La version `0.8.0-dev` demarre la vraie direction interface de Pulse Browser: une coque WinUI 3 dediee, separee du prototype Win32.

## Pourquoi

Win32 a permis de prouver rapidement Rust, CEF, la navigation embarquee, le profil local, le coffre, les favoris et les premiers onglets. Cette couche reste utile comme prototype moteur, mais elle n'est pas adaptee a l'identite graphique finale du navigateur.

WinUI 3 devient donc la surface produit cible.

## Architecture visee

- `PulseBrowser.WinUI`: interface moderne, accueil, centre local, parametres et futures surfaces produit.
- `src/*`: coeur Rust existant, stockage local, profils, confidentialite, favoris, historique, coffre et moteur CEF.
- `scripts/run-winui.ps1`: lancement de la coque WinUI 3.
- `scripts/build-winui.ps1`: compilation de la coque WinUI 3 avec MSBuild Visual Studio.
- `run-dev.cmd`: lanceur desactive pour eviter de developper sur le prototype historique.
- `archive/win32-cef-prototype/`: archive technique du prototype Win32/CEF, sans fonction produit active.

## Palier 0.8.1-dev

La version `0.8.1-dev` corrige la regression visible de la premiere coque WinUI:

- barre d'onglets WinUI avec creation, selection et fermeture d'onglets;
- menu principal `Pulse`, `Favoris` et `Outils`;
- barre d'adresse et commandes retour/avance/recharger/stop presentes dans la surface WinUI;
- barre de favoris relue depuis le profil local Pulse Browser;
- gestionnaire de favoris WinUI compatible avec `bookmarks.tsv`, avec colonne de dossiers, contenu du dossier courant, fil d'Ariane, recherche, creation de dossiers, renommage et suppression;
- import HTML de favoris;
- import depuis les profils Chromium locaux detectes;
- export HTML des favoris Pulse;
- page `A propos`, centre local et parametres.

Le gestionnaire WinUI lit et ecrit le meme fichier local que le prototype Rust:

`%LOCALAPPDATA%\PulseBrowser\profiles\default\navigation\bookmarks.tsv`

Cette etape ne raccorde pas encore CEF dans la fenetre WinUI. Elle evite cependant que la migration d'interface perde les surfaces produit deja acquises.

## Palier 0.8.2-dev

La version `0.8.2-dev` restaure la navigation Internet dans la coque WinUI 3 avec une zone WebView2:

- la barre d'adresse charge les pages web dans la fenetre WinUI;
- les boutons retour, avancer, recharger et stop pilotent la zone web;
- les clics sur favoris ouvrent les sites dans la fenetre WinUI;
- les titres et adresses d'onglets sont mis a jour depuis la navigation;
- les dossiers de la barre de favoris ouvrent un menu deroulant avec leurs liens et sous-dossiers, au lieu d'ouvrir le gestionnaire.

WebView2 est utilise comme pont temporaire pour retablir l'usage navigateur dans WinUI. La cible moteur finale du projet reste Chromium via CEF raccorde au coeur Rust local.

Correction runtime appliquee: la premiere navigation WebView2 est differee jusqu'a `CoreWebView2Initialized`. Cela evite le crash WinUI natif `0xC000027B` observe quand `NavigateToString` etait appele trop tot au demarrage.

## Palier 0.8.3-dev

La version `0.8.3-dev` corrige l'organisation des menus WinUI:

- `A propos de Pulse Browser` est deplace dans le menu `Outils`;
- la page separee `Donnees locales` est supprimee;
- le chemin du profil local et le rappel local-first sont affiches dans `A propos`;
- `Autres favoris` dispose d'un acces direct depuis le menu `Favoris`, au meme niveau que `Barre des favoris`.

## Palier 0.8.4-dev

La version `0.8.4-dev` corrige la regression de gestion des favoris dans la coque WinUI:

- les dossiers et les liens affichent maintenant de vrais glyphes WinUI dans le gestionnaire et la barre de favoris;
- `Autres favoris` est visible comme bouton permanent dans la barre de favoris, en plus de son entree de menu;
- un dossier ouvert depuis le gestionnaire ou le menu contextuel affiche son contenu normalement dans le gestionnaire;
- un lien ouvert depuis les favoris charge le site dans la zone web WinUI;
- les listes de dossiers et de favoris ont un menu contextuel avec `Ouvrir`, `Ouvrir le dossier`, `Renommer` et `Supprimer` selon le type d'element;
- l'import depuis un navigateur installe est separe de l'import HTML et propose maintenant `Fusionner` ou `Remplacer`, avec sauvegarde locale avant remplacement.

## Palier 0.8.5-dev

La version `0.8.5-dev` rend la direction WinUI plus stricte et corrige plusieurs points d'organisation:

- la gestion des favoris est exposee dans `Parametres`, avec acces au gestionnaire, aux racines, a l'import et a l'export;
- le bouton `Gerer` est retire de la barre des favoris;
- `Autres favoris` est place a droite de la barre, separe des favoris de la barre principale;
- le toggle `Barre d'onglets verticale` active une vraie colonne d'onglets et masque la barre horizontale;
- les favicons fournies par WebView2 sont stockees localement dans le profil Pulse Browser et reutilisees dans les signets;
- les lanceurs ambigus du prototype Win32/CEF sont desactives;
- `archive/win32-cef-prototype/` documente le prototype comme reference technique uniquement.

## Palier 0.8.6-dev

La version `0.8.6-dev` corrige les points observes apres activation des onglets verticaux et reorganisation des favoris:

- la gestion des favoris quitte `Parametres` et devient accessible par `Outils > Favoris`;
- le menu principal `Favoris` est retire pour garder une structure plus claire autour de `Outils`;
- le rail d'onglets verticaux devient redimensionnable avec une poignee laterale;
- le rail peut etre reduit en mode compact pour afficher les onglets sous forme d'icones;
- les favicons recuperees par WebView2 sont rattachees aux onglets courants quand elles existent;
- les dossiers dans les menus de favoris affichent maintenant des actions directes (`Ouvrir le dossier`, `Renommer`, `Supprimer`) avant leur contenu;
- les liens de favoris dans les menus gardent leur navigation directe vers la zone web WinUI.

## Palier 0.8.7-dev

La version `0.8.7-dev` transforme les options visibles en vrais reglages persistants:

- ajout de `ui-settings.json` dans le profil local Pulse Browser;
- sauvegarde locale de la barre de favoris visible ou masquee;
- sauvegarde locale de l'activation des onglets verticaux;
- sauvegarde locale du mode compact du rail vertical;
- sauvegarde locale de la largeur du rail vertical;
- reapplication des reglages au demarrage avant l'usage de la fenetre;
- protection contre l'ecrasement des reglages pendant l'initialisation WinUI;
- restauration des favicons d'onglets depuis le cache local quand le fichier correspondant a l'URL existe deja.

## Etapes suivantes

1. Raccorder le rendu CEF/Rust final a la fenetre WinUI.
2. Remplacer le pont WebView2 par le moteur cible CEF quand le raccord est stable.
3. Extraire le stockage favoris WinUI derriere un pont vers le coeur Rust, afin d'eviter une duplication durable.
4. Donner a la coque WinUI une identite visuelle Pulse plus forte.

## Limites

- La coque WinUI 3 navigue via WebView2, pas encore via CEF/Rust.
- Le raccord entre WinUI 3 et le moteur CEF Rust reste a implementer.
- Le stockage favoris WinUI reproduit le format Rust pour garder la compatibilite locale; il devra etre remplace par un pont Rust propre.
- Les favicons sont recuperees quand WebView2 les fournit; l'import direct depuis les bases de favicons des navigateurs installes reste a traiter plus tard.
- Le prototype Win32 est archive et ne doit plus recevoir de fonctions produit.
