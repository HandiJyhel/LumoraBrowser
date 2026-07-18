# 2026-07-18/19 - Mode Incognito : fusion navigation privee + Tor (0.83.24-dev)

## Contexte et decisions produit actees avec l'utilisateur

Suite au point 2 de l'audit initial (integrite de tor.exe), une discussion
produit a change la priorite : repenser l'ensemble plutot que durcir la
fenetre Tor existante. Constat de depart : le mode prive classique (tous
navigateurs confondus, Lumora avant cette version inclus) protege de la
persistance locale mais jamais du reseau - confusion tres repandue. Lumora
avait deux fonctionnalites separees qui se marchaient dessus : "Nouvelle
fenetre privee" (ephemere, rapide, pas de Tor) et "Navigation anonyme
(Tor)" (proxy Tor, mais profil persistant sur disque - pas reellement
ephemere).

1. Un seul mode "Incognito" plutot que deux entrees de menu qui se
   recouvrent.
2. Session ephemere (rien sauvegarde a la fermeture) : toujours vraie,
   inconditionnellement.
3. Anonymat reseau via Tor : optionnel, desactive par defaut (latence
   reelle de Tor, ne pas la punir sur l'usage le plus frequent qui n'en a
   pas besoin).
4. Les deux garanties ne doivent jamais etre fusionnees en un seul
   message ambigu.
5. Fenetre volontairement minimale : pas de hub Modules, pas de
   compagnon, pas de raccourcis.
6. Retour utilisateur (deuxieme tour) : Incognito doit apparaitre dans le
   selecteur de mode (a cote de Neutre/Equilibre/etc.), pas seulement
   dans un sous-menu Navigation.

## Bug bloquant trouve, diagnostique et corrige

### Symptome

Premiere implementation : `LumoraIncognitoWindow` cree un
`CoreWebView2Environment` explicite (`CreateWithOptionsAsync`) avec
`CoreWebView2ControllerOptions` (`ProfileName` + `IsInPrivateModeEnabled`),
puis `EnsureCoreWebView2Async(environment, controllerOptions)`. Resultat
observe en verification UIA + captures d'ecran : le chrome de la fenetre
(barre d'adresse, bascule Tor, textes d'etat) fonctionne et reagit
correctement, mais la zone de contenu reste vide - `view.CoreWebView2`
reste `null` juste apres l'appel, **sans exception**, en ~10ms (trop
rapide pour un vrai demarrage de processus Chromium).

L'utilisateur a teste sur sa propre machine et confirme le meme symptome
exact (capture d'ecran a l'appui : "Moteur web indisponible : la session
n'a pas pu demarrer."), ecartant l'hypothese d'un probleme specifique a
l'environnement de verification isole.

### Diagnostic (par elimination, 8 configurations testees)

1. Dossier de donnees partage avec `MainWindow` (`_profile.BrowserDataDir`) - `core null`.
2. Dossier de donnees dedie, distinct de celui de `MainWindow` - `core null`.
3. Avec `IsInPrivateModeEnabled = true` - `core null`.
4. Sans `IsInPrivateModeEnabled` - `core null`.
5. `EnsureCoreWebView2Async(environment)` sans `ControllerOptions` du tout - `core null`.
6. Environnement "par defaut" (`CreateWithOptionsAsync(null, null, null)`) - `core null`.
7. Avec 3 tentatives et delai entre chacune - `core null` a chaque fois.
8. Apres mise a jour du SDK `Microsoft.Web.WebView2` (1.0.2903.40 ->
   1.0.4078.44, ecart tres important) - `core null`, identique.
9. **Dans un process Windows totalement neuf** (voir plus bas), ou cet
   environnement explicite est le PREMIER ET SEUL moteur WebView2 du
   process, sans aucun autre a proximite - `core null` encore.

Le dernier point (9) a ete decisif : il elimine l'hypothese initiale
("deuxieme environnement WebView2 dans le meme process que MainWindow").
Le vrai facteur commun a tous les echecs : **appeler
`EnsureCoreWebView2Async` avec un `CoreWebView2Environment` explicite en
argument**, quels que soient le dossier ou les options. Le seul chemin
dont la fiabilite est prouvee dans ce projet est
`EnsureCoreWebView2Async()` **sans aucun argument**, deja utilise avec
succes par `MainWindow` (tous ses onglets) et `LumoraAppWindow` (fenetres
d'application web epinglees, deja en production).

Cause plateforme exacte non confirmee (documentation/communaute WebView2
non consultees faute de temps dans cette session), mais le contournement
fonctionne de facon fiable et reproductible.

### Correction retenue

`LumoraIncognitoWindow` abandonne l'environnement explicite. A la place,
comme `WebView2Bootstrap.ConfigureOnce` le fait pour `MainWindow` :

- `WEBVIEW2_USER_DATA_FOLDER` pointe vers un dossier temporaire unique par
  session (`%TEMP%\LumoraIncognito\<guid>`), supprime a la fermeture de
  la fenetre. Filet de securite : au demarrage d'une fenetre Incognito,
  nettoyage des dossiers residuels d'une session precedente qui aurait
  plante ou ete tuee brutalement (Gestionnaire des taches, coupure de
  courant...).
- `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` porte les drapeaux anti-crash-
  reporting habituels, l'anti-fuite WebRTC si actif dans les reglages, et
  le proxy Tor (`--proxy-server=socks5://127.0.0.1:<port>`) si demande.
- `EnsureCoreWebView2Async()` sans argument, une seule fois.

Consequence architecturale assumee, puisque ces variables ne sont lues
qu'une seule fois par process (WebView2Bootstrap suit deja ce meme
principe pour le drapeau anti-fuite WebRTC) :

- **Chaque fenetre Incognito tourne dans son propre process Windows**
  (`IncognitoProcessLauncher` relance `Lumora.WinUI.exe` avec
  `--incognito`/`--incognito-tor`/`--incognito-url=...`, lus par
  `IncognitoLaunchArgs` dans `App.xaml.cs` - meme principe que
  `LumoraAppWindow`/`WebAppLaunchArgs` pour les applications web
  epinglees, deja en production). Une fenetre ouverte depuis un lien
  `target=_blank` dans une fenetre Incognito ouvre donc aussi un nouveau
  process, jamais une fenetre in-process.
- Chaque process a son propre `TorProcessManager`, avec un port SOCKS
  choisi dynamiquement (le systeme d'exploitation attribue un port
  loopback libre) pour eviter un conflit si plusieurs fenetres
  Incognito+Tor tournent en meme temps.
- **Basculer Tor ne peut plus se faire a chaud** : la bascule ferme la
  fenetre courante et en rouvre une neuve (nouveau process) dans l'etat
  souhaite, plutot qu'une migration d'etat impossible entre deux profils
  differents dans un meme process.

## Autre retour utilisateur pris en compte

Incognito est desormais aussi une entree du selecteur de mode
(`UsageModeFlyout`, bouton `UsageModeButton` dans la barre du bas), a cote
de Neutre/Equilibre/Focus/Lecture/Creation/Recherche/Nuit, separee par un
trait et avec une infobulle qui precise qu'elle ouvre une fenetre a part
plutot que de changer le mode courant. Ce n'est pas un vrai "mode" au sens
de `UiSettings.UsageMode` (rien n'est persiste, `ApplyUsageModeFromUi`
n'est jamais appelee) - juste un raccourci place la ou l'utilisateur
s'attend a le trouver, en plus du sous-menu Navigation et du raccourci
Ctrl+Shift+N deja existants.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis.
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles (pilotage UIA, captures d'ecran),
  deux passes completes apres correction :
  1. Ouverture via le sous-menu Navigation > Incognito.
  2. Ouverture via la nouvelle entree du selecteur de mode.
  Dans les deux cas : fenetre Incognito ouverte dans un process separe
  confirme (PID different de la fenetre principale), page d'accueil
  Incognito reellement affichee a l'ecran (les deux phrases distinctes
  visibles), bascule Tor cliquee - message honnete "Moteur Tor non
  installe." affiche et bascule revenue a l'etat desactive (le moteur
  Tor n'est pas installe sur cette machine), navigation de test vers
  https://example.com confirmee a l'ecran (contenu de la page reellement
  rendu, titre de fenetre mis a jour correctement en "Example Domain -
  Incognito"), fermeture propre du process, aucune exception dans la
  trace runtime apres correction.
- Bug reproduit et confirme independamment par l'utilisateur sur sa
  propre machine (capture d'ecran) avant la correction. **Correction
  reverifiee sur cette meme machine apres coup** : capture d'ecran de
  l'utilisateur montrant une vraie page (recherche Google) chargee et
  affichee dans la fenetre Incognito. Tor reste honnetement indisponible
  sur cette machine (moteur non installe, comportement attendu et
  volontaire - le telechargement automatique n'est pas cable).

**Version :** `0.83.24-dev`.
