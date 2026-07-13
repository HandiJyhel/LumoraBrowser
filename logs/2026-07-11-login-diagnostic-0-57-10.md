# 2026-07-11 - 0.57.10-dev - Diagnostic connexion par site

## Contexte

L'utilisateur signale que la connexion Google volontaire sur un site de test ne donne toujours pas de session valide, meme apres acceptation des cookies et activation de la compatibilite connexion.

Le journal utilisateur fourni montrait uniquement la navigation vers le site puis `#signin`, sans assez d'elements pour savoir si le probleme venait d'une ressource Google, d'une redirection, d'un cookie ou d'un script.

## Changements

- Ajout d'un module local `SiteLoginDiagnosticRecorder`.
- Ajout d'un mode `Diagnostic connexion` dans le panneau `Site actuel`.
- Le diagnostic est persistant par domaine racine dans les reglages locaux.
- Le diagnostic capture les navigations, nouvelles fenetres, requetes WebView2, reponses reseau, actions utilisateur et signaux Google Identity.
- Les URL exportees sont nettoyees : valeurs de parametres, fragments, tokens et donnees sensibles ne sont pas stockes.
- Ajout d'un bouton `Exporter le diagnostic` dans `Site actuel`.
- L'export ajoute un contexte minimal : version Pulse, URL active nettoyee, et nombre de cookies du domaine + `google.com`.
- Le diagnostic n'active aucun envoi reseau vers Pulse : le rapport reste local dans le profil utilisateur.

## Mode d'emploi prevu

1. Ouvrir le site concerne.
2. Ouvrir `Site actuel`.
3. Activer `Mode compatibilite connexion`.
4. Activer `Diagnostic connexion`.
5. Recharger la page.
6. Reproduire le clic de connexion.
7. Revenir dans `Site actuel`.
8. Cliquer sur `Exporter le diagnostic`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 174/174 tests verts.
- MSBuild Visual Studio Release x64 : reussi, 0 avertissement/erreur.
- `scripts\build-clean-test-artifact.ps1 -Version 0.57.10-dev` : premier essai bloque par l'acces NuGet du bac a sable, second essai reussi apres autorisation reseau.
- `scripts\build-installer-netfx.ps1 -Version 0.57.10-dev -CleanArtifactDir artifacts\clean-test\PulseBrowser-0.57.10-dev-win-x64-clean-20260711-130909` : reussi.

## Artefacts

- Artefact propre : `artifacts\clean-test\PulseBrowser-0.57.10-dev-win-x64-clean-20260711-130909`.
- SHA256 `PulseBrowser.WinUI.dll` : `7690d29163020955fe9336a205e1c8bfd41ea38c278c11a4816703eae41c6693`.
- SHA256 exe hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- Installateur : `artifacts\installer\PulseBrowserSetup-0.57.10-dev-win-x64.exe`.
- SHA256 installateur : `113e211e1760e994309fbd5d326f1f51ebdee845647d8a2fb4c40c32d9f34140`.
