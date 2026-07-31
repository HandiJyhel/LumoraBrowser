# Molette globale, dedup et pas normalise - 0.84.1.7-dev

## Contexte

Apres `0.84.1.6-dev`, l'utilisateur remonte un comportement reel tres clair :
- la molette ne fonctionne plus que dans le menu Demarrer ;
- elle ne permet que de descendre ;
- remonter ne fonctionne pas.

## Diagnostic

Deux problemes restaient melanges :
- le hook global avait ete retire a tort dans une passe precedente ;
- plusieurs hooks maison pouvaient retraiter le meme evenement de molette,
  ce qui rendait le comportement instable selon la surface ;
- le calcul utilisait la valeur brute `MouseWheelDelta` au lieu d'un pas
  de defilement normalise, peu lisible et trop brutal.

## Correctif applique

- `MainWindow.xaml.cs`
  - reactivation du hook global via `HookManualWheelScrollFallback(root)` ;
  - ajout d'un jeton `_lastManualWheelHandledToken` base sur
    `timestamp + pointerId` pour eviter que plusieurs handlers Lumora
    retraitent le meme evenement ;
  - remplacement du deplacement brut par un pas normalise :
    `56 * max(1, abs(delta)/120)`, applique avec le bon signe.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~UsageModeVisualIdentityTests"`
  - reussi : 19/19 tests.

## Version

- Version courante : `0.84.1.7-dev`
- Regle respectee : increment du 4e chiffre uniquement.
