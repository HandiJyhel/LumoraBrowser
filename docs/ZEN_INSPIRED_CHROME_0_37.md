# Chrome integre et nouvel onglet personnalisable (0.37.0-dev)

Ce palier poursuit le travail visuel apres comparaison avec Chrome et observation de Zen Browser.

## Objectifs

- Supprimer l'effet de double barre Windows + navigateur.
- Garder une surface haute unique, plus proche d'un navigateur moderne.
- Ajouter une premiere version locale du mode compact inspire de Zen.
- Rendre le nouvel onglet personnalisable sans serveur ni compte.

## Changements

- Activation de `ExtendsContentIntoTitleBar` pour integrer le contenu dans la title bar Windows.
- Ajout d'une zone de drag dediee afin de ne pas casser les clics dans les onglets.
- Reservation automatique de la zone des boutons Windows a droite via les insets de title bar.
- Ajout d'un bouton mode compact dans la barre navigateur.
- Ajout d'un toggle `Mode compact inspire de Zen` dans les parametres.
- En mode compact, la barre de favoris est masquee et la barre de navigation est legerement reduite.
- Ajout de reglages de nouvel onglet:
  - titre affiche;
  - affichage des raccourcis;
  - liste de raccourcis editable localement au format `Nom | URL`.
- L'accueil `pulse://accueil` lit maintenant ces reglages et respecte le moteur de recherche choisi.

## Limites

- Le mode compact ne masque pas encore toute la barre de navigation au survol comme Zen; il reste volontairement recuperable.
- Glance, Split View et Workspaces restent des chantiers futurs.

