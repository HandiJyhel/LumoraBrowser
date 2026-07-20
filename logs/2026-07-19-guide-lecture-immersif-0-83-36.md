# Guide de lecture immersif - 0.83.36-dev

## Contexte

Apres les profils de confort et le confort par site, l'objectif etait
d'ajouter une aide de lecture qui fasse vraiment ressortir Lumora face aux
options de base du navigateur.

Le choix retenu a ete un guide de lecture immersif : la page reste visible,
mais le contenu hors zone utile est assombri pour concentrer l'attention sur
une bande de lecture mobile.

En parallele, un probleme de workflow local est reapparu : lancer Lumora
depuis le meme repertoire que la build isolee verrouillait
`Lumora.WinUI.exe`, ce qui cassait la build suivante.

## Changements

### 1. Nouveau stockage de preference

Ajout dans `Lumora.WinUI/Models/UiSettings.cs` de :

- `AccessibilityReadingGuideEnabled`
- `AccessibilityReadingGuideBandHeight`

La hauteur de bande est normalisee et bornee sur quatre valeurs produit :

- `120`
- `160`
- `220`
- `300`

### 2. Nouvelle commande dans le panneau Accessibilite

Ajout dans `Lumora.WinUI/MainWindow.xaml` :

- un toggle `Guide de lecture immersif` ;
- un `ComboBox` pour choisir la hauteur de bande ;
- un texte d'aide expliquant l'effet attendu.

Les handlers de sauvegarde/chargement ont ete raccordes dans
`Lumora.WinUI/MainWindow.Settings.cs`.

### 3. Nouveau moteur de guide de lecture

Ajout du fichier :

- `Lumora.WinUI/MainWindow.ReadingGuide.cs`

Ce partiel genere un script local injecte dans WebView2 qui :

- cree un overlay fixe ;
- conserve une bande centrale lisible ;
- assombrit le reste de la page ;
- suit le pointeur ;
- se recale aussi sur certains deplacements clavier et sur le focus.

Un point important a ete corrige pendant l'implementation :
la reapplique du guide devait d'abord demonter l'ancienne instance JS avant
de la recreer, sinon les listeners gardaient l'ancienne configuration
(notamment la hauteur de bande).

### 4. Integration avec les profils de confort

`Lumora.WinUI/MainWindow.ComfortProfiles.cs` a ete etendu pour que :

- `Vision fatiguee` active le guide ;
- `Lecture profonde` active aussi le guide ;
- le profil detecte se resynchronise avec ce nouveau reglage.

### 5. Reapplication automatique sur les onglets

Le guide est maintenant reapplique :

- lors de `ApplyAccessibilitySettings()` ;
- a l'activation d'un onglet ;
- apres navigation terminee.

Objectif : eviter qu'un onglet deja ouvert ou une nouvelle page chargee
perde l'aide visuelle.

### 6. Correctif du script de lancement local

`scripts/run-winui.ps1` a ete ajuste pour lancer Lumora depuis une copie de
l'artefact de build, dans un dossier de run dedie.

Effet :

- la build continue a utiliser un espace isole ;
- le lancement utilisateur n'ouvre plus l'executable directement dans le
  dossier de sortie de build ;
- un Lumora laisse ouvert ne bloque plus la build suivante par simple lock
  Windows sur `Lumora.WinUI.exe`.

## Verification

- `dotnet test .\\Lumora.Tests\\Lumora.Tests.csproj --no-restore -p:BaseIntermediateOutputPath=artifacts\\tmp\\tests\\obj\\ -p:MSBuildProjectExtensionsPath=artifacts\\tmp\\tests\\obj\\`
  - resultat : succes (`exit code 0`).
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\build-winui.ps1`
  - resultat : succes ;
  - `0 avertissement` ;
  - `0 erreur` ;
  - temps ecoule `00:00:39.06`.

Note de validation :

- un premier echec de build pendant l'etape provenait d'un ancien
  `Lumora.WinUI.exe` encore ouvert depuis une verification precedente ;
- le probleme a ete isole puis neutralise par la modification de
  `run-winui.ps1` ;
- aucune execution automatique supplementaire de l'application n'a ete
  faite ensuite, conformement a la regle projet actuelle.

## Fichiers touches

- `Lumora.WinUI/Models/UiSettings.cs`
- `Lumora.WinUI/MainWindow.ComfortProfiles.cs`
- `Lumora.WinUI/MainWindow.ReadingGuide.cs`
- `Lumora.WinUI/MainWindow.Navigation.cs`
- `Lumora.WinUI/MainWindow.Settings.cs`
- `Lumora.WinUI/MainWindow.SettingsTheme.cs`
- `Lumora.WinUI/MainWindow.xaml`
- `Lumora.WinUI/MainWindow.xaml.cs`
- `Lumora.Tests/AccessibilityRegressionTests.cs`
- `Lumora.Tests/UsageModeVisualIdentityTests.cs`
- `scripts/run-winui.ps1`
- `scripts/build-clean-test-artifact.ps1`
- `scripts/build-installer.ps1`
- `AGENTS.md`
- `MEMORY.md`
