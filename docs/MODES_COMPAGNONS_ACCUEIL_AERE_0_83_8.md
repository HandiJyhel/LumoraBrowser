# Modes compagnons et accueil aere 0.83.8-dev

Cette etape poursuit les modes Lumora dans deux directions : rendre l'accueil
moins compact et poser la premiere base de compagnons de mode disponibles
pendant la navigation.

## Accueil

`lumora://accueil` n'empile plus tout dans une colonne centrale. Sur desktop,
la page se divise en deux zones :

- a gauche : marque Lumora, recherche et reperes personnels ;
- a droite : mode actif, presentation et outil contextuel.

Sur petite largeur, la page redevient une colonne simple pour garder une
lecture confortable.

## Compagnon de mode

Un bouton `Compagnon du mode` est ajoute dans le chrome permanent, a cote du
bouton `Mode d'usage`. Il reste disponible pendant la navigation web.

Le compagnon adapte son icone, son texte et ses actions au mode actif :

- `Equilibre` : modules et actions rapides ;
- `Focus` : palette de commande et plein ecran ;
- `Lecture` : mode lecture et notes ;
- `Creation` : post-it/notes et relance d'idee ;
- `Recherche` : historique local et sources gardees ;
- `Nuit` : lecture douce et voix locale.

## Donnees

Le compagnon ouvre les modules locaux existants. Il ne cree aucun service
distant et n'envoie aucune donnee utilisateur vers Lumora.

## Version

`0.83.8-dev`
