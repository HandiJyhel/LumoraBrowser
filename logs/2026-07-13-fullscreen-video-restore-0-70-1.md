# Restauration plein ecran video - 0.70.1-dev

## Contexte

L'utilisateur a fourni trois captures montrant le scenario suivant :

1. Une video YouTube passe en plein ecran contenu.
2. A la sortie du plein ecran video, Lumora reste en plein ecran/immersif.
3. Quand l'utilisateur quitte ensuite le plein ecran navigateur, l'interface
   revient partiellement seulement : il manque des options et ouvrir un nouvel
   onglet ne restaure pas le chrome complet.

## Diagnostic

Deux problemes concrets ont ete identifies dans la logique WinUI :

- `ApplyFullScreenLayout()` mettait `NavigationRow.Height = 0` en mode
  immersif, mais la branche de sortie ne remettait pas explicitement la hauteur
  normale de la barre de navigation.
- La restauration du plein ecran contenu pouvait conserver `FullScreen` si le
  presenter Windows avait deja bascule en plein ecran au moment du snapshot,
  meme si l'utilisateur n'etait pas en plein ecran Lumora avant la video.

## Changements

- Ajout de `_wasLumoraFullScreenBeforeContentFullScreen` pour distinguer le
  plein ecran Lumora volontaire du plein ecran demande par une page web.
- A la sortie du plein ecran contenu, Lumora force le retour en presenter
  `Overlapped` si le navigateur n'etait pas deja en plein ecran Lumora.
- Centralisation de la sortie du plein ecran contenu dans
  `CompleteContentFullScreenExit`.
- La sortie signalee par le script JS `fullscreenchange` est traitee comme un
  signal fiable, sans attendre que `CoreWebView2.ContainsFullScreenElement`
  soit deja revenu a `false`.
- `ApplyFullScreenLayout()` restaure explicitement `NavigationRow`, la toolbar,
  les onglets, les colonnes du rail vertical, les paddings et arrete les timers
  de masquage immersif.
- La fermeture d'un onglet en plein ecran contenu utilise la meme routine de
  restauration.

## Verification

- Premier `cmd /c .\build-winui.cmd` sous sandbox : echec attendu par blocage
  reseau NuGet (`NU1301`) et acces refuse sur `obj`.
- `cmd /c .\build-winui.cmd` hors sandbox : reussi avec 0 avertissement et
  0 erreur.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` :
  276/276 tests verts.
- Artefact propre :
  `artifacts\clean-test\Lumora-0.70.1-dev-win-x64-clean-20260713-130000`.
- SHA256 de `Lumora.WinUI.exe` :
  `01aa7368b91a8e063ceb572e212e1dc7e0ff17f911cf08e1e9a6de89860f6441`.
- Fichiers essentiels verifies dans l'artefact : `Lumora.WinUI.exe`,
  `App.xbf`, `MainWindow.xbf`, `LumoraAppWindow.xbf`,
  `LumoraPrivateWindow.xbf`, `Lumora.WinUI.pri`, `coreclr.dll`,
  `hostfxr.dll`, `hostpolicy.dll`.
- Installateur :
  `artifacts\installer\LumoraSetup-0.70.1-dev-win-x64.exe`.
- SHA256 installateur :
  `e79423e74a5d0ca0583f0b4553a1b1a69702d4abe8287cec2b69850ed7673ceb`.
- Fichier de verification :
  `artifacts\installer\LumoraSetup-0.70.1-dev-win-x64.VERIFICATION.txt`.
- Manifeste :
  `artifacts\signatures\LumoraSetup-0.70.1-dev-20260713-130127.sha256`.

Pas de validation manuelle interactive du scenario YouTube plein ecran dans
cette passe ; la correction a ete verifiee par build, tests, artefact propre et
installateur.

**Version :** `0.70.1-dev`.
