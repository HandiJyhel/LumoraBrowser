# 2026-07-23 - Relief du chrome et favoris plus presents (0.84.0.22-dev)

## Contexte

Apres la passe `0.84.0.21-dev`, l'utilisateur valide la direction mais estime
que le chrome reste encore trop proche d'un navigateur classique. Les
attentes exprimees sont concretes :

- pousser davantage l'identite visuelle ;
- donner plus de relief aux boutons ;
- grossir legerement les favoris pour les rendre plus presents.

## Modifications appliquees

### MainWindow.xaml

- ajout de nouvelles brosses de relief :
  `NovaChromeButtonShadowBrush`,
  `NovaChromeButtonHighlightBrush`,
  `NovaBookmarkBarButtonBackgroundBrush`,
  `NovaBookmarkBarButtonBorderBrush`,
  `NovaBookmarkBarButtonForegroundBrush` ;
- ajout de deux templates dedies :
  `NovaRaisedIconButtonTemplate` pour les boutons d'action du chrome ;
  `NovaRaisedBookmarkBarButtonTemplate` pour la barre de favoris ;
- `NovaChromeIconButtonStyle` agrandi et moins plat :
  `32x32`, coins plus presents, template releve avec ombre basse et reflet
  superieur ;
- `NovaModuleIconButtonStyle` suit la meme logique ;
- nouvelle ressource `NovaBookmarkBarButtonStyle` pour donner un vrai rendu
  de capsule aux favoris ;
- ligne de favoris agrandie : `BookmarksRow` passe a `34`, padding vertical
  plus genereux.

### MainWindow.SettingsTheme.cs

- calcul des nouvelles brosses de relief dans les themes clair, sombre et
  contraste eleve ;
- integration de ces surfaces au moteur de theme existant, pour que
  l'effet reste coherent avec les palettes Lumora.

### MainWindow.Bookmarks.cs

- les favoris de la barre adoptent `NovaBookmarkBarButtonStyle` ;
- hauteur portee a `28`, coins plus doux, padding plus large ;
- titre des favoris legerement agrandi (`FontSize = 12`) ;
- largeur estimee ajustee pour suivre ce nouveau rendu ;
- bouton de debordement (`»`) harmonise avec le meme style releve.

### Version et tests

- version montee en `0.84.0.22-dev` dans :
  `MainWindow.xaml.cs`, `AGENTS.md`,
  `scripts/build-installer.ps1`,
  `scripts/build-clean-test-artifact.ps1` et
  `UsageModeVisualIdentityTests` ;
- renfort des assertions de `UsageModeVisualIdentityTests` sur les templates
  de relief et le style de la barre de favoris.

## Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
  : succes, `0 avertissement`, `0 erreur` ;
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --filter UsageModeVisualIdentityTests`
  : `11/11` OK.

## Livraison

- aucun installateur ni executable de release genere sur cette passe,
  conformement a la regle projet actuelle.

## Version

- `0.84.0.22-dev` - quatrieme chiffre uniquement, palier `0.84.0` inchange.
