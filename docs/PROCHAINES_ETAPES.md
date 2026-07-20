# Prochaines etapes - Lumora

Document de suivi, pas un journal de version comme les autres fichiers de
`docs/`. Mis a jour a chaque session pour refleter ce qui reste a faire.
Contexte complet dans `MEMORY.md` (historique) et `AGENTS.md` (regles).

**Etat au 2026-07-19, apres la version `0.83.46-dev`.**

---

## Deja fait cette session (17-19 juillet)

- Audit complet du projet (securite, coffre, architecture, tests/build).
- Rattrapage de ~25 versions non commitees, reorganise en 8 commits propres.
- Point bloquant n°1 de l'audit corrige : pont JS<->natif authentifie
  (passkeys via `e.Source`, messages `newtab_*` restreints a la page
  d'accueil interne). Commit `d21062a`.
- Mode Incognito : fusion navigation privee + Tor, session ephemere,
  Tor optionnel, entree dans le selecteur de mode. Bug bloquant de rendu
  WebView2 trouve et corrige (voir `logs/2026-07-18-mode-incognito-fusion-tor-0-83-24.md`
  pour le detail complet du diagnostic). Verifie sur deux machines.
  Commit `a365abe`.
- SDK `Microsoft.Web.WebView2` mis a jour (1.0.2903.40 -> 1.0.4078.44).
- **Integrite de `tor.exe` verifiee avant lancement (0.83.25-dev)** :
  `TorProcessManager.StartAsync` refuse desormais d'executer tout fichier
  sous `<profil>/tor/` dont le SHA256 ne correspond pas a la liste epinglee
  `TorTrustedRelease.TrustedFileHashes` (actuellement : `tor.exe` du Tor
  Expert Bundle 15.0.18 officiel, verifie par signature GPG + SHA256 avant
  d'etre epingle - voir `logs/2026-07-19-integrite-tor-0-83-25.md`). Point 1
  de l'audit initial traite.
- **Telechargement et installation reelle du moteur Tor (0.83.26-dev)** :
  nouveau `TorEngineProvider.cs` telecharge l'archive officielle
  (`TorTrustedRelease.ArchiveUrl`), verifie son SHA256 contre
  `TorTrustedRelease.ArchiveSha256`, extrait uniquement `tor/tor.exe` (via
  `System.Formats.Tar`, deja dans .NET 8, aucune nouvelle dependance),
  reverifie le binaire extrait contre `TorTrustedRelease.TrustedFileHashes`,
  puis le copie dans le profil. Bouton "Installer le moteur Tor" ajoute a
  deux endroits de `LumoraIncognitoWindow` (bascule d'en-tete et ecran
  d'echec au demarrage), jamais declenche automatiquement. Voir
  `logs/2026-07-19-installation-tor-0-83-26.md`. Hash epingle en dur
  choisi deliberement (pas de verification de signature GPG a l'execution)
  pour rester coherent avec l'etape 1 et ne pas ajouter de dependance
  crypto - limite assumee ci-dessous.
- **Versionnement de schema pour `UiSettings` (0.83.27-dev)** : nouvelle
  propriete `SchemaVersion` + `CurrentSchemaVersion`, table
  `MigrationSteps` (vide aujourd'hui) et boucle generique
  `ApplyMigrations` appliquee sur le JSON brut avant deserialisation
  typee. Objectif : qu'un futur renommage/suppression de propriete
  s'absorbe par une entree de migration au lieu de faire echouer
  `JsonSerializer.Deserialize<UiSettings>` et reinitialiser TOUS les
  reglages via le catch-all de `Load()`. Voir
  `logs/2026-07-19-migration-uisettings-0-83-27.md`. Point 4 de l'audit
  initial traite.
- **Restructuration des god files de `MainWindow`, increment 1/2 (0.83.28-dev)** :
  `MainWindow` est une `partial class` deja repartie sur 37 fichiers -
  deplacer des methodes entre fichiers ne change rien au runtime, seulement
  l'organisation. Extractions surs realisees sur les 3 fichiers les plus
  gros :
  - `Navigation.cs` (3010 -> 1426 lignes) : `MainWindow.NewTabHome.cs`
    (1025 l., generateur de page nouvel onglet) et `MainWindow.TabGroups.cs`
    (601 l., groupes/onglets verticaux) extraits.
  - `Settings.cs` (1597 -> 992 lignes) : `MainWindow.SettingsStorage.cs`
    (192 l.) et `MainWindow.SettingsTheme.cs` (458 l., moteur de theme +
    P/Invoke backdrop) extraits.
  - `Bookmarks.cs` (1333 -> 873 lignes) : `MainWindow.BookmarksDialogs.cs`
    (123 l.), `MainWindow.BookmarksImportExport.cs` (171 l.) et
    `MainWindow.BookmarksFlyouts.cs` (216 l.) extraits.
  - `Profile.cs` (1292 l.) et `xaml.cs` (1285 l., contient l'etat partage
    par 7 a 19 autres fichiers + le constructeur a l'ordre d'initialisation
    critique) **volontairement reportes** a une prochaine session - voir
    "Notes utiles" plus bas pour reprendre le fil sans refaire
    l'exploration.
  - **Piege rencontre et corrige** : plusieurs tests verifient du texte
    litteral dans un fichier par chemin en dur plutot que par comportement
    (`UsageModeVisualIdentityTests.cs` contre `Navigation.cs`/`Settings.cs`,
    `BookmarkBarRegressionTests.cs` contre `Bookmarks.cs`). Deplacer le
    contenu attendu casse ces tests sans regression reelle - corriges en
    pointant vers le nouveau fichier. A anticiper pour `Profile.cs`/`xaml.cs`.
  - Voir `logs/2026-07-19-restructuration-mainwindow-0-83-28.md`.
- **Restructuration des god files de `MainWindow`, increment 2/2 (0.83.29-dev)** :
  point 3 de l'audit initial traite. Verification approfondie de
  `Profile.cs` avant d'y toucher : **pas d'extraction necessaire** -
  `RootKeyDown` (routeur clavier global suspecte a l'increment 1) utilise
  en realite directement `_pinBuffer`/`_pinFailCount`, des champs exclusifs
  a `Profile.cs` - ce n'est pas un corps etranger, juste un routeur
  multi-usage qui vit legitimement ici. Le reste du fichier n'a pas de
  couplage problematique, juste de la taille : laisse tel quel.
  `xaml.cs` (1285 -> 652 lignes) : `MainWindow.UsageMode.cs` (553 l., mode
  d'usage/compagnon Lumie) et `MainWindow.WindowChrome.cs` (165 l., couleurs
  de barre de titre/icone/zone de securite) extraits - les deux candidats
  surs deja identifies a l'increment 1, cette fois verifies ligne par ligne
  avant extraction (chaque assertion de test localisee au prealable, aucune
  surprise de test casse cette fois). Voir
  `logs/2026-07-19-restructuration-mainwindow-increment2-0-83-29.md`.
- **Tests pour les modules privacy/Tor/Incognito (0.83.30-dev)** : 33
  nouveaux tests sur `FingerprintProtectionScript`, `GeolocationSpoofScript`
  (deja des classes pures), `IncognitoLaunchArgs` (parseur pur). Deux
  petites extractions de logique pure pour rendre le reste testable, sans
  changement de comportement : `IncognitoProcessLauncher.BuildArguments`
  (construction des arguments, separee de `Launch` qui demarre un vrai
  process) et `IncognitoWelcomeHtml.cs` (nouveau fichier - la page
  d'accueil de la fenetre Incognito etait deja une fonction statique pure,
  coincee dans une classe WinUI non compilable dans `Lumora.Tests`).
  `LumoraIncognitoWindow` elle-meme reste sans tests unitaires (fenetre
  WinUI, deja verifiee en conditions reelles a plusieurs reprises cette
  session) - point 1 de l'audit initial traite pour ces 5 elements. Voir
  `logs/2026-07-19-tests-privacy-incognito-0-83-30.md`.
- **Pipeline CI (0.83.31-dev)** : nouveau `.github/workflows/ci.yml`,
  declenche sur pull request/push vers `main` et manuellement. Deux jobs
  paralleles sur `windows-latest` : `dotnet test` (net8.0-windows ne
  tourne pas sur Linux) et un build MSBuild complet du projet WinUI avec
  `/warnaserror` (`dotnet build` seul echoue sur ce projet - `microsoft/setup-msbuild`
  requis, meme necessite que `scripts/run-winui.ps1` en local). Aucun
  installeur ni executable de release genere par la CI (regle 20
  d'AGENTS.md). Depot pas encore pousse sur GitHub - le workflow prendra
  effet des que ce sera fait. Dernier point de priorite haute de l'audit
  initial traite. Voir `logs/2026-07-19-pipeline-ci-0-83-31.md`.
- **Accessibilite produit plus distinctive (0.83.32-dev -> 0.83.41-dev)** :
  au-dela des libelles UIA et tailles de texte, Lumora a maintenant :
  - des profils de confort prets a l'emploi ;
  - un confort memorise par site ;
  - un guide de lecture immersif avec bande mobile et intensite utile pour
    la lecture longue ;
  - une navigation clavier par zones (`F6`, `Shift+F6`, `Ctrl+Alt+1..5`)
    pour passer vite entre onglets, barre d'adresse, contenu actif,
    outils et compagnon ;
  - un rappel integre des raccourcis Lumora directement dans `Confort`,
    avec relecture par annonce d'accessibilite pour ne pas laisser ces
    aides avancees cachees ;
  - un centre de confort rapide en barre basse, avec profils activables
    partout et raccourcis `Ctrl+Alt+6..9` pour accelerer les bascules ;
  - un repere contextuel global pour faire relire la zone courante et
    recentrer le focus utile via `Ctrl+Alt+F` et `Ctrl+Alt+R` ;
  - un `mode secours` avec retour a l'etat precedent, activable partout
    via `Ctrl+Alt+S` puis `Ctrl+Alt+X` ;
  - un script de lancement WinUI ajuste pour ne plus verrouiller
    l'executable de build pendant les validations locales.
  Voir `logs/2026-07-19-accessibilite-socle-0-83-32.md`,
  `logs/2026-07-19-accessibilite-panneaux-profonds-0-83-33.md`,
  `logs/2026-07-19-profils-confort-0-83-34.md`,
  `logs/2026-07-19-confort-par-site-0-83-35.md` et
  `logs/2026-07-19-guide-lecture-immersif-0-83-36.md`,
  `logs/2026-07-19-navigation-zones-clavier-0-83-42.md`, puis
  `logs/2026-07-19-raccourcis-confort-annonces-0-83-43.md`, puis
  `logs/2026-07-19-confort-rapide-footer-0-83-44.md`.

---

## Priorite haute (bloquants de l'audit initial, encore ouverts)

_Aucun point restant - tous les points de priorite haute de l'audit
initial (integrite Tor, god files `MainWindow`, tests privacy/Incognito,
CI ; le pont JS<->natif avait deja ete traite en tout debut de session)
sont desormais traites._

---

## Priorite moyenne (important, pas bloquant)

- **Coffre - acces rapide sans reauthentification** (`MainWindow.VaultQuickAccess.cs`) :
  compromis ergonomie/securite assume, a reconfirmer consciemment plutot
  que laisser tel quel par defaut.
- **Coffre - secrets en `string` jamais effaces** (mots de passe en clair,
  contrairement aux cles derivees deja zerees).
- **Coffre - AES-GCM sans AAD** (`VaultStore.cs`, `EncryptGcm`/`WrapKey`).
- **Erreurs avalees en silence** sur des points sensibles : echec de pose
  de variable d'environnement WebRTC (`WebView2Bootstrap.cs`), echec de
  `Kill` sur `TorProcessManager.Stop()`.
- **Flag WebRTC potentiellement ecrasable** si `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS`
  contient deja un `--force-webrtc-ip-handling-policy` different.
- **Anti-fingerprinting incomplet** : `getSupportedExtensions`,
  `OffscreenCanvas`, `AudioWorkletNode` non couverts.
- **Pattern `SyncTogglePair` a moitie generalise** (3 usages sur 38
  gestionnaires `Toggled`).
- **Accessibilite encore partielle sur les surfaces profondes** :
  `AccessibilityLargeText` et la passe UIA couvrent maintenant aussi des
  panneaux dynamiques plus profonds (`Sessions`, `Passkeys`,
  `Portefeuille`, acces rapide du coffre, ainsi que les rerenders quand les
  reglages d'accessibilite changent), et un guide de lecture immersif vient
  maintenant completer le lot "lecture/confort". La navigation clavier par
  zones couvre aussi les grandes regions du shell, mais une passe clavier +
  Narrator complete reste a faire sur l'ensemble du navigateur.
- **Aucune doc d'architecture d'ensemble** decrivant comment les
  fichiers `MainWindow.*` s'articulent entre eux.
- **Version dupliquee en dur a 8 endroits** (AGENTS.md, xaml.cs, 2
  scripts, test de garde-fou). Le test empeche l'incoherence mais pas
  l'oubli simultane.
- **Duplication entre les scripts de build** (`Get-Sha256Hex` copie dans
  `build-clean-test-artifact.ps1` et `build-installer.ps1`).
- **Rendu de la barre de favoris pas encore extrait de `MainWindow.Bookmarks.cs`**
  (lignes 46-217 et 548-873 environ, CRUD + toolbar) : candidat identifie
  pour une session future, laisse en place cette session car
  `BookmarkBarRegressionTests.cs` verifie du texte litteral dedans
  (`CreateBookmarksOverflowButton`, `RevealBookmarkInBar`,
  `SelectAllBookmarksButton_Click`, etc.) - a extraire en mettant a jour ce
  test en meme temps, pas apres coup.
- **`MainWindow.ReadingLens.cs` toujours sans test** (reste du perimetre du
  point 1 de l'audit initial, non traite a la session 0.83.30-dev - les 5
  autres elements de la liste l'ont ete).
- **`LumoraIncognitoWindow` sans test unitaire** (par nature - fenetre
  WinUI non compilable dans `Lumora.Tests`) : couverte uniquement par
  verification en conditions reelles (pilotage UIA), plusieurs fois cette
  session. `IncognitoWelcomeHtml.Build` et
  `IncognitoProcessLauncher.BuildArguments` (la logique pure qu'elle
  contenait) sont testes depuis la 0.83.30-dev.
- **`TorTrustedRelease` epingle une version Tor precise (15.0.18) en dur**
  (`Version`, `ArchiveUrl`, `ArchiveSha256`, `TrustedFileHashes["tor.exe"]`).
  Choix assume a l'etape 2 (pas de verification de signature GPG a
  l'execution, pour eviter une dependance crypto) : une nouvelle version de
  Tor a supporter demande de refaire manuellement la verification
  (telecharger l'archive, verifier signature GPG + SHA256, extraire,
  calculer le hash de `tor.exe`) et de mettre a jour ces constantes -
  detail de la methode dans `logs/2026-07-19-integrite-tor-0-83-25.md`. Pas
  de mecanisme de mise a jour automatique du pin.

---

## Notes utiles pour reprendre le fil

- **Bug WebView2 resolu cette session** : creer un `CoreWebView2Environment`
  explicite (`CreateWithOptionsAsync` + `EnsureCoreWebView2Async(environment, ...)`)
  echoue silencieusement sur au moins une installation (les deux machines
  testees), meme en process neuf. Seul `EnsureCoreWebView2Async()` sans
  argument (config par variables d'environnement) est fiable. **A garder
  en tete pour toute future fenetre qui aurait besoin d'un profil WebView2
  isole** (le meme piege se reproduira si on recree ce pattern ailleurs).
  Detail complet : `logs/2026-07-18-mode-incognito-fusion-tor-0-83-24.md`.
- **Chaque fenetre Incognito tourne dans son propre process Windows**
  depuis cette version (`IncognitoProcessLauncher`), consequence directe
  du point ci-dessus. Bascule Tor = fermeture + reouverture d'une
  fenetre neuve, pas un changement a chaud.
- Le script de verification UIA utilise cette session
  (`verify-incognito.ps1`) vit dans le scratchpad de l'agent, pas dans le
  depot - a reconstruire si besoin de revalider la fenetre Incognito plus
  tard (piloter via `AutomationId`, marche recursive `Children` en
  elaguant les noeuds `Document`, cf. skill `verify`).
- **Verification GPG de la signature Tor** : dans l'environnement de
  developpement utilise cette session, `gpg --auto-key-locate wkd` echoue
  (`dirmngr` indisponible dans ce shell). Contournement qui a fonctionne :
  recuperer la cle publique par son empreinte exacte via
  `https://keys.openpgp.org/vks/v1/by-fingerprint/<empreinte>` (GET simple,
  pas de connexion dirmngr), puis `gpg --import`. Detail complet dans
  `logs/2026-07-19-integrite-tor-0-83-25.md`.
