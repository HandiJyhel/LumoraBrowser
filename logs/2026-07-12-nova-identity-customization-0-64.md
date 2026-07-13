# Nova Browser 0.64.0-dev - Identite Nova et personnalisation validee

## Objectif

Suite au changement de nom du navigateur, remettre l'identite visible de Nova Browser en coherence : icone, page nouvel onglet, options de personnalisation et avatar de profil avec validation explicite.

## Changements

- Nouveaux assets `NovaBrowserApp.png` et `NovaBrowserApp.ico`, generes par `scripts/generate-app-icon.ps1`.
- Raccordement de l'icone applicative sur `NovaBrowserApp.ico` dans `NovaBrowser.WinUI.csproj`, `ApplyAppIcon`, les fenetres d'app web et les scripts d'installateur.
- Page `nova://accueil` refondue pour reutiliser le vrai PNG applicatif au lieu d'un logo CSS independant.
- Palette par defaut basculee vers une identite cyan/vert, moins liee a l'ancienne dominance orange.
- Ajout de vrais reglages de personnalisation :
  - palette Nova (`Nova cyan`, `Ocean`, `Foret`, `Ambre`) ;
  - style du nouvel onglet (`Signature`, `Calme`, `Minimal`) ;
  - titre, focus de recherche et raccourcis du nouvel onglet.
- Les reglages du nouvel onglet et de palette passent par un bouton `Valider les options`.
- L'avatar de profil local passe maintenant par un apercu puis un bouton `Valider l'image`; la copie ou suppression reelle n'est faite qu'a la validation.
- Version source mise a `0.64.0-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.

## Verification

- `build-winui.cmd` : reussi, 0 avertissement, 0 erreur apres autorisation reseau NuGet.
- `dotnet test NovaBrowser.Tests\NovaBrowser.Tests.csproj --no-restore` : 233/233 tests verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.64.0-dev` : reussi.
- `scripts\build-installer.ps1 -Version 0.64.0-dev` : reussi.
- Artifact propre : `artifacts\clean-test\NovaBrowser-0.64.0-dev-win-x64-clean-20260712-225228`.
- Installateur : `artifacts\installer\NovaBrowserSetup-0.64.0-dev-win-x64.exe`.
- SHA256 installateur : `5475b095408821383fcf4e88f40baa72905f2b5700fde0e646290e64c95f0005`.

## Limite

Pas de validation visuelle interactive de l'application lancee dans cette session. La compilation XAML, les tests, l'artifact propre, la presence des `.xbf`/`.pri` et l'installateur ont ete verifies.

**Version :** `0.64.0-dev`.
