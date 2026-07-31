# Molette native WinUI, popups et flyouts - 0.84.1.4-dev

## Contexte

L'utilisateur a confirme un symptome stable : la molette fonctionne dans les
pages web (WebView2), mais pas dans les surfaces natives Lumora
(`ScrollViewer`, panneaux internes, menus, flyouts).

## Correctif applique

- `MainWindow.xaml.cs`
  - le module manuel de molette ne s'appuie plus uniquement sur
    `FindElementsInHostCoordinates` ;
  - il remonte d'abord depuis `e.OriginalSource` jusqu'au `ScrollViewer`
    scrollable reel, puis garde le hit-test comme repli ;
  - les hooks de focus/molette sont maintenant etendus aux arbres
    transitoires WinUI (`Popup`, `Flyout`, `ContextFlyout`) ;
  - les popups ouverts sont recuperes via
    `VisualTreeHelper.GetOpenPopupsForXamlRoot(...)` pour raccorder leur
    `Child` au meme mecanisme de molette.
- Flyouts crees en code : raccord explicite via
  `HookFlyoutPointerSupport(flyout)` dans
  `MainWindow.Bookmarks.cs`, `MainWindow.BookmarksFlyouts.cs`,
  `MainWindow.History.cs`, `MainWindow.LayoutStudio.cs`,
  `MainWindow.TabGroups.cs` et `MainWindow.Wallet.cs`.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~UsageModeVisualIdentityTests"`
  - reussi : 17/17 tests.
- `dotnet build Lumora.WinUI\\Lumora.WinUI.csproj --no-restore`
  - bloque par l'environnement local, pas par le patch :
    `Microsoft.Build.Packaging.Pri.Tasks.dll` introuvable dans le SDK/MSBuild
    WinUI utilise par cette machine.

## Version

- Version courante : `0.84.1.4-dev`
- Regle respectee : increment du 4e chiffre uniquement.
