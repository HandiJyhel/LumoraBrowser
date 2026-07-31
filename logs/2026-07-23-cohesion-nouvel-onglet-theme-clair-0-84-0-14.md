# 2026-07-23 - Cohesion page Nouvel onglet en theme clair (0.84.0.14-dev)

## Demande

L'utilisateur montre une capture du theme clair : la chrome est claire, mais
la page Nouvel onglet reste sombre a l'interieur, avec des boutons et
raccourcis non coherents visuellement.

## Corrections appliquees

- `Lumora.WinUI/MainWindow.NewTabHome.cs`
  - ajout de `NewTabIsDarkTheme()` pour suivre le vrai theme actif ;
  - ajout de `NewTabThemeVariablesCss()` pour centraliser les surfaces,
    ombres, couleurs de champ, raccourcis et boutons en clair/sombre ;
  - `NewTabPalette()` rendue bi-theme ;
  - surfaces, bordures, backdrop et traces lumineuses du Nouvel onglet
    recalcules selon le theme clair ou sombre ;
  - remplacement d'une large partie des couleurs HTML/CSS codees en dur par
    des variables adaptees au theme actif.
- montee de version en `0.84.0.14-dev` dans :
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

- artefact propre genere avec succes :
  - `artifacts/clean-test/Lumora-0.84.0.14-dev-win-x64-clean-20260723-202123`
- manifeste SHA256 artefact propre :
  - `artifacts/signatures/Lumora-0.84.0.14-dev-clean-20260723-202203.sha256`
- installateur genere avec succes :
  - `artifacts/installer/LumoraSetup-0.84.0.14-dev-win-x64.exe`
- manifeste SHA256 installeur :
  - `artifacts/signatures/LumoraSetup-0.84.0.14-dev-20260723-202248.sha256`
- SHA256 installeur :
  - `6a1d86a5eceec55218ab08bc5565b44e2e63766268237ae55c0c157c54514e19`
- suppression de l'ancien `0.84.0.13-dev` partiellement reussie :
  - `LumoraSetup-0.84.0.13-dev-win-x64.VERIFICATION.txt` supprime
  - `LumoraSetup-0.84.0.13-dev-20260723-194909.sha256` supprime
  - `LumoraSetup-0.84.0.13-dev-win-x64.exe` encore verrouille par Windows sur
    cette passe

## Version

`0.84.0.14-dev`
