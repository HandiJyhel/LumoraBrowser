# Base navigateur 0.5

La version `0.5.0-dev` ajoute une premiere couche de confort navigateur autour des favoris et prepare la future gestion des onglets.

## Import des favoris

Pulse Browser peut importer les favoris locaux des navigateurs Chromium detectes sur la machine:

- Google Chrome;
- Microsoft Edge;
- Brave;
- Chromium.

L'import lit les fichiers `Bookmarks` des profils `Default` et `Profile *`, puis ajoute uniquement les URLs web `http://` et `https://` aux favoris locaux Pulse Browser.

Les favoris deja presents ne sont pas ecrases. Les donnees restent locales et aucun favori n'est envoye vers un service Pulse Browser.

## Correction 0.5.1

La version `0.5.1-dev` corrige l'import trop silencieux de `0.5.0-dev`.

Le menu affiche maintenant les sources detectees une par une, avec le navigateur, le profil et le nombre de favoris lisibles. Pulse Browser n'importe plus automatiquement tous les profils possibles en une seule action.

Deux actions sont disponibles:

- importer depuis une source precise, pour ajouter les favoris manquants sans toucher aux favoris Pulse existants;
- remplacer les favoris Pulse par une source precise, avec sauvegarde locale automatique du fichier `favorites.tsv` precedent.

Cette correction evite de melanger sans explication un ancien profil Chrome/Chromium avec le profil Google Chrome utilise au quotidien.

## Correction 0.5.2

La version `0.5.2-dev` corrige la couche de gestion qui manquait encore autour des favoris.

Le menu contient maintenant une section `Gerer les favoris Pulse`:

- suppression de tous les favoris Pulse, avec sauvegarde locale automatique;
- suppression individuelle des premiers favoris visibles dans le menu;
- ouverture automatique de la page `Favoris locaux` apres import, remplacement, suppression ou vidage.

L'import detecte aussi plus de navigateurs:

- Google Chrome;
- Microsoft Edge;
- Brave;
- Chromium;
- Vivaldi;
- Opera;
- Opera GX.

Firefox est detecte comme source non encore importable quand un profil local existe. Son import necessite un traitement SQLite dedie via `places.sqlite`, donc il reste volontairement separe tant que cette voie n'est pas implementee proprement.

Limite actuelle: Firefox n'est pas encore importe directement, car ses favoris principaux passent par `places.sqlite` et parfois par des sauvegardes compressees. Il faudra ajouter cette voie proprement avec une dependance ou un lecteur SQLite justifie.

## Suite 0.6.0

La version `0.6.0-dev` remplace le modele de favoris plats par une arborescence locale documentee dans `docs/BOOKMARKS_0_6.md`.

## Barre des favoris

La fenetre provisoire Win32 affiche maintenant une barre de favoris sous la barre d'adresse.

- Les premiers favoris locaux sont exposes sous forme de boutons directs.
- Un clic sur un favori charge la page dans la vue CEF embarquee.
- Le menu permet d'afficher ou masquer cette barre pendant la session.
- Ajouter ou retirer un favori met la barre a jour.
- Importer des favoris met la barre a jour.

Cette barre reste volontairement simple pour la phase Win32. Elle donne le comportement attendu sans figer le futur design WinUI 3.

## Fondation des onglets

Le module `src/tabs.rs` introduit un modele interne de gestion des onglets:

- identifiant stable par onglet;
- onglet actif;
- ouverture, activation et fermeture;
- mise a jour URL/titre/chargement;
- preference de disposition horizontale ou verticale.

Ce modele n'est pas encore raccorde a plusieurs instances CEF visibles. La prochaine etape onglets devra creer une vraie couche de rendu multi-onglets, pas seulement une interface factice.
