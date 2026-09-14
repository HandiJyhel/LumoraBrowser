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
- `CoreWebView2.ProcessFailed(RenderProcessUnresponsive)` **ne se declenche pas
  sur une page qui boucle en silence, sans interaction** (teste le 2026-08-30,
  page locale figee en boucle 60-90s des le chargement puis apres 2s
  d'interactivite : aucun evenement recu dans les deux cas). Le detecteur de
  page non-reactive de Chromium se declenche typiquement quand le navigateur
  ENVOIE un evenement d'entree (clic, touche) au moteur et n'obtient pas
  d'accuse de reception dans le delai - pas par simple minuterie passive. Or
  **le clic dans le contenu web (DOM) est hors de portee de ce pilotage UIA**
  (noeuds `ControlType.Document` elagues, cf. plus haut) : impossible de
  fournir cette precondition depuis ce pilotage. Verifier ce genre de detection
  demande soit un vrai clic humain sur la page pendant qu'elle boucle, soit
  d'appeler `CoreWebView2.ExecuteScriptAsync` (hors de portee sans point
  d'entree deja expose par l'app) pour simuler la sequence.
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
  **Mise a jour (2026-09-11, session "petits ajustements")** : le popup existe
  bel et bien, simplement PAS comme descendant de la fenetre principale -
  meme famille de piege que le contenu deroulant d'un `ComboBox` documente
  plus bas. Apres `InvokePattern.Invoke()` sur le bouton hote (`ModulesButton`,
  `AddBookmarkButton`, `ModeUsageButton`...), chercher l'item par son `Name`
  via `AutomationElement.RootElement.FindAll(TreeScope.Subtree, new
  PropertyCondition(ProcessIdProperty, pid))` (tout le bureau, filtre par PID)
  PLUTOT que depuis la fenetre principale - ca fonctionne de facon fiable
  (confirme sur `MenuFlyoutItem "Parametres"` du Menu Lumora ET sur les 2
  boutons du `Button.Flyout` "Ajouter aux favoris"/"Gerer les favoris"). Le
  noeud trouve est souvent le `TextBlock` du libelle, pas invocable
  directement : remonter au premier ancetre qui supporte `InvokePattern`
  (`TreeWalker.GetParent` en boucle) avant d'appeler `Invoke()`. Un
  `ContentDialog` ouvert PAR ce flyout (ex. editeur de favori) reste lui,
  comme documente plus bas, cherchable directement par `Name` du bouton
  primaire (`"Enregistrer"`, `ControlType.Button`) depuis la fenetre
  principale, sans astuce Subtree - seul le flyout/popup lui-meme sortait de
  l'arbre de la fenetre.
- **`AutomationId` a bien fonctionne cette session (2026-09-11)** pour de
  nombreux controles nommes (`AddressBox`, `ProfileNameBox`,
  `NoPasswordSwitch`, `CreateProfileButton`, `ModulesButton`,
  `SettingsNavAppearance`, `AppearanceSubNavLayout`, `NewTabTitleBox`,
  `ApplySettingsChangesButton`, `UiDensityCombo`, `UsageModeCombo`,
  `AddBookmarkButton`, `BookmarkViewModeListRadio`...), contrairement a la
  note du 2026-08-14 ci-dessus qui le disait "silencieux". Soit corrige entre
  temps cote framework/app, soit le probleme du 2026-08-14 etait plus
  specifique (controles crees dynamiquement ?) - a re-tester par
  `AutomationId` en premier avant de retomber sur `Name`, plutot que
  d'assumer l'echec par defaut.
- **Une `TextBlock` mise a jour par code (`StatusText.Text = "..."`) peut
  renvoyer une valeur PERIMEE via `AutomationElement.Current.Name`**, meme
  interrogee 2 secondes plus tard et meme si le code a bien execute
  l'affectation (confirme par trace `WinUiRuntimeTrace` cote produit) - piege
  reel rencontre le 2026-09-11 (`ApplySettingsChangesButton_Click`), a
  presque fait conclure a un faux bug de message de confirmation manquant.
  Avant de rapporter un texte "qui ne se met pas a jour" comme bug produit,
  verifier via une trace `WinUiRuntimeTrace.Write` juste apres l'affectation
  reelle plutot que de faire confiance a une seule lecture UIA de `Name`.
- **`user32.PrintWindow` (meme avec `PW_RENDERFULLCONTENT`) echoue aussi a
  capturer cette fenetre** (2026-09-11) - meme limite que `CopyFromScreen`
  documentee plus bas (qui, elle, capture une AUTRE fenetre du bureau).
  Aucune des deux methodes de capture d'ecran testees a ce jour n'est fiable
  dans cet environnement pour cette app : s'appuyer sur les valeurs/etats UIA
  et les traces de log, pas sur une image, pour toute verification visuelle.
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
  Meme ambiguite sur les boutons custom de l'app (icone+texte dans un
  `StackPanel`) : le `Button` lui-meme a un `Name` VIDE, le libelle vit sur un
  `ControlType.Text` enfant - chercher par Name+Type=Text puis remonter au
  parent Button. A l'inverse, le bouton PRIMARY/CLOSE d'un `ContentDialog`
  (`PrimaryButtonText`/`CloseButtonText`) porte le libelle DIRECTEMENT comme
  son propre `Name` (pas de Text enfant) - chercher par Name+Type=Button
  directement pour celui-la (2026-08-31, session "gestion des favoris" : un
  bouton "Deplacer ici" introuvable via la 1ere methode a fait perdre du temps
  avant de tester la 2e).
- Le contenu deroulant d'un `ComboBox` (ses `ListItem`) n'est PAS un
  descendant de la fenetre principale dans l'arbre UIA - une marche
  `Children` depuis la fenetre (meme profonde) ne le trouve jamais, y compris
  juste apres `ExpandCollapsePattern.Expand()`. Chercher plutot depuis
  `AutomationElement.RootElement.FindAll(TreeScope.Subtree, ...)` avec une
  condition `ProcessIdProperty` (et eventuellement `ControlTypeProperty` /
  `NameProperty`) : le popup existe bien, juste ailleurs dans l'arbre complet
  du bureau, meme processus (2026-08-31).
- Un `ContentDialog`/petit popup ouvert par un bouton de la barre d'outils
  ("Ajouter aux favoris", etc.) peut lui-meme etre un `Button.Flyout` (menu
  rapide) plutot que le dialogue final attendu - verifier le contenu du
  popup obtenu (walk complet) avant de chercher plus loin les champs
  attendus (ComboBox/TextBox) : ils peuvent vivre dans un 2e niveau
  (bouton DANS ce 1er popup) plutot que directement derriere le premier clic
  (2026-08-31, "Ajouter aux favoris" ouvre d'abord un petit flyout avec
  "Ajouter cette page aux favoris"/"Gerer les favoris", le vrai dialogue
  ContentDialog "Ajouter aux favoris" n'apparait qu'apres un 2e clic).
- `CopyFromScreen` avec le `BoundingRectangle` UIA peut capturer une **autre
  fenetre reelle du bureau** (deja vu : capture qui montre la session Claude Code
  elle-meme) si la fenetre Lumora n'est pas au premier plan - les rectangles UIA
  (existence, `BoundingRectangle`, valeurs de pattern) restent fiables meme quand
  la capture d'ecran ne l'est pas ; ne pas conclure d'un echec a partir d'une
  capture seule.

- **Le `Name` accessible d'un bouton peut differer a la fois de son
  `AutomationProperties.Name` XAML litteral ET de son `ToolTipService.ToolTip`**
  quand du code-behind le reecrit dynamiquement (2026-09-13, session
  "verification au final") : `ModeUsageButton` a `AutomationProperties.Name="Mode"`
  dans le XAML et `ToolTipService.ToolTip="Mode d'usage"`, mais son `Name`
  REEL en cours d'execution est `"Mode d'usage : Neutre."` (ecrit par
  `UpdateUsageModeButtonUi`) - chercher "Mode d'usage" en exact echoue
  silencieusement, chercher par `AutomationId="ModeUsageButton"` marche a coup
  sur. Reflexe a prendre : pour un bouton dont l'etat change (mode, densite,
  bascule...), toujours privilegier `AutomationId` a `Name` des qu'un
  `AutomationId` existe, meme si le XAML semble donner un `Name` fixe.
- **Le bouton "+" de la barre d'onglets HORIZONTALE n'est PAS celui dont
  `AutomationProperties.Name="Nouvel onglet"` est pose dans `MainWindow.xaml`**
  (ce Name-la appartient a un autre bouton, invisible/inutilise dans ce mode) -
  le vrai bouton visible est celui du controle `Tab` natif (`TabView`), `Name`
  reel `"Ajouter un nouvel onglet"`, `AutomationId="AddButton"` (confirme
  2026-09-13). Chercher par `AutomationId="AddButton"` plutot que par le Name
  suppose depuis le XAML statique.
- **Un overlay (`WelcomeOverlay`, `LoginOverlay`...) peut rester actif ET
  invisible dans un dump d'arbre a profondeur limitee** (2026-09-13) : `Walk`
  avec `maxDepth=3` depuis la fenetre s'est arrete juste avant d'atteindre le
  `Pane` de l'assistant de bienvenue, laissant croire a tort que le shell
  principal etait deja pleinement actif (le `MainMenuButton` etc. restent
  trouvables par une marche `Descendants`/`Find-Native` MEME sous un overlay,
  car `InvokePattern.Invoke()` ignore le Z-order et l'etat de l'overlay -
  contrairement a un vrai clic souris). Avant de commencer un scenario,
  chercher explicitement `WelcomeSkipButton`/`CreateProfileGuestLink` par
  `AutomationId` (pas juste constater qu'un bouton de la Toolbar repond) pour
  confirmer qu'aucun overlay ne traine encore.
- **Le Coffre (`VaultMenu_Click`, `MainWindow.VaultAccess.cs`) est
  deliberement desactive en mode invite** (`if (_isGuestMode) { ...; return; }`,
  message "Coffre indisponible en mode invite") - cliquer la tuile "Coffre" du
  Menu Lumora en mode invite ne produit AUCUN effet visible, ce n'est pas un
  bug : utiliser un vrai profil (`CreateProfileButton` + `NoPasswordSwitch`
  pour aller vite) pour tout scenario touchant le Coffre.
- **Meme avec un vrai profil "sans mot de passe", l'ecran d'acces au Coffre
  (`PromptMasterPasswordAsync`) rejette une `PasswordBox` vide sans exception**
  (`if (string.IsNullOrWhiteSpace(pwBox.Password)) return null;`) - combine a
  la limite deja documentee plus haut (`PasswordBox.SetValue` toujours
  refuse), **le contenu du Coffre (ajout/liste/detail d'un identifiant) reste
  hors de portee du pilotage UIA quel que soit le type de profil** (invite OU
  reel). Ne pas s'acharner : confirmer que le dialogue de mot de passe
  s'affiche bien (preuve que la barriere fonctionne), puis s'appuyer sur la
  coherence avec d'autres ecrans deja verifies pour le rendu, comme deja
  pratique les sessions precedentes.
- **La section "Mon Lumora" (Apparence) est elle aussi deliberement bloquee
  en mode invite**, meme politique "Live Linux" que le Coffre
  (`SettingsNav_Click`, `MainWindow.xaml.cs` : `if (_isGuestMode && section
  is "appearance" or "vault") section = "overview";`) - cliquer
  `SettingsNavAppearance` en mode invite retombe silencieusement sur
  "Apercu", aucune erreur, `AppearanceSubNavTheme`/`AccentColorSwatchButton`
  etc. restent introuvables (confirme 2026-09-14, session "couleur
  d'accentuation") : utiliser un vrai profil pour tout scenario touchant
  l'Apparence, pas seulement le Coffre. **Creer ce profil declenche 2 ecrans
  supplementaires avant le shell normal** : un ecran de migration des
  favoris (`MigrationPanel`, lien `HyperlinkButton` "Passer cette etape" ->
  `MigrationSkipButton_Click`, trouvable par son `Name` exact "Passer cette
  etape", PAS d'AutomationId), puis l'assistant premier lancement a 9 etapes
  (`WizardNextButton`, Content="Suivant" puis "Terminer" a la derniere -
  cliquer 9 fois de suite suffit, le handler avance tout seul puis appelle
  `FinishWizard()` au 9e clic).
- **Une recherche `FindFirst`/`FindAll` NATIVE avec `TreeScope.Descendants`
  ou `TreeScope.Subtree` (sans elagage manuel) peut echouer silencieusement
  (retourne `null`/vide) des que l'arbre contient un `ControlType.Document`
  volumineux** (page Nouvel onglet avec son WebView2) - pas juste "plus
  lent", carrement pas fiable dans cet environnement (confirme 2026-09-14) :
  meme la marche pruned deja recommandee plus haut doit passer par
  `TreeWalker` a la main (`GetFirstChild`/`GetNextSibling`, on saute des
  qu'on croise `ControlType.Document`) plutot que par les methodes `Find*`
  natives, y compris pour une recherche Subtree "hors fenetre" (popups/
  flyouts) depuis `AutomationElement.RootElement` filtree par `ProcessId`.
- `VerticalTabsSwitch` (barre d'onglets verticale) vit dans Reglages >
  **Espace de travail** (`SettingsNavNavigation`), PAS dans Apparence >
  Disposition (`AppearanceSubNavLayout`) contrairement a ce qu'on pourrait
  supposer - `CompactModeSwitch` (Interface compacte, lui) est bien dans
  Apparence > Disposition. Les deux reglages ne sont pas cote a cote dans les
  Reglages bien qu'ils se combinent visuellement (rail vertical + ultra-compact).
- **`CreateProfileGuestLink` et plusieurs actions de `MainWindow.Profile.cs`
  appellent `RestartApp()`**, qui relance completement le PROCESSUS (nouveau
  PID, nouveau `Lumora.WinUI.exe` via `Process.Start` puis
  `Application.Current.Exit()`) plutot que de juste rafraichir l'UI en place -
  confirme normal via `winui-runtime-trace.log` (sequence complete "App
  constructor start" -> ... -> "MainWindow constructed" juste apres, 0
  `UNHANDLED`). Si un script de pilotage perd soudain son PID juste apres un
  clic sur "Continuer sans profil"/creation de profil/changement d'emplacement,
  ne pas conclure a un crash sans verifier `Get-Process -Name Lumora.WinUI`
  (souvent deja relance avec un PID different) et l'absence de `UNHANDLED`.
- Pour trouver un item precis dans le Menu Lumora (ex. tuile "Coffre") quand
  plusieurs elements portent le MEME `Name` (vue grille epinglee + vue liste
  recherche, parfois un element degenere de hauteur ~6px), filtrer par
  `ControlType.Button` ET par les dimensions attendues de la tuile (~155x114
  pour une tuile epinglee) plutot que de prendre le premier match - le premier
  trouve par une marche en profondeur n'est pas forcement le bon.
- **Exclure `ControlType.Document` est INDISPENSABLE meme pour une recherche
  Subtree depuis `RootElement` par `ProcessId`** (pas seulement pour la marche
  depuis la fenetre) : le contenu de l'onglet actif (Nouvel onglet, ses
  raccourcis/modules) peut exposer des noeuds accessibles avec des `Name`
  identiques a de vrais controles XAML (ex. "Coffre" apparait aussi comme
  libelle dans la grille HTML du Nouvel onglet) - une recherche qui ne coupe
  pas ces sous-arbres peut invoquer silencieusement un element du DOM au lieu
  du bouton natif attendu, sans la moindre erreur. Un helper de marche manuel
  qui elague `ControlType.Document` (voir `Find-Native` reutilisable) est plus
  sur qu'un simple `FindAll(TreeScope.Subtree, condition)`.

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

## Cluster de boutons invisible en UIA : cause trouvee (largeur de fenetre)

**Cause identifiee le 2026-09-14 (session "etape 3.5")**, apres plusieurs
sessions ou ce symptome etait note "non resolu" : au lancement, la fenetre
occupe par defaut toute la largeur physique de l'ecran (2880px dans cet
environnement). A cette largeur, un cluster entier de boutons - `Menu
Lumora`, `Mode d'usage : ...`, `Compagnon, ...`, `Confort : ...` - devient
invisible dans l'arbre UIA : ni `FindAll(TreeScope.Descendants)` natif ni
une marche manuelle ne les retrouve (confirme : 26 boutons au lieu de ~35),
et ce de facon **reproductible a 100%** sur plusieurs lancements, quel que
soit le chemin (mode invite via `CreateProfileGuestLink`/`RestartApp`, ou
profil pre-configure sans onboarding) - donc PAS lie a `RestartApp` comme on
pouvait le croire d'apres les sessions precedentes, plutot une consequence
de la largeur de fenetre au moment du rendu initial.

**Contournement fiable** : juste apres avoir recupere `MainWindowHandle`,
redimensionner la fenetre a une taille "normale" via `user32.SetWindowPos`
(ex. 1600x1000 ou plus grand si le contenu a verifier a besoin de hauteur) -
les boutons manquants reapparaissent immediatement (confirme : 35 boutons
apres redimensionnement, sur le MEME process/fenetre). Rechercher ensuite
`MainMenuButton` par son **Name accessible** ("Menu Lumora") plutot que par
`AutomationId` - `FindFirst` avec une `PropertyCondition` sur
`AutomationIdProperty="MainMenuButton"` a echoue meme apres le
redimensionnement (x:Name XAML ne s'expose pas forcement comme
AutomationId sur ce controle), alors qu'une recherche par `NameProperty` +
`ControlTypeProperty=Button` a fonctionne du premier coup :

```powershell
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
"@
$proc.Refresh()
[Win32]::SetWindowPos($proc.MainWindowHandle, [IntPtr]::Zero, 50, 20, 1700, 1400, 0x0040) | Out-Null
Start-Sleep -Seconds 2
# ... puis chercher par Name="Menu Lumora" + ControlType=Button, pas par AutomationId
```

A tenter en premier reflexe des qu'un bouton visible a l'ecran reste
introuvable en UIA juste apres un lancement/redemarrage, avant de conclure a
une flakiness non reproductible ou de changer de strategie de verification.
