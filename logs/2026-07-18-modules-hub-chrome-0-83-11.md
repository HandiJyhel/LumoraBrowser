# 2026-07-18 - Hub Modules chrome (0.83.11-dev)

## Contexte

Retour utilisateur : le bouton Modules garde le bon role, comparable au bouton
extensions des autres navigateurs, mais son icone faisait tache par rapport a
la direction artistique Lumora. Le bouton menu principal n'etait plus visible.

Diagnostic : la barre de navigation n'avait que 20 colonnes, alors que le
separateur et le bouton `NavigationMenuButton` etaient places en colonnes 20 et
21.

## Changements

- Passage de la version projet a `0.83.11-dev`.
- Remplacement du style `NovaModulePuzzleButtonStyle` par
  `NovaModuleHubButtonStyle`.
- Ajout des ressources visuelles du hub modules :
  `NovaModuleHubGlassBrush`, `NovaModuleHubStrokeBrush`,
  `NovaModuleHubNodeBrush` et `NovaModuleHubAccentBrush`.
- Remplacement de l'icone simple par une mini grille de modules directement en
  XAML.
- Teinte du bouton Modules via la palette du mode actif dans
  `ApplyUsageModeChrome`.
- Ajout de deux colonnes a la barre de navigation pour restaurer le menu.
- Nommage des colonnes finales :
  `NavigationMenuDividerColumn` et `NavigationMenuButtonColumn`.
- Mise a jour des tests de regression visuelle/chrome.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` hors sandbox
  apres blocage ACL local : 512/512 tests reussis.
- Restore MSBuild WinUI hors sandbox : 0 avertissement, 0 erreur.
- Build WinUI Debug hors sandbox vers
  `artifacts\build-verify\winui-0.83.11-debug\` : 0 avertissement, 0 erreur.

## Notes

- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora effectue.
