# Molette Parametres : diagnostic du focus apres une butee de defilement - 0.84.1.15-dev

## Contexte

Le correctif `0.84.1.14-dev` (FocusState.Pointer au changement de section) a
ete teste : la molette fonctionne desormais sur la 1re section ET apres
changement de section (confirme par l'utilisateur et par la trace). Mais
l'utilisateur signale un nouveau symptome : "au bout d'un moment, ca
s'arrete" - la molette finit par se re-bloquer sans raison apparente.

## Lecture de la trace (run 20260725-210908, fin de fichier)

- Jusqu'a la ligne 868 : des dizaines d'allers-retours de defilement
  reussissent dans la meme section (`scrollableHeight=236`), `handled=True`
  a chaque fois, `Settings viewer view changed` suit systematiquement.
- A 21:10:13.83 : dernier defilement reussi, `offset=236` (fond de la
  section), `intermediate=False` (animation terminee).
- A partir de 21:10:13.94 : **8 signaux molette consecutifs, tous
  `handled=False`**, plus aucun `Settings viewer view changed` - le
  defilement s'arrete net, juste apres avoir atteint une butee et termine
  une animation.

**Hypothese retenue** : le focus quitte le `ScrollViewer` a ce moment precis
(possible effet de bord de la fin d'animation de `ChangeView`), reproduisant
la meme famille de cause que `0.84.1.14-dev` (focus peu fiable), mais
declenchee ici par l'arrivee en butee plutot que par un changement de
section.

## Correctif applique (diagnostic pur, aucun changement de comportement)

- `MainWindow.xaml.cs` : `SettingsPanel_RootWheelDiagnostics(...)` trace
  desormais en plus, a chaque signal molette :
  - `SettingsContentScrollViewer.FocusState` ;
  - le type et le nom de l'element reellement focus
    (`FocusManager.GetFocusedElement(SettingsPanel.XamlRoot)`).
  - Objectif : confirmer ou infirmer, avec une preuve directe, que le focus
    quitte bien le `ScrollViewer` au moment exact ou `handled` bascule en
    `False` de facon permanente.
- Tests : `KeyboardFocusRegressionTests.cs` verrouille l'ajout.
  `UsageModeVisualIdentityTests.cs`, `AGENTS.md`, les scripts et
  `MainWindow.xaml.cs` passent a `0.84.1.15-dev` - **4e chiffre uniquement**.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~WheelScrollMathTests|FullyQualifiedName~UsageModeVisualIdentityTests"` :
  **reussi, 27/27 tests**.
- `MSBuild.exe Lumora.WinUI\\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /p:Restore=false` :
  **reussi**.

## Prochaine etape (pas encore faite)

Reproduire l'echec ("ca s'arrete au bout d'un moment") avec
`LUMORA_TRACE_STARTUP=1`, puis lire `winui-runtime-trace.log` au moment ou
`handled` bascule en `False` de facon permanente : si
`scrollViewerFocusState` n'est plus `Pointer` a ce moment (et/ou
`focusedElement` n'est plus le `ScrollViewer`), l'hypothese de perte de
focus est confirmee et le correctif consistera a re-forcer
`FocusState.Pointer` sur le `ScrollViewer` a la fin de chaque animation de
defilement (evenement `ViewChanged` avec `IsIntermediate=false`), pas
seulement au changement de section.

## Version

- Version courante : `0.84.1.15-dev`
- Regle respectee : increment du 4e chiffre uniquement.
