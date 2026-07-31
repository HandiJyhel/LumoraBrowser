## Objet

Passer d'un simple habillage du chrome a une vraie ergonomie composable pour
Lumora, en donnant a l'utilisateur la possibilite de reconfigurer la place des
onglets et des favoris tout en gardant une identite visuelle Lumora coherente.

## Modifications

- ajout de deux reglages persistants dans `UiSettings` :
  `TabStripPosition` et `BookmarksBarPosition` ;
- ajout d'une section `Disposition Lumora` dans `Mon Lumora` avec :
  - position des onglets : `haut`, `bas`, `gauche`, `droite` ;
  - position des favoris : `haut`, `bas`, `gauche`, `droite` ;
  - presets `Halo`, `Atelier` et `Flux` ;
- refactor de `MainWindow.xaml` :
  - nouvelle grille de travail avec colonnes gauche/droite pour rails
    lateraux ;
  - nouvelle zone `BookmarksBottomRow` ;
  - nouveau rail `BookmarksSideRail` ;
  - nouveau reveal zone plein ecran a droite ;
  - decouplage du `BrowserTabs` pour pouvoir le placer en haut ou en bas ;
- refactor de `MainWindow.Settings.cs` :
  - normalisation des positions ;
  - application dynamique des dispositions ;
  - preservation de la compatibilite avec l'ancien switch
    `VerticalTabsSwitch` ;
  - prise en charge du plein ecran avec reveal gauche/droite selon la position
    reelle du rail d'onglets ;
- refactor de `MainWindow.Bookmarks.cs` :
  - rendu des favoris vers la zone active (haut, bas ou lateral) ;
  - support des hosts multiples ;
  - conservation du style Lumora releve pour les boutons ;
  - bring-into-view adapte aux nouveaux emplacements ;
- montee de version en `0.84.0.23-dev` dans `MainWindow.xaml.cs`,
  `AGENTS.md`, `scripts/build-installer.ps1`,
  `scripts/build-clean-test-artifact.ps1` et
  `UsageModeVisualIdentityTests`.

## Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
  execute avec succes (`0 avertissement`, `0 erreur`) ;
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --filter UsageModeVisualIdentityTests`
  execute avec succes (`12/12` OK).

## Note

Cette passe pose la base d'une ergonomie configurable sans encore aller
jusqu'au glisser-deposer libre de toutes les zones. La coque WinUI n'est plus
figee sur un schema type Chrome : l'utilisateur peut deja changer la structure
principale du navigateur depuis les reglages.
