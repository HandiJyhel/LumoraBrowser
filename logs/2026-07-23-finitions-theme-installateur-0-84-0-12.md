# 2026-07-23 - Finitions theme + nouvel installateur (0.84.0.12-dev)

## Demande

Passer en `0.84.0.12-dev`, ajouter les dernieres corrections de coherence
visuelle, puis supprimer l'installateur courant et generer un nouvel
installateur.

## Corrections appliquees

- `Lumora.WinUI/App.xaml`
  - ajout de brosses semantiques Lumora pour succes / avertissement / danger ;
  - ajout de styles implicites pour `TextBox`, `PasswordBox`, `ComboBox`,
    `ToggleSwitch` et `InfoBar`, afin de sortir les derniers controles du rendu
    WinUI brut.
- `Lumora.WinUI/LumoraTheme.cs`
  - synchronisation des nouvelles brosses semantiques via
    `ApplySharedAppBrushes()` ;
  - ajout d'un helper `TintSurface()` pour deriver des surfaces teintees selon
    le theme actif.
- `Lumora.WinUI/MainWindow.xaml`
  - remplacement de fonds systeme restants par `NovaWarningSurfaceBrush`,
    `NovaSuccessSurfaceBrush` et `NovaDangerBrush` sur les zones sensibles.
- montee de version en `0.84.0.12-dev` dans :
  - `MainWindow.xaml.cs`
  - `AGENTS.md`
  - `scripts/build-installer.ps1`
  - `scripts/build-clean-test-artifact.ps1`
  - `Lumora.Tests/UsageModeVisualIdentityTests.cs`

## Build / installateur

- artefact propre genere avec succes :
  - `artifacts/clean-test/Lumora-0.84.0.12-dev-win-x64-clean-20260723-...`
- installateur genere avec succes :
  - `artifacts/installer/LumoraSetup-0.84.0.12-dev-win-x64.exe`
- manifeste SHA256 genere :
  - `artifacts/signatures/LumoraSetup-0.84.0.12-dev-20260723-191007.sha256`
- SHA256 installeur :
  - `f06a99342d7452cf50080f7b65d2a1a25ae0003b146cf40da36b43128e0935dc`
- verification cible passee :
  - `dotnet test Lumora.Tests\Lumora.Tests.csproj --filter UsageModeVisualIdentityTests`
  - resultat : `11/11` tests reussis
- ancien installateur courant `0.84.0.11-dev` et ses fichiers associes
  supprimes apres generation du nouveau.

## Version

`0.84.0.12-dev`
