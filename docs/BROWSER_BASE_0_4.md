# Base navigateur 0.4

## Objectif

La version `0.4.0-dev` transforme le prototype Internet en base navigateur minimale. Le but n'est pas encore de finaliser l'interface WinUI 3, mais de donner a Pulse Browser des comportements de navigateur plus concrets autour de CEF.

## Ajouts

- page d'accueil locale chargee dans CEF au demarrage;
- bouton `Accueil`;
- bouton `Favori` pour ajouter ou retirer la page web courante;
- historique local des visites web;
- favoris locaux;
- page d'erreur locale quand CEF signale un echec de chargement;
- synchronisation visuelle de base pour `Retour`, `Avancer` et `Stop`;
- mise a jour de la barre d'adresse quand CEF charge une page web.

## Stockage local

Les nouvelles donnees sont stockees sous:

`%LOCALAPPDATA%\PulseBrowser\profiles\default\navigation`

Fichiers actuels:

- `history.tsv`;
- `favorites.tsv`.

Les pages internes chargees en `data:` ne sont pas ajoutees a l'historique ou aux favoris.

## Limites connues

- L'interface reste une coque Win32 provisoire.
- Les favoris n'ont pas encore de panneau de consultation.
- L'historique n'a pas encore d'ecran de consultation ou de suppression.
- Les onglets ne sont pas encore implementes.
- La page d'accueil et la page d'erreur sont generees localement sous forme de `data:`.

## Direction suivante

La prochaine couche logique sera soit les onglets simples, soit un premier ecran local de consultation/suppression de l'historique et des favoris. La transition WinUI 3 doit rester preparee, mais il vaut mieux continuer a stabiliser les comportements navigateur avant de deplacer toute l'interface.

## Nettoyage 0.4.1

La version `0.4.1-dev` nettoie la partie haute de la fenetre provisoire:

- suppression des textes techniques visibles dans la zone principale;
- barre navigateur compacte sur une seule ligne;
- zone Chromium remontee sous la barre;
- statut discret place en bas de fenetre;
- ajout d'un bouton `Menu`;
- menu local avec `Accueil`, favori, historique, favoris, donnees du profil et a propos.

Les ecrans `Historique local`, `Favoris locaux`, `Donnees du profil` et `A propos` sont charges comme pages locales generees par Pulse Browser. Ils ne remplacent pas encore une future interface WinUI 3, mais ils rendent la coque actuelle plus logique et moins bruyante.

## Suite 0.5.0

La version `0.5.0-dev` est documentee dans `docs/BROWSER_BASE_0_5.md`. Elle ajoute l'import de favoris Chrome/Edge/Brave/Chromium, une barre de favoris utilisable et un premier modele interne pour la future gestion des onglets.
