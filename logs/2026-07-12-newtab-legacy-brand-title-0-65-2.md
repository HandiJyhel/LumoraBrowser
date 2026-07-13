# Lumora 0.65.2-dev - Migration du titre Pulse du nouvel onglet

## Objectif

Corriger un reste visible de renommage : a l'ouverture d'un nouvel onglet, le
nom affiche pouvait encore etre `Pulse` au lieu de `Lumora`.

## Diagnostic

Le code neuf avait bien `Lumora` comme titre par defaut, mais les profils
historiques peuvent conserver un `NewTabTitle` personnalise dans
`ui-settings.pulse`. Si cette valeur valait exactement `Pulse`, elle etait
reutilisee telle quelle par la page `lumora://accueil`.

## Changements

- Ajout de `BrandingText.NormalizeLegacyProductTitle`.
- Migration douce des anciens titres exacts `Pulse`, `Pulse Browser`, `Nova` et
  `Nova Browser` vers `Lumora`.
- La migration s'applique au chargement des reglages UI, a l'affichage du champ
  de parametres et au rendu HTML du nouvel onglet.
- Les titres personnalises differents ne sont pas modifies.
- Tests ajoutes dans `NewTabMarkupTests`.
- Version runtime passee a `0.65.2-dev`.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 241/241 tests
  verts.
- `build-winui.cmd` : reussi, 0 avertissement, 0 erreur.
- `scripts\build-clean-test-artifact.ps1 -Version 0.65.2-dev -NoRestore` :
  reussi.
- Artifact propre :
  `artifacts\clean-test\Lumora-0.65.2-dev-win-x64-clean-20260712-235244`.
- Verification artifact : `Lumora.WinUI.exe`, `App.xbf`, `MainWindow.xbf`,
  `LumoraAppWindow.xbf`, `Lumora.WinUI.pri`, `Assets\LumoraApp.ico`,
  `Assets\LumoraApp.png`, `coreclr.dll`, `hostfxr.dll`, `hostpolicy.dll`
  presents.
- Installateur :
  `artifacts\installer\LumoraSetup-0.65.2-dev-win-x64.exe`.
- SHA256 installateur :
  `2cd0586f005bac7754fc4f3ef37b499a3ed30a58c15f6d17156716057703adc4`.
- Installation locale `%LOCALAPPDATA%\Programs\Lumora` remplacee par l'artefact
  propre `0.65.2-dev`; l'entree uninstall HKCU indique `0.65.2-dev`.

## A savoir

Pas de validation visuelle interactive dans cette passe. Le correctif est valide
par test unitaire, build, artefact propre, installateur et remplacement de
l'installation locale.

