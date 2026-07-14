# Lumora 0.76.0-dev - Sites en panne et memoire des demenagements

## Objectif

Retour utilisateur sur la 0.75 : un site dont le domaine ne correspond plus ne
declenche pas toujours une erreur DNS. Cas reel constate : l'ancien domaine
existe encore, repond via Cloudflare, mais l'origine est morte (erreur HTTP
522) — la barre « site introuvable » de la 0.75 ne se declenchait pas. Et dans
Chrome, l'acces « magique » au site venait simplement d'un raccourci pointant
vers un domaine intermediaire qui redirige encore : des que ce domaine mourra
a son tour, Chrome sera aussi perdu, car il ne retient rien.

La 0.76 comble les deux trous : detection des sites morts au-dela du DNS, et
**memoire locale des demenagements de domaines** — la ou Chrome refait la
redirection a chaque visite sans jamais apprendre.

## Ce qui change

- **Detection elargie** de la barre « Site introuvable » :
  - echecs reseau « le site ne repond plus » : `HostNameNotResolved`, `Timeout`,
    `CannotConnect`, `ConnectionAborted`, `ConnectionReset` (le reseau local
    coupe — `Disconnected` — est exclu, et les promotions HTTPS gardent leur
    propre dialogue de repli) ;
  - **erreurs serveur 5xx sur le document principal** (500, 502, 503, 522,
    523...) : le message affiche le code (« ne repond plus (erreur 522) »).
- **Recherche plus maligne** : le bouton « Rechercher ce site sur le web »
  cherche le **nom du site sans extension** (« zone-telechargement » pour
  zone-telechargement.win) — c'est ce qui retrouve le mieux un site qui a
  demenage vers une autre extension. Toujours declenche par un clic explicite,
  vers le moteur configure.
- **Memoire des demenagements** (`SiteRelocationStore`) : Lumora apprend les
  redirections permanentes (301/308) inter-domaines observees sur le document
  principal (page d'accueil vers page d'accueil, ou chemin conserve — les
  raccourcisseurs d'URL sont exclus). 100% local, chiffre dans le profil.
  - Quand un domaine connu comme demenage meurt, la barre propose directement
    **« Aller sur <nouveau domaine> »**, chemin conserve
    (`old.win/film/x` → `new.poker/film/x`). Les chaines sont suivies
    (A→B puis B→C : A mene directement a C).
  - **Barre « site demenage »** : si des favoris ou des raccourcis de la page
    d'accueil pointent encore vers l'ancien domaine, Lumora propose (une seule
    fois par demenagement) de les **mettre a jour en un clic** — titres,
    positions et icones conserves.

## Position produit

Chrome envoie les adresses en echec aux serveurs de Google et refait chaque
redirection sans memoire. Lumora ne transmet rien : detection et apprentissage
100% locaux, recherche uniquement sur clic. Et la memoire des demenagements
fait mieux que Chrome : elle survit a la mort de l'ancien domaine.

## Architecture

- `SiteRelocationStore.cs` (pur, teste) : `RecordPermanentRedirect` (garde-fous
  domaine racine different + chemin, suivi de chaine, purge des entrees
  circulaires, plafond 200 entrees), `TargetFor` (chaine + report du chemin),
  `MarkUpdateOffered`, `RewriteToOrigin`, mode invite sans ecriture.
  `SiteRelocationUpdatePlanner` : calcule hors UI le plan de mise a jour
  favoris/raccourcis.
- `MainWindow.SiteNotFound.cs` : suivi du document principal par **URI + id
  d'onglet** (JAMAIS par instance `CoreWebView2` : les wrappers WinRT n'ont pas
  d'identite de reference fiable — verifie en pratique). Detection 5xx
  **bilaterale** avec `NavigationCompleted` : WebView2 ne garantit aucun ordre
  entre la reponse du document et la fin de navigation (la 522 Cloudflare
  arrive dans les deux ordres selon les cas).
- `BookmarkStore.UpdateUrls` : reecriture en un passage des URL de signets.
- `ProfilePaths.SiteRelocationsFile` (`navigation/site-relocations.lumora`).

## Limites connues

- La memoire ne connait que les redirections observees EN NAVIGUANT : un site
  jamais visite pendant sa periode de redirection passe par la recherche.
- Seules 301/308 sont apprises (une 302/307 temporaire n'est pas un
  demenagement).
- Fenetre privee : non branchee (aucun apprentissage la-bas, c'est voulu).

## Verification

- Build WinUI OK ; 353/353 tests verts dont 20 nouveaux
  (`SiteRelocationStoreTests` + requetes de recherche).
- **Live (UIA, mode invite)** : barre affichee sur le vrai site en panne 522
  (zone-telechargement.win) avec le code d'erreur ; recherche =
  `q=zone-telechargement` ; apprentissage reel `twitter.com -> https://x.com`
  (301 observe) ; puis echec force de twitter.com → bouton **« Aller sur
  x.com »** → navigation vers `https://x.com/lumora-test` (chemin conserve).
  La barre « site demenage » (mise a jour des favoris) est couverte par les
  tests du planner (pas de favoris persistables en mode invite).

Details : `logs/2026-07-14-site-demenage-0-76-0.md`.
