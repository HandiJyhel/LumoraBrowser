# 2026-07-23 - Recalage theme clair/sombre + nouvel installateur (0.84.0.13-dev)

## Demande

Passer en `0.84.0.13-dev`, corriger le manque de cohesion du theme clair avec
la chrome du haut, enlever l'impression "vert fonce" du theme sombre, puis
generer un nouvel installateur et supprimer l'ancien.

Note de version : la demande utilisateur contenait `0.84..0.13-dev` ; la
version appliquee suit la numerotation valide du projet :
`0.84.0.13-dev`.

## Corrections appliquees

- `Lumora.WinUI/MainWindow.SettingsTheme.cs`
  - recalage des surfaces clair/sombre vers une base plus nette ;
  - ajout de `SetChromeGradient()` pour que la barre de navigation adopte une
    vraie base claire en mode clair, au lieu de conserver un rendu sombre ;
  - refroidissement des palettes dark `neutral`, `focus`, `research` et
    `balanced` pour supprimer la dominante verte ;
  - palette cool Lumora decalee vers un cyan plus bleu.
- `Lumora.WinUI/LumoraTheme.cs`
  - alignement des fenetres secondaires sur la meme base ardoise/ivoire et le
    meme cyan Lumora.
- `Lumora.WinUI/MainWindow.xaml`
  - mise a jour des brosses et du gradient de depart pour rester coherents
    meme avant l'application dynamique du theme.
- montee de version en `0.84.0.13-dev` dans :
  - `MainWindow.xaml.cs`
  - `AGENTS.md`
  - `scripts/build-installer.ps1`
  - `scripts/build-clean-test-artifact.ps1`
  - `Lumora.Tests/UsageModeVisualIdentityTests.cs`

## Verification

- test cible passe :
  - `dotnet test Lumora.Tests\Lumora.Tests.csproj --filter UsageModeVisualIdentityTests`
  - resultat : `11/11` tests reussis
- artefact propre genere avec succes :
  - `artifacts/clean-test/Lumora-0.84.0.13-dev-win-x64-clean-20260723-194746`
- manifeste SHA256 artefact propre :
  - `artifacts/signatures/Lumora-0.84.0.13-dev-clean-20260723-194827.sha256`

## Installateur

- installateur genere avec succes :
  - `artifacts/installer/LumoraSetup-0.84.0.13-dev-win-x64.exe`
- manifeste SHA256 genere :
  - `artifacts/signatures/LumoraSetup-0.84.0.13-dev-20260723-194909.sha256`
- SHA256 installeur :
  - `b83e23319aa9baa9fddf72694ff723b61f2593cc0f2873da1266863ec6d86dd7`
- ancien installateur courant `0.84.0.12-dev` et ses fichiers associes
  supprimes apres generation du nouveau.

## Version

`0.84.0.13-dev`
