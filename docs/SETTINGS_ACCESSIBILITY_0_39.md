# Parametres, personnalisation et accessibilite (0.39.0-dev)

Ce palier transforme les reglages visuels recents en options utilisateur plus explicites et commence une vraie base d'accessibilite.

## Objectifs

- Eviter que `Ctrl+K` s'affiche dans des contextes de saisie ou de page web sans intention claire.
- Donner une place dediee a la personnalisation de l'apparence.
- Ajouter des options d'accessibilite visibles et persistantes.
- Garder les choix locaux au profil Pulse, sans compte ni serveur.

## Changements

- Ajout d'une rubrique `Apparence` dans les parametres.
- Ajout d'une rubrique `Accessibilite` dans les parametres.
- Deplacement de la personnalisation du nouvel onglet vers `Apparence`.
- Ajout des options persistantes pour `Ctrl+K`:
  - activer/desactiver la palette;
  - autoriser ou non l'ouverture depuis les pages web;
  - autoriser ou non l'ouverture pendant la saisie dans un champ texte.
- `Ctrl+K` ne vole plus le focus dans la barre d'adresse, les champs texte ou les pages web par defaut.
- Ajout d'options d'accessibilite:
  - contraste renforce;
  - texte plus lisible;
  - reduction des transitions;
  - focus clavier plus visible.
- `pulse://accueil` respecte les options de contraste, texte plus lisible, focus visible et reduction des transitions.

## Limites

- Les lecteurs d'ecran et libelles `AutomationProperties` devront recevoir une passe dediee.
- Les options d'accessibilite ne modifient pas encore chaque panneau secondaire en profondeur.
- Le choix d'un raccourci clavier personnalise pour la palette reste a faire.
