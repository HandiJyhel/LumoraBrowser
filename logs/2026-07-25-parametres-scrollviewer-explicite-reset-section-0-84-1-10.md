# Parametres, ScrollViewer explicite + reset par section - 0.84.1.10-dev

## Contexte

Retour utilisateur immediat apres `0.84.1.9-dev` : dans Parametres, l'ascenseur
restait percu comme non fonctionnel, donc la molette aussi. Le probleme n'etait
plus "la molette dans Lumora en general", mais bien le panneau Parametres comme
surface scrollable native.

## Correctif applique

- `MainWindow.xaml`
  - `SettingsContentScrollViewer` recoit maintenant des proprietes natives
    explicites :
    - `VerticalScrollMode="Enabled"`
    - `VerticalScrollBarVisibility="Auto"`
    - `HorizontalScrollMode="Disabled"`
    - `HorizontalScrollBarVisibility="Disabled"`
- `MainWindow.xaml.cs`
  - `SettingsNav_Click(...)` appelle desormais `ResetSettingsScrollPosition()`
    apres le changement de section ;
  - `ResetSettingsScrollPosition()` force un `UpdateLayout()`, remet la vue
    verticale a `0` via `ChangeView(...)` et rend le focus au
    `SettingsContentScrollViewer`.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~WheelScrollMathTests|FullyQualifiedName~UsageModeVisualIdentityTests"`
  - reussi : `24/24` tests.
- `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe Lumora.WinUI\\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /p:Restore=false`
  - reussi : build WinUI Debug produite dans `Lumora.WinUI\\bin\\x64\\Debug\\net8.0-windows10.0.19041.0\\win-x64\\`.

## Version

- Version courante : `0.84.1.10-dev`
- Regle respectee : increment du 4e chiffre uniquement.
