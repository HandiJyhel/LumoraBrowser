# Prochaines etapes - Lumora

Document de suivi, pas un journal de version comme les autres fichiers de
`docs/`. Mis a jour a chaque session pour refleter ce qui reste a faire.
Contexte complet dans `MEMORY.md` (historique) et `AGENTS.md` (regles).

**Etat au 2026-07-31, apres la version `0.93.8.0-dev` et l'adoption de la
GPLv3.** Remplace la version precedente de ce document, restee figee au
19 juillet malgre ~10 jours de travail commits depuis (voir le rattrapage
de 17 commits du 31 juillet, detaille dans `MEMORY.md`).

---

## Deja fait depuis le 19 juillet (versions 0.83.54.1-dev a 0.93.8.0-dev)

Resume par theme (detail complet, session par session, dans `MEMORY.md`) :

- Securite : Tor/Incognito ephemere, faille mode invite corrigee, audit
  manuel des points d'entree de connexion.
- Navigation/popups : detournement sans reseau publicitaire repertorie,
  popups parasites corriges.
- Confidentialite : refus de cookies fiabilise (traversee Shadow DOM),
  coupure des API de ciblage pub Privacy Sandbox (Topics/FLEDGE).
- Coffre : verification des mots de passe compromis.
- Applications web : identite Windows distincte, icones, barre de titre.
- Ajout des flux RSS, du Menu Demarrer (registre de tuiles, epinglage),
  de l'identite visuelle modulaire "Studio Lumora" et du fond d'ecran.
- Molette : saga de diagnostic close (moteur de scroll natif fiabilise).
- Onglets : menu contextuel a plat, selection multiple, split view.
- **Chantier accessibilite handicap (0.93.0.0-dev -> 0.93.2.0-dev),
  officiellement clos le 27 juillet** sur les 3 angles retenus par
  l'utilisateur : lecteur d'ecran, moteur/motricite, basse vision
  etendue. Voir plus bas les reserves explicites laissees a la cloture.
- Rattrapage de 17 commits (10 jours de travail non commite) + adoption
  de la licence GPLv3 avec exception pour composants Windows
  proprietaires (WebView2, Windows App SDK) - 31 juillet.

---

## Priorite haute (bloquants avant de qualifier une release de "prete")

- ~~Installateur non autonome (WebView2 Fixed Version)~~ **FAIT le 31
  juillet** : bascule Evergreen -> Fixed Version complete et verifiee de
  bout en bout.
  - `WebView2Bootstrap.cs` pose `WEBVIEW2_BROWSER_EXECUTABLE_FOLDER` vers
    `FixedRuntime\<version>` a cote de l'exe (app non empaquetee, pas de
    `Package.Current`).
  - `Lumora.WinUI.csproj` copie ce dossier au build (`WebView2FixedVersion`
    = `150.0.4078.105`).
  - `.cab` officiel x64 trouve via les donnees serveur embarquees dans la
    page Microsoft (SPA Nuxt - aucune URL stable documentee, mais la
    valeur reelle est servie au chargement de la page), telecharge,
    verifie (SHA256 + signature Authenticode Microsoft valide sur
    `msedgewebview2.exe`), et extrait via `scripts/prepare-webview2-fixedversion.ps1`.
  - **Piege trouve et corrige** : le `.cab` contient un dossier imbrique
    (`Microsoft.WebView2.FixedVersionRuntime.<version>.x64\`) que le
    script ne remontait pas au premier essai - corrige (le script
    aplatit desormais automatiquement cette structure).
  - `scripts/installer/Program.cs.template` : case a cocher et
    telechargement Evergreen retires, remplaces par un octroi `icacls`
    obligatoire (`ALL APPLICATION PACKAGES`/`ALL RESTRICTED APPLICATION
    PACKAGES`) sur le dossier FixedRuntime deploye - necessaire depuis la
    v120 du runtime pour une app non empaquetee (sandbox App Container).
  - `scripts/build-installer.ps1` refuse de packager si FixedRuntime est
    vide (garde-fou, plus jamais declenche depuis que le runtime est en
    place).
  - Verifie de bout en bout : build MSBuild propre avec le runtime
    (648 Mo) effectivement copie a la bonne profondeur, installateur
    reel assemble (377 Mo, runtime inclus), `dotnet test` 694/695
    inchange (meme echec preexistant sans rapport).
- **Vrai zoom de l'interface Lumora elle-meme** (chrome native, pas
  seulement les pages web visitees) : explicitement identifie comme le
  point le plus lourd de l'angle basse vision et reporte a une session
  dediee le 27 juillet, jamais traite depuis. ~319 `FontSize=` en dur
  dans `MainWindow.xaml` (verifie a nouveau le 31 juillet), aucun
  mecanisme de mise a l'echelle centralise. Chantier a part entiere, pas
  un correctif ponctuel.
- **Accessibilite handicap jamais verifiee en conditions reelles** :
  tout le chantier 0.93.0-0.93.2 a ete valide par relecture de code et
  automatisation UIA, jamais par un vrai passage a un lecteur d'ecran
  (Narrateur/NVDA/JAWS) ni par de vrais raccourcis clavier physiques
  (injection clavier/souris bloquee dans l'environnement de
  developpement utilise). Documente comme limite assumee a chaque etape
  (0.93.0.0, 0.93.1.0-dev), jamais leve depuis. Pas un blocage absolu
  pour une premiere release, mais un vrai angle mort avant de presenter
  l'accessibilite handicap comme "validee" plutot que "implementee".
- **Test reel a la molette physique** toujours du par l'utilisateur -
  signale comme non verifiable dans l'environnement de dev depuis
  0.84.1.12-dev, rappele encore a 0.93.2.1-dev (filet de secours
  generique). Meme limite d'environnement, jamais un vrai geste de
  molette physique confirme.
- **Correctif plein ecran YouTube (0.93.7.0-dev) non confirme en
  conditions reelles** : le watchdog ajoute n'a jamais ete declenche par
  un vrai clic sur le bouton plein ecran natif du lecteur (arbre
  d'accessibilite Chromium/WebView2 non atteignable par l'automate UIA
  dans cet environnement). A tester par l'utilisateur.
- **Trou de documentation constate le 29 juillet** : `AGENTS.md` etait
  deja a `0.93.6.2-dev` alors que le dernier point journalise dans
  `MEMORY.md` s'arretait a `0.93.5.0-dev` - une etape anterieure n'a
  jamais ete journalisee, contenu non reconstitue (ne pas inventer si le
  sujet revient, juste constater le trou).

---

## Priorite moyenne (important, pas bloquant - liste du 19 juillet, non
revisitee cette session, a reverifier avant d'agir dessus)

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
- **Aucune doc d'architecture d'ensemble** decrivant comment les
  fichiers `MainWindow.*` s'articulent entre eux (le nombre de fichiers
  a fortement augmente depuis le 19 juillet - RSS, Menu Demarrer,
  IdentitySpine, LayoutStudio, SplitView, LoginSecurity...).
- **Version dupliquee en dur a plusieurs endroits** (AGENTS.md, xaml.cs,
  scripts, test de garde-fou). Le test empeche l'incoherence mais pas
  l'oubli simultane.
- **Duplication entre les scripts de build** (`Get-Sha256Hex` copie dans
  `build-clean-test-artifact.ps1` et `build-installer.ps1`).
- **`TorTrustedRelease` epingle une version Tor precise (15.0.18) en dur** :
  pas de mecanisme de mise a jour automatique du pin, une nouvelle
  version de Tor a supporter demande une reverification manuelle
  complete (voir `logs/2026-07-19-integrite-tor-0-83-25.md`).

---

## Certification / distribution (nouveau depuis le 31 juillet)

- **SignPath Foundation** (signature de code gratuite) : conditionne a la
  publication du depot sur GitHub, pas encore faite. A candidater une
  fois public.
- **Microsoft Store** : compte developpeur individuel desormais gratuit
  (verifie par recherche web le 31 juillet). Piste de distribution a
  evaluer une fois le depot public.
- **Badge OpenSSF/CII Best Practices** : gratuit, non encore mis en
  place, mentionne comme signal de confiance complementaire.

---

## Notes utiles pour reprendre le fil

- **Bug WebView2 resolu (session du 19 juillet)** : creer un
  `CoreWebView2Environment` explicite (`CreateWithOptionsAsync` +
  `EnsureCoreWebView2Async(environment, ...)`) echoue silencieusement
  sur au moins une installation. Seul `EnsureCoreWebView2Async()` sans
  argument est fiable. A garder en tete pour toute future fenetre avec
  profil WebView2 isole. Detail : `logs/2026-07-18-mode-incognito-fusion-tor-0-83-24.md`.
- **Limite d'environnement recurrente** : l'injection clavier/souris
  synthetique est refusee dans l'environnement de developpement utilise
  (skill `verify`), et l'arbre d'accessibilite Chromium/WebView2 ne
  s'active pas toujours pour l'automate UIA. Consequence directe : tous
  les correctifs lies a la molette physique, aux raccourcis clavier et
  au plein ecran video sont verifies par relecture de code stricte, pas
  par un vrai geste physique - toujours le signaler comme tel plutot que
  de presenter "verifie" sans nuance.
- **Rattrapage de commits** : utiliser les fichiers `logs/*.md` comme
  frontieres de regroupement quand plusieurs jours de travail
  s'accumulent sans commit (fait le 19 juillet pour ~25 versions en 8
  commits, refait le 31 juillet pour ~10 jours en 17 commits - voir
  `MEMORY.md`, entree du 31 juillet, pour le detail et les limites
  assumees sur les fichiers partages type `MainWindow.xaml`).
- **Licence** : GPLv3 + `LICENSE-EXCEPTIONS.md` pour WebView2/Windows App
  SDK, ajoutee le 31 juillet. Voir section "Licence" dans `AGENTS.md`.
