# Molette Parametres : filet de secours applique directement au ScrollViewer connu - 0.84.1.18-dev

## Contexte

Le filet de secours de `0.84.1.17-dev` (remontee de l'arbre visuel depuis
`e.OriginalSource` jusqu'au premier `ScrollViewer` ancetre) a ete teste en
reel : l'utilisateur rapporte que "ça fonctionne à peu près partout, mais
pas sur toutes les fenêtres". Lecture de la trace : la remontee ne s'est
**jamais** declenchee (aucune ligne "root-fallback-remontee-directe") dans
les cas encore casses, alors que `handled=False` persistait sur des
sections avec un vrai contenu scrollable (`scrollableHeight=1541`). La
remontee d'arbre n'atteignait donc pas `SettingsContentScrollViewer` pour
ces sources precises - topologie de l'arbre visuel plus complexe que prevu
(probablement liee au template interne du `ScrollViewer` lui-meme).

## Correctif applique

- `MainWindow.xaml.cs` : `SettingsPanel_RootWheelDiagnostics(...)` applique
  desormais directement `TryApplyScrollViewerWheel(SettingsContentScrollViewer, ...)`
  quand l'evenement n'est pas encore traite, sans passer par une remontee
  d'arbre incertaine. Ce panneau n'a qu'un seul ScrollViewer de contenu
  pertinent : inutile de le retrouver dynamiquement, on le vise directement,
  peu importe l'element exact sous le curseur au moment de l'evenement.
- Tests : `KeyboardFocusRegressionTests.cs` mis a jour pour verrouiller cette
  version simplifiee. `UsageModeVisualIdentityTests.cs`, `AGENTS.md`, les
  scripts et `MainWindow.xaml.cs` passent a `0.84.1.18-dev` - **4e chiffre
  uniquement**.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~WheelScrollMathTests|FullyQualifiedName~UsageModeVisualIdentityTests"` :
  **reussi, 29/29 tests**.
- `MSBuild.exe Lumora.WinUI\\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /p:Restore=false` :
  **reussi**.
- Test physique reel : pas encore fait sur cette version, a confirmer.

## Note importante

L'utilisateur signale que la molette ne fonctionne "pas sur toutes les
fenêtres" - ce correctif ne couvre que le panneau Parametres
(`SettingsPanel`). Si d'autres panneaux (Favoris, Historique, Coffre...)
ont le meme symptome, la meme cause racine (rattachement statique une seule
fois, elements regeneres dynamiquement non couverts) s'y applique
probablement aussi, et le meme type de filet de secours direct devra y etre
ajoute - mais cible precisement une fois les panneaux concernes identifies,
plutot que d'ajouter le meme code partout par precaution.

## Version

- Version courante : `0.84.1.18-dev`
- Regle respectee : increment du 4e chiffre uniquement.
