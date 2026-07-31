# Correctif demarrage WinUI apres ergonomie composable - 0.84.0.24-dev

## Contexte

Apres l'introduction de l'ergonomie composable pour les onglets et les favoris,
`run-winui.cmd` semblait ne plus rien ouvrir. Le build restait propre, mais
Lumora se fermait immediatement au lancement.

## Diagnostic

- `run-winui.cmd` et `scripts/run-winui.ps1` allaient jusqu'au bout sans erreur
  de build.
- Un lancement GUI court a montre que `Lumora.WinUI.exe` quittait aussitot avec
  le code `0xC000027B`.
- Avec `LUMORA_TRACE_STARTUP=1`, la trace a isole une `XamlParseException`
  pendant `InitializeComponent()` de `MainWindow`.
- La ligne incriminee etait `BookmarksBarSwitch` dans `MainWindow.xaml`.
- Cause retenue : `IsOn="True"` declenchait `BookmarksBarSwitch_Toggled`
  pendant le chargement XAML, alors que les nouvelles zones de favoris
  modulables n'etaient pas encore toutes pretes.

## Changements

- Suppression de `IsOn="True"` sur `BookmarksBarSwitch` dans
  `Lumora.WinUI/MainWindow.xaml`.
- Durcissement de `BookmarksBarSwitch_Toggled` dans
  `Lumora.WinUI/MainWindow.Bookmarks.cs` :
  le handler sort desormais immediatement pendant la phase
  `_suppressUiSettingsSave`.
- Montee de version source a `0.84.0.24-dev`.

## Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
  : succes, `0 avertissement`, `0 erreur`.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --filter UsageModeVisualIdentityTests`
  : succes.
- `run-winui.cmd` relance avec trace startup :
  `App constructor start` -> `MainWindow constructor end` -> processus vivant.

## Impact

Le symptome "plus rien ne demarre" ne venait pas du wrapper `run-winui.cmd`
mais d'un crash WinUI introduit par l'initialisation precoce du switch de
favoris. Le lancement normal de Lumora redevient fonctionnel sans revenir sur
l'ergonomie composable.
