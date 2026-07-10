# 2026-07-10 - Correctif profil personnalise installe 0.54.1-dev

## Contexte

Pendant un pre-test de l'application installee, un profil cree dans un emplacement personnalise et alimente par import de favoris apparaissait ensuite dans le selecteur, mais l'ouverture affichait `Profil actif illisible`.

## Cause

L'installateur `0.54.0-dev` creait un launcher `PulseBrowserLauncher.vbs` qui forcait :

```text
PULSE_BROWSER_PROFILE_DIR=%LOCALAPPDATA%\PulseBrowser\installed-profile
```

Ce comportement etait utile pour un artifact de test isole, mais pas pour l'installation normale. Il pouvait faire diverger le profil vu par `config.json` du profil reellement charge au lancement.

## Corrections

- `MainWindow.Profile.cs` : le selecteur de profils recharge le profil depuis le chemin de l'entree selectionnee et redemarre si l'entree ne correspond pas au runtime courant.
- `MainWindow.Profile.cs` : le dossier cible de creation de profil est conserve dans `_profileCreationTarget` afin que l'import d'onboarding ecrive dans le bon profil.
- `Models/Profiles.cs` : `Discover` compare le dossier actif au dossier reellement charge avant de marquer une entree active.
- `ProfileRegistryTests` : ajout de tests pour verrouiller le cas custom actif/non actif selon le dossier runtime.
- `scripts/build-installer.ps1` : suppression du launcher VBS force, suppression d'un ancien launcher s'il existe, raccourcis directs vers `PulseBrowser.WinUI.exe`.
- Passage de version a `0.54.1-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 159/159 tests verts.
- Premier `build-winui.cmd` bloque par le sandbox reseau NuGet, puis relance autorisee : 0 avertissement, 0 erreur.
- `build-clean-test-artifact.cmd` autorise apres blocage sandbox NuGet : build Release propre reussi avec 0 avertissement, 0 erreur.
- Artifact propre : `artifacts\clean-test\PulseBrowser-0.54.1-dev-win-x64-clean-20260710-183017`.
- SHA256 de `PulseBrowser.WinUI.exe` : `512b669edd5cec018c2d48fffbd20477ebec554ef5acce5fbe996a6edd2e1eae`.
- Manifeste du build propre : `artifacts\signatures\PulseBrowser-0.54.1-dev-clean-20260710-183042.sha256`.
- Premier `build-installer.cmd` bloque par le sandbox reseau NuGet sur `Microsoft.NET.ILLink.Tasks`, puis relance autorisee.
- Installateur genere : `artifacts\installer\PulseBrowserSetup-0.54.1-dev-win-x64.exe`.
- SHA256 installateur : `9bc6666ae6e2757915f2e1785ade3312636c47484a045650867fd74d0f4aded5`.
- Manifeste installateur : `artifacts\signatures\PulseBrowserSetup-0.54.1-dev-20260710-183135.sha256`.
- Verification installer : `Profil: Aucun profil embarque ; aucun dossier de profil force au lancement`.
