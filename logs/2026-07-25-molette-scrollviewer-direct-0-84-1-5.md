# Molette globale, handler direct sur ScrollViewer - 0.84.1.5-dev

## Contexte

Apres `0.84.1.4-dev`, l'utilisateur confirme un progres reel mais incomplet :
la molette fonctionne dans le menu Demarrer, pas dans le panneau
Parametres. Le besoin est explicite : la molette doit fonctionner
**partout** dans Lumora.

## Diagnostic

Le correctif precedent fiabilisait surtout :
- le handler racine (`RootShell_PointerWheelChanged`) ;
- les arbres transitoires (`Popup`, `Flyout`, `ContextFlyout`).

Ce chemin suffisait pour les overlays, mais restait trop indirect pour
certains panneaux integres. Le `ScrollViewer` natif des Parametres avait
besoin d'un raccord local, pas seulement d'un filet de securite a la
racine.

## Correctif applique

- `MainWindow.xaml.cs`
  - ajout d'un set `_manualWheelHookedScrollViewers` ;
  - dans `AttachScrollViewerHoverFocus`, chaque `ScrollViewer` recoit
    desormais aussi un `AddHandler(UIElement.PointerWheelChangedEvent, ...)`
    avec `handledEventsToo: true` ;
  - nouvelle methode `ScrollViewer_PointerWheelChanged(...)` ;
  - logique commune extraite dans `TryApplyManualWheelScroll(...)`, reutilisee
    a la fois par le handler racine et par le handler direct du
    `ScrollViewer`.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~UsageModeVisualIdentityTests"`
  - reussi : 18/18 tests.

## Version

- Version courante : `0.84.1.5-dev`
- Regle respectee : increment du 4e chiffre uniquement.
