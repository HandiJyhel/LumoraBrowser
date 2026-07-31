# Molette reroutee depuis les enfants vers le ScrollViewer reel - 0.84.1.11-dev

## Contexte

Apres `0.84.1.10-dev`, le retour utilisateur restait net : dans Parametres,
la molette ne fonctionnait toujours pas. Il fallait verifier le runtime reel
au lieu de supposer que le `ScrollViewer` etait encore la partie fautive.

## Constat runtime

- `SettingsContentScrollViewer` est bien un vrai `ScrollViewer` WinUI actif.
- Dans `Vie privee locale`, sa surface scrollable mesure bien plus que la
  zone visible :
  - `ScrollableHeight=2153`
  - `ExtentHeight=2815`
  - `ViewportHeight=662`
- Un scroll force via UI Automation (`ScrollPattern`) deplace reellement la
  vue et declenche `view changed`.
- Donc le defilement natif existe ; la rupture se situe avant, dans
  l'acheminement de la molette depuis les controles enfants.

## Correctif applique

- `MainWindow.xaml.cs`
  - ajout d'une table de correspondance `UIElement -> ScrollViewer` pour les
    sources de molette situees dans le contenu d'un `ScrollViewer` ;
  - branchement de `PointerWheelChanged` sur les descendants eux-memes, avec
    `handledEventsToo: true` ;
  - routage de la molette vers le `ScrollViewer` ancetre le plus proche via
    `TryApplyScrollViewerWheel(...)` ;
  - garde `e.Handled` pour eviter qu'un descendant hooke puis le
    `ScrollViewer` parent ne defilent deux fois.

## Verification

- build WinUI Debug reussie avec :
  `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe Lumora.WinUI\\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /p:Restore=false`
- limite de verification restante :
  - l'automatisation locale utilisee pour simuler la molette Windows n'a pas
    donne une preuve visuelle totalement fiable du geste utilisateur final ;
    en revanche, le diagnostic runtime a clairement isole la perte d'evenement
    sur les controles enfants, et le correctif cible exactement ce point.

## Version

- Version courante : `0.84.1.11-dev`
- Regle respectee : increment du 4e chiffre uniquement.
