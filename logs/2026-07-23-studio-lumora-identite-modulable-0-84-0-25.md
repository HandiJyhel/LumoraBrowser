# Studio Lumora, identite modulable et menu contextuel - 0.84.0.25-dev

## Contexte

L'utilisateur voulait que Lumora cesse de ressembler a un navigateur standard
et prenne une vraie identite graphique, tout en laissant a chacun la
possibilite de faconner son ergonomie sans passer par une suite de reglages
lourds. Les favoris et les onglets devaient devenir des briques reconfigurables
en un geste, et le menu contextuel devait participer a cette personnalisation.

## Changements

- ajout d'un bouton `Studio Lumora` directement dans le chrome principal, avec
  des actions instantanees pour :
  - les presets `Halo`, `Atelier` et `Flux` ;
  - la position des onglets (`haut`, `bas`, `gauche`, `droite`) ;
  - la position des favoris (`haut`, `bas`, `gauche`, `droite`) ;
  - le mode de luminosite Lumora (`sombre`, `clair`, `systeme`) ;
  - l'affichage des favoris et le mode compact ;
- application immediate des positions d'onglets et de favoris depuis
  `Mon Lumora`, sans passer par le bouton global d'application du panneau ;
- ajout d'une persistance ciblee des reglages d'espace de travail pour sauver
  les changements ergonomiques sans imposer l'enregistrement d'autres options
  d'apparence encore en attente ;
- enrichissement du menu contextuel sur les zones de travail
  (`barre de favoris`, `rail lateral`, `zone basse`, `rail d'onglets`) avec les
  memes actions de composition rapide ;
- enrichissement du menu contextuel des favoris avec :
  - `Ouvrir dans un nouvel onglet` ;
  - un sous-menu `Studio Lumora` reprenant les actions de modularite ;
- renforcement visuel des favoris pour mieux marquer l'identite Lumora :
  hauteur, padding, rayon et estimation de largeur augmentes ;
- augmentation de la respiration du chrome des favoris et du flyout pour donner
  plus de relief a l'interface ;
- verification de `run-winui.cmd` et `scripts/run-winui.ps1` :
  aucun ajustement necessaire sur cette passe, le chemin de lancement reste
  coherent avec le pipeline WinUI corrige a l'etape precedente ;
- montee de version source a `0.84.0.25-dev`.

## Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
  : succes, `0 avertissement`, `0 erreur` ;
- `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore --filter UsageModeVisualIdentityTests -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
  : succes.

## Impact

Lumora ne se contente plus d'un theme clair/sombre pose sur un navigateur
classique. Le navigateur dispose maintenant d'un point de commande central pour
composer son ergonomie, d'un menu contextuel qui participe a cette logique et
d'un chrome de favoris plus assume visuellement. L'utilisateur peut modifier sa
disposition principale en quelques clics, sans manipulations techniques ni
parcours cache.
