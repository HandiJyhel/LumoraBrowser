# 2026-07-23 - Cohérence graphique + vrai mode sombre (0.84.0.11-dev)

## Demande

Mettre en place une vraie cohérence graphique dans `Lumora.WinUI` et corriger
le "faux mode sombre" actuel, avec passage de version en `0.84.0.11-dev`.

## Constat avant correction

- Le thème principal existait déjà dans `MainWindow.SettingsTheme.cs`, mais une
  partie de la chrome utilisait encore des couleurs codées en dur.
- Plusieurs ressources visuelles clés n'étaient pas resynchronisées quand le
  thème changeait (`NovaAppBackgroundBrush`, `NovaChromeSurfaceRaisedBrush`,
  etc.), ce qui créait des écarts entre la fenêtre principale, certains
  panneaux et les états accentués.
- Les fenêtres secondaires `LumoraIncognitoWindow` et `LumoraAppWindow`
  utilisaient encore leur propre mini-thème local avec des hexas codés en dur,
  sans raccord au réglage `dark/light/system`.

## Correction appliquée

- Ajout de `Lumora.WinUI/LumoraTheme.cs` pour centraliser :
  - la résolution du thème `dark/light/system` ;
  - la mise à jour des ressources d'application partagées
    (`AccentFillColor*`, focus, surfaces, foregrounds) ;
  - l'application d'une palette cohérente aux fenêtres secondaires
    (`Incognito`, `WebApp`) et à leur barre de titre.
- Renforcement du moteur de thème principal dans
  `MainWindow.SettingsTheme.cs` :
  - synchronisation de `NovaAppBackgroundBrush` ;
  - synchronisation de `NovaChromeSurfaceRaisedBrush` ;
  - ajout de `NovaTextOnAccentBrush` et `NovaOverlayBrush` ;
  - propagation finale des ressources partagées vers `Application.Resources`
    via `SyncSharedAppThemeResources()`.
- Nettoyage de la fenêtre principale `MainWindow.xaml` :
  - suppression des derniers `White`/`#66000000` codés en dur sur les badges et
    overlays critiques ;
  - ajout d'un style cohérent pour `FlyoutPresenter` et
    `MenuFlyoutPresenter` ;
  - harmonisation des styles de texte de panneaux.
- Raccord des fenêtres secondaires :
  - `LumoraIncognitoWindow.xaml(.cs)` reçoit maintenant une palette pilotée par
    `LumoraTheme`, des boutons d'outils cohérents et une barre supérieure
    alignée au vrai thème ;
  - `LumoraAppWindow.xaml(.cs)` suit maintenant la même logique, y compris pour
    la barre "hors domaine" et le bouton de retour.
- Montée de version en `0.84.0.11-dev` dans `MainWindow.xaml.cs`,
  `AGENTS.md`, `scripts/build-installer.ps1`,
  `scripts/build-clean-test-artifact.ps1` et les tests de garde-fou.

## Vérification

### Vérifications effectuées

- Contrôle texte des références de version : OK pour `0.84.0.11-dev` dans les
  fichiers modifiés.
- Ajout/ajustement des tests texte dans
  `Lumora.Tests/UsageModeVisualIdentityTests.cs` pour verrouiller :
  - les nouvelles ressources `NovaOverlayBrush` / `NovaTextOnAccentBrush` ;
  - la synchronisation `SyncSharedAppThemeResources()` ;
  - le raccord des fenêtres secondaires à `LumoraTheme`.

### Vérifications bloquées par l'environnement

- Build WinUI hors sandbox : restauration NuGet OK, mais échec outillage sur la
  machine à cause d'une tâche PRI introuvable
  (`Microsoft.Build.Packaging.Pri.Tasks.ExpandPriContent`).
- `dotnet test` hors sandbox : restauration OK, mais la compilation de
  `Lumora.Tests` tombe sur des doublons d'attributs d'assembly déjà présents
  dans les sorties générées (`AssemblyCompanyAttribute`,
  `TargetFrameworkAttribute`, etc.).

## Version

`0.84.0.11-dev`
