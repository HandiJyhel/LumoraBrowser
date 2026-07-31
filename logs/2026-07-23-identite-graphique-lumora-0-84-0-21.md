# 2026-07-23 - Identite graphique Lumora du chrome principal (0.84.0.21-dev)

## Contexte

L'utilisateur recadre la demande initiale : le besoin principal n'etait pas
de produire du cadrage ou de la simple documentation, mais de donner a
Lumora une vraie identite graphique qui sorte de l'ordinaire. Les exemples
de favoris ou d'onglets deplacables restaient secondaires ; la priorite de
cette passe devient donc le chrome principal du navigateur lui-meme.

## Objectif

Faire en sorte que Lumora cesse de ressembler a un navigateur standard
simplement recolore, et qu'il affiche enfin des codes visuels propres :

- lumiere guidee plutot qu'effet gratuit ;
- repere de marque visible dans les zones les plus frequentes ;
- chrome plus atmospherique, sans perdre la lisibilite.

## Modifications appliquees

### MainWindow.xaml

- ajout de nouvelles brosses visuelles pour le chrome :
  `NovaChromeHaloWarmBrush`, `NovaChromeHaloCoolBrush`,
  `NovaChromeMistBrush`, `NovaBrandChipBackgroundBrush`,
  `NovaBrandChipBorderBrush`, `NovaBrandChipForegroundBrush` et
  `NovaBrandChipMutedBrush` ;
- transformation de la bande d'onglets haute avec un fond plus vivant :
  halos chaud/froid et liseré lumineux discret sous les onglets ;
- ajout d'un `TabStripHeader` sur `BrowserTabs` avec une capsule de marque
  `LUMORA` + mention `lumiere locale` ;
- enrichissement de `NavigationToolbar` avec deux halos ambiants et une
  nappe lumineuse legere derriere la zone centrale ;
- transformation de la barre d'adresse en repere Lumora visible via une
  capsule interne de marque, sans casser le fonctionnement du `TextBox`.

### MainWindow.SettingsTheme.cs

- calcul des nouvelles brosses de halo, de brume et de capsule de marque
  dans les themes clair, sombre et contraste eleve ;
- integration de ces ressources au moteur de theme existant, de facon
  coherente avec les palettes de mode d'usage deja en place.

### Tests et version

- version montee en `0.84.0.21-dev` dans :
  `MainWindow.xaml.cs`, `AGENTS.md`,
  `scripts/build-installer.ps1`,
  `scripts/build-clean-test-artifact.ps1` et
  `UsageModeVisualIdentityTests` ;
- `Lumora.Tests/UsageModeVisualIdentityTests.cs` renforce ses assertions sur
  les nouveaux marqueurs d'identite visuelle :
  halos, capsule de marque, `TabView.TabStripHeader`, libelles Lumora.

## Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
  : succes, 0 avertissement, 0 erreur ;
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --filter UsageModeVisualIdentityTests`
  : `11/11` OK.

## Livraison

- aucun installateur ni executable de release genere sur cette passe, conformement
  a la regle projet de ne plus produire automatiquement ce type d'artefact
  sans demande explicite de l'utilisateur.

## Version

- `0.84.0.21-dev` - quatrieme chiffre uniquement, palier `0.84.0` inchange.
