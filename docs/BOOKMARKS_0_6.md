# Favoris hierarchiques 0.6

La version `0.6.0-dev` remplace la logique de favoris plats par un modele local hierarchique.

## Objectif

Pulse Browser ne doit plus afficher les premiers favoris importes au hasard dans la barre. La barre doit correspondre a une vraie racine `Barre des favoris`, tandis que les autres favoris restent organises dans `Autres favoris` ou dans leurs dossiers importes.

## Stockage

Le nouveau fichier local est:

`%LOCALAPPDATA%\PulseBrowser\profiles\default\navigation\bookmarks.tsv`

Chaque entree contient:

- un identifiant local;
- un parent;
- un type `folder` ou `url`;
- une position;
- un titre;
- une URL pour les favoris web.
- eventuellement un chemin local de favicon, ajoute par la coque WinUI quand l'icone du site est disponible.

Le fichier historique `favorites.tsv` reste conserve pour compatibilite. Au premier demarrage avec le nouveau modele, ses entrees sont migrees sous `Autres favoris > Anciens favoris importes`, afin de ne plus polluer la barre des favoris.

## Import navigateur

L'import Chromium conserve maintenant:

- `Barre des favoris`;
- `Autres favoris`;
- dossiers;
- sous-dossiers;
- ordre des elements;
- favoris web `http://` et `https://`.

Les sources supportees restent Chrome, Edge, Brave, Chromium, Vivaldi, Opera et Opera GX. Firefox est detecte mais pas encore importe, car son stockage principal passe par `places.sqlite`.

## Interface produit

La coque WinUI 3 est l'interface produit active et utilise le nouveau modele:

- la barre affiche uniquement les enfants de `Barre des favoris`;
- un bouton de dossier dans la barre ouvre un menu local pour ce dossier;
- le menu principal expose une section `Favoris` avec dossiers et sous-dossiers;
- la page `Favoris locaux` affiche les deux racines et leurs contenus.

## Limites

- La gestion visuelle active se trouve maintenant dans WinUI 3; l'ancien prototype Win32 est archive comme reference technique.
- Les favicons peuvent etre cachees localement par la coque WinUI quand WebView2 les fournit, mais l'import depuis les bases de favicons des navigateurs installes reste a implementer.
- Le deplacement/renommage graphique des favoris n'est pas encore disponible.
- L'import Firefox reste a implementer proprement.
