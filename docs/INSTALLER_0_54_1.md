# Pulse Browser - Correctif installateur 0.54.1-dev

## Objectif

Corriger le blocage observe apres installation quand un profil est cree dans un emplacement personnalise : l'application affichait le profil dans la liste, mais le lancement installe forcait encore un dossier isole via `PULSE_BROWSER_PROFILE_DIR`.

## Corrections

- Le selecteur de profils recharge maintenant le profil depuis le chemin reel de l'entree choisie.
- Le registre des profils ne marque plus un profil custom actif si le runtime utilise un autre dossier.
- L'import de favoris pendant l'onboarding ecrit dans le dossier cible conserve en memoire, pas dans un chemin recalcule via l'environnement.
- L'installateur supprime l'ancien `PulseBrowserLauncher.vbs` s'il existe.
- Les raccourcis Bureau et menu Demarrer lancent directement `PulseBrowser.WinUI.exe`.
- L'installateur ne force plus `PULSE_BROWSER_PROFILE_DIR`.

## Artifact genere

```text
artifacts/installer/PulseBrowserSetup-0.54.1-dev-win-x64.exe
```

SHA256:

```text
9bc6666ae6e2757915f2e1785ade3312636c47484a045650867fd74d0f4aded5
```

Fichier de verification:

```text
artifacts/installer/PulseBrowserSetup-0.54.1-dev-win-x64.VERIFICATION.txt
```

Manifeste:

```text
artifacts/signatures/PulseBrowserSetup-0.54.1-dev-20260710-183135.sha256
```

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 159/159 tests verts.
- `build-winui.cmd` : 0 avertissement, 0 erreur.
- `build-clean-test-artifact.cmd` : build Release propre reussi.
- `build-installer.cmd` : installateur genere.
- `artifacts\installer` contient uniquement l'exe final et son fichier `.VERIFICATION.txt`.

## Limites

- L'installateur n'est pas signe Authenticode.
- Windows peut afficher `Editeur inconnu`.
- Les anciens raccourcis deja installes doivent etre remplaces par ce nouvel installateur pour supprimer le lancement avec profil force.
