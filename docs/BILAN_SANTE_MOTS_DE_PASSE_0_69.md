# Lumora 0.69.0-dev - Bilan de sante des mots de passe

## Objectif

Exploiter le coffre local pour alerter l'utilisateur sur ses mots de passe a risque, sans qu'aucune donnee (ni meme un hash) ne quitte la machine.

## Ce qui change

- Nouveau bouton `Bilan de sante` dans l'en-tete du gestionnaire de mots de passe (et entree dans la palette de commandes).
- L'analyse (`PasswordHealthAnalyzer`, classe pure testee) signale trois familles de problemes :
  - **Reutilises** : meme mot de passe exact sur au moins deux sites distincts. Deux comptes du meme site partageant un mot de passe ne sont pas signales (cas volontaire frequent).
  - **Faibles** : moins de 8 caracteres, une seule classe de caracteres, caractere unique repete, ou present dans une petite liste embarquee de mots de passe courants (azerty, 123456, motdepasse...).
  - **Anciens** : non modifies depuis plus de 2 ans. Les entrees sans date (imports historiques) ne sont pas signalees.
- Le rapport s'affiche dans une boite de dialogue : les mots de passe eux-memes ne sont jamais affiches, seuls les comptes concernes sont nommes.
- Acces protege par la meme barriere que le coffre (le bouton vit dans le panneau coffre, deja soumis au PIN/mot de passe).

## Limites connues

- Pas de verification `compromis dans une fuite` (type Have I Been Pwned) : meme en k-anonymat cela exige une requete sortante, exclue par la regle du projet tant que non explicitement validee.
- Le rapport est informatif : pas encore d'action directe `changer ce mot de passe` depuis la liste (on peut retrouver l'entree via la recherche du coffre).
