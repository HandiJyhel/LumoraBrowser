# 2026-07-23 - Glyphes du chrome clair (0.84.0.17-dev)

## Demande

L'utilisateur montre une nouvelle capture du theme clair et confirme que le
coeur du probleme n'est plus seulement le fond des boutons : plusieurs
glyphes de la barre haute restent trop pales et difficilement lisibles.

## Corrections appliquees

- `Lumora.WinUI/MainWindow.xaml`
  - ajout de styles dedies aux icones du chrome :
    `NovaChromeSymbolIconStyle`,
    `NovaChromeFontIconStyle`,
    `NovaChromeAccentSymbolIconStyle` ;
  - ajout de styles dedies aux icones des modules :
    `NovaModuleSymbolIconStyle`,
    `NovaModuleFontIconStyle` ;
  - application explicite de ces styles aux boutons critiques de la barre
    haute :
    retour, avance, rechargement, stop, ouverture d'adresse, bouclier,
    favoris, coffre, raccourcis modules, recherche assistee et menu Lumora.
- montee de version en `0.84.0.17-dev` dans :
  - `MainWindow.xaml.cs`
  - `AGENTS.md`
  - `scripts/build-installer.ps1`
  - `scripts/build-clean-test-artifact.ps1`
  - `Lumora.Tests/UsageModeVisualIdentityTests.cs`

## Verification

- test cible passe :
  - `dotnet test Lumora.Tests\Lumora.Tests.csproj --filter UsageModeVisualIdentityTests`
  - resultat : `11/11` tests reussis

## Livraison

- aucune regeneration d'artefact ou d'installateur sur cette passe ;
- cette iteration se concentre sur la lisibilite explicite des glyphes du
  chrome clair.

## Version

`0.84.0.17-dev`
