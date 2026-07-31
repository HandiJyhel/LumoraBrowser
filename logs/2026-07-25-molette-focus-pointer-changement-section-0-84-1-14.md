# Molette Parametres : FocusState.Pointer au changement de section - 0.84.1.14-dev

## Contexte

Le diagnostic `0.84.1.13-dev` a ete teste par l'utilisateur, qui a ensuite
precise le symptome exact : dans Parametres, la molette fonctionne sur la
**premiere** section affichee a l'ouverture, mais s'arrete des qu'on change
de section via le menu de gauche.

## Lecture de la trace (`winui-runtime-trace.log`, run 20260725-205535)

- Sur la premiere section (`scrollableHeight=43`) : un evenement molette
  atteint bien `SettingsPanel` (`Settings panel root wheel (diagnostic pur)`,
  `handled=True`) et le `ScrollViewer` defile reellement juste apres
  (`view changed`, offset 0->43 puis 43->0) - la molette fonctionne, geree
  nativement par WinUI (aucune trace `Molette ScrollViewer` de notre propre
  code de secours, notre handler se retire correctement des que
  `e.Handled` est deja vrai).
- Apres changement de section (`scrollableHeight=1541`) : 3 signaux de
  molette bruts (`WM_POINTERWHEEL`/`WM_MOUSEWHEEL`) atteignent bien la
  fenetre, mais **aucun** ne remonte jusqu'a `SettingsPanel` ni au
  `ScrollViewer` - exactement le symptome decrit ("ca ne bouge plus").

## Cause identifiee

`SettingsNav_Click(...)` appelle `ResetSettingsScrollPosition()` a chaque
changement de section, qui re-applique le focus via
`SettingsContentScrollViewer.Focus(FocusState.Programmatic)`. Ce fichier
documente deja, pour un probleme identique rencontre sur WebView2
(`BrowserHost_PointerEntered`) et sur le survol de ScrollViewer
(`ScrollViewer_PointerEntered`), que `FocusState.Programmatic` est le mode
peu fiable pour faire fonctionner la molette, et que `FocusState.Pointer`
est le mode qui marche. Sur la premiere section, la souris survole
naturellement le contenu et pose ce focus fiable via
`ScrollViewer_PointerEntered`. Changer de section ecrasait ensuite ce focus
fiable avec la version peu fiable, cassant la molette jusqu'a un nouveau
survol qui ne se produit pas forcement (le clic de navigation se fait a
gauche, hors du `ScrollViewer`).

## Correctif applique

- `MainWindow.xaml.cs` : `ResetSettingsScrollPosition()` utilise desormais
  `SettingsContentScrollViewer.Focus(FocusState.Pointer)` au lieu de
  `FocusState.Programmatic`.
- Tests : `KeyboardFocusRegressionTests.cs` verrouille la nouvelle valeur.
  `UsageModeVisualIdentityTests.cs`, `AGENTS.md`, les scripts et
  `MainWindow.xaml.cs` passent a `0.84.1.14-dev` - **4e chiffre uniquement**.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore --filter "FullyQualifiedName~KeyboardFocusRegressionTests|FullyQualifiedName~WheelScrollMathTests|FullyQualifiedName~UsageModeVisualIdentityTests"` :
  **reussi, 26/26 tests**.
- `MSBuild.exe Lumora.WinUI\\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /p:Restore=false` :
  **reussi**.
- Test physique reel par l'utilisateur : pas encore fait sur cette version,
  a confirmer.

## Version

- Version courante : `0.84.1.14-dev`
- Regle respectee : increment du 4e chiffre uniquement.
