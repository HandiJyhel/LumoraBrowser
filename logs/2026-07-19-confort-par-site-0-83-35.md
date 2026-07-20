# Confort par site - 0.83.35-dev

## Contexte

Apres l'ajout des profils de confort globaux, l'etape suivante etait de
commencer a memoriser des ajustements de confort au niveau du site.

Le but de ce premier lot etait d'aller plus loin que les options de base du
navigateur en laissant Lumora reappliquer automatiquement, domaine par
domaine, un confort prefere.

## Changements

### 1. Nouveau stockage des regles par site

Ajout de `SiteComfortRules` dans `Lumora.WinUI/Models/UiSettings.cs`.

Chaque regle memorise :

- le domaine racine ;
- le zoom prefere ;
- le renfort de texte ;
- la reduction des animations.

### 2. Nouvelle politique de confort par site

Ajout du fichier :

- `Lumora.WinUI/SiteComfort/SiteComfortPolicy.cs`

Il centralise :

- la normalisation du domaine ;
- les bornes de zoom ;
- la lecture des regles ;
- l'ecriture / mise a jour des regles ;
- la reinitialisation d'un site.

### 3. Nouveau bloc UI dans le Centre du site

Ajout dans `MainWindow.xaml` d'une carte `Confort de ce site` avec :

- un resume de l'etat memorise ;
- un `ComboBox` de zoom ;
- un toggle `Texte plus lisible` ;
- un toggle `Limiter les animations` ;
- un bouton de reinitialisation.

Le panneau est rafraichi depuis `RefreshSiteControlAsync(...)`.

### 4. Reapplication automatique sur navigation

Ajout d'un nouveau partiel :

- `Lumora.WinUI/MainWindow.SiteComfort.cs`

Il gere :

- les interactions de la carte UI ;
- le resume utilisateur ;
- l'application des regles sur l'onglet actif ;
- la reinjection automatique apres navigation et changement d'adresse.

### 5. Correctif technique sur le zoom

La premiere implementation tentait d'utiliser `WebView2.ZoomFactor`, mais
la pile WinUI de ce projet n'expose pas cette API.

Le correctif applique remplace donc ce zoom hote par une injection CSS
locale via `BuildSiteComfortScript(int zoomPercent, bool largeText, bool reduceMotion)`.

Effet :

- plus d'appel a une API absente ;
- zoom par site conserve ;
- texte et animations toujours personnalises au meme endroit.

## Verification

- `dotnet test .\\Lumora.Tests\\Lumora.Tests.csproj --no-restore -p:BaseIntermediateOutputPath=artifacts\\tmp\\testobj\\`
  - resultat : succes (`exit code 0`).
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\build-winui.ps1`
  - resultat : bloque par le bac a sable reseau sur le restore NuGet (`NU1301`).
- build WinUI relancee sans restore avec un repertoire intermediaire temporaire
  - resultat : le blocage initial sur `WebView2.ZoomFactor` n'apparait plus ;
    la verification locale tombe ensuite sur des verrous / doublons deja
    presents dans les fichiers generes `obj`, sans lien direct avec le
    correctif de confort par site.

## Fichiers touches

- `Lumora.WinUI/Models/UiSettings.cs`
- `Lumora.WinUI/SiteComfort/SiteComfortPolicy.cs`
- `Lumora.WinUI/MainWindow.SiteComfort.cs`
- `Lumora.WinUI/MainWindow.SiteControl.cs`
- `Lumora.WinUI/MainWindow.Navigation.cs`
- `Lumora.WinUI/MainWindow.xaml`
- `Lumora.WinUI/MainWindow.xaml.cs`
- `Lumora.Tests/AccessibilityRegressionTests.cs`
- `Lumora.Tests/Lumora.Tests.csproj`
- `Lumora.Tests/UsageModeVisualIdentityTests.cs`
- `AGENTS.md`
- `MEMORY.md`
