# Molette : diagnostic bas niveau WndProc avant un 9e patch XAML - 0.84.1.12-dev

## Contexte

Apres 8 correctifs successifs (`0.84.1.4-dev` a `0.84.1.11-dev`), tous cote
XAML (hooks globaux, panneaux, ScrollViewer direct, routage depuis les
enfants), l'utilisateur signale que la molette physique reste cassee dans
les panneaux natifs (ex. Parametres), malgre une confirmation independante
que le WebView2 (pages web) fonctionne bien. Un aller-retour avec un autre
assistant (GPT) n'a pas non plus resolu le probleme.

## Constat honnete

Tous les correctifs precedents ont ete valides par des tests source ou par
UI Automation (`ScrollPattern`), qui prouvent que le `ScrollViewer` sait
defiler programmatiquement, mais jamais par un geste physique reel de
molette. Aucun diagnostic n'avait jusqu'ici verifie si le message Windows
brut de la molette atteint seulement la fenetre Lumora pendant l'echec
signale. Avant un 9e patch XAML a l'aveugle, ce point devait etre etabli en
premier.

## Correctif applique (diagnostic, aucun changement de comportement)

- `MainWindow.WindowChrome.cs`
  - sous-classement du `WndProc` de la fenetre principale
    (`SetWindowLongPtr(hwnd, GWLP_WNDPROC, ...)`), en relais systematique et
    inconditionnel vers `CallWindowProc` : aucun message n'est intercepte ni
    modifie, seule une trace est ecrite au passage ;
  - trace ecrite via `WinUiRuntimeTrace.Write(...)` a chaque reception de
    `WM_MOUSEWHEEL` (souris classique, `0x020A`) ou `WM_POINTERWHEEL` (pile
    Pointer Windows, touchpads/peripheriques recents, `0x024E`), avec le
    delta brut ;
  - reutilisation de la declaration `SetWindowLongPtr` deja presente dans
    `MainWindow.SettingsTheme.cs` (`RemoveWindowLayeredAlpha`) plutot qu'une
    redeclaration en double (une classe partielle ne peut pas redefinir deux
    fois la meme signature - erreur CS0111 rencontree puis corrigee).
- `MainWindow.xaml.cs`
  - appel de `HookRawMouseWheelDiagnostics(hwnd)` juste apres l'obtention du
    handle de fenetre dans le constructeur.
- Tests
  - `KeyboardFocusRegressionTests.cs` verrouille la presence du hook, des
    constantes de message et du relais vers `CallWindowProc`.
  - `UsageModeVisualIdentityTests.cs`, `AGENTS.md`, les scripts de
    build/installer et `MainWindow.xaml.cs` passent a `0.84.1.12-dev` - **4e
    chiffre uniquement**.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~WheelScrollMathTests|FullyQualifiedName~UsageModeVisualIdentityTests"` :
  **reussi, 25/25 tests**.
- `MSBuild.exe Lumora.WinUI\\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /p:Restore=false` :
  **reussi** (apres correction du doublon `SetWindowLongPtr`).
- Suite complete (`dotnet test` sans filtre) : 651/652 reussis. Le seul echec
  (`AccessibilityComfortNamingTests.Le_texte_d_indication_de_la_recherche_suit_le_contraste_eleve`)
  est **anterieur a cette session** et sans rapport avec la molette : confirme
  via `git stash` que `MainWindow.NewTabHome.cs` porte deja des modifications
  non commitees d'une session precedente. Non traite ici, hors perimetre du Go
  donne (diagnostic molette uniquement).

## Prochaine etape (pas encore faite)

Ce correctif n'ajoute qu'une instrumentation en lecture seule : il ne change
rien au comportement de la molette. La prochaine session doit demander a
l'utilisateur de reproduire l'echec physique (molette dans Parametres) puis
lire `winui-runtime-trace.log` :

- si `WM_MOUSEWHEEL`/`WM_POINTERWHEEL` apparait dans la trace au moment du
  geste => le message brut arrive bien, le probleme est confirme cote XAML
  (probablement un controle interne qui avale l'evenement avant nos hooks) ;
- si rien n'apparait => le message n'atteint jamais la fenetre a ce niveau,
  et les 8 correctifs XAML precedents visaient un maillon qui n'etait pas en
  cause : il faudra chercher plus bas (focus reel de la fenetre, capture de
  pointeur ailleurs, etc.).

## Version

- Version courante : `0.84.1.12-dev`
- Regle respectee : increment du 4e chiffre uniquement.
