# 2026-07-24 - Surfaces internes Lumora et overlays alignes

## Objectif

Continuer la passe identitaire jusqu'aux zones internes du navigateur pour
eviter qu'une fois sorti du chrome principal, Lumora retombe sur des panneaux
et overlays trop generiques.

## Modifications

- `Lumora.WinUI/MainWindow.xaml`
  - ajout de styles communs :
    - `NovaFlyoutHeroCardStyle`
    - `NovaFlyoutSectionCardStyle`
    - `NovaCommandPaletteCardStyle`
    - `NovaCommandPaletteSearchBoxStyle`
    - `NovaPanelShellCardStyle`
  - refonte de `CommandPaletteOverlay` avec halos, hero card, recherche plus
    marquee et resultats sous forme de cartes ;
  - harmonisation de `ModeCompanionFlyout`, `UsageModeFlyout` et
    `ProfileStatusFlyout` avec les memes cartes de hero et de section ;
  - restylage des panneaux internes :
    - `HistoryPanel`
    - `DownloadsPanel`
    - `SavedTabGroupsPanel`
    - `NotesPanel`
    - `WebAppsPanel`
  - passages progressifs vers un fond et des cartes plus coherents avec
    l'identite Lumora.

- `AGENTS.md`
- `Lumora.WinUI/MainWindow.xaml.cs`
- `scripts/build-installer.ps1`
- `scripts/build-clean-test-artifact.ps1`
- `Lumora.Tests/UsageModeVisualIdentityTests.cs`
  - alignement de la version `0.84.0.32-dev`.

## Verification

- Build WinUI :
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
  - resultat : succes, `0 avertissement`, `0 erreur`

- Tests :
  - `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore --filter UsageModeVisualIdentityTests -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
  - resultat : succes

## Livraison

- aucune generation d'installateur ;
- `MEMORY.md` mis a jour ;
- version courante : `0.84.0.32-dev`.
