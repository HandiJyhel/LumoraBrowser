# 2026-07-11 - Pulse Browser 0.59.5-dev - Nettoyage favoris au demarrage et runtime autonome

## Objectif

Suite au retour utilisateur apres installation de `0.59.4-dev` :

- les doublons de favoris existants etaient toujours visibles, car la correction precedente ne s'appliquait qu'au prochain import ;
- le raccourci bureau lancait `PulseBrowser.WinUI.exe` mais Windows demandait le .NET Desktop Runtime, car l'artifact propre etait construit via `Build` et non publie en self-contained.

## Changements

- Ajout de `BookmarkStore.RepairImportedFolderDuplicates()`.
- Le demarrage WinUI appelle cette reparation juste apres creation du `BookmarkStore`.
- La reparation fusionne les dossiers freres homonymes, retire les URL dupliquees et cree une sauvegarde `before-bookmark-duplicate-repair` avant ecriture.
- `scripts\build-clean-test-artifact.ps1` utilise desormais `/t:Publish` avec `SelfContained=true`, `PublishSelfContained=true` et `PublishDir`.
- Tests de regression ajoutes pour verrouiller le nettoyage au demarrage et le publish autonome.
- Passage de version source a `0.59.5-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 183 tests reussis.
- `scripts\build-winui.ps1` : build WinUI reussi, 0 avertissement, 0 erreur.
- `scripts\build-clean-test-artifact.ps1 -Version 0.59.5-dev` : publish autonome reussi, 0 avertissement, 0 erreur.
- Artifact propre genere : `artifacts\clean-test\PulseBrowser-0.59.5-dev-win-x64-clean-20260711-184021`.
- Verification runtime local : `coreclr.dll`, `hostfxr.dll`, `hostpolicy.dll` presents dans le dossier `app`.
- `PulseBrowser.WinUI.runtimeconfig.json` contient `includedFrameworks` avec `Microsoft.NETCore.App` `8.0.28`.
- SHA256 exe hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- SHA256 `PulseBrowser.WinUI.dll` : `201b47d69e2cdde73c23adc46a74bf146ffb7937b4833b297259cf085f93ee31`.
- Installateur genere : `artifacts\installer\PulseBrowserSetup-0.59.5-dev-win-x64.exe`.
- SHA256 installateur : `5bbb302b1577b90c9baea92db3d92dc6f515c513d36826a7c7b78f1cc9f13bc9`.
