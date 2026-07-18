# Modes d'usage et accueil vivant 0.82.0-dev

## Intention

Lumora doit aller plus loin que la personnalisation classique. L'utilisateur ne
doit pas seulement choisir une couleur : il doit pouvoir donner une posture a
son navigateur selon son moment de travail.

Ce palier ajoute un premier `Mode d'usage` visible sur l'accueil Lumora. Il
prepare les futurs Espaces Lumora sans creer encore des espaces separes avec
leurs propres onglets.

## Modes disponibles

- `Equilibre` : navigation quotidienne.
- `Focus` : reprise rapide et reduction du bruit visuel.
- `Lecture` : ambiance calme pour pages longues, lecture et annotations.
- `Creation` : point de depart plus expressif pour idees et notes.
- `Recherche` : collecte, comparaison et sources.
- `Nuit` : rythme plus doux pour navigation tardive.

## Surfaces touchees

- `UiSettings.UsageMode` persiste le mode dans le profil local.
- `Parametres > Mon Lumora` expose le selecteur `Mode d'usage`.
- `lumora://accueil` affiche une capsule de mode avec salutation locale,
  intention du mode et actions suggerees.

## Limites

Les actions suggerees sont volontairement informatives dans ce palier. La suite
logique est de les raccorder aux modules, groupes d'onglets ou espaces de
travail quand ces surfaces seront reorganisees autour des modes.
