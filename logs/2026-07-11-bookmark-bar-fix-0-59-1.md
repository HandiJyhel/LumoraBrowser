# 2026-07-11 - Pulse Browser 0.59.1-dev - Correction barre de favoris

## Probleme

L'utilisateur signale qu'un favori ajoute manuellement, notamment YouTube, n'apparait pas dans la barre des favoris. C'est une regression critique pour l'usage de base d'un navigateur.

## Cause identifiee

La barre des favoris ne rendait que les 18 premiers elements du dossier `Barre des favoris`. Un favori ajoute apres une importation pouvait donc etre correctement enregistre mais totalement absent de la surface visible.

## Correction

- Suppression de la limite arbitraire `.Take(18)` dans le rendu de la barre des favoris.
- Ajout d'un `ScrollViewer` nomme pour permettre a l'interface de reveler un favori ajoute.
- `BookmarkStore.AddOrUpdateUrl` retourne maintenant le noeud sauvegarde.
- Apres ajout dans la barre, Pulse Browser cible explicitement le favori sauvegarde avec `StartBringIntoView`.
- Le message utilisateur indique clairement que le favori a ete ajoute dans la barre des favoris.
- Ajout de tests de regression source pour empecher le retour d'une limite arbitraire sur la barre.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 178/178 tests verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.59.1-dev` :
  - premier essai bloque par l'acces NuGet du bac a sable (`NU1301`) ;
  - second essai avec autorisation reseau : reussi, 0 avertissement/erreur.
- Artefact propre : `artifacts\clean-test\PulseBrowser-0.59.1-dev-win-x64-clean-20260711-174419`.
- SHA256 exe hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- SHA256 `PulseBrowser.WinUI.dll` : `00bd80a12d9494facf218482f0a52c902dcf3145061bbdc9e9f9ffd9184f2bbe`.
- `scripts\build-installer-netfx.ps1 -Version 0.59.1-dev -CleanArtifactDir artifacts\clean-test\PulseBrowser-0.59.1-dev-win-x64-clean-20260711-174419` : reussi, 0 avertissement/erreur.
- Installateur : `artifacts\installer\PulseBrowserSetup-0.59.1-dev-win-x64.exe`.
- SHA256 installateur : `6567c983e005118949131f0bedfddcf8f85472989121dea7f45d770c3de40a43`.

## Version

`0.59.1-dev`
