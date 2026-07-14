# Lumora 0.75.0-dev - Reprise « site introuvable »

## Objectif

Quand un site a change de nom de domaine (ou que son domaine a disparu), l'ancienne
adresse laisse l'utilisateur bloque sur une page d'erreur. Chrome resout ce cas en
envoyant automatiquement l'adresse en echec aux serveurs de Google, qui suggerent
ou redirigent vers le bon site. Lumora offre maintenant le meme service rendu,
sans la fuite : **tout est calcule localement, et rien ne part sur le reseau sans
un clic explicite de l'utilisateur**.

## Ce qui change

Quand une navigation echoue parce que le domaine ne se resout pas
(`HostNameNotResolved` : site ferme, nom de domaine change, faute de frappe),
une barre discrete apparait sous la barre d'adresse de l'onglet actif :

- **Texte** : « Site introuvable : le domaine X ne repond plus. Il a peut-etre
  change d'adresse. »
- **« Essayer <suggestion> »** (si une suggestion locale existe) : en priorite un
  domaine tres proche deja connu du profil — favoris, historique, onglets ouverts
  (faute de frappe probable : « gogle.fr » -> « google.fr » ; ou mauvaise
  extension : « monsite.com » -> « monsite.fr ») — sinon la variante avec/sans
  « www. ». Aucune requete reseau pour calculer cette suggestion.
- **« Rechercher ce site sur le web »** : lance une recherche du domaine avec le
  moteur de recherche configure par l'utilisateur. C'est ce clic — et lui seul —
  qui fait sortir le nom du domaine de la machine, vers un moteur que
  l'utilisateur utilise deja pour ses recherches. Pour un site qui a demenage,
  le moteur retrouve la nouvelle adresse (exactement le service rendu par la
  redirection automatique de Chrome).
- **« Fermer »** : masque la barre, la page d'erreur du moteur reste.

Les pannes transitoires (timeout, reseau coupe, serveur en panne) ne declenchent
rien : seul un domaine qui ne se resout pas est traite, pour ne pas proposer de
« retrouver » un site simplement indisponible cinq minutes.

## Difference avec Chrome (position produit)

Chrome transmet chaque adresse en echec a Google sans demander. Lumora ne
transmet rien : suggestion locale d'abord, et recherche web uniquement sur action
volontaire, vers le moteur choisi par l'utilisateur. Meme confort a l'arrivee,
pas de divulgation silencieuse.

## Architecture

- `SiteNotFoundRecovery.cs` : classe **pure** (aucune dependance UI), compilee
  aussi dans `Lumora.Tests`. `SearchQueryFor`/`DisplayHostOf` (hote sans www.,
  null pour les pages internes), `WwwVariantOf` (bascule du prefixe www.),
  `ClosestKnownUrl` (distance d'edition bornee — 1 faute pour un domaine court,
  2 pour un long — plus regle « meme etiquette enregistrable, extension
  differente » via la Public Suffix List ; jamais le meme hote, qui echouerait
  pareil).
- `MainWindow.SiteNotFound.cs` : partiel UI. `OfferSiteNotFoundRecovery`
  (candidats locaux : onglets + favoris + historique), boutons, masquage.
- `MainWindow.Navigation.cs` : declenchement dans `NavigationCompleted`
  (onglet actif + `HostNameNotResolved` + URL web) ; masquage au depart d'une
  navigation et au changement d'onglet (`ActivateTab`).
- `MainWindow.xaml` : barre `SiteNotFoundBar` (meme patron que les autres barres
  d'information, BrowserHost passe en Row 9).

## Limites connues

- La suggestion locale ne peut pas connaitre le NOUVEAU domaine d'un site qui
  vient de demenager (il n'a jamais ete visite) : ce cas passe par le bouton de
  recherche, comme dans Chrome via Google.
- Le bouton « Essayer www... » peut lui-meme echouer (la barre reapparait alors
  avec la variante opposee) : l'utilisateur garde la main, pas de boucle
  automatique.
- La page d'erreur affichee sous la barre reste celle du moteur WebView2
  (marquee Edge) ; une page d'erreur maison est un chantier separe.

## Verification

- Build WinUI OK ; 333/333 tests verts dont 18 nouveaux
  (`SiteNotFoundRecoveryTests`).
- **Verification live reussie** (profil isole + mode invite, pilotage UIA par
  ValuePattern/InvokePattern) : navigation vers un domaine inexistant -> barre
  affichee avec les trois boutons ; « Fermer » masque la barre sans fermer
  l'onglet ni naviguer ; « Rechercher ce site sur le web » ouvre la recherche
  Google du domaine et la barre disparait. Captures dans le log.

Details : `logs/2026-07-14-site-introuvable-0-75-0.md`.
