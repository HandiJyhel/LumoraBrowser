# Centre du site actuel (0.33.0-dev)

## Objectif

Le centre du site actuel donne a Pulse Browser une vue locale et actionnable du domaine ouvert dans l'onglet visible.

Il regroupe dans une seule surface :

- le domaine racine calcule via la Public Suffix List ;
- l'etat de protection du bouclier ;
- les cookies de session du domaine ;
- le statut de confiance de la session ;
- les identifiants locaux connus dans `vault.pulse` ;
- l'historique local du site.

## Acces

Deux entrees sont disponibles dans `PulseBrowser.WinUI` :

- bouton bouclier de la barre d'adresse, puis `Centre du site` ;
- menu `Outils > Site actuel`.

La coque Win32 archivee n'est pas modifiee.

## Actions

Depuis le panneau, l'utilisateur peut :

- revenir a la page web active ;
- actualiser les informations du site ;
- conserver ou non la session du domaine au demarrage ;
- oublier immediatement les cookies du domaine courant ;
- ouvrir le gestionnaire de mots de passe filtre sur ce domaine ;
- ouvrir l'historique filtre sur ce domaine ;
- rouvrir les dernieres pages connues du site.

## Securite et confidentialite

Le panneau n'envoie aucune donnee a un service externe. Les informations viennent uniquement de l'etat local deja gere par Pulse Browser :

- cookies WebView2 du profil local ;
- `vault.pulse` pour les identifiants ;
- `history.pulse` pour l'historique ;
- `ui-settings.pulse` pour les sites de confiance et la whitelist privacy.

L'ouverture des identifiants respecte la barriere existante du coffre : PIN ou mot de passe selon le profil.

## Limites

- Le compteur du bouclier reste le compteur de la page visible. La limite connue de `0.32.0-dev` peut encore faire remonter des requetes d'arriere-plan via le moteur privacy global.
- Le panneau ne liste pas encore le detail exact des cookies, permissions WebView2 ou stockages locaux par site. Il donne une premiere surface produit claire et actionnable.
- Un test interactif reste utile pour valider le rendu exact avec une vraie page connectee.
