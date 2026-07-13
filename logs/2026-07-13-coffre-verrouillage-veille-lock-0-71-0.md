# Coffre : verrouillage immediat a la veille et au verrouillage Windows - 0.71.0-dev

## Contexte

Suite de la consolidation du coffre demandee par l'utilisateur. Choix de la
brique : « verrouillage auto du coffre ». Constat en ouvrant le code : le
verrouillage auto par INACTIVITE existait deja (`SessionTimer_Tick`,
`SessionTimeoutMinutes`, defaut 10 min) et ne se declenche pas tant qu'un
onglet joue du son (`IsAnyTabPlayingAudio`) — c'est le « deja un peu effectif »
mentionne par l'utilisateur, et la garantie « une video n'est jamais coupee ».

Le vrai manque, par rapport a ce qui avait ete valide : le verrouillage
IMMEDIAT quand l'utilisateur quitte son poste (mise en veille, verrouillage de
la session Windows).

## Changements

- `MainWindow.Profile.cs` :
  - extraction de la logique de verrouillage (purge de la cle du coffre +
    ecran de re-login) dans `LockSessionNow(statusMessage)`, point d'entree
    commun au timer d'inactivite et aux evenements systeme ;
  - `SessionTimer_Tick` appelle desormais `LockSessionNow` (comportement
    inactivite inchange : toujours gate par l'audio) ;
  - `HookSystemLockEvents` / `UnhookSystemLockEvents` : abonnement a
    `Microsoft.Win32.SystemEvents.SessionSwitch` (SessionLock) et
    `PowerModeChanged` (Suspend). Les handlers marshalent vers le thread UI
    (`DispatcherQueue.TryEnqueue`) puis verrouillent SANS tenir compte de
    l'audio : l'utilisateur a quitte son poste, une video qui continue derriere
    l'ecran de verrouillage Windows ne doit rien empecher ;
  - le hook suit le meme interrupteur que le timer : `SessionTimeoutMinutes <= 0`
    (« Jamais ») desactive aussi le verrouillage veille/lock, pour un reglage
    unique et previsible.
- `MainWindow.xaml.cs` : champ `_systemLockHooked` ; desabonnement des
  `SystemEvents` a la fermeture de la fenetre (`Closed`) — indispensable, ces
  evenements gardent une reference statique forte (sinon fuite + crash au
  prochain verrouillage/veille).
- `Lumora.WinUI.csproj` : ajout du paquet Microsoft first-party
  `Microsoft.Win32.SystemEvents` 8.0.0 (l'assembly existe deja dans le runtime
  mais n'etait pas reference). Aucune donnee, purement local : ces evenements
  ne font qu'indiquer « la session Windows se verrouille / la machine s'endort ».

## Points connus

- La detection « video en cours » reste basee sur l'audio (`IsDocumentPlayingAudio`,
  seul signal fiable de WebView2). Une video en sourdine dans une fenetre non
  plein ecran laissee 10 min sans interaction peut donc declencher le
  verrouillage par inactivite — cas limite, sans regression par rapport a
  l'existant.
- Le verrouillage reste un verrouillage de SESSION complet (ecran de re-login),
  pas seulement des secrets : c'est le comportement historique, conserve tel
  quel. Un eventuel mode « soft » (navigation qui continue, seul le coffre se
  reverrouille) serait un changement de design distinct, a discuter.

## Verification

- Build Debug via MSBuild.exe (vswhere) : OK, 0 erreur (restore du nouveau
  paquet + build). `dotnet build` seul echoue toujours ici (MSB4062 PriGen,
  environnement).
- `dotnet test Lumora.Tests\Lumora.Tests.csproj` : 283/283 verts (aucune
  regression ; la logique de verrouillage vit dans la couche UI, non couverte
  par les tests purs).
- Artefact propre Release (0 avertissement, 0 erreur) :
  `artifacts\clean-test\Lumora-0.71.0-dev-win-x64-clean-20260713-172611`.
- SHA256 de `Lumora.WinUI.exe` :
  `4e33a6be6f14c86213f6b5cb99fb7e285b633d6cb7c0af30dc2dfa60aad9908f`.
- Contenu verifie : `Microsoft.Win32.SystemEvents.dll` present dans l'artefact
  autonome, chaines `0.71.0-dev` et `mise en veille` (UTF-16) dans
  `Lumora.WinUI.dll`.
- Manifeste artefact :
  `artifacts\signatures\Lumora-0.71.0-dev-clean-20260713-172636.sha256`.
- Installateur :
  `artifacts\installer\LumoraSetup-0.71.0-dev-win-x64.exe`.
- SHA256 installateur :
  `ae929b7eed6e5c9ac484505bf07229b8bade98f1481d3e34b678dc54957a5644`.
- Manifeste installateur :
  `artifacts\signatures\LumoraSetup-0.71.0-dev-20260713-173116.sha256`.
- Pas de validation manuelle interactive dans cette passe : a verifier par
  l'utilisateur (verrouiller Windows avec Win+L et mettre la machine en veille,
  coffre deverrouille -> ecran de re-login au retour ; une video avec son ne
  doit pas declencher le verrouillage par inactivite).

**Version :** `0.71.0-dev`.
