# 2026-07-10 - Installateur propre 0.54.0-dev

## Objectif

Generer un installateur Windows propre avec toutes les fonctions actuelles de Pulse Browser `0.54.0-dev`, sans profil embarque et avec choix explicite du dossier d'installation.

## Changements

- Alignement de `scripts/build-clean-test-artifact.ps1` sur `0.54.0-dev`.
- Alignement de `scripts/build-installer.ps1` sur `0.54.0-dev`.
- Nettoyage automatique dans `artifacts\installer` des anciens installeurs Pulse Browser avant generation du nouvel installeur.
- Ajout de `docs/INSTALLER_0_54.md`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 157/157 tests verts.
- Premiere tentative de `build-clean-test-artifact.cmd` bloquee par le sandbox reseau NuGet (`NU1301`), puis relance autorisee.
- Build propre Release reussi avec 0 avertissement et 0 erreur.
- Artifact propre cree : `artifacts\clean-test\PulseBrowser-0.54.0-dev-win-x64-clean-20260710-180138`.
- SHA256 de `PulseBrowser.WinUI.exe` : `512b669edd5cec018c2d48fffbd20477ebec554ef5acce5fbe996a6edd2e1eae`.
- Manifeste du build propre : `artifacts\signatures\PulseBrowser-0.54.0-dev-clean-20260710-180202.sha256`.
- Premiere tentative de `build-installer.cmd` bloquee par le sandbox reseau NuGet (`NU1301` sur `Microsoft.NET.ILLink.Tasks`), puis relance autorisee.
- Installateur genere : `artifacts\installer\PulseBrowserSetup-0.54.0-dev-win-x64.exe`.
- Taille de l'installateur : 35 870 346 octets.
- SHA256 installateur : `672f0da6a9c09f3fbe10589e3fb5c92eb66b711a70d8789e7054c10af88480d3`.
- Fichier de verification : `artifacts\installer\PulseBrowserSetup-0.54.0-dev-win-x64.VERIFICATION.txt`.
- Manifeste SHA256 : `artifacts\signatures\PulseBrowserSetup-0.54.0-dev-20260710-180303.sha256`.
- Verification que `artifacts\installer` ne contient que l'exe final et son fichier de verification.
