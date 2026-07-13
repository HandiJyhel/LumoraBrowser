# 2026-07-11 - 0.57.11-dev - Compatibilite modal de connexion

## Contexte

L'utilisateur a fourni le rapport `login-diagnostic-f_a_p_h_o_u_s_e_._c_o_m-20260711-134143.txt`.

Le diagnostic montre que le clic `Connexion` est bien capte, mais que Pulse marque ensuite comme bloquee la requete :

`https://fr.faphouse.com/api/common-modals-api/all`

Cette requete est tres probablement celle qui charge le contenu de la fenetre de connexion. Comme Pulse renvoyait une reponse vide `200 OK`, le site pouvait continuer sans erreur visible, mais sans afficher la fenetre attendue.

## Changements

- Ajout d'une exception locale tres limitee pour les sites en `Compatibilite connexion`.
- L'exception ne s'applique qu'au meme domaine racine que la page active.
- L'exception ne s'applique qu'aux chemins lies a `login`, `signin`, `oauth`, `auth`, `session`, `account` ou `modal`.
- Les requetes de telemetrie comme `sentry envelope` restent bloquees.
- Correction du nom de fichier du diagnostic : `faphouse.com` au lieu de `f_a_p_h_o_u_s_e_._c_o_m`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 174/174 tests verts.
- MSBuild Visual Studio Release x64 : reussi, 0 avertissement/erreur.
- `scripts\build-clean-test-artifact.ps1 -Version 0.57.11-dev` : premier essai bloque par l'acces NuGet du bac a sable, second essai reussi apres autorisation reseau.
- `scripts\build-installer-netfx.ps1 -Version 0.57.11-dev -CleanArtifactDir artifacts\clean-test\PulseBrowser-0.57.11-dev-win-x64-clean-20260711-134805` : reussi.

## Artefacts

- Artefact propre : `artifacts\clean-test\PulseBrowser-0.57.11-dev-win-x64-clean-20260711-134805`.
- SHA256 `PulseBrowser.WinUI.dll` : `465157d9a3a0e0ee9e214510433afa7864176968b8402d2910e5acfcf2993ddc`.
- SHA256 exe hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- Installateur : `artifacts\installer\PulseBrowserSetup-0.57.11-dev-win-x64.exe`.
- SHA256 installateur : `475fdd4a1b245f0a7a4d4877921e6d5db51dbf0404b8a43281e62db1a151fc95`.
