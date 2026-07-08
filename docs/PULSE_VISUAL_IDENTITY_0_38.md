# Direction visuelle Pulse Browser (0.38.0-dev)

Ce palier installe une intention visuelle plus nette pour Pulse Browser apres les passes de comparaison avec Chrome et Zen.

## Objectifs

- Eviter l'impression de navigateur lourd ou empile.
- Donner au chrome navigateur une surface unique, calme et coherente.
- Utiliser l'orange Pulse comme accent de signature, pas comme couleur dominante.
- Rendre le nouvel onglet plus personnel sans ajouter de service distant.

## Changements

- Nouvelle palette d'interface: charbon chaud, surfaces legerement separees et accent orange plus ponctuel.
- Boutons du chrome plus compacts et moins presents.
- Barre d'adresse plus douce, avec texte et placeholder adaptes a la palette Pulse.
- Title bar Windows raccordee a la nouvelle couleur du chrome.
- Barre des favoris et rail d'onglets verticaux harmonises avec la surface haute.
- Ajustement du mode compact pour garder les nouveaux espacements.
- Refonte visuelle de `pulse://accueil`:
  - marque Pulse plus compacte;
  - ligne d'accent discrete;
  - recherche plus lisible;
  - raccourcis plus nets, avec tuiles arrondies et libelles tronques proprement.

## Limites

- Les onglets natifs WinUI restent encore dependants du rendu `TabView`; une passe dediee pourra affiner l'onglet actif et les onglets inactifs.
- Les animations et micro-interactions restent minimalistes pour eviter d'ajouter du bruit avant stabilisation du style.
