# Molette Parametres, ScrollViewer explicite - 0.84.1.8-dev

## Contexte

L'utilisateur recadre la direction : Lumora doit se comporter comme une
application Windows normale. Concretement, le menu Demarrer reagit, mais le
panneau Parametres non. Il ne faut plus raisonner en contournements vagues,
mais en vraie surface scrollable native.

## Correctif applique

- `MainWindow.xaml`
  - le `ScrollViewer` de contenu du panneau Parametres recoit
    `x:Name="SettingsContentScrollViewer"`.
- `MainWindow.xaml.cs`
  - raccord explicite de ce `ScrollViewer` au fallback molette via
    `HookManualWheelScrollFallback(SettingsContentScrollViewer)`;
  - focus explicite du `ScrollViewer` des Parametres a l'ouverture du
    panneau via `FocusPanelScrollViewerIfNeeded(...)`;
  - `PointerEntered` du `ScrollViewer` rebranche aussi la logique de focus
    deja utilisee ailleurs.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~UsageModeVisualIdentityTests"`
  - reussi : 20/20 tests.

## Version

- Version courante : `0.84.1.8-dev`
- Regle respectee : increment du 4e chiffre uniquement.
