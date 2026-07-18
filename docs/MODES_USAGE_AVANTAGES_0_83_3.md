# Modes d'usage avec avantages 0.83.3-dev

Lumora passe de modes surtout descriptifs a des modes qui modifient vraiment
l'experience.

## Objectif

Le retour utilisateur etait clair : changer de mode doit donner un avantage
visible, pas simplement changer un titre sur l'accueil.

## Comportement

- `Equilibre` revient a une experience standard, lisible et polyvalente.
- `Focus` reduit le bruit : accueil minimal, recherche focalisee, interface
  compacte et palette de commande activee.
- `Lecture` calme l'accueil et met en avant les outils de lecture, notes et
  lecture a voix haute.
- `Creation` rend l'accueil plus reactif et rapproche les notes, l'assistant de
  recherche et les reperes rapides.
- `Recherche` prepare la collecte : onglets verticaux, favoris visibles,
  suggestions locales et outils de sources.
- `Nuit` force une posture sombre, plus douce, compacte et orientee lecture.

Chaque mode ajoute une zone d'actions propres sur `lumora://accueil`, avec des
boutons directement branchés aux panneaux ou modules utiles.

## Profil Bob

Le profil local actif `default`, utilise comme profil test Bob, a ete supprime
de `%LOCALAPPDATA%\Lumora\profiles`. La configuration reste pointee vers
`default`, donc Lumora ne retrouvera plus Bob au prochain demarrage.

Le profil `testcoffre` n'a pas ete supprime.

## Version

`0.83.3-dev`
