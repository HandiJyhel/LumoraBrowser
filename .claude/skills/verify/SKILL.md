---
name: verify
description: Lancer et piloter Lumora.WinUI en conditions reelles pour verifier une fonction UI (profil isole, mode invite, pilotage UIA sans injection clavier)
---

# Verifier Lumora.WinUI en conditions reelles

## Build

`dotnet build` echoue sur le packaging PRI (SDK .NET 10). Utiliser MSBuild de
Visual Studio, comme `scripts/run-winui.ps1` :

```powershell
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\Current\Bin\amd64\MSBuild.exe" | Select-Object -First 1
& $msbuild Lumora.WinUI\Lumora.WinUI.csproj /t:Build /p:Configuration=Debug /p:Platform=x64
```

Exe produit : `Lumora.WinUI\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\Lumora.WinUI.exe`.
Les tests purs (`dotnet test Lumora.Tests`) sont l'affaire de CI, pas de verify.

## Lancement isole

- `$env:LUMORA_PROFILE_DIR` -> dossier jetable (scratchpad), `$env:LUMORA_TRACE_STARTUP = "1"`
  (trace dans `bin/.../winui-runtime-trace.log`, exceptions non gerees incluses).
- Le selecteur de profil decouvre quand meme le vrai profil machine (protege par
  PIN). NE PAS cliquer « Continuer avec ce profil » : cliquer
  **« Continuer sans profil (mode invite) »** — navigateur fonctionnel, aucun mur.

## Pilotage UIA (ce qui marche)

PowerShell + `System.Windows.Automation` (`Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes`) :

- **Marche recursive Children** (le scope `Descendants` rate les popups WinUI),
  avec **elagage des noeuds `ControlType.Document`** : c'est le contenu WebView2,
  arbre enorme qui rend la marche interminable sinon.
- Saisie d'adresse : `ValuePattern.SetValue` sur l'element `AutomationId = "AddressBox"`,
  puis `InvokePattern` sur le bouton « Ouvrir l'adresse ». **Aucun refus** avec
  ces patterns, contrairement aux injections souris/clavier synthetiques
  (refusees par l'environnement).
- Matcher les noms de boutons en **exact** quand un libelle en contient un autre
  (« Fermer » vs « Fermer l'onglet »).
- Captures : `System.Drawing` CopyFromScreen du `BoundingRectangle` de la fenetre.
- Faire tout le flux **en une seule commande** : le process WinUI/WebView2 s'est
  deja arrete de facon aleatoire dans cet environnement (0.73/0.74, non reproduit
  en 0.75).

Script complet reutilisable : voir `drive-sitenotfound.ps1` du log 0.75
(structure : Start-Process -> Wait fenetre -> invite -> SetValue adresse ->
Invoke -> polls Find-Element -> screenshots -> Stop-Process).
