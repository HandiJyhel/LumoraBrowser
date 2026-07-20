# 2026-07-19 - Annonces actives du statut (`0.83.41-dev`)

## Objectif

Renforcer la couche accessibilite de Lumora sur les retours d'etat importants,
en allant au-dela d'un simple `LiveSetting="Polite"` sur la barre d'etat.

## Modifications

- `Lumora.WinUI/MainWindow.xaml.cs`
  - ajout de `UpdateStatusText(...)`
  - ajout d'une deduplication courte des annonces identiques
  - levee de `RaiseNotificationEvent(...)` pour les lecteurs d'ecran
  - `ShowPanel(...)` passe par ce helper
- `Lumora.WinUI/MainWindow.xaml`
  - `StatusText` recoit :
    - `AutomationProperties.Name="Etat Lumora"`
    - un `AutomationProperties.HelpText`
- flux metiers relies au helper :
  - `MainWindow.Settings.cs`
  - `MainWindow.VaultAccess.cs`
  - `MainWindow.Passkeys.cs`
  - `MainWindow.Wallet.cs`
  - `MainWindow.Sessions.cs`
  - `MainWindow.SiteControl.cs`
- versionnage et tests mis a jour :
  - `Lumora.Tests/AccessibilityRegressionTests.cs`
  - `Lumora.Tests/UsageModeVisualIdentityTests.cs`
  - `AGENTS.md`
  - `scripts/build-clean-test-artifact.ps1`
  - `scripts/build-installer.ps1`
  - `docs/PROCHAINES_ETAPES.md`

## Verification

- `dotnet test .\\Lumora.Tests\\Lumora.Tests.csproj --no-restore -p:BaseIntermediateOutputPath=artifacts\\tmp\\tests\\obj\\ -p:MSBuildProjectExtensionsPath=artifacts\\tmp\\tests\\obj\\`
  - reussi
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\build-winui.ps1`
  - reussi
  - `0 avertissement`
  - `0 erreur`

## Note

Une premiere tentative de build a echoue parce que certains appels referencaient
`AutomationNotificationKind` sans namespace complet dans les fichiers partiels.
Le raccord a ete corrige en visant explicitement
`Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind`.
