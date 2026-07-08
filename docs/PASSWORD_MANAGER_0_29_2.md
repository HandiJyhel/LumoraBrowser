# Pulse Browser - Password Manager 0.29.2-dev

`0.29.2-dev` corrige un faux positif observe sur Micromania: Pulse indiquait que l'identifiant etait rempli alors que le champ de connexion restait vide.

## Cause

La page contenait plusieurs champs e-mail visibles. Le script pouvait choisir un champ de newsletter ou de footer au lieu du champ du panneau de connexion.

L'application annonçait ensuite un succes parce qu'une valeur avait ete assignee a un champ, sans verifier que le champ cible etait bien le champ de connexion ni que l'ecriture etait acceptee.

## Corrections

- Le remplissage cible maintenant seulement les champs visibles et actionnables au premier plan (`elementFromPoint`).
- Les champs situes derriere un overlay ne sont plus consideres comme des cibles valides.
- Les contextes newsletter, offres, marketing, footer, presse, recrutement, paiement, promo, code ou recherche sont fortement penalises.
- Les contextes connexion, compte, auth, login, continuer et mot de passe sont favorises.
- Apres ecriture, le script verifie que la valeur du champ correspond reellement a la valeur attendue.
- Si le site refuse l'ecriture, Pulse affiche un echec au lieu d'un faux succes.

## Objectif

Sur un panneau de connexion comme Micromania, le champ e-mail du panneau lateral doit etre choisi avant les champs newsletter de la page de fond.
