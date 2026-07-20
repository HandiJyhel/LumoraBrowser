# Lumora 0.83.46-dev - Mode secours de confort

## Intent

Ajouter un lot visible et vite utile sans repartir dans un audit complet :
un bouton "reprendre la main" quand la page, la fatigue visuelle ou la
densite de l'interface deviennent trop penibles.

## Changements

- Nouveau preset `Mode secours` dans les profils de confort.
- Nouveau fichier `Lumora.WinUI/MainWindow.AccessibilityRescue.cs`.
- Nouveaux raccourcis :
  - `Ctrl+Alt+S` : active le mode secours.
  - `Ctrl+Alt+X` : restaure l'etat precedent.
- Le mode secours capture l'etat courant avant de l'ecraser, puis applique
  une posture lisible et stable :
  - contraste renforce ;
  - texte plus lisible ;
  - transitions reduites ;
  - focus visible ;
  - loupe de lecture active ;
  - guide de lecture actif en bande `220 px`.
- Le footer `Confort rapide` montre maintenant aussi l'etat du retour
  disponible et propose deux actions directes :
  - `Activer le mode secours`
  - `Revenir a l'etat d'avant`

## Pourquoi

- Les presets existants sont utiles, mais demandent encore de choisir une
  posture. Le mode secours reduit cette hesitation : un geste pour lisibilite
  maximale, un geste pour retour arriere.
- Cette approche reste distinctive par rapport a un navigateur classique :
  ce n'est pas juste une liste d'options, c'est un filet de rattrapage
  reversible.

## Verification prevue

- `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
