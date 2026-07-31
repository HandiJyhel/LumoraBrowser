# Molette native WinUI, ScrollViewer reels sans hooks globaux - 0.84.1.9-dev

## Contexte

Retour utilisateur tres direct apres la serie `0.84.1.4-dev` a
`0.84.1.8-dev` : la molette etait encore percue comme casse en clair comme
en sombre. Le point important n'etait pas le theme, mais la regression du
comportement natif dans les surfaces WinUI de Lumora.

## Diagnostic retenu

- la pile de correctifs precedente avait empile des hooks sur la racine, les
  panneaux et certains overlays ;
- les tests "molette" validaient surtout la presence de lignes de code, pas un
  comportement utile ;
- la logique la plus fragile etait justement celle qui essayait de retrouver le
  bon `ScrollViewer` depuis des hooks trop globaux.

## Correctif applique

- `Lumora.WinUI/MainWindow.xaml.cs`
  - suppression du pilotage molette depuis des hooks globaux de panneau/fenetre ;
  - nouveau raccord direct sur les `ScrollViewer` reels via
    `AttachScrollViewerPointerSupport(...)` ;
  - chaque `ScrollViewer` recoit :
    - `PointerEntered` pour rendre le focus au survol ;
    - `PointerWheelChanged` via `AddHandler(..., handledEventsToo: true)` ;
  - les flyouts et popups rebranchent la meme logique quand leur contenu
    apparait.
- `Lumora.WinUI/WheelScrollMath.cs`
  - extraction d'un helper pur pour calculer le prochain offset vertical, avec
    clamp et pas normalise.
- `Lumora.Tests`
  - ajout de `WheelScrollMathTests.cs` pour tester le vrai calcul de
    defilement ;
  - `KeyboardFocusRegressionTests.cs` mis a jour pour verrouiller le nouveau
    raccord direct sur les `ScrollViewer` au lieu des anciens hooks globaux.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~WheelScrollMathTests|FullyQualifiedName~UsageModeVisualIdentityTests"`
  - reussi : `24/24` tests.
- `dotnet build Lumora.WinUI\\Lumora.WinUI.csproj --no-restore`
  - toujours bloque par l'environnement WinUI/MSBuild local :
    `Microsoft.Build.Packaging.Pri.Tasks.dll` introuvable.

## Version

- Version courante : `0.84.1.9-dev`
- Regle respectee : increment du 4e chiffre uniquement.
