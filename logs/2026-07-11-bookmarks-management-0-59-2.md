# 2026-07-11 - Pulse Browser 0.59.2-dev - Gestion des favoris

## Objectif

Ameliorer la gestion des favoris pour pouvoir vider une bibliotheque, retester une importation HTML Chrome proprement, conserver les favoris sans nom visible et remplacer l'ascenseur de barre par un debordement de type navigateur.

## Changements

- Gestionnaire de favoris :
  - selection multiple activee sur la liste de favoris ;
  - bouton `Tout selectionner` ;
  - bouton `Supprimer selection` avec confirmation ;
  - bouton `Vider` avec confirmation et sauvegarde locale avant suppression.
- Stockage favoris :
  - ajout de `RemoveNodes` pour supprimer plusieurs favoris/dossiers en une operation ;
  - ajout de `ClearUserBookmarks` pour supprimer tous les favoris utilisateur en conservant les racines ;
  - sauvegarde `.bak.tsv` avant suppression massive ou vidage complet.
- Favoris sans texte :
  - ajout du titre invisible `\u200B` pour les favoris "icone seule" ;
  - option `Nom invisible (icone seule dans la barre)` dans la fenetre d'ajout/modification ;
  - conservation des favoris importes sans nom visible au lieu de les remplacer automatiquement par le domaine ;
  - affichage lisible `(icone seule)` dans le gestionnaire et les menus.
- Barre des favoris :
  - suppression de l'ascenseur horizontal ;
  - rendu plus compact des dossiers/favicons, proche d'une barre Chrome classique ;
  - suppression de l'icone decorative en debut de barre ;
  - estimation de largeur par favori, avec traitement compact des favoris icone seule ;
  - ajout d'un bouton de debordement `»` pour les favoris supplementaires ;
  - les dossiers et favoris en debordement restent ouvrables depuis le menu.
- Passage de version source a `0.59.2-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 180/180 tests verts.
- Premier `scripts\build-winui.ps1` bloque par l'acces NuGet du bac a sable (`NU1301`).
- `scripts\build-winui.ps1` relance avec autorisation reseau : reussi, 0 avertissement/erreur.
- `scripts\build-clean-test-artifact.ps1 -Version 0.59.2-dev` : premier essai bloque par l'acces NuGet du bac a sable (`NU1301`), second essai reussi avec autorisation reseau.
- Artefact propre : `artifacts\clean-test\PulseBrowser-0.59.2-dev-win-x64-clean-20260711-180227`.
- SHA256 exe hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- SHA256 `PulseBrowser.WinUI.dll` : `cd831d3f0e8c390a4ba1debfb5ee70376f2d51f15bb3bbf83f8d36aaf4fdfcbd`.
- `scripts\build-installer-netfx.ps1 -Version 0.59.2-dev -CleanArtifactDir artifacts\clean-test\PulseBrowser-0.59.2-dev-win-x64-clean-20260711-180227` : reussi, 0 avertissement/erreur.
- Installateur : `artifacts\installer\PulseBrowserSetup-0.59.2-dev-win-x64.exe`.
- SHA256 installateur : `7fe92088e38b153c95d8b2e215bd7731bb5e0e42868a354762fcda7aeb8f2d9a`.
