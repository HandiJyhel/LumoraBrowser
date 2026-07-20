# Raccourcis confort et annonces - 2026-07-19

## Contexte

Lumora dispose deja d'aides clavier au-dela du navigateur standard, mais une
partie de cette valeur restait cachee : il fallait connaitre les
raccourcis avant meme de pouvoir en profiter.

## Ajouts

- Nouvelle carte `Raccourcis Lumora` dans `Parametres > Confort`.
- Rappels visibles des commandes :
  - `F6` / `Maj+F6`
  - `Ctrl+Alt+1..5`
  - `Ctrl+K`
  - `Win+H`
- Nouveau bouton `Faire annoncer les raccourcis`.

## Comportement

- Le bouton met a jour la barre d'etat avec un message court :
  `Aide clavier Lumora annoncee.`
- Les details sont ensuite relus par les annonces d'accessibilite, pour ne
  pas surcharger visuellement la barre d'etat tout en restant utiles aux
  lecteurs d'ecran.

## Verification prevue

- `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
