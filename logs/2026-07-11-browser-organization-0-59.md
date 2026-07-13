# 2026-07-11 - Pulse Browser 0.59.0-dev - Organisation generale du navigateur

## Objectif

Revoir la structure visible du navigateur avant de produire un nouvel executable :
moins de boutons disperses, une barre d'outils plus claire et une organisation
commune entre le menu Pulse et la palette de commandes.

## Changements

- Barre d'outils principale allegee :
  - conserve les actions de navigation immediates, l'adresse, le bouclier, les favoris et le menu Pulse ;
  - retire de la surface visible les actions moins quotidiennes comme Accueil, plein ecran, Picture-in-Picture et telechargement video ;
  - ces actions restent disponibles dans le menu Pulse.
- Menu Pulse restructure en groupes produit :
  - `Naviguer` ;
  - `Controle du site` ;
  - `Donnees locales` ;
  - `Coffre local` ;
  - `Outils de page`.
- Palette `Ctrl+K` alignee sur les memes categories pour eviter deux organisations mentales differentes.
- Passage de version source a `0.59.0-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` :
  176/176 tests verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.59.0-dev` :
  premier essai bloque par l'acces NuGet du bac a sable (`NU1301`), second essai reussi avec autorisation reseau.
- Build propre : `artifacts\clean-test\PulseBrowser-0.59.0-dev-win-x64-clean-20260711-172122`.
- SHA256 exe hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- SHA256 `PulseBrowser.WinUI.dll` : `660eae3bcb3ceb5aa01e8c335e4b4bf7e76a604b6b04c204fcd30cc22f4fa38b`.
- `scripts\build-installer-netfx.ps1 -Version 0.59.0-dev -CleanArtifactDir artifacts\clean-test\PulseBrowser-0.59.0-dev-win-x64-clean-20260711-172122` :
  reussi, 0 avertissement/erreur.
- Installateur : `artifacts\installer\PulseBrowserSetup-0.59.0-dev-win-x64.exe`.
- SHA256 installateur : `65d6456a20b1ab1af47cf1394c35cb452244701a7c0aa566d2260b22cd3a2dd9`.

## Note

L'executable n'est pas signe Authenticode ; Windows peut afficher `Editeur inconnu`.
