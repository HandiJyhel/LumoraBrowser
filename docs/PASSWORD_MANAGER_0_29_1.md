# Pulse Browser - Password Manager 0.29.1-dev

`0.29.1-dev` corrige le flux essentiel: si le gestionnaire possede un identifiant pour un site, Pulse doit le proposer des l'etape de connexion.

## Corrections

- Un champ identifiant visible suffit maintenant a proposer le remplissage quand un identifiant existe dans `vault.pulse`.
- Le script de remplissage sait remplir seulement l'identifiant quand le champ mot de passe n'est pas encore visible.
- Quand le champ mot de passe apparait ensuite, le meme flux peut proposer le remplissage complet.
- Les champs de recherche, promo, code ou quantite sont moins susceptibles d'etre confondus avec un champ de connexion.
- Une capture d'identifiant deja present avec le meme mot de passe ne repropose plus l'enregistrement.
- Une capture d'identifiant deja present avec un mot de passe different propose une mise a jour.

## Regle produit

Le gestionnaire de mots de passe n'est utile que s'il intervient au moment de la connexion:

1. chercher par domaine racine;
2. proposer l'identifiant si seul l'e-mail est visible;
3. proposer le mot de passe quand le champ apparait;
4. ne pas creer de doublon si le coffre possede deja la meme entree.
