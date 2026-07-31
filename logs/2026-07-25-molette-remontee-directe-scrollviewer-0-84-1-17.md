# Molette Parametres : remontee directe vers le ScrollViewer a chaque evenement - 0.84.1.17-dev

## Contexte

Le test suivant `0.84.1.16-dev` a fourni la preuve manquante. L'utilisateur
a pousse le test jusqu'au blocage complet avec le diagnostic de focus actif.
Resultat sans ambiguite : `foregroundWindow` et `focusWin32` restent
**identiques** (memes valeurs exactes) du debut a la fin du test, y compris
pile au moment ou le defilement passe de fonctionnel a bloque. Le focus
XAML (`scrollViewerFocusState=Pointer`, `focusedElement=SettingsContentScrollViewer`)
reste lui aussi identique. Aucune des trois notions de focus testees
jusqu'ici (XAML, Win32 clavier, premier plan Windows) ne change au moment
de la panne : ce n'etait donc pas une histoire de focus, contrairement a
toutes les hypotheses precedentes de cette serie.

## Cause reelle identifiee

En croisant la trace `Settings panel root wheel` avec la trace
`Molette ScrollViewer : defilement applique` : quand la molette fonctionne
sans notre propre code (aucune ligne "Molette ScrollViewer" alors que
`handled=True` et l'offset change), c'est WinUI qui gere nativement. Quand
la molette casse (`handled=False`), **aucune trace "Molette ScrollViewer"
n'apparait non plus** - preuve directe qu'aucun handler, ni natif ni le
notre, n'a jamais ete invoque pour l'element precis sous le curseur a ce
moment (un `ContentPresenter` different de ceux qui fonctionnaient juste
avant). Recoupement avec `WheelScrollMath.TryComputeNextVerticalOffset` :
meme en testant les deux sens de molette (`delta=-120` ET `delta=120`),
aucun des deux n'a produit le moindre effet - ce qui exclut un simple
"deja a la butee, rien a faire" (qui n'expliquerait que le sens qui
continue vers la butee, pas le sens inverse).

**Conclusion** : notre rattachement de handler aux descendants du
`ScrollViewer` se fait UNE SEULE FOIS, au moment ou le panneau Parametres
devient visible (`AttachScrollViewerPointerSupport` dans `ShowPanel`). WinUI
regenere dynamiquement certains elements de contenu (typiquement un
`ContentPresenter` de template) apres la fin d'une animation de defilement,
une fois la vue "installee" a sa position finale. Ces elements regeneres
n'ont jamais existe au moment du rattachement initial : ni notre handler, ni
la logique native ne les couvre plus.

## Correctif applique

- `MainWindow.xaml.cs` : `SettingsPanel_RootWheelDiagnostics(...)` (deja
  attache au panneau via `AddHandler(..., handledEventsToo: true)`) ajoute
  desormais un filet de secours reel : si l'evenement n'est pas encore
  traite (`!e.Handled`), on remonte l'arbre visuel EN DIRECT depuis
  `e.OriginalSource` (`VisualTreeHelper.GetParent(...)` en boucle) jusqu'au
  premier `ScrollViewer` ancetre trouve, puis on lui applique
  `TryApplyScrollViewerWheel(...)`. Cette remontee se fait a CHAQUE
  evenement, sans dependre d'un rattachement prealable : peu importe qu'un
  element ait ete cree apres le rattachement initial, il sera toujours
  trouve au moment de l'evenement lui-meme.
- Tests : `KeyboardFocusRegressionTests.cs` verrouille la remontee directe.
  `UsageModeVisualIdentityTests.cs`, `AGENTS.md`, les scripts et
  `MainWindow.xaml.cs` passent a `0.84.1.17-dev` - **4e chiffre uniquement**.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~WheelScrollMathTests|FullyQualifiedName~UsageModeVisualIdentityTests"` :
  **reussi, 29/29 tests**.
- `MSBuild.exe Lumora.WinUI\\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /p:Restore=false` :
  **reussi**.
- Test physique reel : pas encore fait sur cette version, a confirmer par
  l'utilisateur.

## Version

- Version courante : `0.84.1.17-dev`
- Regle respectee : increment du 4e chiffre uniquement.
