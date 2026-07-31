# 2026-07-23 - Overlay PIN opaque et etoiles Lumora

## Contexte

Retour utilisateur sur la nouvelle entree PIN Lumora :
- l'ecran principal restait visible derriere l'overlay de connexion ;
- le pave numerique manquait encore de tenue visuelle ;
- l'indicateur du code PIN devait evoquer davantage la lumiere, avec des
  etoiles plutot que des points classiques.

## Changements

1. `LoginOverlay`
- fond passe sur `NovaAppBackgroundBrush` pour masquer completement le contenu
  du navigateur pendant l'authentification.

2. `NovaPinDigitButtonStyle`
- recentrage explicite horizontal et vertical du contenu des boutons afin
  d'eviter l'effet de decalage visuel des chiffres.

3. `PinDotsDisplay`
- passage des etats PIN vides/remplis a `✧` et `✦` ;
- reinitialisation du tampon PIN avec la nouvelle grammaire visuelle.

## Fichiers touches

- `Lumora.WinUI/MainWindow.xaml`
- `Lumora.WinUI/MainWindow.Profile.cs`
- `Lumora.WinUI/MainWindow.xaml.cs`
- `AGENTS.md`
- `scripts/build-installer.ps1`
- `scripts/build-clean-test-artifact.ps1`
- `Lumora.Tests/UsageModeVisualIdentityTests.cs`
- `MEMORY.md`

## Verification prevue

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
- `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore --filter UsageModeVisualIdentityTests -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
