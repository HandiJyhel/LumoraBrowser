# 2026-07-10 - Build propre de test et authenticite utilisateur

## Changements

- Ajout d'une section `Authenticite du build` dans `A propos`.
- Ajout de `BuildAuthenticity`, qui lit `VERIFICATION.txt` a cote de l'executable quand il existe.
- Ajout de `PULSE_BROWSER_PROFILE_DIR` dans `PulseProfilePaths.Default()` pour lancer Pulse Browser sur un profil isole.
- Ajout de `scripts/build-clean-test-artifact.ps1` et `build-clean-test-artifact.cmd`.
- Le script de build propre genere un artifact horodate, un `VERIFICATION.txt`, un `run-clean-profile.cmd` et un manifeste SHA256.
- Ajout de `docs/CLEAN_TEST_BUILD_AUTHENTICITY.md`.

## Decision

- Pas de certificat Authenticode payant a ce stade.
- Pas de certificat auto-signe presente comme officiel.
- La confiance utilisateur passe par la transparence: hash, explication claire de l'alerte Windows, build propre et profil vierge isole.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 152/152 tests verts.
- Premier `build-winui.cmd` bloque par le sandbox reseau NuGet (`NU1301`), puis relance autorisee reussie avec 0 avertissement et 0 erreur.
- Premier `build-clean-test-artifact.cmd` bloque par le sandbox reseau NuGet, puis deuxieme tentative autorisee compile en Release mais echoue apres build car `Get-FileHash` n'etait pas disponible dans la session PowerShell.
- Correction des scripts SHA256 pour utiliser `System.Security.Cryptography.SHA256` au lieu de `Get-FileHash`.
- Relance autorisee de `build-clean-test-artifact.cmd` reussie avec 0 avertissement et 0 erreur.
- Artifact cree : `artifacts\clean-test\PulseBrowser-0.52.0-dev-win-x64-clean-20260710-162845`.
- SHA256 de `PulseBrowser.WinUI.exe` : `512b669edd5cec018c2d48fffbd20477ebec554ef5acce5fbe996a6edd2e1eae`.
- `VERIFICATION.txt` present et le manifeste `artifacts\signatures\PulseBrowser-0.52.0-dev-clean-20260710-162858.sha256` contient bien l'entree de l'executable.
- `run-clean-profile.cmd` force `PULSE_BROWSER_PROFILE_DIR` vers `_clean-profile`.
- Verification que `_clean-profile` n'existe pas encore dans l'artifact : le dossier reste vierge avant test utilisateur.
