# Pulse Browser - Memoire du projet

Ce fichier garde l'historique chronologique des etapes effectuees sur Pulse Browser. Il ne remplace pas `AGENTS.md` et ne doit pas recopier les regles permanentes du projet.

## 2026-07-03

- Creation du projet a partir de `ProjetPulseBrowser.md`.
- Validation du cadre initial: navigateur web simple, moderne, securise, local-first, avec stockage des donnees utilisateur sur l'ordinateur.
- Decision technique: remplacement du moteur Gecko initialement envisage par Chromium via CEF (Chromium Embedded Framework), car CEF est plus adapte a un navigateur desktop personnalisable et solide.
- Version initiale du projet: `0.0.0-dev`.
- Creation de `AGENTS.md` pour centraliser le contexte, les regles permanentes, la stack cible, le versionnement et les contraintes de securite.
- Creation du dossier `logs/` avec un fichier `README.md` pour encadrer l'usage des logs et rappeler qu'aucune donnee sensible ne doit y etre stockee.
- Installation de Rust via Rustup apres validation par `Go`.
- Verification de la toolchain Rust: `rustc 1.96.1` et `cargo 1.96.1`, avec la toolchain stable `x86_64-pc-windows-msvc`.
- Verification de la presence des outils MSVC via Visual Studio Community 18, utiles pour les futures compilations Windows et l'integration CEF.
- Creation du premier squelette Rust lancable: `Cargo.toml`, `src/main.rs`, `scripts/run-dev.ps1` et `run-dev.cmd`.
- Conservation de la version projet `0.0.0-dev` dans `Cargo.toml`.
- Le premier binaire affiche le nom du projet, la version, le moteur cible Chromium via CEF et un statut de lancement reussi.
- Verification du lancement avec `run-dev.cmd`: compilation dev reussie et execution de `target\debug\pulse-browser.exe`.
- Verification du formatage Rust avec `cargo fmt --check`.
- Creation de la premiere coque Windows visible en Rust: fenetre native minimale avec titre, barre d'adresse factice, bouton `Ouvrir`, moteur cible Chromium via CEF et statut de lancement.
- Verification de cette coque avec `cargo fmt --check`, `cargo build` et lancement visible de `target\debug\pulse-browser.exe`.
- Passage de la version projet a `0.1.0-dev` pour marquer l'ajout d'une premiere fonctionnalite globale de navigation.
- Ajout d'un acces Internet temporaire: la barre d'adresse accepte une adresse simple, ajoute `https://` si le schema est absent, puis le bouton `Ouvrir` demande a Windows d'ouvrir l'adresse dans le navigateur par defaut.
- Ajout de tests unitaires pour la normalisation d'adresse.
- Verification avec `cargo fmt --check`, `cargo test` et `cargo build`.
- Retour utilisateur: `google.com` fonctionnait mais ouvrait Google Chrome, ce qui n'est pas acceptable pour Pulse Browser.
- Passage de la version projet a `0.1.1-dev` pour corriger ce comportement.
- Suppression de l'ouverture externe via Windows: le bouton `Ouvrir` ne lance plus le navigateur par defaut et affiche une demande de navigation interne dans la zone de rendu temporaire.
- Ajout de `docs/CEF_INTEGRATION.md` pour consigner la route technique Chromium via CEF reperee avec le crate Rust `cef = "149.3.0+149.0.6"`.
- Verification avec `cargo fmt --check`, `cargo test`, `cargo build` et lancement visible de `target\debug\pulse-browser.exe`.
- Ajout d'une regle projet dans `AGENTS.md`: Codex peut creer autant de fichiers/dossiers que necessaire et utiliser ou ajouter les technologies utiles sans redemander une autorisation projet a chaque fois, tout en restant transparent et coherent avec Pulse Browser.
- Installation de Ninja via WinGet pour permettre la compilation du runtime CEF.
- Passage de la version projet a `0.2.0-dev` pour marquer la premiere integration Chromium embarquee.
- Ajout de la dependance Rust `cef = "149.3.0"` et creation de `src/cef_runtime.rs` pour isoler l'initialisation CEF, la gestion des sous-processus, le client CEF minimal et la creation de la vue navigateur.
- Remplacement de la fausse zone de rendu par une premiere vue Chromium enfant de la fenetre Pulse Browser.
- Passage a une pompe de messages integree: la boucle Win32 appelle aussi `CefDoMessageLoopWork`, afin que la creation du navigateur se fasse correctement dans le prototype natif.
- Mise a jour de `scripts/run-dev.ps1` pour preparer Rust et Ninja dans le `PATH` avant `cargo run`.
- Verification avec `cargo fmt --check`, `cargo test`, `cargo build`, lancement visible de `target\debug\pulse-browser.exe` et test UI automatise chargeant `https://example.com`.
- Le test UI confirme que Pulse Browser cree des fenetres enfants `CefBrowserWindow`, `Chrome_WidgetWin_1` et `Chrome_RenderWidgetHostHWND`, sans ouvrir Google Chrome ni le navigateur par defaut.
- Passage de la version projet a `0.2.1-dev` pour rendre la premiere vue Chromium plus utilisable.
- Ajout d'un layout Win32 reactif: la barre d'adresse, le bouton `Ouvrir`, le statut, la zone de rendu temporaire et la fenetre enfant CEF suivent la taille de la fenetre principale.
- Ajout du redimensionnement de la vue CEF via le handle natif `CefBrowserWindow`, avec notification `was_resized` au moteur.
- Verification avec `cargo fmt --check`, `cargo test`, `cargo build`, lancement visible et test UI automatise: apres chargement de `https://example.com`, la vue CEF passe de `607x204` a `1173x571` apres agrandissement de la fenetre.
- Retour utilisateur: `tintin.fr` et `google.com` semblaient ne rien faire dans Pulse Browser.
- Diagnostic: le log CEF montrait que Google arrivait bien jusqu'au moteur, mais que le processus GPU Chromium plantait en boucle, ce qui pouvait donner une page blanche ou un rendu silencieux.
- Passage de la version projet a `0.2.2-dev` pour corriger cette stabilite de rendu.
- Ajout d'une `PulseBrowserApp` CEF qui force les switches `disable-gpu`, `disable-gpu-compositing`, `disable-gpu-rasterization` et `disable-gpu-watchdog`.
- Ajout de la navigation avec la touche Entree quand la barre d'adresse est active.
- Verification avec `cargo fmt --check`, `cargo test`, `cargo build`, lancement visible et test UI automatise: `google.com` via Entree puis `tintin.fr` via le bouton creent une vue CEF et mettent a jour le statut de navigation.
- Verification du log CEF apres correction: plus de crash GPU observe sur le demarrage `0.2.2-dev`.
- Retour utilisateur: `google.com` donnait encore l'impression de ne rien faire dans Pulse Browser.
- Diagnostic complementaire: le log CEF a montre un `Timeout of new browser info response`, signe que la pompe de messages CEF et le retour d'etat UI devaient etre renforces.
- Passage de la version projet a `0.2.3-dev`.
- Activation de `external_message_pump` dans les settings CEF et ajout de retours CEF dans l'interface via les handlers de chargement et d'affichage.
- Le statut de Pulse Browser affiche maintenant les etapes CEF: debut de chargement, chargement termine, erreurs reseau, changement d'adresse et titre de page.
- Passage de la version projet a `0.2.4-dev` pour corriger l'affichage silencieux de la vue Chromium.
- Remplacement du repositionnement de la fenetre CEF par `SetWindowPos` avec affichage force, notification `was_resized` et focus explicite du navigateur embarque.
- Verification avec `cargo fmt --check`, `cargo test`, `cargo build`, lancement visible et test UI automatise: `google.com` charge dans CEF, le statut devient `Statut CEF: Page chargee: Google` et les fenetres enfants `CefBrowserWindow`, `Chrome_WidgetWin_1` et `Chrome_RenderWidgetHostHWND` sont presentes.
- Verification du log CEF apres correction: Google emet une ligne console depuis `https://www.google.com/`, ce qui confirme que la page arrive jusqu'au moteur embarque.
- Retour utilisateur: correction d'une formulation dans `AGENTS.md` pour rappeler que Pulse Browser est un navigateur web en developpement, pas un outil de developpement.
- Ajout d'une regle permanente dans `AGENTS.md`: a chaque nouvelle ouverture de session ou de chat sur Pulse Browser, Codex doit lire `MEMORY.md` pour reprendre le contexte historique du projet avant d'agir.

## 2026-07-04

- Clarification produit: l'etat `0.2.4-dev` permet d'acceder a Internet dans une vue Chromium embarquee, mais ce n'est pas encore un navigateur complet.
- Validation du prochain objectif: transformer la vue web fonctionnelle en base de navigateur local-first, pratique et securisee.
- Precision utilisateur: les cookies essentiels ne doivent pas etre un choix demande a l'utilisateur; ils doivent etre acceptes par defaut pour permettre la navigation normale et les sessions.
- Precision utilisateur: Pulse Browser doit eviter la dispersion inter-sites des donnees. Les donnees Google/YouTube doivent servir au fonctionnement de Google/YouTube, mais ne doivent pas etre recuperees silencieusement par un site tiers.
- Precision utilisateur: la securite ne doit pas rendre la navigation infame. Les sessions, identifiants et preferences doivent pouvoir rester disponibles localement sans forcer des reconnexions inutiles.
- Precision utilisateur: le coffre local doit etre transparent. L'utilisateur ne doit presque pas avoir a connaitre son existence; il ne doit devenir visible que dans des cas utiles comme les parametres avances, suppression, export/import futur ou recuperation.
- Confirmation du choix Rust: Rust est coherent avec Pulse Browser car le projet manipule des donnees sensibles, du stockage local, des fichiers chiffres, CEF et des integrations Windows avec un besoin de fiabilite et de securite memoire.
- Passage de la version projet a `0.3.0-dev`.
- Ajout de `src/profile.rs` pour creer un profil local `default` sous `%LOCALAPPDATA%\PulseBrowser\profiles\default`.
- Raccordement de CEF a un `root_cache_path` Pulse Browser et a un `cache_path` de profil persistant.
- Activation de `persist_session_cookies` et du switch CEF `persist-session-cookies` pour eviter les reconnexions inutiles.
- Ajout de `src/privacy.rs` avec une decision de cookies qui autorise les contextes first-party/same-site et bloque les contextes tiers ou first-party inconnus.
- Ajout d'un `CookieAccessFilter` CEF pour bloquer l'envoi et l'enregistrement de cookies tiers sans bloquer toute la requete reseau.
- Ajout de `src/vault.rs` pour initialiser le coffre transparent `default.pbvault`, avec signature de format Pulse Browser et marqueur chiffre par Windows DPAPI pour l'utilisateur Windows courant.
- Ajout de `docs/LOCAL_PROFILE_AND_PRIVACY.md` pour documenter le profil local, la politique cookies/sessions, le coffre transparent et les limites actuelles.
- Reduction des nouveaux statuts de navigation: affichage du domaine plutot que de l'URL complete, afin d'eviter d'exposer inutilement des donnees dans l'interface ou les traces.
- Verification avec `cargo fmt`, `cargo test` et `cargo build`: 10 tests unitaires passes et compilation dev reussie.
- Lancement temporaire de `target\debug\pulse-browser.exe`: reussi, fermeture propre.
- Verification dans `%LOCALAPPDATA%\PulseBrowser\profiles\default`: profil present, dossier `cef-profile` present et coffre `vault\default.pbvault` present.
- Retour utilisateur: il faut mettre en place ce qui n'a pas encore ete mis en place, avec autorisation d'agir dans le dossier projet et d'installer les outils manquants si necessaire.
- Passage de la version projet a `0.3.1-dev`.
- Transformation de `src/vault.rs`: le coffre `default.pbvault` devient un conteneur local d'identifiants chiffre, avec payload interne versionne.
- Ajout du dechiffrement DPAPI Windows via `CryptUnprotectData`.
- Ajout d'une API interne pour charger/sauvegarder le coffre, ajouter ou remplacer un identifiant par origine/nom d'utilisateur, et rechercher les identifiants d'une origine.
- Ajout d'une migration automatique: l'ancien marqueur chiffre cree en `0.3.0-dev` est converti en coffre structure vide au prochain demarrage.
- Raccordement au demarrage CEF: Pulse Browser cree et relit le coffre pour verifier que le conteneur chiffre local est exploitable.
- Verification avec `cargo fmt`, `cargo fmt --check`, `cargo test` et `cargo build`: 14 tests unitaires passes et compilation dev reussie.
- Lancement temporaire de `target\debug\pulse-browser.exe`: reussi, fermeture propre.
- Verification non sensible dans `%LOCALAPPDATA%\PulseBrowser\profiles\default\vault`: `default.pbvault` present.
- Passage de la version projet a `0.3.2-dev`.
- Ajout des commandes de navigation de base dans la coque Win32 provisoire: `Retour`, `Avancer`, `Recharger` et `Stop`.
- Raccordement des boutons aux commandes CEF `go_back`, `go_forward`, `reload` et `stop_load`.
- Verification avec `cargo fmt`, `cargo test` et `cargo build`: 14 tests unitaires passes et compilation dev reussie.
- Lancement temporaire de `target\debug\pulse-browser.exe`: reussi, fermeture propre.

## 2026-07-05

- Passage de la version projet a `0.6.2-dev`.
- Correction de l'import: les favoris "Autres favoris" vont desormais directement dans `root-other` sans dossier wrapper "Import Chrome Default".
- Refactorisation majeure du gestionnaire de favoris (`local_pages.rs`) : layout sidebar + panneau de contenu, recherche live, badges lettre circulaires, thème light/dark auto.
- Ajout du renommage de favoris/dossiers : bouton crayon sur chaque item → `prompt()` natif → `pulse://bookmarks/rename/{id}/{titre_encodé}` → `store.rename_node()`.
- Correction des noms vides : dossiers/favoris sans titre affichent "(sans nom)" en italique grise au lieu d'un champ vide.
- Ajout de la page d'import dédiée (`local_pages::import_page_data_url`) : section "depuis un navigateur installé" (Fusionner/Remplacer par navigateur) + section "depuis un fichier HTML".
- Le sous-menu import dans le menu principal est remplacé par un item unique "Importer des favoris..." qui ouvre la page dédiée.
- Ajout de `PulseInternalAction::ReplaceFromBrowser(usize)` et `RenameBookmark(String,String)` dans `cef_runtime.rs`.
- `import_from_source` ne dépend plus du static menu : appelle `discover_import_sources()` directement.
- Etat courant : 41 tests unitaires passes, compilation 0.6.2-dev reussie.
- Refactorisation suite session contexte précédent : page paramètres réelle (moteur de recherche, page de démarrage) dans `src/settings.rs` + `src/local_pages.rs::settings_page_data_url`.
- Ajout bouton "Nouveau dossier" dans le gestionnaire de favoris : `PulseInternalAction::AddFolder` dans `cef_runtime.rs`, handler dans `main.rs`.
- Correction icône de dossier dans BOOKMARKS_PAGE : `fill="var(--folder)"` sur SVG créé via `createElementNS` ne résout pas les CSS custom properties dans CEF. Corrigé en enveloppant le SVG dans un `<span style="color:var(--folder)">` et en utilisant `fill="currentColor"`.
- Correction menu contextuel clic droit : `showFMenu` et `hideFMenu` étaient appelées mais jamais définies (ReferenceError silencieuse). Fonctions ajoutées dans `local_pages.rs`. Ajout de `document.addEventListener('contextmenu', e => e.preventDefault())` pour supprimer le menu natif CEF.
- Etat courant : 45 tests unitaires passes, compilation 0.6.2-dev reussie.
- Passage de la version projet a `0.6.3-dev`.
- Ajout de l'icone dossier dans la barre de favoris : les dossiers affichent desormais le prefixe Unicode `📁` suivi du titre. Un dossier sans titre affiche uniquement `📁` sans texte supplementaire.
- Ajout du menu contextuel clic droit natif Win32 sur les items de la barre de favoris : `WM_CONTEXTMENU` gere dans `window_proc`, options "Ouvrir" / "Ouvrir le dossier" et "Supprimer de la barre". La suppression appelle `store.remove_node()` et rafraichit la barre.
- Ajout de `WM_CONTEXTMENU` et `GetDlgCtrlID` dans `win32.rs`, `node_id_for_control()` et `action_for_control()` dans `ui_bookmarks_bar.rs`, `show_bar_item_context_menu()` et `MenuCommand::RemoveBookmark(String)` dans `ui_menu.rs`.
- Enrichissement de la page "A propos de Pulse Browser" : tableau avec Langage (Rust), Moteur web (Chromium via CEF), Developpeur (H.J.).
- Etat courant : 45 tests unitaires passes, compilation 0.6.3-dev reussie.
- Passage de la version projet a `0.6.4-dev`.
- Correction definitive du menu contextuel clic droit sur la barre de favoris : l'interception se fait sur `WM_RBUTTONUP` dans la boucle de messages (`PeekMessageW`), avant `DispatchMessageW`. `WM_RBUTTONUP` est un message poste dans la file (contrairement a `WM_CONTEXTMENU` qui est envoye par `DefWindowProc` pendant le dispatch et n'est donc jamais visible dans la file). On recupere le control ID via `GetDlgCtrlID(msg.hwnd)` et la position ecran via `msg.pt`. Le menu affiche "Ouvrir" / "Ouvrir le dossier" et "Supprimer de la barre".
- Etat courant : 45 tests unitaires passes, compilation 0.6.4-dev reussie.
- Audit de reprise apres validation utilisateur `go`: `Cargo.toml` indiquait deja `0.7.0-dev`, tandis que `AGENTS.md` et l'historique s'arretaient a `0.6.4-dev`.
- Confirmation que le checkout contient deja une premiere implementation visible des onglets: `src/tabs.rs` gere le modele, `src/ui_tabs.rs` affiche la barre d'onglets Win32 provisoire, `src/cef_runtime.rs` cree/masque/restaure/ferme les instances CEF par onglet, et `src/main.rs` raccorde ouverture, activation, fermeture et ouverture de favoris dans un nouvel onglet.
- Mise en forme Rust appliquee avec `cargo fmt`.
- Passage de la version courante de gouvernance a `0.7.0-dev` dans `AGENTS.md`.
- Ajout de `docs/TABS_0_7.md` et `logs/2026-07-05-tabs-and-audit-0-7.md` pour documenter le palier onglets et les limites restantes.
- Verification avec `cargo fmt --check`, `cargo test` et `cargo build`: formatage reussi, 49 tests unitaires passes et compilation `0.7.0-dev` reussie.
- Limites restantes: la barre d'onglets reste provisoire en Win32, les onglets ne sont pas encore persistants entre lancements, le deplacement/reordonnancement n'est pas encore disponible, et une verification visuelle longue reste a faire.
- Tentative de verification visuelle automatisee apres nouveau `go`: lancement de `target\debug\pulse-browser.exe`, mais la session a produit des processus CEF sans fenetre top-level detectable. Les processus lances pendant ce test ont ete arretes; une verification visuelle interactive reste necessaire.
- Diagnostic dans `target\debug\debug.log`: CEF refusait le profil persistant car `cache_path` (`%LOCALAPPDATA%\PulseBrowser\profiles\default\cef-profile`) n'etait pas enfant de `root_cache_path` (`%LOCALAPPDATA%\PulseBrowser\cef-user-data`). CEF indiquait donc un retour au stockage memoire.
- Passage de la version projet a `0.7.1-dev` pour corriger ce bug de profil local persistant.
- Correction de `src/profile.rs`: `root_cache_dir` pointe maintenant vers la racine locale `%LOCALAPPDATA%\PulseBrowser`, tandis que `cef_cache_dir` reste dans `profiles\default\cef-profile`.
- Mise a jour de `docs/LOCAL_PROFILE_AND_PRIVACY.md` et ajout de `logs/2026-07-05-cef-cache-path-0-7-1.md`.
- Ajout du test `profile::tests::cef_cache_path_is_inside_root_cache_path` pour empecher le retour d'un `cache_path` hors de `root_cache_path`.
- Verification avec `cargo fmt --check`, `cargo test` et `cargo build`: formatage reussi, 50 tests unitaires passes et compilation `0.7.1-dev` reussie.
- Recadrage utilisateur: l'interface cible etait WinUI 3 depuis le depart; la coque Win32 ne doit plus devenir l'interface produit.
- Passage de la version projet a `0.8.0-dev` pour demarrer la migration interface WinUI 3.
- Ajout de `PulseBrowser.WinUI`, premiere coque WinUI 3 C# separee du prototype Rust/Win32, avec accueil, centre local et parametres.
- Ajout de `run-winui.cmd`, `build-winui.cmd`, `scripts/run-winui.ps1`, `scripts/build-winui.ps1`, `docs/WINUI3_MIGRATION_0_8.md` et `logs/2026-07-05-winui3-shell-0-8.md`.
- Mise a jour de `AGENTS.md`: Win32 est marque comme prototype historique; WinUI 3 devient la direction produit courante; Rust reste le coeur local.
- Creation de `PulseBrowser.slnx` et ajout du projet `PulseBrowser.WinUI`.
- Restore NuGet Windows App SDK reussi apres autorisation reseau; build WinUI 3 reussi avec MSBuild Visual Studio x64.
- Correction du projet WinUI: les fichiers XAML sont inclus automatiquement par le SDK; les inclusions explicites `ApplicationDefinition`/`Page` ont ete retirees pour eviter les doublons.
- Verification Rust maintenue: `cargo fmt --check`, `cargo test` (50 tests) et `cargo build` reussis en `0.8.0-dev`.
- Lancement automatise de `PulseBrowser.WinUI.exe` tente: le processus demarre, mais aucune fenetre top-level n'est detectable dans cette session. Le processus de test a ete arrete; verification visuelle interactive encore necessaire.
- Retour utilisateur: la coque WinUI 3 `0.8.0-dev` etait plus jolie, mais elle avait perdu des fonctions deja presentes dans le prototype Win32: onglets, favoris, menu, page A propos et import/export de favoris.
- Passage de la version projet a `0.8.1-dev` pour restaurer une parite visible minimale dans la nouvelle interface.
- Remplacement de `PulseBrowser.WinUI/MainWindow.xaml` par une surface navigateur plus complete: menu principal, onglets WinUI, barre de navigation, barre de favoris, gestionnaire de favoris, panneau import/export, centre local, parametres et page A propos.
- Ajout dans `PulseBrowser.WinUI/MainWindow.xaml.cs` d'un `BookmarkStore` C# compatible avec le fichier local `%LOCALAPPDATA%\PulseBrowser\profiles\default\navigation\bookmarks.tsv`, afin que la coque WinUI lise/ecrive les memes favoris que le prototype Rust.
- Ajout de l'import HTML de favoris, de l'export HTML et de l'import depuis les profils Chromium locaux detectes (Chrome, Edge, Brave, Chromium, Vivaldi).
- Mise a jour de `AGENTS.md`: la migration WinUI 3 ne doit pas provoquer de regression fonctionnelle visible et doit reprendre les onglets, favoris, menus, import/export, parametres et A propos deja acquis.
- Mise a jour de `PulseBrowser.WinUI/README.md`, `docs/WINUI3_MIGRATION_0_8.md` et ajout de `logs/2026-07-05-winui3-parity-0-8-1.md`.
- Verification WinUI: `build-winui.cmd` reussi avec MSBuild Visual Studio x64, 0 avertissement et 0 erreur.
- Verification Rust maintenue: `cargo fmt --check`, `cargo test` (50 tests) et `cargo build` reussis en `0.8.1-dev`.
- Nouveau recadrage utilisateur: le gestionnaire de favoris WinUI restait trop pauvre par rapport au gestionnaire precedent, qui avait une vraie logique de dossiers.
- Correction du gestionnaire de favoris WinUI: colonne de dossiers, contenu du dossier courant, fil d'Ariane, recherche, creation de dossier, renommage et suppression. Les actions continuent d'utiliser le fichier local `bookmarks.tsv`.
- Verification WinUI apres correction: `build-winui.cmd` reussi avec 0 avertissement et 0 erreur.
- Recadrage utilisateur majeur: la WinUI ne permettait plus de naviguer sur Internet, les favoris ne chargeaient pas les sites, les dossiers de barre ouvraient le gestionnaire au lieu d'un menu, et l'interface n'etait pas une amelioration suffisante pour justifier la regression.
- Passage de la version projet a `0.8.2-dev`.
- Ajout d'une zone WebView2 dans la coque WinUI pour restaurer immediatement la navigation Internet dans la fenetre moderne.
- Raccordement de la barre d'adresse, des boutons retour/avancer/recharger/stop, des clics sur favoris et des onglets a la zone web WinUI.
- Correction de la barre de favoris: les dossiers ouvrent maintenant un menu deroulant avec liens et sous-dossiers; le gestionnaire reste accessible via `Gerer`.
- Documentation explicite: WebView2 est un pont temporaire de migration pour restaurer l'usage navigateur; la cible moteur finale reste Chromium via CEF raccorde au coeur Rust local.
- Ajout de `logs/2026-07-05-winui3-navigation-0-8-2.md` et mise a jour de `AGENTS.md`, `PulseBrowser.WinUI/README.md` et `docs/WINUI3_MIGRATION_0_8.md`.
- Correction du crash WinUI au demarrage avec WebView2 actif: la trace montrait que le controle WebView2 etait cree puis que l'application tombait a la premiere navigation avant `CoreWebView2Initialized`.
- La surface WebView2 est maintenant creee dynamiquement apres activation de la fenetre, force `EnsureCoreWebView2Async`, met la premiere navigation en attente, puis navigue via `CoreWebView2` une fois initialise.
- Ajout de la reference NuGet explicite `Microsoft.Web.WebView2` dans le projet WinUI.
- Verification: `build-winui.cmd` reussi avec 0 avertissement et 0 erreur; lancement visible court et lancement cache court de `PulseBrowser.WinUI.exe` reussis, processus vivant avec fenetre `Pulse Browser 0.8.2-dev`.
- Recadrage utilisateur: l'organisation de la coque WinUI restait incoherente par rapport a l'ancienne version, avec `A propos` dans `Pulse`, une page separee `Donnees locales`, et pas d'acces direct assez clair a `Autres favoris`.
- Passage de la version projet a `0.8.3-dev`.
- Correction du menu WinUI: `A propos de Pulse Browser` est deplace dans `Outils`, la page separee `Donnees locales` est retiree, les informations de profil local sont integrees dans `A propos`, et `Autres favoris` dispose d'un acces direct dans le menu `Favoris`.
- Verification: `build-winui.cmd` reussi avec 0 avertissement et 0 erreur; lancement cache court de `PulseBrowser.WinUI.exe` reussi avec fenetre `Pulse Browser 0.8.3-dev`; `cargo fmt --check`, `cargo test` (50 tests) et `cargo build` reussis en `0.8.3-dev`.
- Nouveau recadrage utilisateur: la gestion des favoris WinUI restait encore trop pauvre par rapport a l'ancienne version, notamment sans vrais icones de dossiers, sans bouton visible `Autres favoris` dans la barre, sans menu contextuel, et avec un import navigateur trop peu explicite.
- Passage de la version projet a `0.8.4-dev`.
- Correction de la parite favoris WinUI: glyphes WinUI pour dossiers/liens, bouton permanent `Autres favoris` dans la barre de favoris, ouverture normale des dossiers dans le gestionnaire, navigation des liens favoris vers la zone web WinUI, menus contextuels sur les listes de favoris et de dossiers.
- Correction de l'import navigateur WinUI: panneau separe pour les navigateurs installes, statut des sources detectees, bouton de fusion et bouton de remplacement. Le remplacement sauvegarde d'abord le fichier local de favoris avant reecriture.
- Ajout de `logs/2026-07-05-winui3-bookmarks-parity-0-8-4.md` et mise a jour de `PulseBrowser.WinUI/README.md` et `docs/WINUI3_MIGRATION_0_8.md`.
- Verification: `build-winui.cmd` reussi apres restore NuGet autorise, avec 0 erreur et 5 avertissements de copie dus a un ancien processus `PulseBrowser.WinUI.exe` qui verrouillait temporairement l'executable; lancement cache court reussi avec fenetre `Pulse Browser 0.8.4-dev`; `cargo fmt --check`, `cargo test` (50 tests) et `cargo build` reussis en `0.8.4-dev`.
- Nouveau cadrage utilisateur: pour eviter qu'un autre assistant modifie l'ancien prototype Win32/CEF par erreur, la seule surface produit active doit etre WinUI 3.
- Passage de la version projet a `0.8.5-dev`.
- Correction de l'organisation favoris WinUI: la gestion des favoris est exposee dans `Parametres`, le bouton `Gerer` est retire de la barre, `Autres favoris` est separe a droite de la barre, et l'import/export reste accessible depuis les parametres.
- Correction du toggle d'onglets verticaux: il active maintenant un rail lateral d'onglets, masque la barre horizontale et permet la selection d'onglets depuis la colonne.
- Ajout d'un cache local de favicons WinUI: WebView2 fournit les icones quand elles existent, elles sont stockees dans le profil local puis rattachees aux signets via une colonne optionnelle de `bookmarks.tsv`.
- Desactivation des lanceurs ambigus du prototype Win32/CEF: `run-dev.cmd` et `scripts/run-dev.ps1` refusent le lancement, tandis que `archive/win32-cef-prototype/` documente un lanceur legacy explicite reserve au diagnostic technique.
- Mise a jour de `AGENTS.md`: `PulseBrowser.WinUI` est la seule interface produit active; aucune nouvelle fonction visible ne doit etre ajoutee au prototype Win32 archive sans demande explicite.
- Ajout de `logs/2026-07-05-winui3-product-direction-0-8-5.md` et mise a jour de `PulseBrowser.WinUI/README.md`, `docs/WINUI3_MIGRATION_0_8.md` et `docs/BOOKMARKS_0_6.md`.
- Verification: `build-winui.cmd` reussi avec 0 avertissement et 0 erreur; lancement cache court reussi avec fenetre `Pulse Browser 0.8.5-dev`; `cargo fmt --check`, `cargo test` (50 tests) et `cargo build` reussis en `0.8.5-dev`; `run-dev.cmd` sort bien avec le message de desactivation du prototype.
- Nouveau cadrage utilisateur: les onglets verticaux doivent etre redimensionnables et reductibles, la gestion des favoris doit quitter `Parametres` pour etre placee sous `Outils`, et les dossiers dans `Autres favoris` doivent rester actionnables depuis leur menu.
- Passage de la version projet a `0.8.6-dev`.
- Correction de l'organisation WinUI: suppression du menu principal `Favoris`, ajout de `Outils > Favoris` avec acces a la barre des favoris, `Autres favoris`, gestionnaire, import, export et affichage/masquage de la barre.
- Nettoyage des parametres WinUI: la section de gestion des favoris est retiree de `Parametres`, qui reste centree sur les options de navigation visibles.
- Correction du rail d'onglets verticaux: ajout d'une poignee de redimensionnement, ajout d'un mode compact en icones, et reutilisation des favicons locales dans les onglets horizontaux et verticaux.
- Correction des menus de dossiers de favoris: les sous-menus exposent `Ouvrir le dossier`, `Renommer`, `Supprimer` puis le contenu du dossier, afin de garder une action directe meme depuis `Autres favoris`.
- Ajout de `logs/2026-07-05-winui3-vertical-tabs-favorites-tools-0-8-6.md` et mise a jour de `PulseBrowser.WinUI/README.md` et `docs/WINUI3_MIGRATION_0_8.md`.
- Verification: `build-winui.cmd` reussi apres restore NuGet autorise avec 0 avertissement et 0 erreur; lancement cache court reussi avec fenetre `Pulse Browser 0.8.6-dev`; `cargo fmt --check`, `cargo test` (50 tests) et `cargo build` reussis en `0.8.6-dev`; `run-dev.cmd` reste desactive et renvoie vers `run-winui.cmd`.
- Nouveau cadrage utilisateur: les parametres visibles, notamment les onglets verticaux, doivent rester actifs apres fermeture et relance; les favicons deja recuperees doivent aussi rester disponibles.
- Passage de la version projet a `0.8.7-dev`.
- Ajout d'un fichier local `ui-settings.json` dans le dossier `navigation/` du profil Pulse Browser pour conserver les reglages UI.
- Persistance WinUI ajoutee pour la barre de favoris visible ou masquee, l'activation des onglets verticaux, le mode compact du rail vertical et la largeur du rail vertical.
- Application des reglages UI au demarrage avec protection contre les sauvegardes intempestives pendant l'initialisation WinUI.
- Amelioration du cache de favicons WinUI: reconstruction du cache depuis les favoris charges et recherche deterministe de l'icone locale par hash d'URL avant d'afficher l'icone generique d'un onglet.
- Ajout de `logs/2026-07-05-winui3-persistent-ui-settings-0-8-7.md` et mise a jour de `PulseBrowser.WinUI/README.md` et `docs/WINUI3_MIGRATION_0_8.md`.
- Verification: `build-winui.cmd` reussi apres restore NuGet autorise avec 0 avertissement et 0 erreur; lancement cache court reussi avec fenetre `Pulse Browser 0.8.7-dev`; `cargo fmt --check`, `cargo test` (50 tests) et `cargo build` reussis en `0.8.7-dev`; `run-dev.cmd` reste desactive et renvoie vers `run-winui.cmd`.

## 2026-07-05 (suite) — 0.9.0-dev

- Passage de la version projet a `0.9.0-dev` pour marquer les trois nouvelles fonctionnalites globales: persistance des onglets, historique de navigation, et gestionnaire de telechargements.
- Ajout de la persistance des onglets: chaque ouverture/fermeture/navigation sauvegarde la session dans `navigation/tabs.json` (titre, adresse, icone, index actif). Au demarrage, les onglets de la session precedente sont restaures automatiquement a la place de l'onglet Accueil par defaut.
- Ajout de l'historique de navigation local: chaque page web chargee avec succes est enregistree dans `navigation/history.json` (URL, titre, date et heure). Cap a 2000 entrees. Accessible via `Outils > Historique` avec recherche en temps reel, double-clic pour rouvrir, menu contextuel pour supprimer une entree, et bouton `Vider l'historique`. Les favicons deja en cache sont affiches sur chaque entree.
- Ajout du gestionnaire de telechargements: hook sur `CoreWebView2.DownloadStarting` pour intercepter chaque fichier telecharge. Le panel `Outils > Telechargements` affiche nom du fichier, domaine source, barre de progression en temps reel, etat (En cours / Termine / Echec), et boutons `Ouvrir` / `Dossier` une fois le telechargement termine.
- Verification: `build-winui.cmd` reussi avec 0 avertissement et 0 erreur; `cargo check` reussi avec 5 avertissements dead-code pre-existants, 0 nouvelle erreur, en `0.9.0-dev`.

## 2026-07-05 (suite) — 0.9.1-dev

- Passage de la version projet a `0.9.1-dev` pour marquer l'ajout du pont IPC Rust→WinUI et de l'interface coffre.
- Ajout de `serde = { version = "1", features = ["derive"] }` et `serde_json = "1"` dans `Cargo.toml` pour le protocole IPC.
- Ajout de `src/ipc_server.rs`: serveur a pipe nomme Windows `\\.\pipe\PulseBrowserCore` en pur Win32 (kernel32.dll, aucune dependance supplementaire). Protocole JSON ligne par ligne. Methodes supportees: `ping`, `get_profile`, `list_credentials`, `delete_credential`, `upsert_credential`, `shutdown`. Le serveur s'initialise avec le vault DPAPI de l'utilisateur courant, puis accepte un client a la fois en boucle.
- Ajout de `LocalVault::remove_credential` et de la fonction publique `vault::remove_credential` dans `src/vault.rs` pour la suppression d'un identifiant par origine+nom d'utilisateur.
- Modification de `src/main.rs`: si l'argument `--serve` est present, le processus s'oriente vers `ipc_server::run()` sans initialiser Win32/CEF. En mode normal, le comportement est inchange.
- Ajout de `PulseCoreClient` (classe interne C#): cherche `pulse-browser.exe` en remontant jusqu'a 9 niveaux depuis le dossier de l'exe WinUI (debug/release) ou via `PULSE_BROWSER_CORE_PATH`. Demarre le process avec `CreateNoWindow`, se connecte via `NamedPipeClientStream`, expose `ListCredentialsAsync`, `DeleteCredentialAsync`, `UpsertCredentialAsync`.
- Ajout de `VaultCredential` (record public C# avec attributs `[JsonPropertyName]` pour le snake_case Rust).
- Ajout du panel `Coffre de mots de passe` accessible via `Outils > Coffre de mots de passe`: liste des identifiants avec origine, nom d'utilisateur, date de mise a jour, bouton supprimer par entree, bouton ajouter (dialogue avec TextBox + PasswordBox). Le panel affiche un message clair si le coeur Rust est indisponible.
- Verification: Rust compile sans erreur (`cargo build`, 5 avertissements pre-existants); WinUI compile sans erreur (MSBuild VS18, 0 avertissement, 0 erreur) en `0.9.1-dev`.

## 2026-07-05 (suite) — 0.9.2-dev

- Passage de la version projet a `0.9.2-dev` pour corriger les favicons et les onglets verticaux.
- **Correction favicons** : `OriginOf` et `BookmarkStore.UrlOrigin` normalisent desormais l'origine en supprimant le prefixe `www.` du host. Ainsi `https://www.youtube.com/watch?v=...` et `https://youtube.com/` partagent la meme origine `https://youtube.com`, le meme fichier favicon et la meme correspondance dans les favoris. Les favicons se mettent a jour automatiquement pour tous les favoris du meme domaine au premier passage.
- **Correction onglets verticaux** : refonte de `RenderVerticalTabs`. En mode compact : boutons 40x36 px centres, icone 20px, tooltip toujours visible, onglet actif en style accent. En mode etendu : bouton pleine largeur, icone 16px + titre, onglet actif en style accent. Suppression de l'ancien `TabHeaderContent(compact)` dans le rail vertical.
- **Poignee de redimensionnement** : agrandie a 10px avec une barre centrale visible (2px, arrondie), plus facile a attraper.
- **Icone du bouton compact/etendre** : remplace le glyphe `Pin` par `OpenPane`/`ClosePane` selon l'etat, plus intuitif.
- Verification: WinUI compile sans erreur (MSBuild VS18, 0 avertissement, 0 erreur) en `0.9.2-dev`.

## 2026-07-05 (suite) — 0.9.3-dev / 0.9.4-dev / 0.9.5-dev

- Passage de la version projet a `0.9.5-dev` en livrant trois niveaux d'evolution en une seule session.

### 0.9.3-dev : trois corrections

- **Favicon avec fallback** : `CaptureCurrentFaviconAsync` reecrit avec trois niveaux : 1) `GetFaviconAsync` WebView2, 2) injection JS `document.querySelector('link[rel*="icon"]')` + telechargement HTTP via `HttpClient`, 3) `/favicon.ico` standard. Reutilisation du favicon existant si age inferieur a 24h. Extraction de `ApplyFaviconToUi` pour eviter la repetition de code.
- **Resize vertical fluide** : `VerticalTabsResizeThumb_DragDelta` applique maintenant `VerticalTabsRail.Width = targetWidth` directement sans snapping. Le flag `_verticalTabsCompact` bascule au seuil `VerticalTabsCompactWidth + 24px`, permettant de glisser en continu depuis la vue icones jusqu'a la vue etendue sans blocage.
- **Page de demarrage configurable** : ajout de `StartupMode` (`restore` / `home` / `custom`) et `StartupUrl` dans `UiSettings`. Section `Demarrage` ajoutee dans le panel Parametres avec `RadioButtons` et `TextBox` URL conditionnelle. Methode `ApplyStartupPage` remplace l'appel direct a `RestoreTabSession` dans le constructeur.

### 0.9.4-dev : capture et auto-remplissage des identifiants

- **Capture JS des formulaires** : injection de `InjectCredentialMonitorAsync` apres chaque `NavigationCompleted` sur une page web. Le script JS detecte les `submit` d'un formulaire contenant un `input[type="password"]`, extrait le couple login/mot de passe et les envoie via `window.chrome.webview.postMessage`.
- **Barre de sauvegarde** : `CredentialSaveBar` (Border en haut de BrowserPanel) affiche le login detecte avec deux boutons `Enregistrer` / `Ignorer`. A l'acceptation : appel `UpsertCredentialAsync` vers le coeur Rust.
- **Barre d'auto-remplissage** : `AutoFillBar` affiche une offre de remplissage si le coffre contient des identifiants pour l'origine courante. Au clic `Remplir` : injection JS qui remplit les champs et declenche les evenements `input`/`change` pour que les frameworks JS (React, Vue...) detentent le changement.
- **Nouveau champ `WebMessageReceived`** : attache sur `CoreWebView2Initialized`, route les messages `{t:"cred"}` vers l'affichage de la barre de sauvegarde.
- **Champ `password` dans `VaultCredential`** : le protocole IPC `list_credentials` renvoie maintenant le mot de passe en clair (uniquement en memoire locale, jamais loggue), pour permettre l'auto-remplissage cote C#.

### 0.9.5-dev : mot de passe maitre

- **`src/vault_lock.rs`** (nouveau) : gestionnaire de verrou par mot de passe maitre. Derive une cle PBKDF2-SHA256 (100 000 iterations) avec sel aleatoire 32 octets (via `RtlGenRandom`). Stocke `sel + hash` dans un fichier `vault-master.lock` protege par DPAPI. Expose `has_master_password`, `set_master_password`, `verify_master_password`, `clear_master_password`.
- **`sha2 = "0.10"`** : ajout dans `Cargo.toml` pour le HMAC-SHA256 interne de PBKDF2.
- **`src/ipc_server.rs`** : ajout de l'etat serveur `ServerState { vault_locked, lock_path }`. Les commandes `list_credentials`, `upsert_credential`, `delete_credential` retournent `{"ok":false,"error":"vault_locked"}` si le coffre est verrouille. Nouvelles commandes : `lock_status`, `unlock_vault`, `set_master_password`, `verify_master_password`, `clear_master_password`.
- **`PulseCoreClient`** (C#) : nouvelles methodes `LockStatusAsync`, `SetMasterPasswordAsync`, `VerifyMasterPasswordAsync`, `UnlockVaultAsync`, `ClearMasterPasswordAsync`.
- **Panel Parametres** : section `Coffre de mots de passe` avec `ToggleSwitch` (activer/desactiver le mot de passe maitre) et bouton `Definir / modifier`. Activation demande un nouveau mot de passe (avec confirmation), desactivation verifie l'ancien.
- **Acces coffre** : `VaultMenu_Click` appelle `UnlockVaultIfNeededAsync` avant `RefreshVaultPanelAsync`. Si le coffre est verrouille, une boite de dialogue `PromptMasterPasswordAsync` est affichee.
- **`UiSettings`** : ajout de `StartupMode`, `StartupUrl`, `MasterPasswordEnabled`.
- Verification : Rust `cargo check` reussi sans erreur en `0.9.5-dev`; WinUI compile sans erreur (MSBuild VS18) en `0.9.5-dev`.

## 2026-07-05 (suite) — 0.9.6-dev

- Passage de la version projet a `0.9.6-dev` pour deux corrections signalees apres tests.
- **Resize onglets verticaux (vraie cause)** : `VerticalTabsRail.Width` retournait `NaN` quand la largeur etait geree par la colonne Grid en mode Auto. `NaN + delta = NaN` rendait `Math.Clamp` sans effet. Correction : utiliser `ActualWidth` en fallback dans `VerticalTabsResizeThumb_DragDelta`.
- **Favicon sur clic favori** : `LoadFaviconCacheFromBookmarks` n'indexait les icones que par `node.Url` exact. Tout ecart (trailing slash, www. vs sans) provoquait un cache miss et l'onglet restait sans icone. Corrections : 1) `LoadFaviconCacheFromBookmarks` indexe aussi par `OriginOf(node.Url)`, 2) Ajout de `PreloadBookmarkFavicon(node)` appele depuis `OpenBookmarkNode` et `BookmarkFlyoutUrl_Click` pour pré-charger le cache immediatement avant la navigation.
- Verification : WinUI compile sans erreur (MSBuild VS18, 0 avertissement, 0 erreur) en `0.9.6-dev`.

## 2026-07-05 (suite) — 0.10.0-dev

- Passage de la version projet a `0.10.0-dev`.
- Adoption de l'extension `.pulse` et du chiffrement DPAPI pour tous les fichiers de donnees : `bookmarks.pulse`, `history.pulse`, `ui-settings.pulse`, `tabs.pulse`, `profile.pulse`. Classe statique `PulseFile` (`ReadAllText`, `WriteAllText`, `TryReadAllText`) avec `ProtectedData` et entropie fixe `PulseBrowser.WinUI.v1`. Migration automatique des anciens fichiers `.tsv` / `.json` au premier lancement.
- Ajout du mode invite (Option A — profil vide) : bouton "Continuer sans profil (mode invite)" sur tous les panneaux de login. `BookmarkStore.SetGuestMode` et `HistoryStore.SetGuestMode` bloquent toute ecriture. Gardes sur `AddHistoryEntry`, `SaveTabSession`, `CredentialSaveBar`, `VaultMenu_Click`. Le coffre renvoie "indisponible en mode invite" sans exception. Titre fenetre : "Pulse Browser 0.10.0-dev — Mode invite". Indicateur `ProfileStatusText` en barre de statut bas droite : "Mode invite" en orange, "Connecte : [Nom]" sinon.
- Restructuration des parametres en sidebar : colonne gauche 200 px (RadioButtons de navigation) + colonne droite (ScrollViewer avec sections Navigation · Demarrage · Coffre · Profil). Handler `SettingsNav_Click` affiche/masque les sections par `Tag`. Structure extensible sans refactoring.
- Favicons sur la barre de favoris : methode `EnrichNodesWithFaviconCache` appelee dans `ReloadBookmarks()` juste apres `AllNodes()`, cache memoire + fichiers `favicons/hash-origin.png` sur disque.
- Contrainte de largeur minimale fenetre avec onglets verticaux : `_appWindow` via `WindowNative.GetWindowHandle` + `AppWindow.GetFromWindowId`, handler `AppWindow_Changed` → `EnforceMinWindowWidth()`, largeur min = rail + 620 px.
- InfoBar avertissement mode invite sur les panneaux `CreateProfilePanel`, `LoginPasswordPanel` et `LoginPinPanel`.
- Correction de `AGENTS.md` : la version courante etait restee a `0.9.7-dev`; mise a jour a `0.10.0-dev`.
- Etat courant : interface active `PulseBrowser.WinUI`, `MainWindow.xaml.cs` environ 4100 lignes, coeur Rust (`src/`) inchange.

## 2026-07-06 — 0.11.0-dev

- Passage de la version projet a `0.11.0-dev`.

### Corrections (session 2026-07-06)

- **Thumb de resize invisible** : la poignee de redimensionnement du rail d'onglets verticaux etait quasi-invisible (fond opaque a 0.18). Refonte du template XAML : fond transparent par defaut, barre accent a 45 % d'opacite visible en permanence, etats `PointerOver` et `Pressed` avec fond colore, tooltip "Faire glisser pour redimensionner". Largeur passee a 12 px.
- **Redirect Amazon au toggle onglets** : basculer de onglets verticaux vers onglets horizontaux declenchait `BrowserTabs_SelectionChanged`, qui appelait `NavigateBrowser(tab.Address)` et naviguait vers l'onglet actif (Amazon si session restauree). Correction : flag `_suppressTabNavigation = true` pendant tout `ApplyVerticalTabsLayout()`.

### Stockage des donnees (section "Stockage" dans Parametres)

- Nouveau fichier `PulseConfig.cs` : config bootstrap `%LOCALAPPDATA%\PulseBrowser\config.json` (JSON plain, hors profil). Stocke `CustomProfilePath`. Ce fichier est toujours au meme endroit ; c'est lui qui indique ou chercher le profil.
- `PulseProfilePaths.Default()` lit `PulseConfig` au demarrage. Si le chemin custom existe, il est utilise comme racine du profil ; sinon, le chemin par defaut est maintenu.
- Nouveau fichier `PulseBackup.cs` : export/import chiffre. Format `.pulsebackup` : magic "PULSEBAK" + version + sel 16 octets + IV 16 octets + AES-256-CBC(PBKDF2-SHA256 100 000 iterations). Le payload chiffre est un ZIP contenant les fichiers de navigation decryptes (bookmarks, history, tabs, ui-settings, profile en texte brut). Le coffre Rust n'est pas inclus (DPAPI lie au compte Windows).
- Bouton "Changer de dossier" : FolderPicker, copie recursive du profil vers le nouvel emplacement, mise a jour de `PulseConfig`, InfoBar "Redemarrage requis" avec bouton "Fermer Pulse Browser".
- Boutons "Exporter une sauvegarde" et "Importer une sauvegarde" : dialogue mot de passe (avec confirmation a l'export), `FileSavePicker` / `FileOpenPicker`, appel `PulseBackup.Export` / `PulseBackup.Import`. Apres import, les favoris sont recharges immediatement ; les autres changements sont appliques au redemarrage.
- Mode invite : les trois operations sont bloquees.
- Nouvelle section "Stockage" dans le panel Parametres (RadioButton + `SettingsSectionStorage`).

### Stabilisation — moteur de recherche, barre d'adresse, page d'accueil

- **Moteur de recherche fonctionnel** : le `ComboBox` dans Parametres → Navigation est maintenant branche. Options : Google, DuckDuckGo, Brave Search, Bing. Propriete `SearchEngine` ajoutee dans `UiSettings` (defaut `"google"`). Sauvegarde immediate au changement, restauration au demarrage via `ApplyUiSettings`.
- **Barre d'adresse corrigee** : `NormalizeAddress` devient une methode d'instance. Nouvelles regles : texte avec espace → recherche (avant : traite comme URL potentielle) ; `localhost` / `localhost:port` → `http://localhost` sans TLS ; prefixe `file://` reconnu. Nouvelle methode `SearchUrl(query)` qui route vers le moteur selectionne.
- **Page d'accueil** : remplacement du texte placeholder "Interface WinUI avec navigation web active..." et de la version figee "0.9.2-dev". Nouvelle page avec logo "P" gradient orange, version dynamique `Version`, et trois cartes : "Prive par defaut", "Coffre local", "Votre profil".
- Etat courant : `MainWindow.xaml.cs` environ 4300 lignes, deux nouveaux fichiers (`PulseConfig.cs`, `PulseBackup.cs`), coeur Rust (`src/`) inchange.

### Redesign de la page de connexion

- **Card layout** : l'overlay de login passe d'un StackPanel flottant a une `Border` avec `CardBackgroundFillColorDefaultBrush` + `CornerRadius="12"`, centre dans un `ScrollViewer`. Largeur reduite a 300 px.
- **Logo unifie** : en-tete remplace par une `Border` gradient orange (40x40 px, `CornerRadius="10"`) avec "P" blanc + TextBlock "Pulse Browser" sur une ligne, coherent avec la page d'accueil.
- **InfoBars supprimees** : les deux `InfoBar IsOpen="True"` Warning toujours ouvertes dans `LoginPasswordPanel` et `LoginPinPanel` sont retirees. Remplacees par un separateur + `HyperlinkButton` semi-transparent "Continuer sans profil (mode invite)".
- **PIN pad compact** : boutons reduits de 80x56 a 72x46, espacement de 10 a 8 px.

### Corrections — profil et onboarding

- **Favoris herites a la creation de profil** : `CreateProfileButton_Click` efface maintenant tous les fichiers `.pulse` du `NavigationDir` avant de valider. Plus de donnees residuelles d'une session precedente.
- **Choix du dossier de stockage a l'onboarding** : ajout d'un selecteur "Dossier de stockage" + bouton "Choisir" dans `CreateProfilePanel`. Si un dossier custom est choisi, `PulseConfig` est mis a jour, le profil est cree dans le nouveau dossier, et l'app redemarre (car `_profile` et `_bookmarks` sont `readonly`). Sinon, pas de redemarrage.
- **Bouton "Reinitialiser le profil"** dans Parametres > Profil : confirmation obligatoire, supprime tout le dossier de profil recursif (nav, vault, favicons, profile.pulse), remet `PulseConfig.CustomProfilePath = null`, redemarre l'app.

### Migration depuis un autre navigateur (onboarding)

- Apres `CreateProfileButton_Click` reussi, appel de `ShowMigrationOrDismiss()` au lieu de `DismissLoginOverlay()` direct.
- Si aucune source detectee : fermeture directe. Sinon : panneau `MigrationPanel` avec liste des navigateurs, bouton "Importer les favoris", lien "Passer cette etape".
- `ShowLoginPanel` mis a jour avec le cas `"migration"`.
- `BrowserImportSource.Discover()` : ajout d'Opera et Opera GX (chemins `%APPDATA%\Opera Software\...`, Roaming). Firefox reste en attente (format SQLite).
- Etat courant : `MainWindow.xaml.cs` environ 4500 lignes.

## 2026-07-06 — 0.12.0-dev

- Passage de la version projet a `0.12.0-dev`.

### Migration navigateur — vraie implémentation

- Le panneau de migration affiche desormais une **liste fixe** de tous les navigateurs connus (Chrome, Edge, Brave, Opera, Opera GX, Vivaldi, Firefox), independamment de ce qui est installe. Chaque entree indique soit le nombre de favoris detectes, soit "non detecte".
- Chrome, Edge, Brave, Opera, Opera GX, Vivaldi : import automatique si detecte.
- Firefox : non supporte (SQLite) — message explicatif + bouton "Depuis un fichier HTML" comme alternative.
- Bouton "Depuis un fichier HTML..." expose directement dans le panneau migration (`.html` / `.htm`), couvre Firefox et tout autre navigateur.
- `MigrationBrowserEntry` : record `(Name, Source?)` avec `Label` dynamique.
- `_migrationEntries` remplace `_migrationSources`. `ShowMigrationOrDismiss()` affiche toujours le panneau (plus de "dismiss si vide").
- `ImportAndFinish(tree, browserName)` : methode commune pour import navigateur et import HTML, gere le restart si dossier custom.
- `_restartRequired` : flag pose quand dossier custom choisi ; les handlers migration restartent apres import au lieu de `DismissLoginOverlay`.

### Corrections onboarding et UX

- **Lien "Continuer sans profil" supprime de `CreateProfilePanel`** : n'avait aucun sens pendant la creation d'un profil. Reste uniquement sur `LoginPasswordPanel` et `LoginPinPanel`.
- **Suppression profil via PowerShell** : profil de test (`E:\Documents\PulseBrowser`) supprime, `PulseConfig` remis a zero pour repartir du premier lancement.

### Saisie PIN au clavier physique

- `RootKeyDown` : handler sur `Content.KeyDown` (root UIElement). Quand `LoginPinPanel` est visible, intercepte les touches `0-9` (clavier normal + pave numerique), `Backspace` et `Echap`. `e.Handled = true` pour eviter la propagation.
- `ProcessPinInputAsync(string tag)` : logique PIN extraite de `PinDigit_Click` dans une methode async partagee. `PinDigit_Click` et `RootKeyDown` appellent tous les deux cette methode.
- Le pad visuel reste fonctionnel en parallele.

### Verrouillage automatique de session

- `SessionTimeoutMinutes` ajoute dans `UiSettings` (defaut `0` = jamais).
- `ComboBox SessionTimeoutCombo` dans Parametres > Profil : options Jamais / 10 min / 1h / 5h.
- `DispatcherTimer _sessionTimer` : demarre a `DismissLoginOverlay`, se reinitialise a chaque interaction (souris via `Content.PointerMoved`, clavier via `RootKeyDown`).
- `SessionTimer_Tick` : re-affiche `LoginOverlay` avec panneau mot de passe ou PIN selon le profil, message "Session verrouillee automatiquement."
- Inactive en mode invite et si `LoginOverlay` deja visible.
- Sauvegarde dans `ui-settings.pulse`, restauration dans `ApplyUiSettings`.

## 2026-07-06 — 0.13.0-dev

- Passage de la version projet a `0.13.0-dev`.

### Architecture modules privacy — `PulseBrowser.WinUI/Privacy/`

Nouvelle architecture modulaire : interface `IPrivacyModule` (Id, DisplayName, IsEnabled, ShouldBlock, CleanUrl) + `PrivacyEngine` qui orchestre tous les modules. Chaque module est independant et activable/desactivable par l'utilisateur.

**NetworkBlocker** (`Privacy/NetworkBlocker/`)

- `SeedList.cs` : ~140 domaines tracker bloqués d'emblée (Google Ads, Meta, Criteo, Taboola, Outbrain, Hotjar, Mixpanel, Amplitude, FullStory, Adobe Analytics, etc.). Protection active dès le premier lancement, sans téléchargement.
- `FilterParser.cs` : parseur du format Adblock Plus / uBlock Origin. Gère `||domain^`, exceptions `@@||domain^`, option `$third-party`, cosmetics `##` (ignorés, réservés v0.15+), commentaires `!`. Sépare règles de domaine (HashSet O(1)) et règles sous-chaîne (List).
- `FilterListManager.cs` : télécharge et met en cache 4 listes officielles dans `%LOCALAPPDATA%\PulseBrowser\privacy\lists\` — EasyList, EasyPrivacy, uBlock Origin filters, AdGuard Base. Mise à jour automatique si ancienneté > 7 jours, forcée depuis les paramètres. Métadonnées dans `meta.json` (timestamp par liste). Aucune donnée utilisateur envoyée : GET pur vers sources open source publiques.
- `NetworkBlockerModule.cs` : HashSet<string> domaines bloqués + HashSet<string> exceptions. Support sous-domaines par remontée des labels (cdn.ads.com → ads.com → com). Détection third-party par eTLD+1 (couvre co.uk, com.au, co.jp, etc.). Whitelist utilisateur (jamais bloqué). Événement `StatusChanged` pour affichage de l'état dans l'UI.
- Interception : `CoreWebView2.AddWebResourceRequestedFilter("*", All)` + `WebResourceRequested` enregistré dans `BrowserView_CoreWebView2Initialized`. Réponse 200 vide pour les requêtes bloquées (pas d'erreur visible dans la page).

**ParameterCleaner** (`Privacy/ParameterCleaner/`)

- 50+ paramètres de tracking supprimés des URLs avant navigation : `utm_*`, `gclid`, `gclsrc`, `gad_source`, `gbraid`, `wbraid`, `dclid`, `fbclid`, `msclkid`, `_hsenc`, `_hsmi`, `__hssc`, `__hstc`, `__hsfp`, `hsCtaTracking`, `mc_cid`, `mc_eid`, `mkt_tok`, `ttclid`, `epik`, `li_fat_id`, `ScCid`, `twclid`, `rdt_cid`, `yclid`, `ymclid`, `igshid`, `zanpid`, `elqTrackId`, `s_kwcid`, `vero_id`, `_kx`, `ck_subscriber_id`, `_itb`, `sib_uid`, `rb_clickid`, `wickedid`, `_openstat`, et paramètres HubSpot Ads (`hsa_*`), Pardot (`trk_*`), IBM Campaign (`cm_mmc*`).
- Appliqué dans `BrowserView_NavigationStarting` via `_privacy.CleanUrl()`. Idempotent : `CleanUrl` retourne `null` si rien à nettoyer → pas de boucle de redirection.

**HttpsEnforcer** (`Privacy/HttpsEnforcer/`)

- Upgrade `http://` → `https://` avant navigation, sauf réseau local (localhost, 127.x, 192.168.x, *.local, ::1).
- Appliqué dans `BrowserView_NavigationStarting` via `_privacy.CleanUrl()`.

**Intégration MainWindow**

- `InitPrivacyEngine()` : appelé juste après `ApplyUiSettings()` dans le constructeur. Instancie et enregistre les trois modules avec les états issus de `_uiSettings`.
- `ApplyPrivacySettings()` : synchronise les `IsEnabled` des modules avec `_uiSettings` sans recréer l'engine.
- `UpdatePrivacyUi()` : met à jour compteur de blocages, nombre de règles, date de dernière mise à jour.
- Nouvelles propriétés `UiSettings` : `NetworkBlockerEnabled`, `ParameterCleanerEnabled`, `HttpsEnforcerEnabled` (bool, défaut true), `PrivacyWhitelist` (List<string>).

**Section "Confidentialité" dans les Paramètres**

- RadioButton "Confidentialite" ajouté dans la nav des paramètres → `SettingsSectionPrivacy`.
- Bandeau vert : compteur de requêtes bloquées + nombre de règles actives.
- Toggles par module (NetworkBlocker, ParameterCleaner, HttpsEnforcer).
- Bouton "Mettre à jour les listes maintenant" + `InfoBar` de confirmation.
- `TextBlock` statut et date de dernière mise à jour des listes.
- Whitelist utilisateur : saisie domaine + bouton Ajouter, liste des domaines exclus avec bouton Retirer.

## 2026-07-06 — 0.14.0-dev

- Passage de la version projet a `0.14.0-dev`.

### CnameUncloaker — `Privacy/CnameUncloaker/`

Quatrième module privacy : détecte et bloque les trackers cachés derrière un alias CNAME (technique dite "CNAME cloaking"). Exemple : `analytics.monsite.com` est un alias CNAME de `collect.tracker-tiers.net`. Les bloqueurs classiques ne voient que `analytics.monsite.com` et laissent passer.

**`CnameResolver.cs`**

- P/Invoke `DnsQuery_W` (dnsapi.dll) + `DnsRecordListFree`. Pas de dépendance externe, aucune donnée envoyée à un serveur Pulse ou à un tiers : la résolution utilise le DNS configuré sur le PC de l'utilisateur.
- Layout struct x64 calculé manuellement : `pNext` (+0), `pName` (+8), `wType` (+16), `wDataLength` (+18), `Flags` (+20), `dwTtl` (+24), `dwReserved` (+28), `pNameHost` (+32 pour CNAME). Lecture via `Marshal.ReadIntPtr`.
- Parcours de la liste chaînée DNS_RECORD pour trouver le premier enregistrement CNAME.
- Cache par hostname, TTL 1h (`ConcurrentDictionary`).
- Throttle : max 8 résolutions DNS concurrentes (`SemaphoreSlim`).
- Limite : max 10 sauts CNAME (détection de boucle par HashSet visité).

**`CnameUncloakerModule.cs`**

- `ConcurrentDictionary<string, bool>` pour les hosts confirmés cloakés et pour les résolutions en cours (thread-safe : `WebResourceRequested` peut être appelé depuis n'importe quel thread).
- Stratégie : la première requête vers un host cloaké passe toujours (résolution async). Toutes les requêtes suivantes sont bloquées en O(1).
- Ne lance une résolution que pour les hosts third-party ayant au moins un sous-domaine (réduit les faux positifs et le volume DNS).
- Dépend de `NetworkBlockerModule.IsBlocked(host)` (méthode ajoutée) pour vérifier si la cible CNAME est dans la blocklist.
- Compteur `DetectedCount` affiché dans l'UI : "X règles · Y CNAME cloakés détectés".

**Intégration**

- `NetworkBlockerModule.IsBlocked(host)` ajouté (public) : `!whitelist && !allowed && blocked`.
- `UiSettings.CnameUncloakerEnabled` (bool, défaut true).
- `InitPrivacyEngine()` : `CnameUncloakerModule` enregistré après les trois premiers modules.
- Toggle "Détecter le CNAME cloaking" dans la section Confidentialité (entre HttpsEnforcer et la whitelist).
- `PrivacyRuleCountText` mis à jour : "X règles · Y CNAME cloakés détectés".

## 2026-07-06 — 0.15.0-dev

- Passage de la version projet à `0.15.0-dev`.

### CosmeticFilter — `Privacy/CosmeticFilter/`

Cinquième module privacy : masquage visuel des emplacements publicitaires par injection CSS dans chaque page WebView2. Complémentaire au NetworkBlocker — élimine les espaces vides laissés par les requêtes réseau bloquées.

**`CosmeticSeedSelectors.cs`**

- ~120 sélecteurs CSS intégrés en dur, actifs dès le premier lancement sans téléchargement.
- Couvre : Google Ads (`ins.adsbygoogle`, `[data-ad-slot]`, `[id^='div-gpt-ad']`), Taboola (`[id^='taboola-']`, `.trc_related_container`), Outbrain (`.OUTBRAIN`), Criteo (`.criteo-ad`), Yandex (`[id^='yandex_rtb']`), Meta/Facebook (`[id^='fb-ad-']`), classes génériques (`.advertisement`, `.sponsored`, `.ad-banner`, `.ad-container`, formats `.ad-300x250`…), attributs `[aria-label='Advertisement']`, `[data-testid='ad']`, etc.

**`CosmeticFilterParser.cs`**

- Parseur du format Adblock Plus / uBlock Origin pour les règles cosmétiques (`##` et `#@#`).
- Extrait : `domain##selector` (règle de domaine), `##selector` (règle générique).
- Ignore les filtres procéduraux uBlock non-CSS : `:has-text(`, `:upward(`, `:matches-css`, `:-abp-`, `:remove`, `:contains(`, `+js(`, `:xpath(`, `:min-text-length` — réservés au ScriptletInjector (v0.16+).
- Gère les règles multi-domaines (virgule), ignore les exclusions `~domaine`.
- Retourne `CosmeticRule(Domain?, Selector, IsException)`.

**`CosmeticFilterModule.cs`**

- `HashSet<string> _genericSelectors` : sélecteurs pour toutes pages.
- `Dictionary<string, HashSet<string>> _siteSelectors` : sélecteurs par domaine.
- `LoadAsync()` : lit les mêmes fichiers que `FilterListManager` depuis `%LOCALAPPDATA%\PulseBrowser\privacy\lists\` (easylist.txt, easyprivacy.txt, ublock-filters.txt, adguard-base.txt). Aucun téléchargement — le NetworkBlocker s'en charge.
- `BuildGenericInjectionScript()` : JS mis en cache, crée `<style id="__pulse_cf_generic">` dans `document.documentElement`. Enregistré via `AddScriptToExecuteOnDocumentCreatedAsync` (actif sur toutes les pages dès la création du document).
- `BuildSiteInjectionScript(pageUri)` : injecte via `ExecuteScriptAsync` après `NavigationCompleted`. Retourne null si aucune règle ne correspond au host.
- `BuildRemovalScript()` : statique, retire `__pulse_cf_generic` et `__pulse_cf_site` (utilisé quand le module est désactivé à chaud).
- CSS batché en groupes de 2 000 sélecteurs max (limite Chromium).
- CSS échappé pour embedding dans string JS (backslash, apostrophes, newlines).
- `GenericRuleCount` + `SiteRuleCount` : propriétés de diagnostic.

**Intégration `MainWindow.xaml.cs`**

- `_cosmeticFilter` (champ `CosmeticFilterModule`) + `_cosmeticScriptId` (string? pour retrait/ré-enregistrement du script global).
- `UiSettings.CosmeticFilterEnabled` (bool, défaut true).
- `InitCosmeticAsync()` : await `_networkBlocker.LoadAsync()` + await `_cosmeticFilter.LoadAsync()` → `RegisterCosmeticScriptAsync()` → `DispatcherQueue.TryEnqueue(UpdatePrivacyUi)`.
- `RegisterCosmeticScriptAsync()` : retire l'ancien `_cosmeticScriptId` si présent, enregistre le nouveau script via `AddScriptToExecuteOnDocumentCreatedAsync`, ou exécute `BuildRemovalScript()` si désactivé.
- `InjectSiteCosmeticAsync(pageUri)` : appelée depuis `NavigationCompleted`, exécute `BuildSiteInjectionScript()` via `ExecuteScriptAsync`.
- `CosmeticFilterSwitch_Toggled()` : persiste `IsEnabled`, appelle `RegisterCosmeticScriptAsync()`.
- `CoreWebView2Initialized` : si `_cosmeticFilter` actif et `_cosmeticScriptId` null (chargement en cours), relance `RegisterCosmeticScriptAsync()`.
- `ApplyUiSettings()` : `CosmeticFilterSwitch.IsOn = _uiSettings.CosmeticFilterEnabled`.

**Intégration `MainWindow.xaml`**

- Nouveau bloc "Masquage visuel des emplacements pub" dans `SettingsSectionPrivacy`.
- `CosmeticFilterSwitch` (ToggleSwitch) inséré entre le séparateur post-CnameUncloaker et la section whitelist.
- Texte descriptif : seed intégrée, listes téléchargées, complémentaire au bloqueur réseau.

**Points d'attention pour la suite**

- Les règles cosmétiques d'exception (`#@#`) sont parsées mais ignorées en v0.15 — non implémentées intentionnellement (nécessite de savoir quelle règle générique annuler).
- Le ScriptletInjector (v0.16 prévu initialement) a été remplacé en priorité par le bouton bouclier (v0.16.0-dev).
- Thread safety : `BuildGenericInjectionScript()` peut être appelé depuis `RegisterCosmeticScriptAsync` (thread pool) ET depuis le DispatcherQueue — le cache `_cachedGenericScript` est invalidé uniquement dans `LoadAsync`, qui s'exécute avant l'enregistrement du script.

## 2026-07-06 — 0.16.0-dev

- Passage de la version projet à `0.16.0-dev`.

### Bouton bouclier — contrôle confidentialité par-site

Bouton permanent dans la barre d'adresse (colonne 6, entre "Ouvrir" et "Favoris"). Remplace la nécessité d'aller dans Paramètres → Confidentialité pour débloquer un site. Design inspiré du bouton uBlock Origin : clic → popup compact avec toutes les infos sur la page en cours.

**`PrivacyEngine.cs`**

- Ajout de `_pageBlockedCount` (int) + `PageBlockedCount` : compteur de requêtes bloquées pour la page en cours (reset à chaque nouvelle navigation).
- `ResetPageBlockedCount()` : appelé dans `BrowserView_NavigationStarting`.
- `ShouldBlock()` incrémente désormais les deux compteurs : `_blockedCount` (total session) et `_pageBlockedCount` (page en cours).

**`MainWindow.xaml`**

- Ajout d'une 8ème colonne (`Width="Auto"`) dans la grille de la barre d'adresse.
- `ShieldButton` (colonne 6) : bouton 38px avec `FontIcon` glyph `EA18` (Segoe MDL2 Assets shield/VPN).
- Flyout `ShieldFlyout` (`Opening="ShieldFlyout_Opening"`, `Placement=BottomEdgeAlignedRight`) contenant :
  - `ShieldDomainText` : domaine de la page en cours (ou "Aucune page active").
  - `ShieldBlockedCountText` : "X requête(s) bloquée(s) sur cette page" / "Aucune requete bloquee".
  - Séparateur visuel.
  - `ShieldSiteExcludeToggle` (ToggleSwitch "Exclure ce site du bloqueur") : alimenté par la whitelist existante.
  - `HyperlinkButton` "Paramètres de confidentialité" : ouvre directement Paramètres → Confidentialité.
- Bouton Favoris décalé en colonne 7.

**`MainWindow.xaml.cs`**

- `_currentPageDomain` (string?) : domaine extrait à chaque `NavigationStarting`.
- `_suppressShieldToggle` (bool) : évite que le setter de `ShieldSiteExcludeToggle.IsOn` dans `ShieldFlyout_Opening` déclenche `Toggled`.
- `ExtractDomain(string?)` : méthode statique `Uri.Host` avec try/catch.
- `ShieldFlyout_Opening` : peuple domaine, compteur, état du toggle (en supprimant l'event Toggled).
- `ShieldSiteExcludeToggle_Toggled` : ajoute/retire le domaine de `_uiSettings.PrivacyWhitelist`, appelle `_networkBlocker.SetUserWhitelist()`, `SaveUiSettings()`, `RenderPrivacyWhitelist()`.
- `ShieldSettingsLink_Click` : masque le flyout, appelle `ShowPanel(SettingsPanel)`, affiche `SettingsSectionPrivacy`, coche `SettingsNavPrivacy`, appelle `UpdatePrivacyUi()`.

**Comportement**

- La première requête d'une page bloquée par CnameUncloaker passe toujours (résolution async) — le compteur page ne la comptabilise pas si elle n'est pas encore identifiée. Comportement intentionnel, cohérent avec v0.14.
- L'exclusion de site via le bouclier est instantanée et persistée dans `ui-settings.pulse`. La page doit être rechargée manuellement pour voir l'effet (comportement attendu — pas de rechargement forcé).
- `_suppressShieldToggle` est distinct de `_suppressUiSettingsSave` : les deux peuvent coexister sans interférence.

## 2026-07-06 — 0.17.0-dev

- Passage de la version projet à `0.17.0-dev`.

### ConsentManager — `Privacy/ConsentManager/`

Sixième module privacy : gestion automatique des bandeaux cookies RGPD/CCPA. Refuse automatiquement tout consentement non nécessaire, sans intervention de l'utilisateur.

**Deux couches complémentaires :**

**Couche 1 — Stubs IAB TCF v1/v2 (injectés avant le JS du site)**
- `window.__cmp` (TCF v1) : répond "aucun consentement" à tous les CMPs qui interrogent ce point d'entrée avant d'afficher leur bandeau.
- `window.__tcfapi` (TCF v2) : répond avec un objet de consentement vide (`purpose.consents: {}`, `vendor.consents: {}`) pour les commandes `ping`, `getTCData`, `addEventListener`. Couvre Cookiebot, OneTrust, Didomi, Axeptio, Quantcast, Usercentrics, Consentmanager, TrustArc, Iubenda, et tout CMP implémentant IAB TCF. Estimation : ~60-70% des sites.
- Injectés uniquement si le CMP n'a pas encore défini ces APIs (test `if (!window.__cmp/tcfapi)`) pour ne pas casser des implémentations légitimes.

**Couche 2 — Clic automatique "Refuser tout" (MutationObserver)**
- 24 sélecteurs CSS couvrant les CMPs les plus répandus : `#CybotCookiebotDialogBodyButtonDecline`, `#onetrust-reject-all-handler`, `#didomi-notice-disagree-button`, `.axeptio_btn_dismiss`, `.tarteaucitronDeny`, `#BorlabsCookieBtn--decline`, `button[data-cookiefirst-action="reject"]`, `.iubenda-cs-reject-btn`, `.orejime-Button--decline`, etc.
- Recherche par texte en fallback (15 textes : "tout refuser", "refuser", "reject all", "decline all", "continuer sans accepter"…) dans des conteneurs suspects (`[class*="cookie"]`, `[class*="consent"]`, `[role="dialog"]`…).
- `MutationObserver` sur `document.documentElement` avec `subtree: true` — détecte les CMPs qui s'affichent après le DOM initial. Désactivé après 12s pour éviter la consommation CPU inutile.
- Script de rattrapage (`RetryScript`) injecté via `ExecuteScriptAsync` après `NavigationCompleted` — pour les CMPs qui finalisent leur rendu après le chargement complet.

**`ConsentManagerScripts.cs`** (static)
- Listes partagées entre `InjectionScript` et `RetryScript` via des constantes C# interpolées dans des raw string literals (`$$"""..."""`).
- `InjectionScript` : script complet (stubs IAB + observer + tentative immédiate).
- `RetryScript` : version allégée (sélecteurs seulement, sans stubs IAB ni observer) pour l'injection post-navigation.

**`ConsentManagerModule.cs`**
- `Id = "consent-manager"`, `DisplayName = "Refus automatique des cookies"`, `IsEnabled` (défaut true).
- `BuildInjectionScript()` → `ConsentManagerScripts.InjectionScript`.
- `RetryScript` (static) → `ConsentManagerScripts.RetryScript`.

**Intégration `MainWindow.xaml.cs`**
- `_consentModule` (champ `ConsentManagerModule?`) + `_consentScriptId` (string?).
- `UiSettings.ConsentManagerEnabled` (bool, défaut true).
- `InitCosmeticAsync()` : appelle `await RegisterConsentScriptAsync()` après `RegisterCosmeticScriptAsync()`.
- `RegisterConsentScriptAsync()` : retire l'ancien script, enregistre le nouveau via `AddScriptToExecuteOnDocumentCreatedAsync`.
- `InjectConsentRetryAsync()` : `ExecuteScriptAsync(ConsentManagerModule.RetryScript)` après NavigationCompleted.
- `BrowserView_NavigationCompleted` : appelle `_ = InjectConsentRetryAsync()` dans le bloc `IsSuccess`.
- `CoreWebView2Initialized` : si `_consentModule` actif et `_consentScriptId` null → `RegisterConsentScriptAsync()`.
- `ConsentManagerSwitch_Toggled` : persiste `IsEnabled`, `RegisterConsentScriptAsync()`.
- `ApplyUiSettings()` : `ConsentManagerSwitch.IsOn = _uiSettings.ConsentManagerEnabled`.

**Intégration `MainWindow.xaml`**
- Toggle `ConsentManagerSwitch` dans `SettingsSectionPrivacy`, entre CosmeticFilterSwitch et la section whitelist.
- Description complète : IAB TCF, liste des CMPs supportés, "aucune donnée envoyée, tout local".

**Points d'attention**
- Les stubs IAB TCF ne s'activent que si le CMP n'a pas encore défini ses APIs. Si un CMP charge ses scripts en premier (rare), les stubs ne s'appliquent pas — la couche 2 (clic) prend le relais.
- Tarteaucitron (populaire en France) expose `tarteaucitron.userInterface.respondAll("deny")` en JS. Le clic sur `#tarteaucitronDeny` est plus fiable — implémenté via sélecteur.
- Certains CMPs sans bouton "Refuser tout" (légalement autorisé hors UE ou pour sites non-RGPD) ne peuvent être traités. Comportement gracieux : aucun effet, le bandeau reste visible.
- Pas de rechargement forcé après désactivation du module — l'utilisateur doit recharger la page pour voir le changement.

## 2026-07-06 — 0.18.0-dev

- Refactoring MainWindow : `MainWindow.xaml.cs` (5153 lignes) éclaté en 8 fichiers partial class + `PulseModels.cs`
  - `MainWindow.Privacy.cs` — moteur confidentialité, bouclier, whitelist
  - `MainWindow.Profile.cs` — session, profil, PIN, migration
  - `MainWindow.Vault.cs` — capture identifiants, autofill, coffre
  - `MainWindow.History.cs` — historique, téléchargements
  - `MainWindow.Bookmarks.cs` — favoris, barre, import/export
  - `MainWindow.Navigation.cs` — onglets, navigation, favicons
  - `MainWindow.Settings.cs` — stockage, démarrage, UI, onglets verticaux
  - `PulseModels.cs` — toutes les classes de données (hors MainWindow)

## 2026-07-06 — 0.19.0-dev

### Wizard de premier lancement

Assistant de configuration affiché une seule fois, après la création d'un nouveau compte. Déclenché depuis `DismissLoginOverlay()` si `!_isGuestMode && !_uiSettings.SetupWizardCompleted`.

**3 étapes :**
1. Bienvenue — présentation des protections intégrées (bloqueur, refus cookies, stockage local)
2. Moteur de recherche — choix entre Google, DuckDuckGo, Brave Search, Bing (RadioButtons)
3. Résumé — confirmation des réglages, bouton "Terminer"

**Fichiers créés/modifiés :**
- `MainWindow.SetupWizard.cs` (nouveau) — partial class : `ShowSetupWizard()`, `UpdateWizardStep()`, `WizardPrevButton_Click`, `WizardNextButton_Click`, `WizardSearch_Checked`, `FinishWizard()`
- `MainWindow.xaml` — overlay `SetupWizardOverlay` ajouté après `LoginOverlay` (même pattern `Grid.RowSpan="6"`, `Visibility="Collapsed"`)
- `PulseModels.cs` — `UiSettings.SetupWizardCompleted` (bool, défaut false)
- `MainWindow.Profile.cs` — `DismissLoginOverlay()` : appel conditionnel `ShowSetupWizard()` si wizard non complété et non-invité

**Design :** card WinUI centrée 340px, même style que LoginOverlay, navigation Précédent/Suivant/Terminer. Le wizard ne bloque pas le navigateur (WebView2 déjà initialisé par `App.OnLaunched`) — il ajoute seulement un overlay sur le dessus. Le choix du moteur de recherche est sauvegardé à la fermeture via `_uiSettings.Save()`.

## 2026-07-06 — 0.19.1-dev

### Corrections curseur sablier et favicons

**Curseur sablier permanent** (cause racine) : `CoreWebView2_WebResourceRequested` appelait `DispatcherQueue.TryEnqueue(UpdatePrivacyUi)` à chaque requête bloquée. Sur une page avec 150+ trackers bloqués, 150 callbacks s'accumulaient dans la file du thread UI, ce que Windows interprète comme "application occupée" et affiche le curseur flèche+sablier (`IDC_APPSTARTING`) en continu. Suppression du `TryEnqueue` dans le handler réseau. `UpdatePrivacyUi()` est désormais appelé depuis `BrowserView_NavigationCompleted` (une fois par page).

**Favicons absentes sur la barre de favoris** : `GetFaviconAsync` de WebView2 échoue souvent depuis `NavigationCompleted` (trop tôt, Chromium n'a pas encore chargé le favicon). `core.FaviconUri` est la propriété WebView2 remplie exactement quand `FaviconChanged` tire — c'est la source la plus fiable. Ajoutée comme priorité 1 dans `DownloadFaviconFallbackAsync`, avant la détection JS et le fallback `/favicon.ico`.

**Constante Version** corrigée : était bloquée à `"0.12.0-dev"` dans `MainWindow.xaml.cs`, mis à jour à `"0.19.1-dev"`.

## 2026-07-06 — 0.19.2-dev

### Capture d'identifiants — prise en charge Google et SPAs modernes

Le script JS précédent écoutait uniquement l'événement natif `submit`, que Google, Microsoft et la plupart des sites React/Vue ne déclenchent jamais. Réécriture de `InjectCredentialMonitorAsync` dans `MainWindow.Vault.cs` avec trois niveaux de détection :

1. **`input` event sur `input[type="password"]`** — capture le mot de passe dès la frappe, mémorisé dans `_pw`/`_user`/`_org`. Couvre Google Accounts, Microsoft Live, tout SPA.
2. **`submit` event traditionnel** — conservé, complète les champs manquants depuis les valeurs mémorisées.
3. **`click` event sur bouton** — si `_pw` est défini et qu'un bouton (button/[role=button]/[type=submit]) est cliqué, `trySend()` est appelé après 200 ms. Les boutons avec texte "annuler/retour/cancel/dismiss/fermer/back" sont ignorés pour éviter les faux positifs.

## 2026-07-06 — 0.20.0-dev

### Passkeys (clés d'accès FIDO2 / WebAuthn)

Nouvelle fonctionnalité : suivi local des sites utilisant des clés d'accès (passkeys). WebView2/Chromium gère nativement le protocole WebAuthn via Windows Hello — Pulse Browser n'implémente pas son propre authenticateur FIDO2. Il enregistre uniquement les métadonnées (site + dates) dans un fichier `.pulse` chiffré.

**Modèle de données (`PulseModels.cs`)**
- `PasskeyEntry(Origin, CreatedAt, LastUsedAt)` : record sérialisable en TSV URL-encodé.
- `PulseProfilePaths.PasskeysFile` : `navigation/passkeys.pulse` chiffré DPAPI.

**Détection JS (`MainWindow.Navigation.cs` — `RegisterPasskeyMonitorAsync`)**
- Injecté via `AddScriptToExecuteOnDocumentCreatedAsync` (avant le JS du site).
- Wrapping de `navigator.credentials.create` (création de passkey) et `navigator.credentials.get` (utilisation de passkey).
- En cas de succès de l'opération, postMessage `{t:'passkey_created', o:location.origin}` ou `{t:'passkey_used', o:location.origin}` vers le C#.

**Traitement C# (`MainWindow.Vault.cs`)**
- `BrowserCore_WebMessageReceived` : route les types `passkey_created` et `passkey_used` vers `RecordPasskeyCreated()` / `RecordPasskeyUsed()`.
- `RecordPasskeyCreated(origin)` : crée ou met à jour l'entrée dans `_passkeys`, persiste.
- `RecordPasskeyUsed(origin)` : met à jour `LastUsedAt`, persiste.
- `LoadPasskeys()` : lecture depuis `passkeys.pulse` via `PulseFile.TryReadAllText`. Appelé dans le constructeur de `MainWindow`.
- `SavePasskeys()` : écriture via `PulseFile.WriteAllText`.

**Panneau de gestion (`MainWindow.xaml` + `MainWindow.Vault.cs`)**
- Accessible via `Outils > Clés d'accès (Passkeys)`.
- `PasskeysPanel` : description du fonctionnement (Windows Hello), bouton vers `ms-settings:privacy-passkeys`, liste des sites enregistrés triés par dernière utilisation.
- Chaque carte : origine, date de création, dernière utilisation, bouton supprimer.
- En mode invité : fonctionnalité désactivée.

**Build : 0 erreur, 4 warnings pré-existants (CS8625 dans `MainWindow.Profile.cs:302`).**

## 2026-07-06 — Corrections gestionnaire de mots de passe (post 0.20.0-dev)

Série de corrections sur le gestionnaire de mots de passe suite aux tests utilisateur. La barre de sauvegarde n'apparaissait jamais et une popup Windows Hello s'affichait systématiquement à la connexion.

**Bug 1 — Popup Windows Hello**
- Cause : `IsPasswordAutosaveEnabled` de WebView2 est `true` par défaut, ce qui déclenchait le gestionnaire Windows.
- Fix : `sender.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false` dans `BrowserView_CoreWebView2Initialized` (`MainWindow.Navigation.cs`).

**Bug 2 — Crash STATUS_BREAKPOINT**
- Cause : `navigator.credentials.get({publicKey:...})` et `.create({publicKey:...})` interceptés avec `Promise.reject(new DOMException(...))`. Chromium crash sur ce type de rejet dans un contexte WebAuthn natif.
- Fix : remplacement par `Promise.resolve(null)` — le site voit "aucune passkey disponible", tombe sur son formulaire mot de passe classique, aucune popup Windows Hello.

**Bug 3 — Barre de sauvegarde effacée à chaque redirection**
- Cause : `BrowserView_NavigationCompleted` effaçait `_pendingCredential` et masquait `CredentialSaveBar` à chaque navigation. Après login, la redirection post-connexion effaçait le credential avant que l'utilisateur le voie.
- Fix : conserver la barre si `OriginOf(address)` correspond à `OriginOf(_pendingCredential.Value.Origin)`.

**Bug 4 — Comparaison www vs sans-www toujours fausse**
- Cause : `OriginOf(address)` retourne `https://amazon.fr` (www strippé) mais `_pendingCredential.Value.Origin` vient de `location.origin` JS qui retourne `https://www.amazon.fr`. La comparaison échouait toujours, effaçant systématiquement le credential.
- Fix : `OriginOf()` appliqué des deux côtés dans la comparaison de `NavigationCompleted`.

**Bug 5 — Email non capturé sur les flux SPA multi-étapes**
- Cause : Amazon, Google, Microsoft affichent email puis mot de passe sur la même page (SPA, pas de rechargement). L'événement `input` n'écoutait que `type="password"`. L'email affiché en texte brut sur l'étape mot de passe ne se trouvait dans aucun `<input>`.
- Fix : l'événement `input` écoute aussi `type="email"` et les champs dont `name`/`id` contient `email`, `user`, `login`, `mail`. Le clic sur le bouton "Continuer" capture aussi l'email depuis `findUser(document)` si `_pw` n'est pas encore défini.

**Bug 6 — setTimeout annulé par navigation (cause racine)**
- Cause : `setTimeout(trySend, 200)` dans le click handler est enregistré dans l'ancien contexte de page JavaScript. Quand le site redirige après login (souvent en moins de 200 ms), le contexte de l'ancienne page est détruit et le setTimeout est annulé — `trySend()` n'est jamais appelé.
- Fix : ajout de `storePendingCred()` qui écrit le credential dans `sessionStorage.__pulse_pending_cred` avant la navigation. Au début de chaque injection de script (avant le garde `window.__pulse_monitor`), le script lit ce `sessionStorage` et, s'il trouve un credential en attente, l'envoie via `postMessage` et nettoie. La déduplication C# dans `BrowserCore_WebMessageReceived` ignore un doublon si `_pendingCredential` a déjà le même username et la même origine normalisée.

**Fichiers modifiés :** `MainWindow.Navigation.cs`, `MainWindow.Vault.cs`.
**Build : 0 erreur, 4 warnings pré-existants.**

## 2026-07-06 — 0.20.1-dev — VaultStore : suppression de la dépendance Rust IPC

Refactoring architectural majeur du coffre de mots de passe. L'ancien code passait par `PulseCoreClient` (IPC named pipe → `pulse-browser.exe --serve`) pour toutes les opérations de coffre. Depuis cette version :

**Nouveau fichier : `VaultStore.cs`**

Coffre C# pur — zéro IPC, zéro Rust, zéro processus externe.
- Fichier `vault.pulse` stocké directement dans le dossier profil (suit le chemin custom défini par l'utilisateur).
- **Sans mot de passe maître** : chiffrement DPAPI via `ProtectedData.Protect` (entropie `PulseBrowser.Vault.v1`).
- **Avec mot de passe maître** : PBKDF2-SHA256 (100 000 itérations) + AES-256-CBC. Le sel est stocké dans l'entête du fichier ; la clé n'est jamais persistée. Si l'utilisateur formate Windows et reinstalle, il pointe vers le même dossier profil et entre son mot de passe maître — toutes ses données sont récupérées.
- Format du fichier : JSON plaintext `{ version, mode, salt?, data }` où `data` est le payload chiffré en base64.
- Méthodes : `ListCredentials()`, `Upsert()`, `Delete()`, `Unlock()`, `Lock()`, `SetMasterPassword()`, `VerifyMasterPassword()`, `ClearMasterPassword()`.
- Propriétés : `HasMasterPassword`, `IsLocked`.
- Zeroisation mémoire de la clé AES via `CryptographicOperations.ZeroMemory`.

**Fichiers modifiés :**
- `PulseModels.cs` : ajout de `VaultFile` dans `PulseProfilePaths` (`vault.pulse` à la racine du profil).
- `MainWindow.xaml.cs` : `private PulseCoreClient? _core` → `private readonly VaultStore _vault` ; initialisation `new VaultStore(_profile.VaultFile)` dans le constructeur.
- `MainWindow.Vault.cs` : toutes les méthodes `_core.*Async()` remplacées par des appels synchrones à `_vault.*()`. `RefreshVaultPanelAsync` → `RefreshVaultPanel` (sync). `OfferAutoFillAsync` → `OfferAutoFill` (sync). `CredentialSaveAccept_Click` n'est plus `async`. `VaultMenu_Click`, `MasterPasswordSwitch_Toggled`, `ChangeMasterPasswordButton_Click`, `UnlockVaultIfNeededAsync` : réécrits sans IPC.
- `MainWindow.Navigation.cs` : `_ = OfferAutoFillAsync(address)` → `OfferAutoFill(address)`.

**Build : 0 erreur, 0 avertissement nouveau.**

**Philosophie :** le coffre survit à un formatage Windows. L'utilisateur qui choisit de stocker son profil sur `E:\Documents\PulseBrowser\` peut réinstaller Windows, remonter Pulse Browser, pointer vers `E:\Documents\PulseBrowser\`, entrer son mot de passe maître — et retrouver tous ses identifiants intacts. Contrairement à Chrome qui cache le profil dans un chemin AppData obscur lié à un compte Windows spécifique.

## 2026-07-07 — 0.21.0-dev

### Capture d'identifiants — migration vers interception réseau POST (C# pur)

Le gestionnaire de mots de passe ne fonctionnait plus de manière fiable : les scripts JS injectés (`InjectCredentialMonitorAsync`) ne déclenchaient pas sur les SPAs React/Vue/Angular qui ne produisent pas d'événements DOM standards.

**Approche retenue : interception POST réseau dans `CoreWebView2_WebResourceRequested`**

- `CoreWebView2_WebResourceRequested` converti en `async void` avec pattern deferral (`args.GetDeferral()` avant le premier `await`).
- Filtre : méthode POST, HTTPS, body non vide, mode non-invité.
- `TryCapturePostCredentialAsync` : lit le body via `DataReader(ras.GetInputStreamAt(0))` + `reader.LoadAsync()`, remet le curseur à 0 via `ras.Seek(0)` pour ne pas casser la requête HTTP en cours.
- Parseurs : `ParseFormBody` (`application/x-www-form-urlencoded`, `Uri.UnescapeDataString`) et `ParseJsonBody` (`application/json`, recherche récursive via `FindJsonString`).
- `ExtractPostOrigin` : extrait `scheme://host` de l'URI de la requête.
- Appelle `OfferCredentialSave(origin, username, password)` — méthode centralisée qui gère la déduplication et affiche `CredentialSaveBar`.
- Aucun JS injecté pour la capture — fonctionne indépendamment du framework du site (React, Vue, Angular, formulaire natif).

**Nettoyage :** suppression de `RegisterCredentialMonitorAsync` et de `InjectCredentialMonitorAsync` dans `MainWindow.Navigation.cs`.

**Fichiers modifiés :** `MainWindow.Privacy.cs`, `MainWindow.Vault.cs`, `MainWindow.Navigation.cs`.

---

### ProfileLocationPanel — nouvelle étape d'onboarding

Nouvelle page insérée entre `CreateProfilePanel` et `MigrationPanel` dans le wizard de création de profil.

**Flux modifié :**
1. `CreateProfileButton_Click` (async void) : valide le formulaire, crée `_pendingUserProfile` en mémoire (sans écriture disque), met à jour `ProfileLocationPathText`, appelle `ShowLoginPanel("location")`.
2. `ProfileLocationPanel` affiché : liste le contenu du profil (favoris, historique, mots de passe/coffre, paramètres, onglets), chemin actuel, bouton "Changer" (`ChooseProfileLocationButton_Click`), bouton "Continuer" (`ProfileLocationContinueButton_Click`).
3. `ProfileLocationContinueButton_Click` : applique le dossier custom dans `PulseConfig`, résout les chemins, sauvegarde `_userProfile` sur disque, appelle `ShowMigrationOrDismiss()`.

**Ancien sélecteur de dossier** (`ProfileDirText` + `ChooseProfileDirButton_Click`) retiré de `CreateProfilePanel`.

**Fichiers modifiés :** `MainWindow.xaml`, `MainWindow.Profile.cs`, `MainWindow.xaml.cs` (ajout du champ `_pendingUserProfile`).

---

### Purge des données WebView2 + bouton "Vider les données de navigation"

**Cause identifiée :** les cookies et sessions WebView2 sont stockés dans `%LOCALAPPDATA%\PulseBrowser\Default\` (chemin par défaut WebView2), totalement séparé des fichiers profil Pulse (`profiles/default/`). Supprimer le profil Pulse ne supprime pas les sessions WebView2 — l'utilisateur restait connecté sur Amazon après suppression/recréation du profil.

**Actions :**
- Suppression manuelle de `%LOCALAPPDATA%\PulseBrowser\Default\`, `cef-user-data\Default\`, `cef-root-cache\Default\` (navigateur fermé).
- Ajout du bouton "Vider les données de navigation" dans `Paramètres → Stockage` : appelle `core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite)` — méthode sur `CoreWebView2Profile` (pas sur `CoreWebView2`).

**Limite WinRT :** `CoreWebView2Environment.CreateAsync` dans le binding WinRT ne prend pas de dossier utilisateur en paramètre (différent du binding .NET) — isolation WebView2 par profil non réalisable via l'API. `EnsureCoreWebView2Async()` sans argument conservé.

**Fichiers modifiés :** `MainWindow.Settings.cs`, `MainWindow.xaml`, `PulseModels.cs` (ajout de `BrowserDataDir` dans `PulseProfilePaths`).

---

### Page À propos — stack technique corrigée

La section technique de `AboutPanel` reflète désormais la stack réelle :

| Composant | Technologie |
|---|---|
| Langage principal | C# + WinUI 3 |
| Moteur web | WebView2 (Chromium embarqué) |
| Modules privacy | Rust |
| Chiffrement données | AES-256-CBC / DPAPI (C# pur) |

Version mise à jour : `0.20.1-dev` → `0.21.0-dev` dans `MainWindow.xaml.cs` et `AGENTS.md`.

## 2026-07-07 — 0.22.0-dev

### Coffre souverain (chantier 1) : Argon2id + couplage au mot de passe du profil + export/import

**Constat de départ :** le gestionnaire de mots de passe « ne fonctionnait pas ». Diagnostic disque : aucun fichier `vault.pulse` nulle part (ni dans `%LOCALAPPDATA%\PulseBrowser\`, ni dans le profil custom `E:\Documents\Navtest\test`) → le coffre n'avait jamais rien enregistré. Ce qui « retenait » les identifiants = les cookies de session du profil Chromium WebView2 (`%LOCALAPPDATA%\PulseBrowser\` racine), pas un coffre. Décision produit (utilisateur) : coffre souverain, fichier appartenant à l'utilisateur, portable, aucune récupération, pas de stockage douteux.

**`VaultStore.cs` réécrit :**
- KDF **Argon2id** (package `Konscious.Security.Cryptography.Argon2` 1.3.1) : mémoire 64 Mio, 3 itérations, parallélisme 4. Remplace PBKDF2-100k.
- `VaultHeader` v2 : champs `kdf` (défaut lecture = `pbkdf2` pour compat ancien format), `argon2_mem/iter/par`, `mode` (`aes256` par défaut = portable, `dpapi` = confort/legacy).
- Nouveau `EnsureUnlockedWith(password)` : active le coffre s'il est neuf, sinon le déverrouille. Point d'entrée unique du couplage.
- `ExportClear()` / `ImportClear(items)` pour l'export/import.

**Couplage coffre ↔ mot de passe du profil (un seul mot de passe) :**
- `MainWindow.xaml.cs` : champ `_pendingProfilePassword`.
- `CreateProfileButton_Click` retient le mot de passe ; `ProfileLocationContinueButton_Click` clé le coffre si pas de redémarrage (dossier par défaut).
- `LoginButton_Click` : `EnsureUnlockedWith(pw)` après vérification.
- `ChangeProfilePasswordButton_Click` : re-clé le coffre (déverrouille avec l'ancien, `SetMasterPassword` avec le nouveau).
- Login par PIN : coffre reste verrouillé, `UnlockVaultIfNeededAsync` redemande le mot de passe du profil à l'ouverture.

**Toggle « mot de passe maître » séparé retiré** (devenu redondant) : suppression de `MasterPasswordSwitch_Toggled` et `ChangeMasterPasswordButton_Click` dans `MainWindow.Vault.cs`, des refs dans `MainWindow.Settings.cs`, et des contrôles XAML. Section « Coffre » des paramètres remplacée par un texte explicatif + avertissement « aucune récupération ». `MasterPasswordEnabled` (UiSettings) devient vestigial.

**Export/Import dans le panneau Coffre** (`MainWindow.Vault.cs` + XAML) : bouton Export (CSV en clair, dialogue d'avertissement, `FileSavePicker`), bouton Import (CSV Chrome/Firefox, parse par en-tête url/username/password, `FileOpenPicker`). Format export : `name,url,username,password`.

**Reste à faire (chantier 2, non fait) :** fiabiliser la capture/remplissage (script `submit` JS réémis vers `{t:"cred"}`, bug de redirection cross-origine à [MainWindow.Navigation.cs:164], autofill multi-formulaires). Le tuyau `BrowserCore_WebMessageReceived` existe mais plus rien ne l'alimente.

**Build :** MSBuild VS18 x64 réussi, 0 erreur (2 warnings CS8625 préexistants à `MainWindow.Profile.cs:318`, hors périmètre). Version `0.21.0-dev` → `0.22.0-dev` dans `MainWindow.xaml.cs` et `AGENTS.md`.

## 2026-07-07 — 0.23.0-dev

### Chantier 2 (capture credentials) + fenêtres parasites + recherche localisée

Retour test utilisateur sur 0.22 : login Amazon → rien de proposé, coffre vide ; navigation onglets pas fluide ; pages/recherche Google en anglais s'ouvrant seules.

**Diagnostic (3 causes distinctes) :**
1. Coffre vide = la capture (chantier 2) n'existait plus (le tuyau `{t:"cred"}` de `BrowserCore_WebMessageReceived` n'était plus alimenté). L'interception réseau POST seule ne suffit pas (Amazon = POST page complète non fiable).
2. Onglets pas fluides = **un seul WebView2 partagé** ; `BrowserTabs_SelectionChanged` fait `NavigateBrowser()` à chaque changement → rechargement complet. **Architectural, NON corrigé** (chantier 3 : un WebView2 par onglet).
3. Fenêtres/recherche qui s'ouvrent seules = **aucun handler `NewWindowRequested`** (popups/target=_blank non contrôlés) + Google sans param de langue.

**Corrigé en 0.23.0-dev :**
- **`CoreWebView2_NewWindowRequested`** (`MainWindow.Navigation.cs`) : `args.Handled=true`, ouvre l'URL dans un onglet Pulse au lieu d'une fenêtre parasite.
- **`RegisterCredentialMonitorAsync()`** : script injecté via `AddScriptToExecuteOnDocumentCreatedAsync` — écoute `submit` + clic sur bouton dans un form avec `input[type=password]`, extrait login+mdp, envoie `{t:"cred"}`. Gère les logins 2 étapes via `sessionStorage.__pulse_last_user` (même origine). Indépendant du framework du site. Complète le sniff réseau POST existant.
- **Bug barre de sauvegarde** (`NavigationCompleted`) : ne se masque plus sur redirection cross-origine ; reste visible tant que `_pendingCredential` est défini (fermée seulement par Enregistrer/Ignorer).
- **`SearchUrl`** (`MainWindow.xaml.cs`) : ajout de la langue système (`hl`/`setlang`/`kl`) pour Google/Bing/DuckDuckGo → résultats dans la langue de l'OS.

**Build :** MSBuild VS18 x64, 0 erreur (mêmes 2 warnings préexistants). Version → `0.23.0-dev`.

**Reste (chantier 3, gros, non fait) :** vrais onglets = un WebView2 par onglet (fluidité, conservation d'état). Cause connue : `_browserView` unique partagé.

## 2026-07-07 — 0.23.1-dev

### Bug majeur : session Amazon survivait à la purge (WebView2 hors profil)

Test 0.23.0 : profil purgé + recréé (« bob »), mais sur Amazon → « Bonjour Jeremy » déjà connecté sans saisie → aucun login soumis → rien à capturer → coffre vide. **La capture n'était pas en cause.**

**Cause racine :** WebView2 (appli non packagée) stocke ses cookies/sessions dans un dossier **collé à l'exe** : `bin\x64\Debug\...\win-x64\PulseBrowser.WinUI.exe.WebView2\EBWebView\Default\Network\Cookies`. Confirmé en lisant `--user-data-dir` du process `msedgewebview2` enfant de `PulseBrowser.WinUI.exe`. Ce dossier est **hors du profil Pulse** → supprimer/changer de profil ne déconnecte pas des sites, et la purge du profil ne le touche pas.

**Fix :** dans le constructeur `MainWindow` (AVANT toute création WebView2), `Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", _profile.BrowserDataDir)` → WebView2 range désormais ses données dans `profil/webview2`. Conséquences : changer/supprimer un profil déconnecte réellement, la purge est complète, « Vider les données » cohérent. (Corrige la note antérieure fausse qui situait les cookies dans `%LOCALAPPDATA%\PulseBrowser\Default`.)

**Purge de test effectuée :** suppression de `E:\Documents\Navtest\test`, `%LOCALAPPDATA%\PulseBrowser`, et du `PulseBrowser.WinUI.exe.WebView2` obsolète. Process `PulseBrowser.WinUI` (PID verrouillant l'exe) fermé pour rebuild.

**Build :** MSBuild VS18 x64, 0 erreur. Version → `0.23.1-dev`. À retester : nouveau profil → Amazon doit redemander le login (2FA) → barre de sauvegarde → coffre.

## 2026-07-07 — 0.23.2-dev

### Décision : gestionnaire de mots de passe = moteur natif Chromium (fin de la capture maison)

Après le fix session (0.23.1), re-login Amazon → **toujours aucune proposition de sauvegarde**. La capture maison (POST réseau + script `submit` JS) ne détecte pas les logins complexes (Amazon 2 étapes, JS). Trop de cycles de test sans résultat.

**Décision produit :** activer le **gestionnaire de mots de passe natif Chromium** (même moteur que Chrome, détection fiable) au lieu de réécrire la détection à la main.
- `MainWindow.Navigation.cs` : `IsPasswordAutosaveEnabled = true` + `IsGeneralAutofillEnabled = true` (étaient `false`).
- Stockage : magasin Chromium chiffré DPAPI, **dans le profil Pulse** (grâce à `WEBVIEW2_USER_DATA_FOLDER` de 0.23.1) → local, dans le dossier de l'utilisateur.
- Capture maison **désactivée** pour éviter un double message : appel `RegisterCredentialMonitorAsync()` commenté + branche POST de `CoreWebView2_WebResourceRequested` neutralisée (`await Task.CompletedTask`). Méthodes `TryCapturePostCredentialAsync`/`ParseFormBody`/`ParseJsonBody`/`FindJsonString`/`ExtractPostOrigin`/`RegisterCredentialMonitorAsync` devenues dead-code (à nettoyer plus tard).
- **Coffre `vault.pulse` conservé** (Argon2id, ajout manuel, export/import CSV, autofill si entrée présente) mais n'est plus alimenté automatiquement par le web.

**Compromis assumé :** ce n'est plus le coffre souverain `vault.pulse` qui capte les mots de passe web, mais le magasin Chromium (local, chiffré, dans le profil). **Piste future** : pont Chromium `Login Data` (SQLite + DPAPI) → `vault.pulse` pour réconcilier fiabilité + souveraineté.

**Build :** MSBuild VS18 x64, 0 erreur. Version → `0.23.2-dev`. À retester : login Amazon → bulle Chromium « Enregistrer le mot de passe ? ».

## 2026-07-07 — 0.23.3-dev

### Pont Chromium → vault.pulse (le coffre reflète enfin les mots de passe web)

Test 0.23.2 : la bulle Chromium « Enregistrer le mot de passe ? » **fonctionne** (Amazon), mais le mot de passe va dans le magasin Chromium → panneau Coffre `vault.pulse` toujours vide.

**Solution — pont de lecture Chromium :** `ChromiumCredentialReader.cs` (nouveau) lit la base `Login Data` (SQLite) et déchiffre les mots de passe.
- Emplacement (confirmé disque) : `<profil>\webview2\EBWebView\Default\Login Data` + clé dans `<profil>\webview2\EBWebView\Local State`. WebView2 crée `EBWebView` DANS le dossier `WEBVIEW2_USER_DATA_FOLDER` (= `_profile.BrowserDataDir`).
- Déchiffrement : clé maître = `os_crypt.encrypted_key` (base64) du `Local State`, préfixe ASCII `DPAPI` retiré, puis `ProtectedData.Unprotect` (DPAPI CurrentUser). Mots de passe format `v10`/`v11` : `préfixe(3)+nonce(12)+ciphertext+tag(16)`, AES-256-GCM via `System.Security.Cryptography.AesGcm(key, 16)`. Fallback DPAPI direct pour l'ancien format.
- `Login Data` verrouillé par WebView2 → copie temp avant lecture SQLite (`Mode=ReadOnly`). Filtre les origines non-http (ignore android://…).
- Dépendance ajoutée : `Microsoft.Data.Sqlite` 8.0.10.

**Câblage (`MainWindow.Vault.cs`) :** `SyncFromBrowserStore()` appelle le reader et `_vault.ImportClear(...)`. Appelé automatiquement à l'ouverture du Coffre (`VaultMenu_Click`, après unlock) et via le bouton Actualiser (affiche le nombre synchronisé).

**Résultat :** Chromium capture (fiable) → à l'ouverture du Coffre, les identifiants sont importés dans `vault.pulse` (Argon2id) et affichés. Réconcilie fiabilité + souveraineté.

**Build :** MSBuild VS18 x64, 0 erreur. Version → `0.23.3-dev`. À retester : ouvrir Outils → Coffre → les identifiants Amazon enregistrés doivent apparaître.

**Confirmé (test utilisateur) :** l'identifiant Amazon apparaît bien dans le coffre. Le pont fonctionne.

### 0.23.4-dev : synchro auto à la connexion

Le pont est **universel** (lit tous les `logins` de Chromium, aucun code spécifique par site). Ajout : `SyncFromBrowserStore()` appelé dans `DismissLoginOverlay()` → le coffre se synchronise automatiquement à chaque connexion au profil, sans clic sur Actualiser. Version → `0.23.4-dev`. Build OK.

## 2026-07-07 — 0.24.0-dev

### Déverrouillage du coffre par code PIN (idée utilisateur)

Décision UX : pas de mot de passe maître séparé (déjà retiré en 0.22) — le coffre s'ouvre avec le **mot de passe du profil**, et si un **PIN** est activé, le PIN l'ouvre aussi. Modèle de sécurité assumé (validé utilisateur) : PIN 6 chiffres = faible, mais l'emballage PIN est protégé par DPAPI (compte Windows) → équivalent Windows Hello, et déjà au-dessus de Chrome/Edge (aucun gate).

**Crypto (`VaultStore.cs`) :** en-tête `pin_salt` + `pin_key`. `EnablePinUnlock(pin)` : dérive une clé PIN (Argon2id, sel dédié), emballe la clé AES du coffre en AES-256-GCM, puis DPAPI-protège le tout. `UnlockWithPin(pin)` : DPAPI-unprotect → AES-GCM decrypt → clé du coffre → `TryDecrypt` valide. `DisablePinUnlock()`. `HasPinUnlock`. Note : `SetMasterPassword` reconstruit l'en-tête → l'emballage PIN saute au changement de mot de passe (ré-emballé en redemandant le PIN dans `ChangeProfilePasswordButton_Click`).

**Câblage (`MainWindow.Profile.cs`) :** login PIN (`ProcessPinInputAsync`) → `_vault.UnlockWithPin(pin)` avant `DismissLoginOverlay`. Création profil avec PIN → `_pendingProfilePin` → `EnablePinUnlock` à la finalisation (hors restart custom-dir). Toggle PIN ON → `EnablePinUnlock` ; OFF → `DisablePinUnlock`. Changement de mot de passe → re-demande le PIN pour ré-emballer.

**Limite connue :** profil sur dossier custom (restart) + 1er login par PIN → coffre non encore clé (le keying se fait au 1er login mot de passe). Étroit.

**Build :** MSBuild VS18 x64, 0 erreur. Version → `0.24.0-dev`.

### 0.24.1-dev : barrière d'accès au coffre + confirmation suppression + suppression persistante

Retour test : (1) ouvrir le coffre ne demandait pas le PIN (le login déverrouillait tout), (2) supprimer un identifiant ne demandait aucune confirmation. Corrections :
- **`RequireVaultAccessAsync()`** (`MainWindow.Vault.cs`) : re-demande le PIN (si activé) sinon le mot de passe **à chaque ouverture du coffre**, même déjà connecté (le coffre = zone la plus sensible, barrière dédiée). Remplace `UnlockVaultIfNeededAsync` dans `VaultMenu_Click` (auth AVANT d'afficher le panneau). Décision : le déverrouillage au login sert à l'autofill silencieux ; l'accès au PANNEAU coffre est re-gated.
- **Confirmation de suppression** : `BuildVaultCard` → `ContentDialog` « Supprimer cet identifiant ? » avant `_vault.Delete`.
- **Suppression persistante (tombstone)** : `VaultStore` en-tête `deleted` (clés `origin|username`). `Delete` ajoute la clé, `ImportClear` la saute (sinon la resynchro Chromium réimportait l'entrée supprimée), `Upsert` la lève (ré-ajout manuel explicite).

**Build :** MSBuild VS18 x64, 0 erreur. Version → `0.24.1-dev`.

### 0.24.2-dev : nom personnalisé par entrée + affichage propre sans identifiant

- `VaultCredential.Label` (nouveau champ `label`, préservé par `Upsert`/`ImportClear`). `VaultStore.SetLabel(origin, username, label)`.
- `BuildVaultCard` : titre = nom perso sinon hôte épuré (`PrettyHost`, sans schéma ni www.), bouton **crayon** pour renommer (dialogue), rappel du site en sous-titre si nom perso, et libellé « (aucun identifiant enregistre) » en italique grisé quand `Username` est vide (cas Micromania — Chromium a capté sans identifiant).
- Version → `0.24.2-dev`. Build OK.

**Restant demandé (#3, à investiguer) :** les popups natives Chromium (proposition identifiant/mot de passe) se positionnent aléatoirement à l'écran. Devenu **sans objet** en 0.25.0 (Chromium ne gère plus les mots de passe → plus de popups natives).

## 2026-07-07 — 0.25.0-dev

### Bascule STOCKAGE 100% MAISON (décision utilisateur)

Problème constaté : le vrai coffre était celui de **Chromium** ; `vault.pulse` n'était qu'une copie. Supprimer dans l'app ne supprimait pas dans Chromium → Chromium continuait à remplir et ne reproposait pas d'enregistrer. Mots de passe stockés « ailleurs que dans l'app » → refusé par l'utilisateur.

Choix (via question) : **100% maison** — Chromium ne stocke NI ne remplit plus aucun mot de passe ; `vault.pulse` devient le seul coffre.

**Implémenté :**
- `IsPasswordAutosaveEnabled = false` + `IsGeneralAutofillEnabled = false` (`MainWindow.Navigation.cs`).
- Capture par l'app **réactivée** : `RegisterCredentialMonitorAsync()` (script JS submit/clic → `{t:"cred"}`) + branche POST de `CoreWebView2_WebResourceRequested` (filet). Alimentent `OfferCredentialSave` → `CredentialSaveBar` → `_vault.Upsert`.
- Remplissage par l'app : `OfferAutoFill` (déjà présent, lit `vault.pulse`, `AutoFillBar`).
- **Migration + purge** : `MigrateAndClearBrowserPasswordsAsync()` (`MainWindow.Vault.cs`) rapatrie ce que Chromium avait (`SyncFromBrowserStore`) PUIS `ClearBrowsingDataAsync(PasswordAutosave | GeneralAutofill)`. **Garde critique : ne vide QUE si `!_vault.IsLocked`** (sinon on viderait avant d'avoir rapatrié → perte). Appelé à la connexion (`DismissLoginOverlay`) et à l'init WebView2 (early-return si verrouillé). Effet de bord bénéfique : les entrées supprimées (tombstone) ne sont pas réimportées puis Chromium est vidé → la suppression devient enfin totale.

**Tradeoff assumé :** la capture maison peut rater sur des sites JS complexes (comme au début du projet). Mitigation : ajout manuel dans le coffre. C'est le prix du « rien hors de l'app ».

**Enum WebView2 :** le membre est `CoreWebView2BrowsingDataKinds.PasswordAutosave` (PAS `Passwords`).

**Build :** MSBuild VS18 x64, 0 erreur. Version → `0.25.0-dev`.

**Principe acté (utilisateur) :** « je sais ce que je fais, en vue de la philosophie du navigateur c'est la meilleure des solutions. » → Le stockage 100% maison est un **choix de fond durable**, pas un compromis temporaire. Ne pas réactiver le gestionnaire natif de Chromium par facilité. Toute amélioration future de la capture doit se faire côté app (JS/interception), jamais en déléguant à Chromium.

### Diagnostic : auto-connexion Micromania (pas une fuite de mot de passe)

Retour utilisateur : Micromania connecte automatiquement sans saisie, alors que le site n'apparaît pas dans le coffre → suspicion de mot de passe stocké hors de l'app.

**Vérifié sur disque (profil actif `C:\Users\Handi-Jyhel\Desktop\bob`) :**
- `webview2\EBWebView\Default\Login Data` (magasin de mots de passe Chromium) : **aucune trace micromania**. Aucun mot de passe stocké hors de `vault.pulse`. Le principe souverain est respecté.
- `webview2\EBWebView\Default\Network\Cookies` : cookies `micromania.fr` présents, dont `dwcustomer` (cookie « rester connecté » Salesforce Commerce Cloud) et cookies `auth.micromania.fr`.

**Mécanisme :** l'auto-connexion vient d'un **cookie de session persistant** (« remember me ») posé lors d'une connexion antérieure — aucun mot de passe n'est rejoué. Comportement identique dans Chrome/Firefox, et voulu par `AGENTS.md` (ne pas forcer les reconnexions). Conséquence directe : comme l'utilisateur ne retape jamais son mot de passe, la capture 0.25.0 n'a jamais rien à capter → Micromania absent du coffre.

**Piste produit proposée (en attente de Go) :** panneau « Sites connectés » — lister les domaines avec cookies de session via `CoreWebView2CookieManager`, action par site « Se déconnecter / Oublier ce site », pour rendre les sessions aussi visibles et contrôlables que les mots de passe. → **Fait en 0.26.0-dev** (voir ci-dessous), étendu avec la purge au démarrage (idée utilisateur).

## 2026-07-07 — 0.26.0-dev

### Sessions éphémères : purge des cookies au démarrage + panneau « Sites connectés » (Go utilisateur)

Décision produit (utilisateur) : « les sites ne se souviennent de toi que si tu l'as décidé » — à chaque lancement, les cookies/sessions de la visite précédente sont purgés, SAUF pour les sites de confiance choisis. Le coffre `vault.pulse` n'est jamais touché (hors du dossier WebView2) et l'autofill prend le relais. Positionnement marché : Firefox (purge à la fermeture + exceptions) et Brave (forget-me par site) font des morceaux ; l'assemblage purge-au-démarrage + coffre souverain + panneau unifié n'existe pas.

**Choix techniques clés :**
- **Purge au DÉMARRAGE, pas à la fermeture** : garantie même après crash/arrêt brutal du PC (« éteint l'ordinateur Grand Max »). Branchée dans `BrowserView_CoreWebView2Initialized` (devenu `async void`), **avant** la navigation de `_pendingBrowserAddress` → aucune page ne charge d'anciens cookies.
- **Optimisation** : zéro site de confiance → un seul appel natif `ClearBrowsingDataAsync(AllSite)` (cookies + stockage sites, pas d'énumération). Avec sites de confiance → `CookieManager.GetCookiesAsync("")` une fois, suppression sélective par domaine racine. Énumération des cookies uniquement au démarrage (si exceptions) ou à l'ouverture du panneau — rien en tâche de fond.
- **Domaine racine** : `RootDomainOf()` regroupe `auth.micromania.fr`/`www.micromania.fr` → `micromania.fr`, avec une liste courte de suffixes publics à deux niveaux (`co.uk`, `com.au`…).

**Nouveau fichier `MainWindow.Sessions.cs`** : `PurgeStartupSessionsAsync`, `DeleteUntrustedCookiesAsync(core, onlyRootDomain?)` (double usage : purge globale hors confiance / oubli ciblé d'un site), panneau (`SessionsMenu_Click`, `RefreshSessionsPanelAsync`, `BuildSessionCard`), `SetTrustedSessionSite`, `SessionPurgeSwitch_Toggled`.

**UI :**
- `Outils > Sites connectés` : cartes par domaine racine (nb cookies, ToggleSwitch « Session conservée / Purgée au démarrage », bouton Oublier), boutons Actualiser + « Tout oublier maintenant » (respecte la confiance). Les sites de confiance sans cookie restent listés pour pouvoir être décochés.
- `Paramètres > Confidentialité > Sessions éphémères` : ToggleSwitch « Purger les sessions au démarrage » (**activé par défaut** — identité produit) + lien vers le panneau.
- `UiSettings` : `SessionPurgeEnabled` (défaut `true`) + `TrustedSessionSites` (dans `ui-settings.pulse`, pas de fichier séparé).

**Limite documentée :** avec des sites de confiance configurés, la purge sélective ne couvre que les cookies (pas localStorage/IndexedDB — `ClearBrowsingDataAsync` ne fait pas de par-site). Sans exception, la purge `AllSite` est complète. Tradeoff assumé : bannières RGPD à chaque session sur les sites non-confiance.

**Corrections en passant :** `ShowPanel` ne masquait pas `PasskeysPanel` (bug latent de superposition) — ajouté avec `SessionsPanel` ; version « 0.20.1-dev » codée en dur dans la page À propos → `AboutVersionText` alimenté par la constante `Version`.

**Build :** MSBuild VS18 x64, 0 erreur (2 warnings CS8625 préexistants). Version → `0.26.0-dev` (`MainWindow.xaml.cs` + `AGENTS.md`).

**À tester (utilisateur) :** 1) lancer → Micromania doit être déconnecté ; 2) se reconnecter en tapant le mot de passe → barre « Enregistrer » → coffre ; 3) marquer un site de confiance dans Outils > Sites connectés → relancer → sa session survit.

**Confirmé (test utilisateur) :** capture OK — `auth.micromania.fr` apparaît dans le coffre après re-login.

## 2026-07-07 — 0.26.1-dev

### Bouton « Retour au site » dans la barre d'état

Retour test 0.26.0 : une fois dans un panneau interne (Coffre, Historique, Paramètres…), aucun moyen de revenir à la page web en cours. Choix utilisateur : bouton en barre d'état (plutôt que panneaux-en-onglets, qui attendra le chantier 3 — un WebView2 par onglet, façon `chrome://settings`).

- `MainWindow.xaml` : bouton accent `BackToPageButton` (icône ← + « Retour au site ») dans la ligne de statut, entre `StatusText` et `ProfileStatusText`.
- `ShowPanel` (`MainWindow.xaml.cs`) : le bouton est visible dès que le panneau affiché n'est pas `BrowserPanel`. Clic → `ShowPanel(BrowserPanel, titre de l'onglet courant)` — le WebView2 n'a jamais été déchargé, la page réapparaît telle quelle.

**Build :** 0 erreur, 0 avertissement (après fermeture de l'instance qui verrouillait l'exe). Version → `0.26.1-dev`. Relancé pour test utilisateur.

## 2026-07-07 — 0.26.2-dev

### Deux bugs révélés par le test 0.26.1 (Micromania déconnecté ✔, mais autofill absent + onglet inerte)

**Bug 1 — L'autofill ne pouvait JAMAIS s'afficher (dormant depuis 0.20.1).** Dans `BrowserView_NavigationCompleted`, la ligne `AutoFillBar.Visibility = Collapsed` (reset « à chaque nouvelle page ») était placée APRÈS l'appel `OfferAutoFill(address)` : la barre était affichée puis masquée dans la même passe. Cause historique : en 0.20.1, `OfferAutoFillAsync` (fire-and-forget, s'exécutait après le reset) est devenu synchrone → l'ordre s'est inversé silencieusement. Invisible jusqu'ici car il fallait un credential dans le coffre + une page de login déconnectée (rendu possible par la purge 0.26.0). **Fix :** reset déplacé AVANT `OfferAutoFill` — c'est elle qui décide d'afficher.

**Bug 2 — Cliquer sur un onglet déjà actif ne ramenait pas à la page.** `BrowserTabs_SelectionChanged` ne se déclenche que si la sélection change ; depuis un panneau interne (coffre…), cliquer l'onglet courant = aucun événement. **Fix :** `ReturnToBrowserIfHidden()` — handler `Tapped` sur le TabView + appel en fin de `VerticalTabButton_Click` : si `BrowserPanel` n'est pas visible, `ShowPanel(BrowserPanel, titre onglet courant)`. Le bouton « Retour au site » (0.26.1) reste en secours.

**Build :** 0 erreur (2 warnings CS8625 préexistants). Version → `0.26.2-dev`. Relancé pour test utilisateur.

**À tester :** aller sur la page de login Micromania → la barre « Remplir avec le compte handijyhel@gmail.com ? » doit apparaître ; ouvrir le coffre puis cliquer l'onglet Micromania → retour direct à la page.

## 2026-07-07 — 0.27.0-dev

### Le coffre adopte les techniques des grands gestionnaires (idée utilisateur)

Constat utilisateur : les gestionnaires (Bitwarden, Chrome, KeePassXC) stockent l'URL de la page de connexion avec chaque identifiant et rapprochent par domaine racine — pas nous. Décision : leur emprunter les techniques (pas le code — GPL/AGPL contaminantes, et leurs heuristiques vivent dans des extensions navigateur incompatibles WebView2).

**1. Rapprochement par domaine racine (`OfferAutoFill`, `MainWindow.Vault.cs`) :** origine exacte en priorité, sinon même domaine racine via `RootDomainOf()` (réutilisé de `MainWindow.Sessions.cs`). Un identifiant capturé sur `auth.micromania.fr` est désormais proposé partout sur `micromania.fr`. Avant : égalité stricte d'origine → aucune proposition hors du sous-domaine exact de capture.

**2. Champ `login_url` (`VaultCredential.LoginUrl`) :** capturé au moment de l'offre d'enregistrement (= `_browserView.Source` au submit, avant la redirection post-login), transporté par `_pendingCredential` (tuple à 4 éléments), persisté par `Upsert(origin, username, password, loginUrl)`. Préservé par `SetLabel` et `ImportClear` ; sur update, l'ancien `LoginUrl` est gardé si le nouveau est vide. Compatible coffres existants (champ JSON absent = vide).

**3. Carte du coffre cliquable :** bouton Globe (U+E774) « Ouvrir la page de connexion » sur chaque carte → `OpenVaultLoginPage()` : navigue vers `LoginUrl` (repli sur `Origin`, qui est une URL navigable, pour les anciennes entrées) via `NavigateCurrentTab` → referme le panneau. Flux complet : coffre → clic → page de login → barre « Remplir ».

**Étape suivante actée (0.27.x, non faite) :** intégrer la vraie Public Suffix List (fichier de données Mozilla, licence MPL — remplace la liste maison `TwoPartPublicSuffixes`) et réécrire le script de détection de champs en s'inspirant des heuristiques KeePassXC-Browser (réimplémentation, pas de copie de code).

**Build :** 0 erreur (2 warnings CS8625 préexistants). Version → `0.27.0-dev`. Relancé pour test utilisateur.

## 2026-07-07 — 0.27.1-dev

### Nouveau module propre pour le gestionnaire de mots de passe

Suite au constat que les tentatives successives rendaient le gestionnaire de mots de passe trop fragile, decision de repartir proprement sur la partie capture/remplissage sans jeter le coffre existant.

**Architecture :**
- Creation de `PulseBrowser.WinUI/Credentials/`.
- `CredentialService.cs` devient le point d'entree navigateur: injection du script de capture, reception des messages WebView2 et execution de l'autofill.
- `CredentialCaptureScript.js` remplace le script inline historique: il suit les saisies d'identifiant, les champs mot de passe, les clics, `submit`, touche Entree, `pagehide`, `visibilitychange` et les formulaires sans `<form>`.
- `CredentialAutofillScript.js` remplit avec le setter natif `HTMLInputElement.value` et declenche `input`/`change`, pour mieux fonctionner avec React/Vue/SPA.
- `CredentialMatcher.cs` choisit l'identifiant par origine exacte, URL de connexion puis domaine racine.
- `PublicSuffixService.cs` centralise la normalisation hote/origine/domaine racine.
- Ajout d'un banc de test local `Credentials/TestPages/credential-lab.html` et de `docs/CREDENTIALS_0_27_1.md`.

**Branchement :**
- `MainWindow.Navigation.cs` initialise maintenant `_credentialService.InitializeAsync(...)` au demarrage WebView2.
- `MainWindow.Vault.cs` recoit les captures via `CredentialService_CredentialCaptured`, garde l'UX existante `Enregistrer` / `Remplir`, mais delegue le remplissage a `CredentialService.FillAsync`.
- L'ancien sniff POST de `MainWindow.Privacy.cs` est gele: les methodes restent comme reference legacy, mais ne sont plus appelees afin d'eviter doublons et faux positifs.

**Principe maintenu :** stockage 100% maison. Chromium/WebView2 ne stocke ni ne remplit les mots de passe; `vault.pulse` reste le seul coffre actif.

**Verification :**
- `node --check` passe sur `CredentialCaptureScript.js` et `CredentialAutofillScript.js`.
- `build-winui.cmd` passe avec 0 erreur. Il reste 4 warnings nullable preexistants dans `MainWindow.Profile.cs`.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: l'application s'ouvre puis se ferme proprement.

**Version :** `0.27.1-dev`.

## 2026-07-07 — 0.28.0-dev

### Module Password Manager autonome

Apres retour utilisateur et comparaison avec l'approche type Proton Pass, changement d'axe: construire d'abord un vrai gestionnaire de mots de passe local simple, puis seulement ensuite continuer l'implantation fine dans les pages web.

**Architecture :**
- Creation de `PulseBrowser.WinUI/PasswordManager/PasswordManagerService.cs`.
- Creation de `PulseBrowser.WinUI/PasswordManager/PasswordManagerEntryDraft.cs`.
- Le module encapsule les operations metier autour de `vault.pulse`: lister, rechercher, ajouter/mettre a jour, renommer, supprimer, importer/exporter et retrouver le meilleur identifiant pour une URL.
- Le module ne depend pas de WebView2 ni de `MainWindow`: le navigateur devient client du gestionnaire au lieu de porter la logique lui-meme.

**Interface :**
- L'ancien panneau `Coffre de mots de passe` devient `Gestionnaire de mots de passe`.
- Ajout d'une recherche locale par nom, site, domaine, URL ou identifiant.
- Ajout manuel enrichi: nom optionnel, origine, URL de connexion, identifiant et mot de passe.
- Ajout d'actions simples de gestionnaire: ouvrir la page de connexion, copier l'identifiant, copier le mot de passe, renommer, supprimer avec confirmation.

**Integration navigateur :**
- `OfferAutoFill` utilise maintenant `PasswordManagerService.FindBestForAddress(...)` pour demander au module s'il existe un identifiant pertinent pour la page courante.
- L'autofill WebView2 reste une couche separee appelee apres validation utilisateur; il n'est plus le coeur du systeme.

**Principe maintenu :** `vault.pulse` reste la source de verite locale et chiffree. Chromium/WebView2 ne stocke toujours aucun mot de passe.

**Verification :**
- `node --check` passe sur `CredentialCaptureScript.js` et `CredentialAutofillScript.js`.
- `build-winui.cmd` passe avec 0 erreur. Il reste 4 warnings nullable preexistants dans `MainWindow.Profile.cs`.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: l'application s'ouvre puis se ferme proprement.

**Version :** `0.28.0-dev`.

## 2026-07-07 — 0.29.0-dev

### Vrai module d'interaction Password Manager

Suite au retour utilisateur indiquant que `0.28.0-dev` allait dans le bon sens mais ressemblait encore trop a un ajout rapide, refonte plus stricte du gestionnaire de mots de passe pour eviter l'empilement de correctifs dans `MainWindow`.

**Architecture :**
- Ajout de `PasswordManagerInteractionService`, responsable des decisions entre une page web observee et les identifiants connus.
- Ajout de `PasswordManagerPageDecision`, `PasswordManagerPromptKind` et `PasswordManagerSaveOffer`.
- Ajout de `CredentialPageState` pour decrire l'etat vivant d'une page de connexion.
- `PasswordManagerService` reste le coeur metier local autour de `vault.pulse`.
- `CredentialService` devient le pont WebView2: il expose maintenant `CredentialCaptured` et `PageStateChanged`.

**Detection navigateur :**
- `CredentialCaptureScript.js` devient un observateur de page: champs identifiant visibles, champs mot de passe visibles, identifiant recent, mutations DOM, focus, clics, touche Entree, chargement de page.
- Le script continue de capturer les identifiants au submit/clic/pagehide, mais il sait aussi signaler qu'un champ mot de passe n'est pas encore visible.

**Integration UI :**
- `MainWindow.Vault.cs` ne decide plus directement quand proposer le remplissage. Il demande une decision a `PasswordManagerInteractionService`, puis affiche la barre de remplissage ou un statut d'attente.
- Cas Micromania traite comme cas normal de connexion en deux etapes: identifiant connu par domaine racine, attente du champ mot de passe si seul le champ e-mail est visible, proposition de remplissage quand le champ mot de passe apparait.

**Nettoyage :**
- Suppression de l'ancien `CredentialMatcher.cs`.
- Suppression de l'ancien `RegisterCredentialMonitorAsync` inline dans `MainWindow.Navigation.cs`.
- `BrowserCore_WebMessageReceived` ne traite plus l'ancien chemin `{t:"cred"}` et reste limite aux passkeys.
- Lecture des messages WebView2 passkeys rendue robuste aux messages envoyes comme chaine JSON.
- Ajout de `docs/PASSWORD_MANAGER_0_29.md` et `logs/2026-07-07-password-manager-real-module-0-29.md`.

**Verification :**
- `node --check` passe sur `CredentialCaptureScript.js` et `CredentialAutofillScript.js`.
- Premier `build-winui.cmd` bloque sous sandbox réseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- `build-winui.cmd` passe avec 0 erreur. Il reste 4 warnings nullable preexistants dans `MainWindow.Profile.cs`.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: l'application s'ouvre puis se ferme proprement.

**Version :** `0.29.0-dev`.

## 2026-07-07 — 0.29.1-dev

### Proposition d'identifiant des la page de connexion

Retour utilisateur apres test Micromania: le gestionnaire possedait bien l'identifiant, mais Pulse ne le proposait toujours pas au moment de la connexion, et reproposait ensuite d'enregistrer un identifiant deja present. Correction ciblee du flux essentiel.

**Comportement corrige :**
- Ajout de `PasswordManagerPromptKind.UsernameFillAvailable`.
- `PasswordManagerInteractionService.EvaluatePage` propose maintenant le remplissage si un champ identifiant est visible et qu'un identifiant existe pour le domaine, meme sans champ mot de passe visible.
- `CredentialAutofillScript.js` sait remplir l'identifiant seul et retourne le statut `Identifiant rempli. Mot de passe attendu.`
- Quand le champ mot de passe apparait ensuite, le flux existant peut proposer le remplissage complet.

**Doublons et mises a jour :**
- Ajout de `PasswordManagerService.FindExistingLogin(origin, username, loginUrl)`.
- `BuildSaveOffer` ignore les captures deja presentes avec le meme identifiant et le meme mot de passe.
- Si le mot de passe differe pour un identifiant deja connu, la barre propose une mise a jour au lieu d'un nouvel enregistrement.

**Detection :**
- `CredentialCaptureScript.js` filtre mieux les faux champs identifiant: recherche, promo, code, quantite.

**Documentation :**
- Ajout de `docs/PASSWORD_MANAGER_0_29_1.md`.
- Ajout de `logs/2026-07-07-password-manager-login-offer-0-29-1.md`.
- Version courante mise a `0.29.1-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.

**Verification :**
- `node --check` passe sur `CredentialCaptureScript.js` et `CredentialAutofillScript.js`.
- Premier `build-winui.cmd` bloque sous sandbox réseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- `build-winui.cmd` passe avec 0 erreur. Il reste 4 warnings nullable preexistants dans `MainWindow.Profile.cs`.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: l'application s'ouvre puis se ferme proprement.

**Version :** `0.29.1-dev`.

## 2026-07-07 — 0.29.2-dev

### Correction du faux succes de remplissage

Retour utilisateur avec capture Micromania: Pulse affichait `Identifiant rempli. Mot de passe attendu.`, mais le champ e-mail du panneau de connexion restait vide. La capture montrait que l'e-mail avait probablement ete ecrit dans un champ newsletter/footer de la page de fond.

**Cause :**
- Le script choisissait le meilleur champ e-mail visible, sans verifier qu'il etait au premier plan ni dans un contexte de connexion.
- Le script retournait succes apres tentative d'ecriture sans verifier que la valeur finale du champ correspondait bien a l'identifiant.

**Corrections :**
- `CredentialAutofillScript.js` cible maintenant seulement les champs visibles et actionnables au premier plan via `elementFromPoint`.
- Les champs couverts par un overlay ne sont plus des cibles valides.
- Les contextes newsletter, offres, marketing, footer, presse, recrutement, paiement, promo, code et recherche sont fortement penalises.
- Les contextes connexion, compte, auth, login, continuer et mot de passe sont favorises.
- Apres `setValue`, le script verifie que `el.value` correspond a la valeur attendue avant d'annoncer le succes.
- Si le site refuse l'ecriture, Pulse doit afficher un echec au lieu d'un faux succes.
- `CredentialCaptureScript.js` applique aussi la logique topmost/contexte pour eviter de declencher la barre sur des champs newsletter couverts.

**Documentation :**
- Ajout de `docs/PASSWORD_MANAGER_0_29_2.md`.
- Ajout de `logs/2026-07-07-password-manager-target-field-0-29-2.md`.
- Version courante mise a `0.29.2-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.

**Verification :**
- `node --check` passe sur `CredentialCaptureScript.js` et `CredentialAutofillScript.js`.
- Premier `build-winui.cmd` bloque sous sandbox réseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- `build-winui.cmd` passe avec 0 erreur. Il reste 4 warnings nullable preexistants dans `MainWindow.Profile.cs`.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: l'application s'ouvre puis se ferme proprement.

**Version :** `0.29.2-dev`.

### Validation utilisateur du gestionnaire de mots de passe

Retour utilisateur apres test réel:
- Micromania fonctionne enfin apres la correction du ciblage de champ.
- Amazon a aussi ete teste avec succes.
- Le gestionnaire est considere fonctionnel pour une version de developpement, meme s'il n'est pas parfait.

Decision de suite:
- Ne pas chercher la perfection immediate.
- Garder Micromania et Amazon comme cas de reference pour les regressions futures du gestionnaire.
- Passer a un autre chantier, avec l'idee que les heuristiques seront durcies progressivement si de nouveaux sites posent probleme.

## 2026-07-07 — 0.29.3-dev

### Nettoyage du dead-code legacy (chantier A du plan en 4 chantiers)

Plan valide par l'utilisateur : tout faire (nettoyage, Public Suffix List, import Firefox, un WebView2 par onglet), proprement, sans casser le gestionnaire de mots de passe valide en 0.29.2. Ordre choisi du moins risque au plus risque pour le gestionnaire.

**Supprime (plus aucun appelant) :**
- `MainWindow.Privacy.cs` : tout l'ancien sniff POST gele (`TryCapturePostCredentialAsync`, `ParseFormBody`, `ParseJsonBody`, `FindJsonString`, `ExtractPostOrigin`) + usings devenus inutiles. `CoreWebView2_WebResourceRequested` redevient `void` simple (plus d'await).
- `MainWindow.Vault.cs` : `OfferCredentialSave` (n'etait plus appele que par le sniff mort ; le chemin vivant est `CredentialService_CredentialCaptured` → `ShowCredentialSaveOffer`).
- `PulseModels.cs` : classe `PulseCoreClient` entiere (IPC named pipe vers le Rust, abandonne depuis 0.20.1), `using System.IO.Pipes`, propriete vestigiale `UiSettings.MasterPasswordEnabled` (morte depuis 0.22).
- `MainWindow.xaml.cs` : `using System.IO.Pipes` inutile.

**Conserve volontairement :** `ChromiumCredentialReader` + `SyncFromBrowserStore` + `MigrateAndClearBrowserPasswordsAsync` — garde-fou actif du principe souverain (rapatrie puis purge tout mot de passe que Chromium aurait capte), appele au login et a l'init WebView2.

**Warnings CS8625 corriges (les 4 preexistants) :** `BookmarkStore` accepte desormais `string?` pour les deux chemins legacy (bookmarks.tsv / favorites.tsv), avec gardes null aux points d'usage. L'appel `new BookmarkStore(..., null, null)` de `ImportAndFinish` est desormais conforme.

**Non touche :** coeur Rust `src/` (reste la cible CEF long terme), scripts Credentials v2, PasswordManager.

**Build :** MSBuild x64, 0 erreur, **0 avertissement** (premiere fois depuis 0.20). Version → `0.29.3-dev` (`MainWindow.xaml.cs` + `AGENTS.md`).

## 2026-07-07 — 0.30.0-dev

### Vraie Public Suffix List Mozilla (chantier B)

Remplace la liste maison `TwoPartPublicSuffixes` (annoncee des 0.27.0 comme etape suivante) par la Public Suffix List officielle (Mozilla, licence MPL 2.0, https://publicsuffix.org).

**Implementation :**
- `Credentials/public_suffix_list.dat` (333 Ko, snapshot du jour) embarque en `EmbeddedResource` dans le csproj — aucun telechargement au runtime, conforme au principe local-first.
- `PublicSuffixService.cs` reecrit : algorithme officiel complet (regles exactes, wildcards `*.x`, exceptions `!x`, regle implicite `*`), chargement paresseux thread-safe (`Lazy<T>`), conversion punycode des regles Unicode via `IdnMapping` (les hotes WebView2 sont en ASCII), gestion des adresses IP et hotes mono-label.
- **Repli de securite** : si la ressource ne charge pas, retour a l'heuristique courte historique — le gestionnaire de mots de passe et les sessions ne cassent jamais.
- `MainWindow.Sessions.cs` : suppression de la copie locale de `RootDomainOf` + sa liste ; delegation a `PublicSuffixService` (une seule source de verite pour coffre ET sessions).

**Validation par reflexion sur l'assembly compile (13 cas) :** cas de reference intacts (`auth.micromania.fr` → `micromania.fr`, `amazon.fr`), suffixes deux niveaux (`amazon.co.uk`, `bbc.co.uk`), section privee PSL desormais geree (`user123.github.io` et `domain.blogspot.com` isoles par utilisateur — impossible avant), wildcard (`example.ck` reste entier) et exception (`foo.city.kawasaki.jp` → `city.kawasaki.jp`) conformes, IP et `localhost` inchanges.

**Build :** 0 erreur, 0 avertissement. Version → `0.30.0-dev`.

## 2026-07-07 — 0.31.0-dev

### Import des favoris Firefox (chantier C) — le dernier navigateur manquant

**Nouveau fichier `FirefoxBookmarkReader.cs`** (meme pattern que `ChromiumCredentialReader`) :
- Decouverte des profils : `%APPDATA%\Mozilla\Firefox\Profiles\*` contenant `places.sqlite`.
- Lecture SQLite (`Microsoft.Data.Sqlite`, deja reference) : `moz_bookmarks` (arborescence) + `moz_places` (URLs), racines reperees par GUID stables (`toolbar_____`, `menu________`, `unfiled_____`, `mobile______`).
- Base verrouillee quand Firefox tourne → copie temporaire, **avec le journal WAL** (`places.sqlite-wal`) pour ne pas perdre les favoris recents. `Mode=ReadOnly`, suppression de la copie apres lecture.
- Mapping : barre personnelle → `root-toolbar` ; menu + « Autres favoris » → `root-other` ; favoris mobiles → dossier « Favoris mobiles » sous `root-other`. Separateurs (type 3) et URLs internes (`place:`) ignores.

**Branchement :**
- `BrowserImportSource.Discover()` ajoute les profils Firefox detectes ; `ReadTree()` dispatch sur le nom de fichier (`places.sqlite` → lecteur Firefox, sinon JSON Chromium). Firefox apparait donc automatiquement dans le panneau migration ET la page d'import des parametres.
- `MainWindow.Profile.cs` : l'entree migration Firefox n'est plus un stub « non supporte » ; le message special renvoyant vers l'import HTML est retire (l'import HTML reste disponible en secours).

**Verification :** build 0 erreur / 0 avertissement. Firefox absent de ce PC (0 profil, degradation propre, Chrome 310 favoris toujours detecte) → parseur valide sur une `places.sqlite` synthetique au vrai schema : 4 favoris comptes (`place:` et separateur ignores), arborescence exacte (barre + dossier imbrique, menu dans Autres, mobiles dans « Favoris mobiles »). **A tester sur un vrai profil Firefox a l'occasion.**

**Version :** `0.31.0-dev`.

## 2026-07-07 — 0.32.0-dev

### Un WebView2 par onglet (chantier D = ancien « chantier 3 », le gros morceau)

Fin du WebView2 unique partage : changer d'onglet ne recharge plus la page. Chaque onglet garde son moteur, son etat, ses formulaires. Documentation complete dans `docs/TABS_PER_WEBVIEW_0_32.md`.

**Architecture :**
- `BrowserTabState` porte `View` (son WebView2) + `PendingAddress`. `BrowserHost` (Grid) empile les vues ; `ActivateTab` bascule la `Visibility` — zero rechargement.
- `_browserView` = vue de l'onglet ACTIF : le code existant (boutons nav, purge sessions, parametres) fonctionne sans changement.
- Creation paresseuse (`EnsureTabView`) : un onglet restaure en arriere-plan ne cree son moteur qu'a la premiere activation. Garde `_browserSurfaceReady` (pas de vue avant activation de la fenetre — crash historique 0.8.x).
- Fermeture (`CloseTabView`) : detache Credentials, oublie les scripts privacy du moteur, `Close()`.

**Scoping des evenements :** tous les handlers retrouvent leur onglet via `TabForView`/`TabForCore` ; barre d'adresse, statut, barres remplissage/sauvegarde ne sont touchees que par l'onglet visible (`IsActiveView`). Un onglet d'arriere-plan met a jour titre/favicon/historique, jamais les barres.

**Gestionnaire de mots de passe protege (cas de reference Micromania/Amazon) :**
- `CredentialService` : `InitializeAsync` (mono-core, permutait) → `AttachAsync` (multi-cores) + `SetActiveCore` + `Detach`. Les messages `page-state` des onglets d'arriere-plan sont ignores (pas de barre pour une page invisible) ; les captures restent acceptees de tous les onglets (redirection post-login pendant un changement d'onglet). `FillAsync` remplit l'onglet visible.
- Scripts de capture inchanges (`CredentialCaptureScript.js`/`CredentialAutofillScript.js` non touches).

**Privacy par moteur :** `_cosmeticScriptId`/`_consentScriptId` (singletons) → dictionnaires par `CoreWebView2` ; enregistrement a chaque init de moteur + re-enregistrement sur tous les moteurs vivants (`AttachedCores`) aux toggles. `InjectSiteCosmeticAsync`/`InjectConsentRetryAsync`/`RegisterPasskeyMonitorAsync` parametres par core.

**Gardes par lancement :** purge sessions = premier moteur seulement (profil Chromium partage) ; migration mots de passe Chromium → vault = une fois (`_browserPasswordsMigrated`).

**Limites connues :** compteur du bouclier « sur cette page » peut compter des requetes d'onglets d'arriere-plan (compteur PrivacyEngine global) ; memoire par onglet actif (standard navigateurs, mitige par creation paresseuse).

**Verification :** build 0 erreur / 0 avertissement ; lancement 12 s : fenetre `Pulse Browser 0.32.0-dev`, moteur `msedgewebview2.exe` cree par l'onglet actif, arret propre. **A tester interactivement : fluidite du changement d'onglet, etat conserve, login Micromania/Amazon (capture + remplissage), fermeture d'onglets, onglets verticaux.**

**Version :** `0.32.0-dev`.

## 2026-07-07 — 0.33.0-dev

### Centre du site actuel

Ajout d'une premiere version du centre de controle par site dans la surface active `PulseBrowser.WinUI`, apres validation utilisateur du chantier.

**Acces :**
- `Outils > Site actuel`.
- Flyout du bouclier : ajout d'un resume du domaine et du bouton `Centre du site`.

**Panneau :**
- Nouveau `SiteControlPanel` dans `MainWindow.xaml`.
- Nouveau fichier `MainWindow.SiteControl.cs`.
- Le panneau regroupe le domaine racine courant, l'adresse active, le statut privacy de la page visible, le nombre de cookies du domaine, le statut de session de confiance, le nombre d'identifiants locaux connus dans `vault.pulse`, le nombre d'entrees d'historique local et les dernieres pages connues du site.

**Actions :**
- Revenir a la page active.
- Actualiser les informations du site.
- Conserver ou purger la session du domaine au demarrage.
- Oublier immediatement les cookies du domaine courant.
- Ouvrir le gestionnaire de mots de passe filtre sur le domaine, avec la barriere existante du coffre.
- Ouvrir l'historique filtre sur le domaine.
- Rouvrir une page recente du site depuis le panneau.

**Principe :** aucune donnee envoyee a un serveur. Le panneau lit seulement les informations locales deja gerees par Pulse Browser : cookies WebView2 du profil local, `vault.pulse`, `history.pulse` et `ui-settings.pulse`.

**Documentation :**
- Ajout de `docs/SITE_CONTROL_CENTER_0_33.md`.
- Ajout de `logs/2026-07-07-site-control-center-0-33.md`.

**Verification :**
- `build-winui.cmd` bloque d'abord sous sandbox reseau sur NuGet (`NU1301`), puis restore reussi apres relance avec reseau autorise.
- Le build standard echoue ensuite uniquement a la copie finale car une instance existante `PulseBrowser.WinUI (26164)` verrouille l'executable Debug.
- Build de verification vers `artifacts/winui-sitecontrol-build/` reussi avec 0 erreur et 0 avertissement.
- Pas de lancement interactif supplementaire dans cette passe, l'application etant deja ouverte.

**Version :** `0.33.0-dev`.

## 2026-07-07 — 0.34.0-dev

### Palette de commande Ctrl+K

Suite au chantier `0.33.0-dev`, ajout d'une palette de commande moderne pour acceder rapidement aux actions et donnees locales de Pulse Browser.

**Acces :**
- Raccourci `Ctrl+K`.
- `Echap` ferme la palette.
- `Entree` ouvre le resultat selectionne.

**Implementation :**
- Ajout de `CommandPaletteOverlay` dans `MainWindow.xaml`.
- Ajout de `MainWindow.CommandPalette.cs`.
- Ajout d'un `KeyboardAccelerator` `Ctrl+K` dans le constructeur de `MainWindow`.
- Extension de `RootKeyDown` pour ouvrir/fermer la palette sans perturber l'ecran de connexion ou le wizard.

**Recherche locale :**
- Commandes internes : nouvel onglet, accueil, site actuel, parametres, gestionnaire de mots de passe, historique, telechargements, sites connectes, cles d'acces, ajouter aux favoris, a propos.
- Onglets ouverts.
- Favoris locaux.
- Historique local.
- Adresse ou recherche web saisie directement dans la palette.

**Principe :** aucun service externe. La palette lit uniquement les donnees locales deja presentes en memoire ou dans les stores Pulse Browser. Les mots de passe ne sont pas inclus dans cette premiere version pour eviter d'exposer du contenu sensible dans une recherche globale.

**Documentation :**
- Ajout de `docs/COMMAND_PALETTE_0_34.md`.
- Ajout de `logs/2026-07-07-command-palette-0-34.md`.

**Verification :**
- Build MSBuild x64 vers `artifacts/winui-commandpalette-build/` reussi avec 0 erreur et 0 avertissement.
- Pas de lancement interactif supplementaire dans cette passe. A tester : comportement de `Ctrl+K` quand le focus est dans WebView2, selection clavier, ouverture d'onglet, favori, historique et recherche web.

**Version :** `0.34.0-dev`.

## 2026-07-08 — 0.35.0-dev

### Premiere passe visuelle legere

Suite au retour utilisateur indiquant que Pulse Browser semblait trop lourd visuellement par rapport a Chrome, Opera ou Zen, lancement d'un chantier volontairement plus leger centre sur l'aspect de la surface active `PulseBrowser.WinUI`.

**Changements visuels :**
- Remplacement de la barre de menus permanente par une barre superieure compacte avec marque Pulse, acces nouvel onglet, accueil et bouton menu.
- Reduction des hauteurs principales: onglets horizontaux, barre d'adresse, barre de favoris et pied de statut.
- Remplacement du bouton texte `Ouvrir` par un bouton icone pour alleger le chrome navigateur.
- Boutons de navigation, favoris, confidentialite et palette rendus plus compacts.
- Barre de favoris plus discrete avec icone, espacement reduit et boutons generes plus bas.
- Rail d'onglets verticaux moins large par defaut, moins rembourre et onglets generes plus denses.
- Barres d'identifiants/remplissage, pied de statut et palette de commande rendus moins massifs.

**Documentation :**
- Ajout de `docs/VISUAL_REFRESH_0_35.md`.
- Ajout de `logs/2026-07-08-visual-refresh-0-35.md`.

**Verification :**
- Premier `build-winui.cmd` bloque sous sandbox reseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- Correction d'une erreur XAML: `Window.Resources` n'est pas accepte sur cette fenetre WinUI; les styles locaux ont ete deplaces dans `Grid.Resources`.
- `build-winui.cmd` reussi avec 0 erreur et 0 avertissement.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.35.0-dev`, processus repondant, fermeture propre.

**Version :** `0.35.0-dev`.

## 2026-07-08 — 0.36.0-dev

### Chrome navigateur allege apres comparaison visuelle

Suite a la comparaison entre les captures de Pulse Browser et Google Chrome, nouveau palier visuel dans `PulseBrowser.WinUI` pour attaquer les causes principales de lourdeur: bandes empilees, title bar orange, statut permanent et accueil trop proche d'une page produit.

**Changements visuels :**
- La bande superieure `Pulse` separee n'est plus affichee.
- Les acces `Accueil` et `Menu Pulse` sont replaces dans la barre de navigation.
- La title bar Windows est neutralisee en sombre via `AppWindow.TitleBar`.
- La barre d'adresse est rendue plus arrondie, avec fond et bordure plus doux.
- Le pied de statut permanent disparait quand la page web est visible.
- Le pied de fenetre reste disponible uniquement dans les panneaux internes pour garder le bouton `Retour au site`.
- L'accueil `pulse://accueil` est refondu en surface de nouvel onglet: marque Pulse, recherche centrale et raccourcis legers.
- Les cartes explicatives `Prive par defaut`, `Coffre local` et `Votre profil` sont retirees de l'accueil quotidien.

**Documentation :**
- Ajout de `docs/BROWSER_CHROME_LIGHTENING_0_36.md`.
- Ajout de `logs/2026-07-08-browser-chrome-lightening-0-36.md`.

**Verification :**
- Premier `build-winui.cmd` bloque sous sandbox reseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- `build-winui.cmd` reussi avec 0 erreur et 0 avertissement.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.36.0-dev`, processus repondant, fermeture propre.

**Version :** `0.36.0-dev`.

## 2026-07-08 — 0.37.0-dev

### Chrome integre et inspiration Zen

Suite au retour utilisateur sur l'effet de double barre et apres consultation de Zen Browser, nouvelle passe visuelle dans `PulseBrowser.WinUI` pour mieux integrer la title bar, ajouter un mode compact et commencer la personnalisation locale du nouvel onglet.

**Changements visuels et UX :**
- Activation de `ExtendsContentIntoTitleBar` pour integrer le contenu dans la title bar Windows.
- Ajout d'une zone de drag dediee pour ne pas casser les clics des onglets.
- Reservation automatique de la zone des boutons systeme Windows a droite via les insets de title bar.
- Ajout d'un bouton `Mode compact` dans la barre navigateur.
- Ajout d'un toggle `Mode compact inspire de Zen` dans les parametres.
- En mode compact, la barre de favoris est masquee et la barre de navigation est legerement reduite.
- Ajout d'une personnalisation locale du nouvel onglet: titre, affichage des raccourcis et edition des raccourcis au format `Nom | URL`.
- `pulse://accueil` lit maintenant ces reglages et respecte le moteur de recherche choisi.

**Documentation :**
- Ajout de `docs/ZEN_INSPIRED_CHROME_0_37.md`.
- Ajout de `logs/2026-07-08-zen-inspired-chrome-0-37.md`.

**Verification :**
- Premier `build-winui.cmd` bloque sous sandbox reseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- `build-winui.cmd` reussi avec 0 erreur et 0 avertissement.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.37.0-dev`, processus repondant, fermeture propre.

**Version :** `0.37.0-dev`.

## 2026-07-08 — 0.38.0-dev

### Direction visuelle Pulse

Suite a la validation utilisateur pour aller plus loin sur l'identite du navigateur, nouvelle passe visuelle dans `PulseBrowser.WinUI`.

**Changements visuels :**
- Nouvelle palette d'interface: charbon chaud, surfaces plus coherentes et accent orange plus ponctuel.
- Ajout de ressources visuelles Pulse dans `MainWindow.xaml`.
- Boutons du chrome legerement reduits et moins presents.
- Barre d'adresse harmonisee avec la palette Pulse.
- Title bar Windows raccordee a la nouvelle couleur du chrome.
- Barre des favoris et rail d'onglets verticaux harmonises avec la surface haute.
- Espacements du mode compact ajustes.
- `pulse://accueil` refondu visuellement: marque compacte, ligne d'accent, recherche plus nette, raccourcis plus propres et libelles tronques.

**Documentation :**
- Ajout de `docs/PULSE_VISUAL_IDENTITY_0_38.md`.
- Ajout de `logs/2026-07-08-pulse-visual-identity-0-38.md`.

**Verification :**
- Premier `build-winui.cmd` bloque sous sandbox reseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- `build-winui.cmd` reussi avec 0 erreur et 0 avertissement.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.38.0-dev`, processus repondant, fermeture propre.

**Version :** `0.38.0-dev`.

## 2026-07-08 — 0.39.0-dev

### Parametres, personnalisation et accessibilite

Suite a la demande utilisateur sur les parametres du navigateur, la personnalisation et les options pour les personnes en situation de handicap, nouvelle passe dans `PulseBrowser.WinUI`.

**Changements produit :**
- Ajout de champs persistants dans `UiSettings` pour cadrer la palette `Ctrl+K` et stocker les options d'accessibilite.
- Ajout des rubriques `Apparence` et `Accessibilite` dans les parametres.
- Deplacement des reglages du nouvel onglet vers `Apparence`.
- Ajout d'options pour activer/desactiver `Ctrl+K`, autoriser son ouverture depuis les pages web, et autoriser son ouverture pendant la saisie dans un champ texte.
- `Ctrl+K` ne vole plus le focus par defaut dans la barre d'adresse, les champs texte ou les pages web.
- Ajout des options contraste renforce, texte plus lisible, reduction des transitions et focus clavier plus visible.
- Application immediate des options d'accessibilite au chrome Pulse et a `pulse://accueil`.

**Documentation :**
- Ajout de `docs/SETTINGS_ACCESSIBILITY_0_39.md`.
- Ajout de `logs/2026-07-08-settings-accessibility-0-39.md`.

**Verification :**
- Premier `build-winui.cmd` bloque sous sandbox reseau sur NuGet (`NU1301`), puis relance hors sandbox.
- Compilation du code reussie, mais la copie vers le dossier Debug normal a echoue car une instance utilisateur `PulseBrowser.WinUI.exe` verrouillait l'executable.
- Build de verification vers `artifacts/winui-settings-accessibility-build/` reussi avec 0 erreur et 0 avertissement.
- Lancement court de `artifacts/winui-settings-accessibility-build/PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.39.0-dev`, processus repondant, fermeture propre.

**Version :** `0.39.0-dev`.

## 2026-07-08 — 0.39.1-dev

Suite au retour utilisateur signalant que les priorites produit de la session precedente n'avaient pas ete realisees, premier correctif cible dans `PulseBrowser.WinUI` apres validation `Go`.

- Correction du bouton etoile : l'ajout aux favoris n'est plus un ajout silencieux dans la barre.
- Ajout d'un dialogue de favori avec nom modifiable, choix du dossier cible et rappel de l'URL courante.
- Detection d'un favori deja existant pour la page courante : le dialogue passe en mode modification au lieu de creer un doublon.
- Ajout d'une suppression directe depuis le dialogue quand le favori existe deja.
- Ajout de `BookmarkStore.AddOrUpdateUrl` pour mettre a jour le titre, l'URL, le dossier et l'icone locale d'un favori existant, ou creer un nouveau favori si necessaire.
- Ajout de `docs/BOOKMARK_STAR_0_39_1.md` et `logs/2026-07-08-bookmark-star-0-39-1.md`.
- Verification : tentative `build-winui.cmd` bloquee par l'acces NuGet du bac a sable (`NU1301`), puis relance autorisee avec acces reseau ; restore et build WinUI reussis avec 0 avertissement et 0 erreur. Apres alignement version/documentation, meme blocage sandbox puis relance autorisee reussie avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` reussi : fenetre `Pulse Browser 0.39.1-dev`, processus repondant, fermeture du processus de test.

**Version :** `0.39.1-dev`.

## 2026-07-08 — 0.40.0-dev

Suite au `go` utilisateur, deuxieme chantier prioritaire : permettre une recuperation locale des donnees si le mot de passe est oublie, sans introduire de serveur ni de contournement du chiffrement.

- Ajout d'une cle de recuperation locale `PULSE-...` generee a la creation d'un profil.
- `UserProfile` stocke uniquement un hash PBKDF2-SHA256 de cette cle avec sel aleatoire, jamais la cle en clair.
- Affichage d'un dialogue de cle de recuperation avec bouton de copie ; l'utilisateur doit la noter, sinon Pulse Browser ne peut pas la retrouver.
- Ajout d'un bouton `Creer une nouvelle cle de recuperation` dans `Parametres > Profil`.
- Ajout du lien `Mot de passe oublie ?` sur l'ecran de connexion.
- `VaultStore` maintient maintenant une copie de recuperation chiffree du coffre, protegee par une cle de donnees de secours emballee par la cle de recuperation.
- La copie de recuperation du coffre est maintenue a jour quand le coffre est ouvert, sans stocker la cle de recuperation en clair.
- En cas de recuperation reussie, l'utilisateur definit un nouveau mot de passe, le coffre est rechiffre avec ce nouveau mot de passe et le PIN est desactive pour eviter un emballage obsolete.
- Ajout de `docs/RECOVERY_KEY_0_40.md` et `logs/2026-07-08-recovery-key-0-40.md`.
- Verification : tentative `build-winui.cmd` bloquee par l'acces NuGet du bac a sable (`NU1301`), puis relance autorisee avec acces reseau ; restore et build WinUI reussis avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` reussi : fenetre `Pulse Browser 0.40.0-dev`, processus repondant, fermeture du processus de test.

**Version :** `0.40.0-dev`.

## 2026-07-08 — 0.40.1-dev

Suite au retour utilisateur, correction prioritaire du mode compact : il masquait la barre des favoris, ce qui rendait le travail recent sur les favoris moins utile.

- Renommage visible du reglage en `Interface compacte`.
- L'interface compacte ne masque plus la barre des favoris par defaut.
- Ajout d'une option separee `Masquer les favoris en interface compacte`, desactivee par defaut.
- Ajout de `CompactModeHidesBookmarks` dans `UiSettings` pour persister ce choix.
- Ajout d'un reglage `Effet translucide` dans `Parametres > Apparence` avec les choix `Desactive`, `Mica` et `Acrylic`.
- Ajout de `WindowBackdrop` dans `UiSettings`.
- Branchement des backdrops WinUI `MicaBackdrop` et `DesktopAcrylicBackdrop` avec retour au rendu solide en cas d'erreur.
- Ajout de `docs/COMPACT_TRANSLUCENT_0_40_1.md` et `logs/2026-07-08-compact-translucent-0-40-1.md`.
- Verification : tentative `build-winui.cmd` bloquee par l'acces NuGet du bac a sable (`NU1301`), puis relance autorisee avec acces reseau ; restore et build WinUI reussis avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` reussi : fenetre `Pulse Browser 0.40.1-dev`, processus repondant, fermeture du processus de test.

**Version :** `0.40.1-dev`.

## 2026-07-08 — 0.41.0-dev

Suite au `go` utilisateur, reprise de trois points d'ergonomie inspires de Zen mais adaptes a Pulse Browser.

- Le rendu translucide repart de l'approche Pulse Explorer : `MicaBackdrop` / `DesktopAcrylicBackdrop` plus fenetre Win32 `WS_EX_LAYERED`.
- Ajout de `WindowTransparency` dans `UiSettings`.
- Ajout du curseur `Intensite de transparence` dans `Parametres > Apparence`.
- En contraste renforce, Pulse Browser revient au rendu solide.
- Le menu des trois points est allege : il garde les actions rapides et retire les entrees de gestion avancee comme coffre, sites connectes et passkeys.
- Les raccourcis de `pulse://accueil` deviennent editables directement depuis la page : ajout, modification et suppression.
- Ajout des messages WebView2 `newtab_add_shortcut`, `newtab_edit_shortcut`, `newtab_delete_shortcut`.
- Les modifications de raccourcis sont persistees dans `UiSettings.NewTabShortcuts` et les pages d'accueil ouvertes sont rechargees.
- Ajout de `docs/ZEN_UI_REWORK_0_41.md` et `logs/2026-07-08-zen-ui-rework-0-41.md`.
- Verification : tentative `build-winui.cmd` bloquee par l'acces NuGet du bac a sable (`NU1301`), puis relance autorisee avec acces reseau ; restore et build WinUI reussis avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` reussi : fenetre `Pulse Browser 0.41.0-dev`, processus repondant, fermeture du processus de test.

**Version :** `0.41.0-dev`.

## 2026-07-08 — 0.42.0-dev

Suite au `Go` utilisateur, mise en place du prochain chantier prioritaire sans empiler de fonctions inutiles : le multi-utilisateur local dans `PulseBrowser.WinUI`.

- Ajout de `ActiveProfileId` dans `PulseConfig`.
- Ajout de `PulseProfilePaths.ProfilesRoot`, `ForProfileId`, `FromDirectory` et `NormalizeProfileId` pour separer les dossiers de profil locaux.
- Ajout de `PulseProfileEntry` et `PulseProfileRegistry` pour decouvrir les profils locaux existants, conserver le profil personnalise actif et creer des identifiants de profil propres.
- Ajout d'un selecteur de profil dans l'overlay de connexion quand plusieurs profils sont disponibles.
- Ajout de la creation d'un autre profil depuis le selecteur et depuis `Parametres > Profil`.
- Les nouveaux profils locaux sont stockes sous `%LOCALAPPDATA%\PulseBrowser\profiles\<id>`.
- Les emplacements personnalises restent supportes, mais le changement vers un dossier different redemarre l'application pour eviter un melange de favoris, historique, coffre et reglages deja charges en memoire.
- `Parametres > Profil` affiche maintenant le dossier actif et expose `Changer de profil` / `Creer un autre profil`.
- La reinitialisation du profil nettoie aussi la configuration d'emplacement personnalise quand le profil actif etait un profil custom.
- Ajout de `docs/MULTI_USER_0_42.md` et `logs/2026-07-08-multi-user-0-42.md`.
- Verification : tentative `build-winui.cmd` bloquee par l'acces NuGet du bac a sable (`NU1301`), puis relance autorisee avec acces reseau ; restore et build WinUI reussis avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` reussi : fenetre `Pulse Browser 0.42.0-dev`, processus repondant, fermeture du processus de test.

**Version :** `0.42.0-dev`.
