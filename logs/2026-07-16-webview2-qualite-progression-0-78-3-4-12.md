# 2026-07-16 - GUID WebView2, qualite video et barre de progression (0.78.3.4.12-dev)

## Contexte

Retour de test reel de la 0.78.3.4.11-dev par l'utilisateur : l'installeur
n'a pas detecte un WebView2 deja present et a echoue apres tentative
d'installation ; le module video telecharge bien mais dans une qualite
plafonnee, sans indicateur de progression reel.

## Diagnostic

- `scripts/installer/Program.cs.template:693-694` : le GUID de detection du
  runtime WebView2 etait tronque
  (`{F3017226-FE2A-4295-8BDF-00C3A9C7}` au lieu de
  `{F3017226-FE2A-4295-8BDF-00C3A9C7C2BF}`), donc la cle de registre cherchee
  ne pouvait jamais exister : `IsWebView2RuntimeInstalled()` renvoyait
  toujours faux.
- `BuildYouTubeDownloadStartInfo` utilisait `-f best[ext=mp4]/best`, qui ne
  choisit que des formats deja fusionnes (plafonnes par YouTube autour de
  360-720p). La vraie meilleure qualite necessite de fusionner des flux
  video/audio separes via `ffmpeg`, absent du projet.
- Le telechargement lisait toute la sortie de yt-dlp d'un bloc
  (`ReadToEndAsync`) apres la fin du process : aucune mise a jour
  intermediaire possible.

## Changements

- Correction du GUID WebView2 (2 occurrences) dans
  `scripts/installer/Program.cs.template`.
- Ajout de `Lumora.WinUI/VideoDownload/FfmpegLocator.cs` : recherche read-only
  d'un ffmpeg local (variable d'env `LUMORA_FFMPEG_PATH`, dossier `tools` de
  l'appli, `%LOCALAPPDATA%\Lumora\tools`, `PATH`). Pas d'installation
  automatique pour l'instant (portee choisie avec l'utilisateur).
- `BuildYouTubeDownloadStartInfo` : si un ffmpeg local est trouve, utilise
  `-f bv*+ba/b --merge-output-format mp4 --ffmpeg-location <chemin>` (vraie
  meilleure qualite) ; sinon conserve `best[ext=mp4]/best` avec message clair
  sur la qualite limitee.
- Ajout de `Lumora.WinUI/VideoDownload/YtDlpProgress.cs` : parsing pur d'une
  ligne de progression yt-dlp (`--newline` ajoute a la commande pour forcer
  une ligne par mise a jour).
- `StartYouTubeDownloadAsync` : abonnement `OutputDataReceived` /
  `BeginOutputReadLine` au lieu d'une lecture bloquante ; chaque ligne de
  progression parsee alimente en direct l'entree existante de
  `DownloadHistoryEntry` via `WithProgress` (deja utilisee pour les
  telechargements WebView2 normaux, `MainWindow.History.cs`), ce qui anime la
  barre de progression du panneau Telechargements. Ajout d'une `ProgressBar`
  directement dans le flyout video (`MainWindow.xaml`), alimentee par le meme
  pourcentage.
- Tests : `YtDlpProgressTests.cs` (plusieurs formats de lignes, y compris
  taille inconnue et lignes non reconnues) et `FfmpegLocatorTests.cs` (meme
  esprit que les tests existants de `YtDlpEngineProvider`).

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 456/456 tests
  verts (10 nouveaux : 6 `YtDlpProgressTests` + 2 `FfmpegLocatorTests`, deja
  comptes avec les 2 existants de la 0.78.3.4.11-dev).
- `scripts\build-winui.ps1` : 0 avertissement / 0 erreur.
- Verification manuelle en conditions reelles (lancement de l'app, navigation
  YouTube, ouverture du flyout) : aucune regression, moteur et ffmpeg tous
  deux detectes sur la machine de test, message "Pret a telecharger cette
  video YouTube en meilleure qualite disponible." affiche correctement, barre
  de progression bien masquee au repos.
- Test reel d'un telechargement complet (pour valider que la barre se remplit
  et que la qualite est effectivement meilleure) laisse a l'utilisateur / a
  faire sur accord explicite, car il declenche un vrai telechargement reseau.

## Artefacts

- Artefact propre :
  `artifacts\clean-test\Lumora-0.78.3.4.12-dev-win-x64-clean-20260716-223911`
- SHA256 executable hote (lanceur natif, inchange comme d'habitude) :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
- Installateur : `artifacts\installer\LumoraSetup-0.78.3.4.12-dev-win-x64.exe`
- SHA256 installateur :
  `144a7752e2dbbe932ce9784eb6a9e2b0b2836aebcaeb38a6365ceccea1008a63`
