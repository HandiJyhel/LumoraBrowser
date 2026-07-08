# Palette de commande (0.34.0-dev)

## Objectif

La palette de commande donne a Pulse Browser un acces rapide et moderne aux actions et contenus locaux.

Raccourci principal :

- `Ctrl+K`

## Contenu recherche

La premiere version cherche localement dans :

- les commandes principales de Pulse Browser ;
- les onglets ouverts ;
- les favoris ;
- l'historique local ;
- une adresse ou une recherche web saisie directement.

## Commandes integrees

La palette expose notamment :

- nouvel onglet ;
- accueil ;
- site actuel ;
- parametres ;
- gestionnaire de mots de passe ;
- historique ;
- telechargements ;
- sites connectes ;
- cles d'acces ;
- ajouter aux favoris ;
- a propos.

## Navigation

- `Entree` ouvre le resultat selectionne.
- `Echap` ferme la palette.
- Fleche bas depuis le champ de recherche passe a la liste.
- Double-clic sur un resultat l'ouvre.

## Vie privee

Tout est local. La palette lit uniquement les donnees deja presentes dans Pulse Browser :

- onglets en memoire ;
- favoris locaux ;
- historique local ;
- commandes internes.

Elle ne contacte aucun service externe pour suggerer des resultats.

## Limites

- La palette ne cherche pas encore dans le contenu des pages.
- Elle ne cherche pas encore dans les mots de passe, pour eviter d'exposer trop vite du contenu sensible dans un champ global.
- Selon le focus WebView2, le raccourci `Ctrl+K` devra etre teste en navigation reelle. Un `KeyboardAccelerator` WinUI a ete ajoute pour rendre le raccourci plus global que le simple `KeyDown`.
