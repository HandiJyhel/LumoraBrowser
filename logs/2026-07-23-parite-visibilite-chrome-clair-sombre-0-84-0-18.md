# 2026-07-23 - Parite de visibilite chrome clair/sombre (0.84.0.18-dev)

## Demande

L'utilisateur rappelle la regle produit de base : en theme clair comme en
theme sombre, la qualite de visibilite doit etre equivalente. Le cas le plus
evident reste l'icone favoris, encore trop faible en theme clair hors etat
actif.

## Corrections appliquees

- `Lumora.WinUI/MainWindow.Bookmarks.cs`
  - suppression de la logique `_bookmarkStarDefaultForeground` ;
  - l'etoile non active utilise maintenant directement
    `NovaChromeButtonForegroundBrush` ;
  - l'etoile active continue d'utiliser `NovaAccentBrush`.
- `Lumora.WinUI/MainWindow.SettingsTheme.cs`
  - assombrissement leger des couleurs d'icones du chrome en theme clair ;
  - assombrissement leger des couleurs d'icones modules en theme clair ;
  - assombrissement leger de `NovaModuleHubNodeBrush` pour que le bouton
    modules reste lisible avec la meme qualite qu'en sombre.
- `Lumora.WinUI/MainWindow.xaml`
  - opacites des carreaux neutres du bouton modules relevees pour mieux tenir
    en theme clair.
- montee de version en `0.84.0.18-dev` dans :
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
- cette iteration se concentre sur la parite de visibilite entre theme clair
  et theme sombre dans le chrome.

## Version

`0.84.0.18-dev`
