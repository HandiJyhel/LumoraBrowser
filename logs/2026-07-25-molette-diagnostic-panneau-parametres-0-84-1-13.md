# Molette : diagnostic racine du panneau Parametres - 0.84.1.13-dev

## Contexte

Le diagnostic bas niveau precedent (`0.84.1.12-dev`, sous-classement WndProc)
a ete teste par l'utilisateur en conditions reelles. Resultat lu dans
`winui-runtime-trace.log` :

- la molette produit un vrai defilement ailleurs dans l'app (offset qui
  oscille correctement entre 0 et 148 sur ~3 secondes, trace
  `Molette ScrollViewer : defilement applique via Grid/ContentPresenter`) ;
- sur `SettingsContentScrollViewer` precisement : **zero** ligne
  "Settings viewer raw wheel" pendant ~21 secondes de test, alors que le
  survol/focus y arrive bien (3 lignes "settings viewer getting focus") ;
- le hook WndProc bas niveau (`0.84.1.12-dev`) n'a rien trace du tout, meme
  pendant le defilement qui a fonctionne : les messages `WM_MOUSEWHEEL`/
  `WM_POINTERWHEEL` n'atteignent jamais le HWND de premier niveau qu'on a
  sous-classe (probablement intercepte par le HWND enfant de l'ilot XAML) -
  diagnostic sur le mauvais HWND, mais confirme que le probleme n'est pas
  "Windows ne delivre jamais la molette a Lumora".

**Conclusion tiree** : le focus/survol atteint bien `SettingsContentScrollViewer`,
mais l'evenement molette lui-meme n'y arrive jamais - ni en direct ni via
`handledEventsToo`. Quelque chose intercepte la molette avant qu'elle
n'atteigne cette zone precise, alors que ca marche ailleurs dans la meme
fenetre.

## Correctif applique (diagnostic pur, aucun changement de comportement)

- `MainWindow.xaml.cs`
  - `HookSettingsScrollDiagnostics()` ajoute un `AddHandler` sur
    `SettingsPanel` (le `Grid` racine du panneau, pas seulement le
    `ScrollViewer`), avec `handledEventsToo: true` ;
  - nouvelle methode `SettingsPanel_RootWheelDiagnostics(...)` : trace
    l'`OriginalSource`, `e.Handled`, le delta, et l'etat de visibilite de
    `BrowserPanel`/`BrowserHost` au moment de l'evenement.
  - Objectif : savoir si la molette entre ne serait-ce qu'une fois dans
    l'arbre XAML de Parametres. Si oui, un descendant intermediaire l'avale
    avant le `ScrollViewer` (bug XAML classique). Si non, autre chose la
    capte avant meme d'atteindre Parametres - hypothese principale : le
    WebView2 sous-jacent (`BrowserPanel`/`BrowserHost`, meme cellule de
    grille `Grid.Column="3"` que `SettingsPanel`) dont le HWND natif ne
    serait pas totalement neutralise quand son wrapper XAML est masque.
    Piste distincte de "l'airspace" deja ecartee le 2026-07-24 (qui
    concernait des Flyouts ouverts par-dessus la page, pas un panneau qui la
    remplace en place).
- Tests : `KeyboardFocusRegressionTests.cs` verrouille le hook et la methode.
  `UsageModeVisualIdentityTests.cs`, `AGENTS.md`, les scripts et
  `MainWindow.xaml.cs` passent a `0.84.1.13-dev` - **4e chiffre uniquement**.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~WheelScrollMathTests|FullyQualifiedName~UsageModeVisualIdentityTests"` :
  **reussi, 26/26 tests**.
- `MSBuild.exe Lumora.WinUI\\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /p:Restore=false` :
  **reussi**.

## Prochaine etape (pas encore faite)

Reproduire l'echec physique dans Parametres avec `LUMORA_TRACE_STARTUP=1`,
puis lire `winui-runtime-trace.log` :

- si `Settings panel root wheel (diagnostic pur)` apparait => la molette
  entre bien dans l'arbre XAML de Parametres, le probleme est un descendant
  qui l'avale avant le `ScrollViewer` ;
- si rien n'apparait => la molette n'atteint jamais l'arbre XAML de
  Parametres, et l'hypothese WebView2/HWND sous-jacent devient la piste
  principale a verifier ensuite.

## Version

- Version courante : `0.84.1.13-dev`
- Regle respectee : increment du 4e chiffre uniquement.
