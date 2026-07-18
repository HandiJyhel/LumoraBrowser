# Accueil et modes alleges - 0.83.15-dev

## Objectif

Conserver l'idee du bas de chrome et des modes d'accueil, mais avec un rendu
plus discret, plus aere et plus agreable a lire.

## Changement

- Les boutons `Lumie` et `Mode d'usage` du bas deviennent plus discrets.
- Le mode `Neutre` reste le mode de base et garde un acces visible aux
  raccourcis rapides.
- Le compagnon reste accessible en mode `Neutre`.
- Le mode `Equilibre` perd son empilement de cartes trop lourd.
- Les autres modes profitent aussi d'une grille et de panneaux plus aeres.

## Intention produit

L'accueil doit respirer davantage :

- la zone centrale garde la recherche et les reperes rapides ;
- la colonne laterale ne doit plus donner une impression de bloc serre ;
- le mode `Neutre` doit rester le plus simple et le plus pratique au quotidien ;
- le mode `Equilibre` doit etre utile sans devenir envahissant.

## Implementation

- Ajout de styles bas dedies : `NovaFooterPillButtonStyle` et
  `NovaFooterCompanionPillButtonStyle`.
- Reduction du poids visuel de `ModeCompanionButton` et `UsageModeButton`.
- Conservation du compagnon en mode `Neutre` avec action rapide
  `Ajouter un raccourci`.
- Reactivation de `NewTabShortcutsHtml()` dans l'accueil `Neutre`.
- Refonte de `.home-grid`, `.mode-side` et `.mode-panel` pour mieux repartir
  les volumes.
- Recomposition de l'accueil `Equilibre` avec `balanced-dock` sur deux colonnes
  et suppression du bloc d'introduction redondant.
- Ajustement des tests de garde-fou sur les modes et sur la structure visuelle.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 516 tests
  reussis.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : restore
  WinUI reussi, build WinUI reussi, 0 avertissement, 0 erreur.
