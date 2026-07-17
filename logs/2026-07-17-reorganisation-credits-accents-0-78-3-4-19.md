# 2026-07-17 - Reorganisation des onglets A propos et accents corriges (0.78.3.4.19-dev)

## Contexte

Retour utilisateur apres la 0.78.3.4.18-dev : l'onglet Credits ajoute a la
page A propos etait mal range dans l'ordre des onglets.

## Diagnostic

- Ordre precedent : Resume / Modules / Confidentialite / Authenticite /
  Technique / Credits / Profil local. Credits (une info de confiance et de
  transparence, comme Confidentialite et Authenticite) etait separe de ces
  deux onglets par Technique (des faits bruts sur la stack), cassant le
  regroupement thematique naturel.
- Faute d'orthographe reperee a la relecture : tout le texte ecrit dans la
  section Credits de la 0.78.3.4.18-dev etait sans accents ("dependances",
  "recupere", "utilisees"...), alors que le reste de la page en a partout
  (Confidentialite, Authenticite, Parametres...). Contraire a la regle 12
  d'AGENTS.md.

## Changements

- `MainWindow.xaml` : onglet et section Credits deplaces juste apres
  Authenticite (nouvel ordre : Resume / Modules / Confidentialite /
  Authenticite / Credits / Technique / Profil local), regroupant les trois
  onglets de confiance/transparence avant les details techniques.
- Tout le texte de la section Credits reecrit avec les accents corrects
  (« Credits et dependances tierces » -> « Crédits et dépendances
  tierces », etc.), et le libelle de l'onglet corrige en « Crédits ».
- `MainWindow.xaml.cs` : ordre des assignations dans `ShowAboutSection`
  aligne sur le nouvel ordre des onglets (cosmetique, aucun changement de
  logique).
- Version passee a `0.78.3.4.19-dev` (`AGENTS.md`, constante `Version`,
  scripts de build).

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 469/469
  tests verts (aucune logique testable modifiee).
- Build MSBuild Debug : 0 avertissement / 0 erreur.
- Verification manuelle en conditions reelles (profil jetable, mode invite,
  pilotage UIA + capture d'ecran) : ordre des onglets confirme via
  l'enumeration UIA des `RadioButton` (Resume, Modules, Confidentialite,
  Authenticite, Credits, Technique, Profil local) ; capture d'ecran de
  l'onglet Credits confirmant le rendu correct des accents partout
  (« Crédits et dépendances tierces », « téléchargement », « détecté »,
  etc.).

## Artefacts

- Artefact propre :
  `artifacts\clean-test\Lumora-0.78.3.4.19-dev-win-x64-clean-20260717-022301`
- SHA256 executable hote (lanceur natif, inchange comme d'habitude) :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
- Installateur : `artifacts\installer\LumoraSetup-0.78.3.4.19-dev-win-x64.exe`
- SHA256 installateur :
  `af2be9fa5e9c4928aac2f694e9af6d37d2b4287d17b651228cfc3ee9ef66aadc`
