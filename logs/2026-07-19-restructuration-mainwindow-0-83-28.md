# 2026-07-19 - Restructuration des god files de MainWindow, increment 1/2 (0.83.28-dev)

Point 3 de l'audit initial, attaque avant le point 4 (versionnement de
`UiSettings`) a la demande de l'utilisateur, qui a finalement invers l'ordre :
`UiSettings` d'abord pour servir de filet de securite avant de restructurer
le code qui le manipule (`Settings.cs`). `MainWindow` pese 17 229 lignes sur
37 fichiers partiels, dont 5 "god files" concentrent ~8500 lignes (~49% du
total) : `Navigation.cs` (3010), `Settings.cs` (1597), `Bookmarks.cs`
(1333), `Profile.cs` (1292), `xaml.cs` (1285).

**Exploration prealable** (3 agents en parallele) a cartographie chaque
fichier avant toute modification - voir le detail complet des blocs et de
leurs dependances dans la conversation ayant mene a cette session. Perimetre
retenu avec l'utilisateur : traiter les 3 fichiers les plus gros/risques
cette session (`Navigation.cs`, `Settings.cs`, `Bookmarks.cs`), reporter
`Profile.cs`/`xaml.cs` a une session suivante.

**Nature du risque** : `MainWindow` est deja une `partial class` - deplacer
une methode d'un fichier vers un autre ne change RIEN au comportement
runtime (tous les champs prives restent accessibles a l'identique quel que
soit le fichier qui les declare). Le risque est mecanique, pas fonctionnel :
erreur de transcription, using manquant, ou test verifiant du texte litteral
par chemin de fichier plutot que par comportement.

## Extractions realisees

- **`Navigation.cs`** (3010 -> 1426 lignes) :
  - `MainWindow.NewTabHome.cs` (1025 l.) : generateur de page nouvel onglet
    (`HomePageHtml()` + ~40 helpers HTML/CSS) + le record
    `NewTabModeContext` (declare a la toute fin du fichier original, loin de
    son usage). Ne depend que de `_uiSettings`.
  - `MainWindow.TabGroups.cs` (601 l.) : rendu des onglets verticaux,
    groupes, drag/drop, menus contextuels. Possede en propre
    `_tabGroups`/`_collapsedGroupIds`/`_savedGroupIds`.
  - Reste dans `Navigation.cs` : cycle de vie des onglets, pipeline de
    navigation, favicon, popups - `BrowserView_CoreWebView2Initialized` est
    un carrefour qui cable des gestionnaires definis dans 5 autres fichiers
    partiels, volontairement pas touche cette session.

- **`Settings.cs`** (1597 -> 992 lignes) :
  - `MainWindow.SettingsStorage.cs` (192 l.) : deplacement de dossier,
    export/import `.lumorabackup`, effacement des donnees de navigation.
  - `MainWindow.SettingsTheme.cs` (458 l.) : moteur de theme clair/sombre,
    palette d'accent, couleurs du mode d'usage + P/Invoke de l'arriere-plan
    de fenetre (regroupes, meme thematique visuelle) + le record
    `ModeChromePalette` (orphelin dans `Settings.cs` une fois
    `ResolveModeChromePalette`, son seul consommateur, deplace).
  - Reste dans `Settings.cs` : layout/feature toggles,
    `ApplyUiSettings`/`SaveUiSettings`, et le cluster plein-ecran/onglets-
    verticaux (`_isFullScreenMode`, `_contentFullScreenCore`, etc.) -
    identifie comme le plus imbrique du fichier, garde groupe.

- **`Bookmarks.cs`** (1333 -> 873 lignes) :
  - `MainWindow.BookmarksDialogs.cs` (123 l.) : dialogues de creation/
    renommage + le record `BookmarkFolderChoice` (declare juste apres
    l'ouverture de la classe, loin de son usage).
  - `MainWindow.BookmarksImportExport.cs` (171 l.) : plomberie d'import/
    export HTML et sources de navigateurs tiers.
  - `MainWindow.BookmarksFlyouts.cs` (216 l.) : menus contextuels
    dynamiques + rendu de l'arbre/contenu (delegue a `BookmarkTreePresenter`).
  - Reste dans `Bookmarks.cs` : CRUD, rendu de la barre de favoris (voir
    piege ci-dessous), favicon - volontairement pas touche.

## Piege rencontre et corrige : tests couples a un chemin de fichier

Plusieurs tests verifient du texte litteral present dans un fichier via son
chemin en dur plutot que via un comportement observable - deplacer ce texte
casse le test sans regression reelle :

- `Lumora.Tests/UsageModeVisualIdentityTests.cs` : 6 lectures de
  `MainWindow.Navigation.cs` (tout le contenu "mode d'usage" avait migre
  vers `NewTabHome.cs`) + 3 tests lisant `MainWindow.Settings.cs` pour des
  chaines desormais dans `SettingsTheme.cs` (`ApplyUsageModeChrome`,
  `ResolveModeChromePalette`, `ChromeTint`, `"neutral" => 1`, etc.). Un test
  (`Modes_lumora_ont_un_compagnon_permanent_et_un_accueil_aere`) melangeait
  une chaine restee dans `Settings.cs` (`UpdateModeCompanionUi();`) et une
  chaine deplacee (`ChromeTint`) - necessite deux variables distinctes,
  pas un simple changement de chemin.
- `Lumora.Tests/BookmarkBarRegressionTests.cs` : un test verifiait
  `"Nom invisible (icone seule dans la barre)"` et
  `"BookmarkStore.InvisibleTitle"`, tous deux deplaces vers
  `BookmarksDialogs.cs` - pas repere lors de la verification initiale (qui
  avait seulement confirme que les extractions choisies evitaient les
  chaines deja identifiees), decouvert par l'echec du test apres coup et
  corrige.

**Lecon retenue** : avant toute extraction future (Profile.cs/xaml.cs),
grep systematiquement le nom du fichier source dans tout `Lumora.Tests/`
et lister TOUTES les chaines asserties contre ce fichier, pas seulement
celles reperees lors d'une lecture manuelle du test.

## Autre point de vigilance : caracteres d'echappement Unicode

Lors de la premiere extraction (`TabGroups.cs`), les sequences d'echappement
``/`` (glyphes d'icone) ont ete accidentellement transformees en
caracteres Unicode bruts lors de la reecriture manuelle du bloc (un artefact
du passage par le format d'echange des outils, qui interprete `\uXXXX` comme
un echappement JSON). Detecte et corrige par comparaison octet-a-octet avec
le fichier source (`cmp`/`diff`), puis reconstruit correctement via
`[char]92` en PowerShell pour garantir un backslash litteral. Pour les
extractions suivantes (`NewTabHome.cs`, fichiers Settings/Bookmarks), le
contenu a ete deplace par concatenation shell directe (`cat`/`sed`) plutot
que retype manuellement, eliminant ce risque - verifie par diff
octet-a-octet a chaque extraction.

## Report a une session suivante : `Profile.cs` et `xaml.cs`

- **`Profile.cs`** (1292 l.) est presque autonome : 14 champs
  (`_pendingUserProfile`, `_profileEntries`, `_pinBuffer`, `_pinFailCount`,
  `_pendingProfileDir/Id`, `_profileCreationTarget`,
  `_pendingProfilePassword/Pin`, `_pendingRecoveryKey`, `_migrationEntries`,
  `_restartRequired`, `_sessionTimer`, `_systemLockHooked`) ne sont utilises
  nulle part ailleurs. **Exception a traiter avec prudence** : `RootKeyDown`
  (lignes ~981-1026) est le routeur global de touches clavier de toute la
  fenetre (reset du minuteur de session, palette de commandes, sortie plein
  ecran, PIN) - n'a rien de "profil", cable depuis le constructeur de
  `xaml.cs` (`Content.KeyDown += RootKeyDown;`), a isoler separement.
- **`xaml.cs`** (1285 l.) est plus dur : ses ~155 premieres lignes
  (champs + constructeur) sont le manifeste d'etat partage de toute la
  classe - `_uiSettings` utilise par 19 autres fichiers, `_isGuestMode` par
  17, `_profile` par 14, `_browserView` par 10, `_vault`/`_tabs` par 8
  chacun, `_historyPanel` par 7. Le constructeur a un ordre d'initialisation
  critique (`UiSettings.Load` AVANT `WebView2Bootstrap.ConfigureOnce` - le
  drapeau WebRTC est fige au demarrage du moteur) a ne jamais perturber.
  Candidats surs deja identifies : bloc Mode d'usage/Compagnon (lignes
  540-1054, ~515 l., ne depend que de `_uiSettings`) et bloc chrome de
  fenetre (1120-1242). Le champ/constructeur/`InitializeBrowserSurface`/
  `ShowPanel` (utilise par 18 fichiers) doivent rester le "noyau" intact.

## Documentation et version

- `docs/PROCHAINES_ETAPES.md` : point 3 marque "reduit, pas clos" avec le
  detail de reprise pour `Profile.cs`/`xaml.cs` (evite de refaire
  l'exploration). Nouvelle note sur le rendu de la barre de favoris,
  candidat differe.
- Version passee a `0.83.28-dev`.

**Verification** :

- Build MSBuild x64 (Debug) execute et 0 avertissement/erreur confirme
  apres CHAQUE extraction (Navigation, puis Settings, puis Bookmarks) -
  pas seulement a la fin.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 534/534
  apres chaque etape (deux corrections de tests couples au chemin de
  fichier en cours de route, detaillees plus haut).
- Fidelite de chaque extraction verifiee par comparaison octet-a-octet
  (`diff`/`cmp`) entre le contenu deplace et le contenu original, pas
  seulement par la compilation.
- Verification en conditions reelles, profil de test isole vierge (jamais
  le profil de l'utilisateur), mode invite (pilotage UIA par PID) :
  - Nouvel onglet ouvert : page d'accueil et bloc Mode d'usage/Compagnon
    affiches correctement (valide `NewTabHome.cs`).
  - Reglages > Personnaliser : changement du theme "Systeme" -> "Clair"
    confirme applique (lecture directe de la selection du ComboBox), sans
    exception (valide `SettingsTheme.cs` : `ThemeModeCombo_SelectionChanged`,
    `ApplyAccessibilitySettings`, `ApplyUsageModeChrome`,
    `ApplyWindowTitleBarColors()`).
  - Favoris : dialogue "Nouveau dossier" ouvert (`PromptTextAsync`), nom
    saisi et valide - dossier reellement cree et affiche correctement dans
    l'arbre (`BookmarkNode` avec le bon `ParentId`/`Kind`/`Title`), boutons
    "Importer"/"Exporter" presents (valide `BookmarksDialogs.cs` et
    `BookmarksFlyouts.cs` (rendu de l'arbre), `BookmarksImportExport.cs`
    visible et cablee).
  - `TabGroups.cs` couvert par build + tests (extraction a plus faible
    risque, champs `_tabGroups`/`_collapsedGroupIds`/`_savedGroupIds`
    exclusifs) ; interaction UI directe (bascule onglets verticaux) non
    testee manuellement cette session, poursuite jugee superflue vu le
    niveau de preuve deja obtenu sur les extractions les plus complexes.
  - Aucune exception, aucun crash sur l'ensemble du parcours. Nettoyage :
    process arrete, profil de test supprime.

**Version :** `0.83.28-dev`.
