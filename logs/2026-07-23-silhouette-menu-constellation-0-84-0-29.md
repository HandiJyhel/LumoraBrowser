# 2026-07-23 - Silhouette Lumora, constellation favoris et menus signes

## Contexte

Nouvelle passe d'identite visuelle demandee pour pousser Lumora au-dela d'un
simple navigateur sombre/clair :
- silhouette generale encore trop proche des navigateurs existants ;
- favoris pas assez affirmes visuellement ;
- menus et context menus encore trop standards.

## Changements

1. Coque principale
- ajout de volumes distincts sur la barre haute pour mieux separer navigation,
  adresse et outils ;
- capsule d'adresse renforcee avec ligne lumineuse, badge Lumora et repere
  `local`.

2. Favoris et ergonomie modulable
- boutons de favoris agrandis et estimation de largeur adaptee ;
- rails haut et bas transformes en barres `constellation` avec signature visuelle ;
- rails lateraux des favoris et onglets verticaux presentes comme des cartes
  flottantes plus distinctives.

3. Menus
- `MenuFlyoutPresenter` restyle avec plus de rayon et une base Lumora ;
- menus principaux enrichis d'entetes et d'icones coherentes ;
- context menus des favoris et du Studio recontextualises avec des entetes
  Lumora.

## Fichiers touches

- `Lumora.WinUI/MainWindow.xaml`
- `Lumora.WinUI/MainWindow.Bookmarks.cs`
- `Lumora.WinUI/MainWindow.BookmarksFlyouts.cs`
- `Lumora.WinUI/MainWindow.LayoutStudio.cs`
- `Lumora.WinUI/MainWindow.xaml.cs`
- `AGENTS.md`
- `scripts/build-installer.ps1`
- `scripts/build-clean-test-artifact.ps1`
- `Lumora.Tests/UsageModeVisualIdentityTests.cs`
- `MEMORY.md`

## Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
- `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore --filter UsageModeVisualIdentityTests -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
