# Chrome Lumie premium - 0.83.10-dev

Cette etape corrige le rendu trop retro des boutons `Lumie` et `Mode`.
L'objectif est de garder une interface identifiable sans tomber dans des gros
contours cyan/or ou une esthétique arcade.

## Direction visuelle

- `Lumie` devient une capsule de compagnon plus douce, avec un fond verre mat,
  un petit point d'activite et une separation discrete entre le nom et le mode.
- `Mode` devient un selecteur plus calme : fond legerement teinte, fine barre
  d'accent, libelle `Mode` discret et nom du mode mis en avant.
- Les icones ne sont plus enfermees dans un carre interne epais.
- Les bordures sont moins fortes et derivees par teinte subtile du mode actif.
- Le duo cyan/or reste disponible comme signature Lumora, mais il n'est plus
  applique en aplats massifs sur les boutons.

## Comportement par mode

Les couleurs continuent de suivre le mode actif, mais via une couche de teinte
plus fine :

- Lumie utilise surtout l'accent frais du mode.
- Le selecteur de mode utilise surtout l'accent principal du mode.
- Les couleurs sont melangees avec la surface du chrome pour eviter l'effet
  bouton retro.

## Accessibilite

- Le contraste renforce garde un rendu explicite noir/blanc/accent.
- Les controles restent dans le traitement d'accessibilite existant.
- Aucun changement n'ajoute de service distant, de telemetrie ou de
  synchronisation.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` hors sandbox
  apres blocage ACL local : 512/512 tests reussis.
- Restore MSBuild WinUI hors sandbox : 0 avertissement, 0 erreur.
- Build WinUI Debug vers
  `artifacts\build-verify\winui-0.83.10-debug\` : 0 avertissement, 0 erreur.

## Limites

- Aucun lancement automatique de Lumora effectue.
- Aucun installateur ni executable de release genere.
