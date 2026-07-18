# 2026-07-18 - Accueil et modes alleges (0.83.15-dev)

## Contexte

Retour utilisateur direct : l'idee etait bonne, mais le rendu restait trop
gros en bas et trop lourd dans l'accueil, surtout en mode `Equilibre`. Il
fallait conserver l'intention tout en nettoyant la composition.

## Actions

- Version passee a `0.83.15-dev`.
- Alleger visuellement les boutons bas `Lumie` et `Mode d'usage`.
- Garder `Neutre` comme mode de base avec raccourci rapide visible.
- Rendre le compagnon du bas disponible aussi en mode `Neutre`.
- Reintegrer les raccourcis rapides dans l'accueil `Neutre`.
- Recomposer la grille d'accueil pour donner plus d'air au contenu principal et
  a la colonne laterale.
- Simplifier le mode `Equilibre` en supprimant le bloc d'introduction en
  doublon et en repensant son dock.
- Mettre a jour les tests de garde-fou sur les modes et la structure visuelle.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 516 tests
  reussis.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : restore
  WinUI reussi, build WinUI reussi, 0 avertissement, 0 erreur.

## Notes

- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora effectue.
