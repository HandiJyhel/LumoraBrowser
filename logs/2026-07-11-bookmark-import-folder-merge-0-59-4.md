# 2026-07-11 - Pulse Browser 0.59.4-dev - Fusion des dossiers de favoris importes

## Objectif

Suite au constat visuel de dossiers repetes dans le gestionnaire de favoris apres import Chrome, corriger la logique d'import : Pulse ne doit pas recreer un dossier `Collection`, `DL`, `Games`, etc. si un dossier homonyme existe deja au meme niveau.

## Changements

- `BookmarkStore.AddImportItems` reutilise desormais un dossier existant de meme nom au meme parent.
- Les dossiers crees pendant un import puis restes vides parce que toutes leurs URL existaient deja sont retires immediatement.
- Ajout de `MergeSiblingImportFolders` apres fusion/remplacement d'import pour reparer les doublons de dossiers freres crees par d'anciennes versions.
- Le test de regression des favoris verifie que l'import contient bien cette protection contre les dossiers homonymes.
- Passage de version source a `0.59.4-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 181 tests reussis.
- `scripts\build-winui.ps1` : build WinUI reussi, 0 avertissement, 0 erreur.
- Artifact propre genere : `artifacts\clean-test\PulseBrowser-0.59.4-dev-win-x64-clean-20260711-182847`.
- SHA256 exe hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- SHA256 `PulseBrowser.WinUI.dll` : `f9bb2c538be83271803ca02e2cd74211e7e5314dda0d8845c854921eb85e5236`.
- Installateur genere : `artifacts\installer\PulseBrowserSetup-0.59.4-dev-win-x64.exe`.
- SHA256 installateur : `acaf12f5ff6de494c86d18ef4760c3aee34ed31e18e273291dd921922bd0afd4`.
