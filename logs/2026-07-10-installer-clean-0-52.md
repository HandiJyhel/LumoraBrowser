# 2026-07-10 - Installateur propre 0.52.0-dev

## Changements

- Ajout de `scripts/build-installer.ps1`.
- Ajout de `build-installer.cmd`.
- Ajout de `docs/INSTALLER_0_52.md`.
- Reprise apres retour utilisateur: ajout de vraies options visibles dans l'installateur.

## Objectif

Generer un installateur Windows propre pour tester Pulse Browser comme une application installee, sans profil embarque et sans certificat Authenticode payant.

## Comportement attendu

- Installation par utilisateur dans `%LOCALAPPDATA%\Programs\PulseBrowser`.
- Affichage du dossier d'installation avant installation.
- Possibilite de choisir un autre dossier avec `Parcourir...`.
- Raccourci Bureau et Menu Demarrer.
- Entree de desinstallation Windows sous `HKCU`.
- Lancement via `PULSE_BROWSER_PROFILE_DIR=%LOCALAPPDATA%\PulseBrowser\installed-profile`.
- Profil cree seulement au premier lancement.
- Conservation de `VERIFICATION.txt` et generation de SHA256.

## Options utilisateur

- Installation propre : supprimer le profil installe precedent.
- Dossier d'installation visible et modifiable.
- Creer un raccourci Bureau.
- Creer un raccourci Menu Demarrer.
- Lancer Pulse Browser apres installation.
- L'option propre ne touche que `%LOCALAPPDATA%\PulseBrowser\installed-profile`.

## Verification

- Premiere tentative avec `IExpress` abandonnee: le payload CAB etait valide avec `makecab`, mais la generation SFX IExpress echouait sans code exploitable.
- Remplacement par un installateur .NET WinForms genere par `scripts/build-installer.ps1`, avec `app.zip` embarque comme ressource.
- Premiere publication .NET bloquee par le sandbox reseau NuGet (`NU1301` sur `Microsoft.NET.ILLink.Tasks`), puis relance autorisee.
- Correction de la publication: retrait de `EnableCompressionInSingleFile`, reserve aux applications self-contained.
- Generation reussie de `artifacts\installer\PulseBrowserSetup-0.52.0-dev-win-x64.exe`.
- Regeneration apres ajout des options utilisateur et du choix explicite du dossier d'installation.
- Nettoyage automatique du dossier `staging-dotnet` apres generation reussie pour ne laisser que l'exe et le fichier de verification dans `artifacts\installer`.
- Taille de l'installateur : 35 865 226 octets.
- SHA256 installateur : `f8c41c4eef38fa2af123995f41f8eb69ab224893130c6b075c9bcba3e35ae1b5`.
- Fichier de verification cree : `artifacts\installer\PulseBrowserSetup-0.52.0-dev-win-x64.VERIFICATION.txt`.
- Manifeste SHA256 cree : `artifacts\signatures\PulseBrowserSetup-0.52.0-dev-20260710-174414.sha256`.
- Verification que `%LOCALAPPDATA%\PulseBrowser\installed-profile` n'existe pas encore apres generation: l'installateur n'a pas cree de profil avant lancement.
- Remise au propre du poste de test: l'ancien `CustomProfilePath` vers `Desktop\bob` a ete retire de `%LOCALAPPDATA%\PulseBrowser\config.json`, remplace par `ActiveProfileId=default`.
- Les dossiers `Desktop\bob` et `%LOCALAPPDATA%\PulseBrowser\installed-profile` ont ete deplaces en quarantaine datee sous `%LOCALAPPDATA%\PulseBrowser\profile-quarantine\`, sans suppression definitive.
