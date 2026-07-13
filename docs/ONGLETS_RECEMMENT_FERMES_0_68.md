# Lumora 0.68.0-dev - Rouvrir l'onglet ferme (Ctrl+Shift+T)

## Objectif

Restaurer en un geste un onglet ferme par erreur : reflexe universel des navigateurs, absent de Lumora jusqu'ici.

## Ce qui change

- `Ctrl+Shift+T` rouvre le dernier onglet ferme ; disponible aussi via le menu `Naviguer > Rouvrir l'onglet ferme` et la palette de commandes.
- La palette de commandes liste les onglets recemment fermes (categorie `Onglet ferme`) : on peut restaurer un onglet precis, pas seulement le dernier.
- La pile est bornee a 20 entrees (`ClosedTabHistory`, classe pure testee), le plus recent en tete.
- Les onglets restes sur l'accueil ne sont pas memorises : les rouvrir donnerait la meme chose que `Nouvel onglet`.
- L'onglet restaure retrouve son groupe si celui-ci existe encore, et son etat epingle.
- Le passage en mode invite vide la pile : les onglets du profil precedent ne sont pas restaurables.

## Limites connues

- La pile vit en memoire uniquement : au redemarrage, la restauration de session couvre deja les onglets encore ouverts ; conserver les onglets fermes sur disque serait une trace de navigation supplementaire, contraire a l'esprit du projet.
- L'historique de navigation interne de l'onglet (boutons precedent/suivant) n'est pas restaure, seulement sa derniere adresse.
