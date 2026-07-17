# 2026-07-17 - Credits des dependances tierces et version affichee corrigee (0.78.3.4.18-dev)

## Contexte

Suite a la 0.78.3.4.17-dev, l'utilisateur exprime une inquietude legitime en
vue de la mise a disposition publique de Lumora : utiliser des outils/listes
open source (yt-dlp, ffmpeg, listes de filtrage, modeles de traduction) sans
jamais le mentionner explicitement dans l'application pourrait lui etre
reproche. Demande : ajouter une page de credits, avec montee de version et
nouvel executable.

## Diagnostic complementaire

- Aucune section credits n'existait dans la page « A propos » : les
  dependances tierces (yt-dlp, ffmpeg, listes de filtrage, modeles ONNX)
  n'etaient nommees nulle part dans l'interface.
- Bug decouvert au passage : `MainWindow.xaml.cs:38` avait une constante
  `Version` figee sur `0.78.3.4.10-dev` depuis plusieurs versions (utilisee
  pour le titre de la fenetre et `AboutVersionText`) — jamais mise a jour en
  parallele d'`AGENTS.md` malgre les montees de version successives
  (0.78.3.4.11 a 0.78.3.4.17).

## Changements

- `MainWindow.xaml` : nouvel onglet « Credits » dans la page A propos
  (`AboutNavCredits` / `AboutSectionCredits`), listant chaque dependance
  tierce utilisee par Lumora avec sa licence et un lien vers sa source
  officielle : yt-dlp (Unlicense), FFmpeg (LGPL/GPL selon compilation),
  EasyList/EasyPrivacy (GPLv3), uBlock Origin uAssets (GPLv3), AdGuard Base
  Filter (GPLv3), ONNX Runtime / ONNX Runtime GenAI (MIT),
  Phi-3-mini-4k-instruct (MIT), OPUS-MT/Helsinki-NLP export Xenova (licence
  indiquee sur la fiche Hugging Face). Texte d'introduction precisant
  qu'aucune de ces ressources n'est embarquee dans l'installateur : chacune
  est recuperee a la demande depuis sa source officielle.
- `MainWindow.xaml.cs` : branchement de la nouvelle section dans
  `ShowAboutSection`, et correction de la constante `Version` vers
  `0.78.3.4.18-dev`.
- `AGENTS.md`, `scripts/build-clean-test-artifact.ps1`,
  `scripts/build-installer.ps1` : version passee a `0.78.3.4.18-dev`.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 469/469 tests
  verts (aucun changement de logique testable, uniquement UI statique).
- Build MSBuild Debug : 0 avertissement / 0 erreur.
- Verification manuelle en conditions reelles (profil jetable, mode invite,
  pilotage UIA + capture d'ecran) : le titre de fenetre et la page d'accueil
  affichent desormais correctement `0.78.3.4.18-dev` (confirmation que le
  correctif de la constante `Version` fonctionne partout) ; ouverture de
  A propos > Credits confirmee visuellement par capture d'ecran : les 8
  entrees s'affichent proprement avec licence et lien cliquable, sans
  chevauchement.

## Artefacts

- Artefact propre :
  `artifacts\clean-test\Lumora-0.78.3.4.18-dev-win-x64-clean-20260717-020955`
- SHA256 executable hote (lanceur natif, inchange comme d'habitude) :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
- Installateur : `artifacts\installer\LumoraSetup-0.78.3.4.18-dev-win-x64.exe`
- SHA256 installateur :
  `26f290c799f20b68755f8358f449001e6884542041c5b040f8594557add84745`
