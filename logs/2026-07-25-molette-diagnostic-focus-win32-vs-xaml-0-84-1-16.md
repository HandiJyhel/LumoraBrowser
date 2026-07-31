# Molette : diagnostic focus Win32 reel vs focus XAML logique - 0.84.1.16-dev

## Contexte

Test suivant `0.84.1.15-dev` : cette fois, **aucun** signal molette detecte
a aucun niveau (ni `Settings panel root wheel`, ni `WM_MOUSEWHEEL`/
`WM_POINTERWHEEL` bruts), alors qu'un test precedent en avait bien capte a
tous les niveaux. L'utilisateur confirme utiliser une vraie souris
(Logitech MX Master) et que la fenetre Lumora reste bien active (PowerShell
ne reste pas au premier plan, meme constat avec le lancement normal).
Hypothese Logitech Options ecartee par l'utilisateur a raison : la molette
fonctionne partout ailleurs, y compris dans PowerShell, un programme Win32
classique - un probleme de pilote se verrait aussi la.

## Piste retenue

Deux notions de "focus" distinctes existent dans une app WinUI 3 : le focus
Windows classique (quelle fenetre Win32 recoit reellement les entrees) et le
focus XAML logique (`FocusState`) manipule depuis le debut de cette serie de
correctifs. Rien jusqu'ici n'avait verifie le premier. Si Windows ne
considere pas la fenetre de Lumora comme active/focus au moment du geste,
aucun code applicatif (XAML ou notre hook bas niveau) ne peut recevoir la
molette, quelle que soit sa qualite.

## Correctif applique (diagnostic pur, aucun changement de comportement)

- `MainWindow.WindowChrome.cs`
  - ajout de `GetForegroundWindow()` et `GetFocus()` (P/Invoke user32.dll) ;
  - nouvelle methode `TraceWin32FocusState(reason)` : compare
    `GetForegroundWindow()` et `GetFocus()` au HWND principal de Lumora
    (stocke dans `_diagnosticsMainHwnd` lors du sous-classement WndProc de
    `0.84.1.12-dev`).
- `MainWindow.xaml.cs`
  - `ScrollViewer_PointerEntered` appelle `TraceWin32FocusState(...)` a
    chaque survol d'un ScrollViewer (independant de la molette, donc utile
    meme quand aucun signal molette n'arrive du tout) ;
  - `SettingsPanel_RootWheelDiagnostics` appelle aussi
    `TraceWin32FocusState(...)` a chaque signal molette recu par le panneau
    Parametres.
- Tests : `KeyboardFocusRegressionTests.cs` verrouille l'ajout.
  `UsageModeVisualIdentityTests.cs`, `AGENTS.md`, les scripts et
  `MainWindow.xaml.cs` passent a `0.84.1.16-dev` - **4e chiffre uniquement**.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~WheelScrollMathTests|FullyQualifiedName~UsageModeVisualIdentityTests"` :
  **reussi, 28/28 tests**.
- `MSBuild.exe Lumora.WinUI\\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /p:Restore=false` :
  **reussi**.

## Prochaine etape (pas encore faite)

Reproduire un test physique (idealement jusqu'a l'echec complet observe la
derniere fois) avec `LUMORA_TRACE_STARTUP=1`, puis lire
`winui-runtime-trace.log` : si `foregroundWindow` ou `focusWin32` ne
correspondent plus a `estLumora=True` au moment du blocage, la cause est
confirmee au niveau Windows (pas dans le code XAML de Lumora), et la
recherche devra porter sur pourquoi Lumora perd le focus/premier plan
Windows sans que ce soit visible a l'oeil.

## Version

- Version courante : `0.84.1.16-dev`
- Regle respectee : increment du 4e chiffre uniquement.
