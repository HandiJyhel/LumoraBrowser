# Centre Lumora 0.81.0-dev

## Intention

Les anciens parametres etaient fonctionnels, mais leur organisation restait
trop proche d'un navigateur classique : une liste de categories techniques,
avec des reglages empiles. Pour eloigner Lumora d'une experience type Google,
les reglages deviennent un centre de pilotage organise par intention
utilisateur.

## Nouvelle organisation visible

- `Vue d'ensemble` : point d'entree avec cartes d'acces rapide.
- `Mon Lumora` : personnalisation, ambiance, avatar, accueil, rythme visuel.
- `Profils locaux` : identite, verrouillage, PIN, recuperation, utilisateurs.
- `Confort` : lisibilite, focus, reduction des animations, aides vocales.
- `Espace de travail` : recherche, onglets, commandes rapides, productivite.
- `Ouverture` : comportement au demarrage.
- `Vie privee locale` : protections, sessions, cookies, exceptions.
- `Coffre et donnees` : mots de passe et portefeuille.
- `Stockage local` : dossier du profil, sauvegarde, import/export, nettoyage.

## Notes techniques

Les tags internes existants (`appearance`, `privacy`, `storage`, etc.) restent
en place pour limiter le risque. La refonte modifie le vocabulaire visible, le
point d'entree et la navigation, sans deplacer les handlers metier.

La page par defaut n'est plus une section de reglages : `Parametres` ouvre le
`Centre Lumora`, puis les cartes orientent l'utilisateur vers les zones utiles.
