# Pulse Browser - Installateur propre 0.54.0-dev

## Objectif

Produire un installateur Windows testable localement, sans profil utilisateur embarque, avec les fonctions applicatives actuelles et un dossier d'installation visible et modifiable.

## Commande

```powershell
.\build-clean-test-artifact.cmd
.\build-installer.cmd
```

## Artifact genere

```text
artifacts/installer/PulseBrowserSetup-0.54.0-dev-win-x64.exe
```

SHA256:

```text
672f0da6a9c09f3fbe10589e3fb5c92eb66b711a70d8789e7054c10af88480d3
```

Fichier de verification:

```text
artifacts/installer/PulseBrowserSetup-0.54.0-dev-win-x64.VERIFICATION.txt
```

Manifeste:

```text
artifacts/signatures/PulseBrowserSetup-0.54.0-dev-20260710-180303.sha256
```

## Fonctions incluses

- import CSV des mots de passe, dont Proton Pass;
- gestion des utilisateurs/profils dans `Parametres > Profil`;
- page `A propos` organisee en sections;
- informations d'authenticite du build dans l'application;
- historique local des telechargements;
- permissions locales par site;
- profils locaux separes avec coffre `vault.pulse`.

## Comportement de l'installation

- dossier d'installation visible avant installation;
- choix de modifier le dossier avec `Parcourir...`;
- installation par defaut par utilisateur, sans privilege administrateur;
- copie de l'application par defaut dans `%LOCALAPPDATA%\Programs\PulseBrowser`;
- choix possible d'un autre dossier, par exemple `Program Files`, si l'installateur est lance avec les droits necessaires;
- choix de creer ou non un raccourci Bureau;
- choix de creer ou non un raccourci Menu Demarrer;
- option d'installation propre pour supprimer le profil installe precedent;
- creation d'une entree de desinstallation Windows sous le registre utilisateur;
- conservation de `VERIFICATION.txt` dans le dossier installe;
- aucun profil utilisateur n'est embarque ou copie.

Le raccourci installe lance Pulse Browser avec:

```text
PULSE_BROWSER_PROFILE_DIR=%LOCALAPPDATA%\PulseBrowser\installed-profile
```

Le dossier de profil n'est pas cree pendant la fabrication de l'installateur. Il sera cree au premier lancement.

## Limites

- L'installateur n'est pas signe Authenticode.
- Windows peut afficher `Editeur inconnu`.
- La confiance repose pour ce palier sur `VERIFICATION.txt`, le SHA256 et la transparence du statut de signature.
