# Lumora 0.65.0-dev - Renommage identite, icone et installateur

## Objectif

Renommer l'identite active du navigateur de Nova Browser vers Lumora, sans
changer le nom du dossier racine du depot que l'utilisateur veut modifier lui
plus tard.

## Changements

- Renommage des projets actifs en `Lumora.WinUI`, `Lumora.Tests` et
  `Lumora.slnx`.
- Renommage des fichiers applicatifs principaux : `LumoraConfig`,
  `LumoraBackup`, `LumoraFile`, `LumoraAppWindow`.
- Regeneration des assets `LumoraApp.png` et `LumoraApp.ico`.
- Mise a jour de la page `lumora://accueil`, des libelles visibles, des menus,
  des options de personnalisation et des scripts de build/installation.
- Nouveaux profils par defaut sous `%LOCALAPPDATA%\Lumora`.
- Nouveaux fichiers de donnees en `.lumora`, avec lecture de secours des
  anciens fichiers `.nova` quand un profil existant en contient deja.
- Compatibilite conservee pour les anciennes configs `%LOCALAPPDATA%\NovaBrowser`
  et `%LOCALAPPDATA%\PulseBrowser`, ainsi que pour les anciennes entropies DPAPI
  Nova.
- Version source passee a `0.65.0-dev`.

## Verification

- `build-winui.cmd` : reussi hors sandbox apres blocage NuGet `NU1301` dans le
  sandbox, 0 avertissement, 0 erreur.
- `dotnet vstest Lumora.Tests\bin\Debug\net8.0-windows\Lumora.Tests.dll` :
  233/233 tests verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.65.0-dev -NoRestore` :
  reussi hors sandbox.
- Artifact propre :
  `artifacts\clean-test\Lumora-0.65.0-dev-win-x64-clean-20260712-232315`.
- Verification artifact : `Lumora.WinUI.exe`, `App.xbf`, `MainWindow.xbf`,
  `LumoraAppWindow.xbf`, `Lumora.WinUI.pri`, `Assets\LumoraApp.ico`,
  `Assets\LumoraApp.png`, `coreclr.dll`, `hostfxr.dll`, `hostpolicy.dll`
  presents.
- SHA256 `Lumora.WinUI.exe` :
  `01aa7368b91a8e063ceb572e212e1dc7e0ff17f911cf08e1e9a6de89860f6441`.
- Installateur :
  `artifacts\installer\LumoraSetup-0.65.0-dev-win-x64.exe`.
- SHA256 installateur :
  `06978e5bb24d28836def9f25c0f2ac4cddc5c0259998da5c7a395657864a1d89`.

## A savoir

Les seules mentions `NovaBrowser` restantes dans la surface active sont des
fils de compatibilite volontaires pour relire les anciens profils et fichiers
chiffres. Le lancement visuel interactif n'a pas ete effectue dans cette passe.
