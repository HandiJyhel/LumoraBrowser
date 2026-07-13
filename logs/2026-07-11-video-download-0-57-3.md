# 2026-07-11 - Pulse Browser 0.57.3-dev

## Objectif

Ajouter le petit module demande par l'utilisateur pour telecharger une video YouTube a la demande, directement depuis Pulse Browser.

## Changements

- Ajout d'un bouton `Telecharger la video` dans la barre d'outils, a cote du Picture-in-Picture et du menu Pulse.
- Ajout d'un flyout natif Pulse qui analyse l'onglet actif, detecte une page video YouTube et affiche le titre de la video.
- Ajout de `MainWindow.VideoDownload.cs`, module isole qui :
  - extrait l'ID video YouTube depuis `youtube.com/watch?v=...` ou `youtu.be/...` ;
  - lit le titre de la page via WebView2 quand c'est possible ;
  - cherche un moteur local `yt-dlp.exe` dans `PULSE_BROWSER_YTDLP_PATH`, `AppContext.BaseDirectory\tools`, `%LOCALAPPDATA%\PulseBrowser\tools`, puis dans le `PATH` ;
  - lance le telechargement sur clic explicite de l'utilisateur, jamais automatiquement ;
  - enregistre l'etat du telechargement dans l'historique local des telechargements Pulse.
- Le module telecharge dans le dossier Windows `Downloads` avec un nom base sur le titre YouTube et l'ID video.
- L'entree `Telecharger la video` est aussi disponible dans `Menu Pulse > Outils`.

## Limite technique assumee

- Pulse Browser ne telecharge pas silencieusement un moteur externe et n'embarque pas de contournement maison fragile. Pour YouTube, le telechargement robuste passe par un moteur local specialise (`yt-dlp.exe`) detecte par Pulse.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 174/174 tests verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.57.3-dev` : reussi, 0 avertissement/erreur apres autorisation reseau NuGet.
- `scripts\build-installer.ps1 -Version 0.57.3-dev` : reussi apres autorisation reseau NuGet.

## Artefacts

- Artefact propre : `artifacts\clean-test\PulseBrowser-0.57.3-dev-win-x64-clean-20260711-020040`
- SHA256 executable hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`
- Installateur : `artifacts\installer\PulseBrowserSetup-0.57.3-dev-win-x64.exe`
- SHA256 installateur : `f9a6c7a25dec340aae0b70c6bd52ef723048f874bf7c2d8e4d334b169d520d90`
