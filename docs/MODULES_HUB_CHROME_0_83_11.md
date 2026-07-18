# Hub Modules chrome - 0.83.11-dev

Cette etape corrige le bouton d'acces aux modules Lumora. La fonction reste la
meme que le bouton extensions des navigateurs classiques : un point d'entree
compact vers les modules/outils du navigateur. Le changement porte sur la
direction artistique et sur la restauration du menu principal.

## Changements visuels

- L'ancien bouton modules de type icone simple est remplace par un mini hub :
  quatre blocs de modules, un bloc accentue et un point central Lumora.
- Le bouton utilise maintenant `NovaModuleHubButtonStyle`, avec un fond verre
  mat et une bordure teintee par l'accent du mode actif.
- Les couleurs internes du hub suivent les ressources du chrome :
  `NovaModuleHubGlassBrush`, `NovaModuleHubStrokeBrush`,
  `NovaModuleHubNodeBrush` et `NovaModuleHubAccentBrush`.
- Le bouton reste compact, comme un bouton extensions, et garde son flyout
  `Extensions Lumora`.

## Correction du menu

- La barre de navigation contient maintenant 22 colonnes.
- Les deux colonnes finales sont nommees :
  `NavigationMenuDividerColumn` et `NavigationMenuButtonColumn`.
- Le separateur en colonne 20 et le bouton `NavigationMenuButton` en colonne 21
  redeviennent donc visibles et valides.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` hors sandbox
  apres blocage ACL local : 512/512 tests reussis.
- Restore MSBuild WinUI hors sandbox : 0 avertissement, 0 erreur.
- Build WinUI Debug vers
  `artifacts\build-verify\winui-0.83.11-debug\` : 0 avertissement, 0 erreur.

## Limites

- Aucun lancement automatique de Lumora effectue.
- Aucun installateur ni executable de release genere.
