# Page A propos style officiel - 0.78.3.4.20-dev

## Contexte

L'utilisateur souhaite que la page `A propos` de Lumora adopte une ergonomie
plus moderne, proche d'une page officielle de navigateur comme celle de
Google/Chrome, tout en conservant toutes les informations deja presentes.

## Changements

- Version passee a `0.78.3.4.20-dev`.
- Refonte de `AboutPanel` dans `Lumora.WinUI/MainWindow.xaml`.
- En-tete centre avec marque Lumora, phrase produit et badge de version de
  developpement.
- Remplacement de la rangée d'onglets horizontale par un sommaire vertical plus
  calme : Vue d'ensemble, Modules, Confidentialite, Authenticite, Dependances,
  Technique, Profil local.
- Conservation des sections existantes et des donnees deja affichees.
- Vue d'ensemble reorganisee en tuiles de lecture rapide : local-first, moteur
  web, modules integres et transparence.
- Credits/dependances remises en forme sous forme de fiches compactes avec nom,
  licence, usage et lien source.
- Informations techniques et profil local presentes en blocs plus lisibles.
- Alignement de la version par defaut dans les scripts d'artefact et
  d'installateur.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 469/469 tests
  verts.
- `scripts\build-winui.ps1` : OK hors sandbox apres blocage attendu du restore
  NuGet dans le sandbox ; compilation XAML/C# reussie avec 0 avertissement et
  0 erreur.
- `scripts\build-clean-test-artifact.ps1` : OK hors sandbox apres blocage
  sandbox sur la creation du dossier d'artefact.
- `scripts\build-installer.ps1 -CleanArtifactDir
  artifacts\clean-test\Lumora-0.78.3.4.20-dev-win-x64-clean-20260717-024142` :
  OK hors sandbox apres blocage sandbox sur le dossier de staging installateur.
- Tentative de capture visuelle automatisee : non retenue, car la capture a pris
  la fenetre Codex/VS Code au lieu de Lumora. Aucun artefact de capture trompeur
  n'a ete conserve.

## Artefacts

- Build propre :
  `artifacts\clean-test\Lumora-0.78.3.4.20-dev-win-x64-clean-20260717-024142`.
- SHA256 executable hote :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`.
- Installateur :
  `artifacts\installer\LumoraSetup-0.78.3.4.20-dev-win-x64.exe`.
- SHA256 installateur :
  `efa2d4d6168ad0d318747f269e4534ba8d266ad89e260490ab2239b20d2de865`.

## Limite

La validation visuelle interactive finale reste a faire dans l'application
lancee normalement.
