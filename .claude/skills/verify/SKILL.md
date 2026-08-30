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
  Positionner la variable et lancer l'exe dans le MEME appel d'outil : l'etat du
  shell d'un agent ne persiste pas entre deux appels separes, un lancement dans un
  appel different herite d'un process non isole sans aucun avertissement (`-ProfileDir`
  de `scripts/run-winui.ps1` fait les deux d'un coup, ajoute le 2026-08-19).
- Depuis le correctif du 2026-08-14 (`LumoraProfileRegistry.Discover`), le
  selecteur de profil sous `LUMORA_PROFILE_DIR` ne decouvre plus JAMAIS le vrai
  profil machine - seul le profil isole du dossier jetable existe. Mode invite
  (**« Continuer sans profil (mode invite) »**) reste la voie la plus simple pour
  un test sans creer de profil du tout.

## Modifier un fichier `.lumora` a la main : il DOIT etre chiffre DPAPI

Tous les fichiers `.lumora` du profil (`ui-settings.lumora`, `profile.lumora`,
`vault.lumora`...) sont proteges par `ProtectedData.Protect` (voir
`Storage/LumoraFile.cs`, entropie `"Lumora.WinUI.v1"`,
`DataProtectionScope.CurrentUser`) - **jamais du JSON en clair**. Ecrire du
JSON en clair a la place ne leve AUCUNE erreur visible : `TryReadAllText`
avale l'exception de dechiffrement et retourne `null`, et `UiSettings.Load()`
(ou l'equivalent) retombe silencieusement sur `Default()`. Resultat : le
reglage qu'on croit avoir pose n'a jamais existe pour l'app, sans le moindre
message d'erreur - piege reel rencontre le 2026-08-18 (`VaultTotpFeatureEnabled`
qui semblait "ne jamais s'appliquer"), qui a aussi retroactivement explique un
"mystere" d'une session precedente (`SetupWizardCompleted: true` ecrit a la
main mais l'assistant premier lancement continuait d'apparaitre).

Pour ecrire un `.lumora` valide a la main (PowerShell) :

```powershell
Add-Type -AssemblyName System.Security
$plain = [System.Text.Encoding]::UTF8.GetBytes($json)
$entropy = [System.Text.Encoding]::UTF8.GetBytes("Lumora.WinUI.v1")
$cipher = [System.Security.Cryptography.ProtectedData]::Protect($plain, $entropy, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
[System.IO.File]::WriteAllBytes($path, $cipher)
```

Prudence : le "profil de test craftable directement (DPAPI+PBKDF2)" deja
documente pour `UserProfile`/`VaultStore` (voir MEMORY.md) utilise deja ce
mecanisme via les classes du produit (`UserProfile.Save`, `new VaultStore(...)`)
- toujours preferer cette voie (code produit reel) a l'ecriture manuelle
ci-dessus quand c'est possible, l'ecriture manuelle n'etant qu'un filet pour
les cas (comme `UiSettings`) sans fabrique de test dediee.

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
  en 0.75 ; **reconfirme le 2026-08-30, 0.94.3.0-dev** : plusieurs lancements se
  sont arretes silencieusement - aucun crash Windows, aucun rapport WER, aucune
  ligne `UNHANDLED` dans `winui-runtime-trace.log` - au bout de 30 a 90s environ,
  y compris SANS la moindre interaction. Ne pas prendre ca pour un bug applicatif
  sans avoir d'abord verifie qu'un flux tenu dans un seul appel survit bien de
  bout en bout ; entre deux appels separes, ne jamais supposer qu'un process
  lance precedemment est toujours celui qu'on retrouve - tuer les instances
  orphelines (`Get-Process Lumora.WinUI | Stop-Process -Force`) avant un nouveau
  lancement, et ne jamais faire confiance a `Get-Process -Name ... | Select
  -First 1` quand plusieurs lancements de test se sont accumules : ca peut
  retrouver une fenetre perimee d'un lancement precedent plutot que l'actuel).
- Un `RadioButton` (ex. items de nav `SettingsNav*`) n'a pas d'`InvokePattern` -
  utiliser `SelectionItemPattern.Select()` a la place (`GetCurrentPattern`
  renvoie "Modele non pris en charge" sur `InvokePattern` sinon).
- `PasswordBox.GetCurrentPattern(ValuePattern.Pattern).SetValue(...)` **echoue
  toujours** dans WinUI3 (`Erreur non reconnue`, cote runtime XAML - le pattern
  est annonce comme supporte par `GetSupportedPatterns()` mais `SetValue` est
  refuse), protection anti-lecture/ecriture de mot de passe par automation.
  Confirme le 2026-08-30 sur `CreatePasswordBox`. Aucun contournement trouve
  dans cet environnement (l'injection clavier synthetique est deja refusee par
  ailleurs) - signaler explicitement a l'utilisateur qu'un champ mot de passe
  ne peut pas etre verifie en saisie live, et s'appuyer sur la logique en amont/
  aval (deja testee unitairement si possible) plutot que d'insister.
- **Aucun `AutomationId` explicite** sur la quasi-totalite des controles (boutons,
  TextBox, ToggleSwitch...) : matcher par `Name` (le `Content`/`Header` visible,
  ou l'`AutomationProperties.Name` explicite quand pose) plutot que par
  `AutomationIdProperty`, qui echoue silencieusement (2026-08-14).
- **Les `MenuFlyoutItem`/`Button.Flyout` (menus contextuels type "..." en haut a
  droite) ne s'ouvrent pas de facon fiable via `InvokePattern.Invoke()` dans cet
  environnement** (2026-08-14, session "Compte et ouverture") : le popup ne se
  materialise pas (aucune nouvelle fenetre, aucun `ControlType.MenuItem` autre que
  le menu Systeme de la fenetre), meme apres `SetForegroundWindow`/`ShowWindow`.
  A l'inverse, les overlays en plein `Grid` avec `Visibility` bascule (LoginOverlay,
  SetupWizardOverlay, CommandPaletteOverlay...) fonctionnent normalement - le
  probleme est specifique aux vrais `Popup`/`Flyout` XAML, pas aux ecrans qui
  changent juste de visibilite. **Contournement** : si la fonction testee vit
  derriere un flyout sans alternative directe, verifier sa logique via un test
  `dotnet test` cible (extraire la logique pure si besoin, comme
  `RetryDelete.cs`) plutot que de s'acharner sur l'ouverture du menu.
- **`Windows.Storage.Pickers.FileOpenPicker` (boite de dialogue systeme, pas
  XAML) ne materialise aucune fenetre/process observable dans cet
  environnement** (2026-08-18, session "Coffre V4", scan QR) : le bouton qui
  l'ouvre s'invoque sans erreur, l'app reste reactive (`Responding=True`,
  aucun `UNHANDLED` dans le journal), mais ni `root.FindAll(Children)` ni
  `Get-Process | Where MainWindowTitle` ne voient de nouvelle fenetre "Ouvrir"
  apparaitre - meme famille de limite que les `MenuFlyoutItem` ci-dessus
  (une boite modale native, pas un ecran a bascule de `Visibility`), mais sur
  un dialogue Windows standard cette fois, pas un Popup/Flyout XAML.
  **Contournement** : verifier la logique de lecture/decodage en aval du
  picker par un test cible (ex. round-trip encodage/decodage ZXing hors app,
  voir MEMORY.md "Coffre V4" 2026-08-18) plutot que d'essayer de piloter la
  selection du fichier ; s'appuyer sur le fait qu'un pattern de picker deja
  utilise ailleurs dans l'app (ex. `MainWindow.Avatar.cs`) fonctionne en
  usage reel pour estimer le risque residuel, et signaler explicitement à
  l'utilisateur que ce point precis reste a confirmer manuellement.
- Dans un `MenuFlyoutItem`, le noeud dont le `Name` correspond au libelle est
  souvent le `TextBlock` interne (pas invocable) : `TryGetCurrentPattern` sur ce
  noeud echoue ("Modele non pris en charge") - remonter au parent avec
  `TreeWalker.GetParent` jusqu'a trouver un noeud qui supporte `InvokePattern`.
- `CopyFromScreen` avec le `BoundingRectangle` UIA peut capturer une **autre
  fenetre reelle du bureau** (deja vu : capture qui montre la session Claude Code
  elle-meme) si la fenetre Lumora n'est pas au premier plan - les rectangles UIA
  (existence, `BoundingRectangle`, valeurs de pattern) restent fiables meme quand
  la capture d'ecran ne l'est pas ; ne pas conclure d'un echec a partir d'une
  capture seule.

Script complet reutilisable : voir `drive-sitenotfound.ps1` du log 0.75
(structure : Start-Process -> Wait fenetre -> invite -> SetValue adresse ->
Invoke -> polls Find-Element -> screenshots -> Stop-Process).

## Un bouton qui "ne fait rien" : instrumenter avant de deviner

Si une action semble ne rien produire (pas de crash, pas de message), ne pas
enchainer un 2e correctif sur la seule base d'une relecture du code. Ajouter
des `WinUiRuntimeTrace.Write("...")` (classe `WinUiRuntimeTrace`, deja
disponible, gardee par `LUMORA_TRACE_STARTUP=1`) a chaque etape de la
methode suspecte, puis relancer via `run-winui-trace.cmd` et lire
`winui-runtime-trace.log` (chemin : dossier de lancement, ex.
`artifacts\tmp\winui-run\...\current\winui-runtime-trace.log`) - accessible
en lecture directe, inutile de demander a l'utilisateur de le copier.

Le journal capture aussi les **exceptions non gerees avec pile d'appel
complete** (ligne `UNHANDLED: ...`), la source la plus fiable pour ce genre
de bug. Deux causes reelles rencontrees ce genre de symptome muet :
- `COMException 0x8001010E` (`RPC_E_WRONG_THREAD`) : un objet XAML
  (`PasswordBox.Password`, `TextBox.Text`...) lu DANS un lambda `Task.Run`
  (thread de pool) - toujours lire la propriete sur le thread UI d'abord,
  dans une variable locale, avant `Task.Run`.
- `DefaultButton = ContentDialogButton.Close` sur un dialogue avec un champ
  de saisie : Entree apres avoir tape ferme le dialogue comme une annulation
  SILENCIEUSE (`ContentDialogResult.None`), sans qu'aucune exception ne soit
  levee - seul le journal instrumente le revele (le crash COMException,
  ci-dessus, log deja son "UNHANDLED" sans instrumentation ; celui-ci non,
  il faut l'ajouter expres).
