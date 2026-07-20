# Profils de confort prets a l'emploi - 0.83.34-dev

## Contexte

La section `Confort` de Lumora contenait deja des aides utiles
(`Contraste renforce`, `Texte plus lisible`, `Limiter les transitions`,
`Focus clavier`, `Dictee`, `Lecture a voix haute`, `Loupe de lecture`), mais
restait tres proche d'une simple page de toggles.

L'objectif de ce lot etait de commencer a transformer `Confort` en vraie
posture produit : moins de bricolage case par case, plus de bases
coherentes et immediates.

## Changements

### 1. Nouveau modele de profil

Ajout de `AccessibilityComfortProfile` dans `Lumora.WinUI/Models/UiSettings.cs`
avec les valeurs :

- `balanced`
- `calm`
- `vision`
- `reading`
- `custom`

Ce champ ne remplace pas les toggles existants : il memorise seulement la
posture courante pour mieux representer le choix utilisateur.

### 2. Logique de presets

Ajout d'un nouveau partiel :

- `Lumora.WinUI/MainWindow.ComfortProfiles.cs`

Il centralise :

- la definition des profils ;
- leur application sur les toggles existants ;
- la detection automatique de l'etat `Personnalise` ;
- la resynchronisation du resume et de la radio de selection.

Profils integres dans ce premier lot :

- `Equilibre`
- `Mode calme`
- `Vision fatiguee`
- `Lecture profonde`

### 3. Evolution de l'UI Confort

La section `Confort` de `MainWindow.xaml` contient maintenant une carte
`Profils prets a l'emploi` avec :

- une explication du principe ;
- une selection rapide ;
- un resume dynamique du profil actif.

Le profil `Personnalise` n'est pas traite comme un preset autonome :
Lumora l'affiche automatiquement des que l'utilisateur s'eloigne des
combinaisons predefinies.

### 4. Synchronisation avec les toggles existants

Les handlers suivants remettent maintenant a jour le profil courant :

- `AccessibilitySwitch_Toggled`
- `ReadAloudEnabledSwitch_Toggled`
- `ReadingLensEnabledSwitch_Toggled`

Effet : si l'utilisateur modifie manuellement les aides apres avoir choisi un
profil, l'interface n'affiche plus un preset trompeur.

### 5. Tests

- correction du test de regression sur le `Centre du site` pour suivre le
  correctif `descriptor.Label` ;
- ajout d'un test source qui verifie :
  - la nouvelle propriete `AccessibilityComfortProfile` ;
  - la presence de la radio de profils dans `MainWindow.xaml` ;
  - la presence de la logique de presets dans
    `MainWindow.ComfortProfiles.cs`.

## Verification

- `dotnet test Lumora.Tests\\Lumora.Tests.csproj --no-restore`
  - resultat : `571/571` tests reussis.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\build-winui.ps1`
  - resultat : build WinUI complet reussi, `0 avertissement`, `0 erreur`.

## Fichiers touches

- `Lumora.WinUI/Models/UiSettings.cs`
- `Lumora.WinUI/MainWindow.ComfortProfiles.cs`
- `Lumora.WinUI/MainWindow.Settings.cs`
- `Lumora.WinUI/MainWindow.xaml`
- `Lumora.WinUI/MainWindow.xaml.cs`
- `Lumora.Tests/AccessibilityRegressionTests.cs`
- `Lumora.Tests/UsageModeVisualIdentityTests.cs`
- `AGENTS.md`
- `MEMORY.md`
