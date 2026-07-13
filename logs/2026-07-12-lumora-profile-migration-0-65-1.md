# Lumora 0.65.1-dev - Correction migration profil Pulse/Nova vers Lumora

## Objectif

Corriger le demarrage apres renommage de dossier de donnees utilisateur : une
installation Lumora recente demandait de recreer un profil alors que les donnees
existaient encore dans un dossier renomme par l'utilisateur.

## Diagnostic

- La config active `%LOCALAPPDATA%\Lumora\config.json` pointait encore vers
  `E:\Documents\PulseBrowser\H.J`.
- Ce dossier n'existait plus.
- Le profil reel etait present dans `E:\Documents\LumoraBrowser\H.J`.
- Le dossier reel contenait les anciens fichiers `profile.pulse`,
  `vault.pulse`, `navigation\bookmarks.pulse`, `history.pulse`, etc.
- Lumora 0.65.0-dev cherchait les fichiers `.lumora`, puis `.nova`, mais pas
  les anciens `.pulse`. Des petits fichiers `.nova` crees pendant le mauvais
  demarrage pouvaient donc masquer les vrais fichiers `.pulse`.

## Changements

- `LumoraProfilePaths.DataFile` reutilise maintenant les fichiers `.pulse` quand
  aucun `.lumora` n'existe, avant de retomber sur `.nova`.
- `LumoraConfig.Load()` repare automatiquement un `CustomProfilePath` legacy
  introuvable en essayant les remplacements de segment `PulseBrowser` /
  `NovaBrowser` vers `LumoraBrowser` / `Lumora`, uniquement si le candidat
  ressemble a un vrai dossier profil.
- `LumoraFile` et `VaultStore` essaient aussi les entropies DPAPI legacy Pulse
  en secours, apres Lumora puis Nova.
- Ajout de tests pour la priorite `.pulse` et la reparation de chemin custom.
- Version runtime passee a `0.65.1-dev` dans `MainWindow.xaml.cs` ; `AGENTS.md`
  indiquait deja `0.65.1-dev`.

## Nettoyage installations

- Anciennes installations navigateur supprimees :
  - `%LOCALAPPDATA%\Programs\PulseBrowser`
  - `%LOCALAPPDATA%\Programs\NovaBrowser`
  - raccourcis Bureau `Pulse Browser.lnk` et `Nova Browser.lnk`
  - dossiers Menu Demarrer `Pulse Browser`, `Nova Browser` et `Pulse Apps`
  - cles uninstall HKCU `PulseBrowser` et `NovaBrowser`
- `PulseAuth` n'a pas ete touche : c'est une autre application.
- Les anciens installateurs Nova/Pulse ont ete supprimes de `artifacts\installer`.
- L'installation Lumora existante sous `%LOCALAPPDATA%\Programs\Lumora` a ete
  remplacee par l'artefact propre `0.65.1-dev`, sans toucher aux donnees
  utilisateur.
- `%LOCALAPPDATA%\Lumora\config.json` a ete repointe explicitement vers
  `E:\Documents\LumoraBrowser\H.J`.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 237/237 tests
  verts.
- `build-winui.cmd` : reussi, 0 avertissement, 0 erreur.
- `scripts\build-clean-test-artifact.ps1 -Version 0.65.1-dev -NoRestore` :
  reussi.
- Artifact propre :
  `artifacts\clean-test\Lumora-0.65.1-dev-win-x64-clean-20260712-234012`.
- Verification artifact : `Lumora.WinUI.exe`, `App.xbf`, `MainWindow.xbf`,
  `LumoraAppWindow.xbf`, `Lumora.WinUI.pri`, `Assets\LumoraApp.ico`,
  `Assets\LumoraApp.png`, `coreclr.dll`, `hostfxr.dll`, `hostpolicy.dll`
  presents.
- Installateur :
  `artifacts\installer\LumoraSetup-0.65.1-dev-win-x64.exe`.
- SHA256 installateur :
  `5c973e8fca01fff590b19601dfa97b992af73a2c9036377c424c3d231d43fc1c`.
- Verification finale : il ne reste dans les installations Windows que Lumora
  et PulseAuth ; les installations navigateur Pulse/Nova ont disparu.
- La config Lumora locale pointe vers `E:\Documents\LumoraBrowser\H.J`.

## A savoir

Le correctif est dans l'installateur genere et dans l'installation Lumora locale
remplacee manuellement par l'artefact propre de cette passe.
