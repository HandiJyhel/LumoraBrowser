# Molette globale sur panneaux Lumora - 0.84.1.6-dev

## Contexte

Apres `0.84.1.5-dev`, l'utilisateur signale une regression franche :
la molette ne fonctionne plus nulle part, y compris dans le menu Demarrer
qui reagissait au moins en `0.84.1.4-dev`.

## Diagnostic

Le branchement direct ajoute sur chaque `ScrollViewer` etait trop agressif.
La piste retenue ensuite a ete plus simple :
- conserver le support deja utile sur les `Popup` / `Flyout` ;
- raccorder explicitement les grands panneaux natifs de Lumora eux-memes
  au fallback de molette ;
- ne plus essayer de brancher un handler direct sur chaque `ScrollViewer`
  du tree.

## Correctif applique

- `MainWindow.xaml.cs`
  - suppression du branchement direct `PointerWheelChanged` sur chaque
    `ScrollViewer` ;
  - ajout de `HookStaticPanelWheelFallbacks()` ;
  - les panneaux natifs (`SettingsPanel`, `HistoryPanel`, `ModulesPanel`,
    `VaultPanel`, `WalletPanel`, etc.) recoivent chacun
    `HookManualWheelScrollFallback(...)` ;
  - `ShowPanel(...)` reraccorde aussi explicitement le panneau rendu visible.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~UsageModeVisualIdentityTests"`
  - reussi : 18/18 tests.

## Version

- Version courante : `0.84.1.6-dev`
- Regle respectee : increment du 4e chiffre uniquement.
