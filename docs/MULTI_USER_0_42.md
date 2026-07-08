# Multi-utilisateur local (0.42.0-dev)

Cette passe met en place la base multi-utilisateur de `PulseBrowser.WinUI` sans compte distant et sans synchronisation serveur.

## Principe

Chaque profil local a son propre dossier sous :

`%LOCALAPPDATA%\PulseBrowser\profiles\<id>`

Le profil `default` reste le profil de depart. Les nouveaux profils recoivent un identifiant stable derive du nom choisi, nettoye pour rester compatible avec un nom de dossier.

Les profils avec emplacement personnalise restent supportes. Quand un emplacement personnalise est actif, il est affiche comme profil personnalise dans le selecteur.

## Experience utilisateur

Au demarrage, Pulse Browser affiche le selecteur de profil quand plusieurs profils locaux sont disponibles.

Depuis `Parametres > Profil`, l'utilisateur peut :

- changer de profil ;
- creer un autre profil ;
- voir le dossier du profil actif ;
- conserver les actions existantes de nom, mot de passe, PIN, cle de recuperation et reinitialisation.

## Isolation

Les donnees restent locales et separees par profil :

- favoris ;
- historique ;
- onglets ;
- coffre ;
- identifiants ;
- reglages ;
- favicons et donnees de navigation.

Le changement de profil redemarre l'application quand le dossier cible est different du dossier charge au lancement. Ce redemarrage est volontaire : il evite de garder en memoire des stores ouverts sur l'ancien profil.

## Limites volontaires

Cette version ne cree pas encore de panneau avance de suppression/renommage de tous les profils. La reinitialisation actuelle concerne le profil actif.

La synchronisation et les comptes Pulse Browser restent exclus du cadre produit actuel.
