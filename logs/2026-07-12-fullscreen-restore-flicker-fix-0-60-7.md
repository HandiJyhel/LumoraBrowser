# 2026-07-12 - Deux correctifs plein ecran video (0.60.7-dev)

## Contexte

Apres avoir teste l'installateur `0.60.6-dev` (build repris et termine par Claude
Code suite au blocage NuGet de Codex, voir log 0.60.6), l'utilisateur a rapporte
deux bugs persistants :

1. En quittant le plein ecran d'une video, la fenetre Pulse reste en plein
   ecran au lieu de revenir a son etat precedent.
2. En survolant la barre superieure immersive (mode plein ecran), elle
   clignote au lieu de rester affichee.

## Diagnostic

Relecture complete de la logique 0.60.6 (`RestorePresenterAfterContentFullScreen`,
`BrowserCore_ContainsFullScreenElementChanged`) : la logique de restauration
est correcte sur le papier et n'a pas pu etre prise en defaut par lecture de
code seule. Le point de defaillance le plus probable est que l'evenement
WinRT natif `CoreWebView2.ContainsFullScreenElementChanged` ne se declenche
pas de facon fiable a la sortie du plein ecran dans ce runtime WebView2 —
un comportement documente comme capricieux sur certaines versions. Aucun
outil de pilotage UI n'etait disponible dans cette session pour observer le
comportement reel et confirmer la cause exacte.

Pour le clignotement : `FullScreenTopRevealZone` (bande de survol 10px) reste
visible en permanence sous `FullScreenTopBar` (44px, ZIndex superieur) une
fois celle-ci affichee. Si le pointeur ne bouge pas apres l'apparition de la
barre, WinUI ne reassigne pas immediatement le `PointerEntered`/`PointerExited`
entre les deux elements superposes, et le `Collapsed` instantane au premier
`PointerExited` fait disparaitre la barre avant que le survol reel ne soit
confirme dessus — d'ou le clignotement. Meme structure pour le rail d'onglets
verticaux (`FullScreenLeftRevealZone` / `VerticalTabsRail`), qui n'avait
d'ailleurs pas de `PointerEntered` cable du tout sur le rail lui-meme.

## Changements

- Ajout d'un signal de sortie de plein ecran redondant, independant de
  l'evenement WinRT : un listener JS `fullscreenchange` est injecte sur
  chaque page (`RegisterFullScreenExitMonitorAsync`) et previent le C# par
  `postMessage` quand `document.fullscreenElement` devient null. Traite par
  `HandleContentFullScreenExitSignal`, strictement idempotent avec le chemin
  existant (ReferenceEquals sur le coeur WebView2 courant).
- Masquage differe (350 ms, `DispatcherTimer`) des barres immersives au lieu
  d'un `Collapsed` instantane sur `PointerExited`, pour la barre superieure
  et le rail d'onglets verticaux. Tout `PointerEntered` (zone de survol ou
  barre elle-meme) annule le minuteur en attente.
- Ajout du `PointerEntered` manquant sur `VerticalTabsRail` pour annuler un
  masquage en attente en cas de re-survol direct du rail.
- Passage de version source a `0.60.7-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` : 233 tests
  reussis (aucun test ne couvre directement le comportement WinUI/WebView2).
- `scripts\build-clean-test-artifact.ps1 -Version 0.60.7-dev` : build MSBuild
  reussi, 0 avertissement, 0 erreur.
- Artefact propre :
  `artifacts\clean-test\PulseBrowser-0.60.7-dev-win-x64-clean-20260712-151018`.
- SHA256 exe hote (identique aux versions precedentes : l'exe est un simple
  apphost, la logique reelle est dans les DLL) :
  `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- `scripts\build-installer.ps1 -Version 0.60.7-dev` : installateur genere.
- Installateur :
  `artifacts\installer\PulseBrowserSetup-0.60.7-dev-win-x64.exe`.
- SHA256 installateur :
  `ee1049af34e1c524a0a5cbabb9b4a4621794af7e89f08429dbeaaf5e0964aef3`.

## Limite

- Non teste manuellement par l'IA : aucun outil de pilotage UI (souris/
  clavier reel sur une fenetre WinUI) n'est disponible dans cette session.
  Le correctif du clignotement repose sur une explication plausible et
  standard (hysteresis contre les evenements de survol instables), mais n'a
  pas ete observe en conditions reelles. Le signal JS redondant pour la
  sortie de plein ecran est une mesure de robustesse qui ne resoudra le
  probleme QUE si la cause est bien la non-fiabilite de l'evenement WinRT ;
  si la fenetre reste plein ecran pour une autre raison (ex. presenter Windows
  qui ignore silencieusement `SetPresenter(Overlapped)` juste apres
  `FullScreen`), ce correctif ne suffira pas.
- A verifier par l'utilisateur : la sortie du plein ecran video restaure bien
  la fenetre, ET la barre superieure reste stable au survol.
