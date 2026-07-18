# 2026-07-17 - Chrome Lumie premium (0.83.10-dev)

## Contexte

Retour utilisateur apres capture : les boutons `Lumie` et `Mode Equilibre`
sont lisibles mais pas assez modernes. Le rendu est percu comme trop retro,
avec des contours trop presents et des boutons trop proches d'une interface
ancienne.

## Changements

- Passage de la version projet a `0.83.10-dev`.
- Ajout de ressources dediees au rendu moderne :
  `NovaCompanionGlassBrush`, `NovaCompanionStrokeBrush`,
  `NovaModeSelectorGlassBrush` et `NovaModeSelectorStrokeBrush`.
- Refonte du bouton `Lumie` :
  - capsule plus arrondie et plus legere ;
  - petit point d'activite colore ;
  - fond teinte par l'accent frais du mode ;
  - suppression du carre interne epais autour de l'icone.
- Refonte du bouton `Mode` :
  - fine barre d'accent ;
  - libelle `Mode` discret ;
  - nom du mode separe dans `UsageModeCurrentText` ;
  - chevron et icone plus calmes.
- Ajout de `ChromeTint` pour melanger les accents avec les surfaces du chrome,
  au lieu d'utiliser des aplats cyan/or trop forts.
- Mise a jour des tests de regression visuelle.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` hors sandbox
  apres blocage ACL local : 512/512 tests reussis.
- Restore MSBuild WinUI hors sandbox : 0 avertissement, 0 erreur.
- Build WinUI Debug hors sandbox vers
  `artifacts\build-verify\winui-0.83.10-debug\` : 0 avertissement, 0 erreur.

## Notes

- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora effectue.
