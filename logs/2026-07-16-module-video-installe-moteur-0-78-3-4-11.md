# 2026-07-16 - Installation a la demande du moteur yt-dlp (0.78.3.4.11-dev)

## Contexte

Le module « Telecharger la video » detectait bien les pages YouTube, mais le
bouton `Telecharger` restait desactive des qu'aucun `yt-dlp.exe` n'etait
present sur la machine (ni dans `%LOCALAPPDATA%\Lumora\tools`, ni dans le
`PATH`, ni dans le dossier de l'application). Le module etait concu depuis
0.57.3-dev pour ne jamais aller chercher ce moteur tout seul, ce qui laissait
l'utilisateur sans solution simple — et ce serait pareil pour n'importe quel
autre utilisateur de Lumora.

## Changements

- Ajout de `Lumora.WinUI/VideoDownload/YtDlpEngineProvider.cs` :
  - `FindLocalEngine()` reprend inchange l'ordre de recherche existant
    (`LUMORA_YTDLP_PATH`, dossier `tools` de l'appli, `%LOCALAPPDATA%\Lumora\tools`,
    `PATH`) ;
  - `DownloadEngineAsync(...)` telecharge `yt-dlp.exe` depuis la release
    GitHub officielle (`github.com/yt-dlp/yt-dlp`, open source, licence
    Unlicense), verifie son SHA256 face au fichier `SHA2-256SUMS` publie sur
    le meme release, puis installe le binaire verifie dans
    `%LOCALAPPDATA%\Lumora\tools`. Ecriture atomique via fichier temporaire
    `.part` + `File.Move`.
- `MainWindow.VideoDownload.cs` : nouveau bouton « Installer le moteur yt-dlp
  (open source) » dans le flyout, visible uniquement quand le moteur est
  absent. Le clic est explicite (jamais de telechargement automatique), avec
  retour de progression et gestion d'erreur claire (reseau, hash invalide).
  Apres succes, le bouton `Telecharger` s'active automatiquement.
- `MainWindow.xaml` : ajout du bouton `VideoDownloadInstallEngineButton` dans
  `VideoDownloadFlyout`.
- Les chemins manuels existants restent utilisables tels quels pour les
  utilisateurs avances (pas de regression).
- `Lumora.Tests/YtDlpEngineProviderTests.cs` : tests unitaires de la logique
  pure de parsing du hash (`ParseExpectedHash`), sans toucher au reseau —
  meme principe que `NetworkBlockerModuleTests` pour `FilterListManager`.

## Limite assumee

Le telechargement du moteur necessite un acces reseau vers GitHub, uniquement
sur clic explicite de l'utilisateur. Aucune donnee utilisateur n'est envoyee :
c'est un GET pur vers une release publique open source, avec verification
d'integrite avant toute ecriture/execution du binaire.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 448/448 tests
  verts (6 nouveaux tests pour `YtDlpEngineProviderTests`).
- `scripts\build-winui.ps1` : build complet de `Lumora.WinUI` reussi, 0
  avertissement / 0 erreur (XAML + code-behind compiles).
- Verification manuelle en conditions reelles faite (lancement de l'app,
  navigation vers une vraie page YouTube, ouverture du flyout, controle UIA +
  capture d'ecran) : titre detecte, texte moteur absent, bouton
  d'installation visible et actif, bouton `Telecharger` desactive, texte de
  statut correct. Le clic reel sur le bouton d'installation (telechargement
  GitHub effectif) n'a pas ete fait, en attente d'accord explicite.

## Artefacts

- Artefact propre :
  `artifacts\clean-test\Lumora-0.78.3.4.11-dev-win-x64-clean-20260716-220521`
- SHA256 executable hote :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
  (identique aux versions precedentes : c'est le lanceur natif generique
  `apphost`, independant du code managed — verifie separement que le nouveau
  code est bien present dans `Lumora.WinUI.dll` compile).
- Installateur : `artifacts\installer\LumoraSetup-0.78.3.4.11-dev-win-x64.exe`
- SHA256 installateur :
  `ea5c9a1fc31703e6a29691f6bf2def0a4198dc92fedd76119280c1429fb1b4f1`
