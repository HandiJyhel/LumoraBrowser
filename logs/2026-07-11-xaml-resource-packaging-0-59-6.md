# 2026-07-11 - Pulse Browser 0.59.6-dev - Ressources XAML embarquees dans l'installateur

## Objectif

Suite au retour utilisateur apres installation de `0.59.5-dev` : l'application ne se lance toujours pas. Le correctif runtime etait present, mais le paquet publie restait incomplet pour WinUI.

## Diagnostic

- Le dossier installe contenait bien le runtime .NET autonome (`coreclr.dll`, `hostfxr.dll`, `hostpolicy.dll`).
- Le lancement diagnostic de l'artifact `0.59.6-dev` avec uniquement les `.xbf` recopies crashait encore avant `OnLaunched`.
- Comparaison `bin` / artifact : `PulseBrowser.WinUI.pri` etait genere par WinUI mais absent du dossier publie.
- Cause retenue : publication self-contained incomplete pour une application WinUI non packagee, avec ressources XAML applicatives non embarquees.

## Changements

- `scripts/build-clean-test-artifact.ps1` copie maintenant `App.xbf`, `MainWindow.xbf`, `PulseAppWindow.xbf` et `PulseBrowser.WinUI.pri` dans le dossier `app` final.
- Le script echoue explicitement si le fichier de ressources WinUI applicatif est introuvable.
- Ajout d'un mode `-NoRestore` pour permettre une generation locale quand NuGet est indisponible mais que le cache est deja restaure.
- Test de regression ajoute pour verrouiller la presence de `PulseBrowser.WinUI.pri`.
- Passage de version source a `0.59.6-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 183 tests reussis.
- `scripts\build-clean-test-artifact.ps1 -Version 0.59.6-dev -NoRestore` : publish autonome reussi, 0 avertissement, 0 erreur.
- Artifact propre genere : `artifacts\clean-test\PulseBrowser-0.59.6-dev-win-x64-clean-20260711-185340`.
- Verification artifact : `App.xbf`, `MainWindow.xbf`, `PulseAppWindow.xbf`, `PulseBrowser.WinUI.pri`, `coreclr.dll`, `hostfxr.dll`, `hostpolicy.dll` presents dans le dossier `app`.
- SHA256 exe hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- SHA256 `PulseBrowser.WinUI.dll` : `2c32e6c6e5495552fe7bba017f566381675c9ff9992d2687ae1ee20a6e2ca2b3`.
- Installateur genere : `artifacts\installer\PulseBrowserSetup-0.59.6-dev-win-x64.exe`.
- SHA256 installateur : `4094dd88a0247c084e79b5742291c7366a4566b1baf7197b2ec493bfa4c438c3`.
- Verification du zip embarque par l'installateur : les fichiers XAML, le `.pri` applicatif et le runtime autonome sont presents.

## Limite

Le lancement GUI final n'a pas pu etre relance apres correction complete car l'environnement a refuse l'autorisation d'execution graphique. La verification fichier confirme cependant que le fichier WinUI manquant dans le paquet est maintenant embarque dans l'installateur.
