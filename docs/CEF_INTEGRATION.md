# Integration Chromium via CEF

## Etat actuel

Pulse Browser cible Chromium via CEF comme moteur web embarque.

La version `0.1.1-dev` corrige le faux acces Internet qui ouvrait le navigateur par defaut via Windows. Pulse Browser ne doit plus deleguer la navigation a Chrome, Edge ou un autre navigateur externe.

La version `0.2.0-dev` ajoute une premiere integration CEF: la barre d'adresse charge maintenant l'URL dans une vue Chromium enfant de la fenetre Pulse Browser.

La version `0.2.1-dev` rend cette vue redimensionnable avec la fenetre principale.

La version `0.2.2-dev` stabilise le rendu sur les machines ou le processus GPU Chromium plante, en forcant le rendu sans acceleration GPU pour le prototype.

La version `0.2.3-dev` active la pompe de messages externe CEF et ajoute des retours de chargement visibles dans le statut de la fenetre.

La version `0.2.4-dev` force l'affichage, le redimensionnement et le focus de la fenetre enfant CEF afin d'eviter une navigation chargee mais invisible.

La version `0.3.0-dev` pose le premier vrai profil local Pulse Browser: cache CEF persistant dans un profil `default`, cookies de session persistants, filtre de cookies tiers via CEF et coffre local transparent initialise avec DPAPI Windows.

La version `0.3.1-dev` transforme le coffre local en conteneur d'identifiants chiffre et relisible, prepare pour le futur enregistrement/remplissage transparent des connexions.

La version `0.3.2-dev` ajoute les commandes de navigation de base: retour, avance, rechargement et arret du chargement.

La version `0.3.3-dev` ajoute une premiere couche de gestion transparente des identifiants: capture locale prudente de formulaires de connexion compatibles, stockage chiffre dans `default.pbvault` et injection d'un autoremplissage sur la meme origine.

## Route technique retenue

Le crate Rust repere pour l'integration est:

- `cef = "149.3.0"`
- Depot: `https://github.com/tauri-apps/cef-rs`
- Licences: MIT ou Apache-2.0

Ce crate s'appuie sur CEF et telecharge/prepositionne le runtime CEF necessaire au build de developpement. Sur Windows, le build demande aussi CMake et Ninja.

## Prochain palier

Le palier actuel:

- initialise CEF dans le processus principal;
- laisse CEF gerer ses sous-processus;
- cree une vue Chromium enfant dans la fenetre Pulse Browser;
- fait naviguer la barre d'adresse dans Pulse Browser lui-meme.
- utilise une pompe de messages integree: la boucle Win32 appelle `CefDoMessageLoopWork`.
- active `external_message_pump` cote CEF pour aligner CEF avec cette boucle hote.
- redimensionne la fenetre enfant CEF quand la fenetre Pulse Browser change de taille.
- affiche et replace la fenetre enfant CEF avec `SetWindowPos`, puis lui rend le focus.
- force les switches CEF/Chromium `disable-gpu`, `disable-gpu-compositing`, `disable-gpu-rasterization` et `disable-gpu-watchdog`.
- lance aussi la navigation avec la touche Entree quand la barre d'adresse est active.
- remonte les evenements CEF de chargement, d'erreur, d'adresse et de titre dans le statut de la fenetre.
- utilise un profil local persistant sous `%LOCALAPPDATA%\PulseBrowser\profiles\default`.
- active la persistance des cookies de session pour eviter les reconnexions inutiles.
- installe un filtre CEF `CookieAccessFilter` qui autorise les cookies first-party/same-site et bloque les cookies dans les contextes tiers ou inconnus.
- initialise un coffre local transparent `default.pbvault`, protege par Windows DPAPI pour l'utilisateur courant.
- relit le coffre au demarrage pour verifier que le conteneur local chiffre est exploitable par Pulse Browser.
- capture localement certains identifiants envoyes par un formulaire `POST` URL-encode quand l'origine de la requete correspond a l'origine principale.
- stocke ces identifiants dans le coffre chiffre par origine exacte.
- injecte un autoremplissage discret sur la frame principale quand un identifiant local existe pour cette meme origine.
- expose des boutons de navigation provisoires dans la coque Win32: `Retour`, `Avancer`, `Recharger` et `Stop`.
- raccorde ces boutons aux commandes CEF `go_back`, `go_forward`, `reload` et `stop_load`.
- evite d'afficher des URL completes dans les nouveaux statuts de navigation et privilegie le domaine.

La creation de la vue a ete validee par un test UI local: apres chargement de `https://example.com`, la fenetre Pulse Browser contient les enfants CEF `CefBrowserWindow`, `Chrome_WidgetWin_1` et `Chrome_RenderWidgetHostHWND`.

La correction `0.2.2-dev` a ete validee par un test UI local avec `google.com` puis `tintin.fr`.

La correction `0.2.4-dev` a ete validee par un test UI local avec `google.com`: le statut devient `Statut CEF: Page chargee: Google`, la page emet une ligne console depuis `https://www.google.com/` dans le log CEF, et les enfants natifs CEF sont presents.

Le prochain palier devra renforcer cette base avec une politique de permissions web, une surface d'erreur reseau dediee et une premiere structure d'onglet/profil plus visible cote produit.
