# 2026-07-17 - Qualite video reelle et bug « fichier introuvable » (0.78.3.4.17-dev)

## Contexte

Retour utilisateur apres la 0.78.3.4.12-dev : le module de telechargement
video n'offre toujours aucun choix reel de qualite (1080p/720p), et un
telechargement termine avec succes (exit code 0, video bien accessible sur
YouTube) s'est retrouve marque « fichier introuvable » dans le panneau
Telechargements.

## Diagnostic

- Aucun bug cote reseau/yt-dlp : le telechargement reussit reellement, la
  video existe bien sur le disque apres coup.
- `MainWindow.VideoDownload.cs` (`FindDownloadedVideo`) filtrait les fichiers
  du dossier `Downloads` par `LastWriteTime >= debut_telechargement - 5s`.
  Or **yt-dlp regle par defaut la date de modification du fichier ecrit sur
  le `Last-Modified`/date d'upload de la video** (comportement historique de
  youtube-dl, toujours actif par defaut dans yt-dlp), pas sur l'heure reelle
  du telechargement. Pour une video plus vieille que quelques minutes, ce
  filtre echoue systematiquement.
- Le repli `ExtractLastExistingPath` ne cherchait que des lignes
  `"Destination: "`, qui pointent vers les fichiers video-only/audio-only
  intermediaires supprimes par yt-dlp apres la fusion ffmpeg — jamais vers la
  ligne `[Merger] Merging formats into "..."` qui contient le vrai chemin
  final. Ce repli echouait donc lui aussi apres une fusion.
- Consequence : les deux mecanismes de detection echouaient, `LocalPath`
  retombait sur le dossier `Downloads` lui-meme (pas un fichier) ->
  `File.Exists` renvoie faux pour toujours -> « fichier introuvable » affiche
  a l'utilisateur alors que le fichier existe reellement sur le disque.
- Cote fonctionnalite : aucun selecteur de qualite n'existait dans le
  flyout (`MainWindow.xaml`) — le module choisissait toujours "meilleure
  qualite disponible" sans jamais laisser un choix explicite 1080p/720p,
  malgre la demande initiale.

## Changements

- `Lumora.WinUI/VideoDownload/VideoDownloadFormat.cs` (nouveau) : construction
  pure du selecteur de format yt-dlp (`-f`) pour une qualite demandee
  (Meilleure qualite / 1080p / 720p / 480p), avec ou sans ffmpeg local. Le
  filtre `height<=X` laisse yt-dlp se rabattre automatiquement sur la
  resolution disponible la plus proche si la video n'existe pas dans la
  qualite demandee.
- `Lumora.WinUI/VideoDownload/YtDlpOutputParser.cs` (nouveau) : extraction
  pure des chemins candidats depuis la sortie yt-dlp, priorisant la ligne de
  fusion `[Merger] Merging formats into "..."` avant les lignes
  `"Destination: "` (repli pour un telechargement direct sans fusion).
- `BuildYouTubeDownloadStartInfo` : ajout de `--no-mtime` (le fichier final
  recoit desormais l'heure reelle du telechargement, ce qui repare la
  detection par date) et branchement sur `VideoDownloadFormat` selon la
  qualite choisie.
- `MainWindow.xaml` : nouveau `ComboBox` "Qualite" dans le flyout de
  telechargement (Meilleure qualite disponible / 1080p / 720p / 480p),
  desactive tant que le moteur yt-dlp n'est pas detecte.
- `MainWindow.VideoDownload.cs` : nouveau champ `_videoDownloadQuality`,
  gestionnaire `VideoDownloadQualityCombo_SelectionChanged`, quality
  propagee jusqu'a `StartYouTubeDownloadAsync` et au message de statut.
- Tests : `VideoDownloadFormatTests.cs` (6 cas, selecteurs + libelles) et
  `YtDlpOutputParserTests.cs` (4 cas : priorite fusion, repli destination,
  fins de ligne CRLF, sortie sans chemin).

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 469/469 tests
  verts (13 nouveaux).
- `scripts\build-winui.ps1` : 0 avertissement / 0 erreur.
- Verification manuelle en conditions reelles (build MSBuild Release, profil
  jetable, mode invite, pilotage UIA) : navigation vers une page YouTube
  reelle, ouverture du flyout, ComboBox "Qualite" present avec exactement 4
  options (Meilleure qualite disponible / 1080p / 720p / 480p), desactive
  puis active une fois le moteur yt-dlp detecte, texte de statut mis a jour
  correctement, capture d'ecran du menu deroulant confirmant un rendu propre
  sans chevauchement. Aucun clic sur "Telecharger" pendant cette verification
  (pas de declenchement d'un vrai telechargement reseau automatise).
- Test reel d'un telechargement complet (pour confirmer que le fichier
  n'est plus marque « introuvable » et que la qualite choisie est bien
  respectee) laisse a l'utilisateur.

## Artefacts

- Artefact propre :
  `artifacts\clean-test\Lumora-0.78.3.4.17-dev-win-x64-clean-20260717-015146`
- SHA256 executable hote (lanceur natif, inchange comme d'habitude) :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
- Installateur : `artifacts\installer\LumoraSetup-0.78.3.4.17-dev-win-x64.exe`
- SHA256 installateur :
  `b81db26f2a3e67eaee01184238f0a5a69f0554d78ab4aace0d3e7c47ad41190a`
