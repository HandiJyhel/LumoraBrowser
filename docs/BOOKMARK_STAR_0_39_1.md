# Favori etoile complet (0.39.1-dev)

Cette passe corrige le bouton etoile de la surface active `PulseBrowser.WinUI`.

## Comportement

- Le bouton etoile n'ajoute plus silencieusement la page dans la barre des favoris.
- Un dialogue permet de modifier le nom du favori avant enregistrement.
- L'utilisateur choisit le dossier cible parmi l'arborescence existante.
- Si la page est deja en favori, le meme dialogue sert a modifier le favori existant.
- Pour un favori existant, le dialogue expose aussi l'action de suppression.
- L'icone locale deja connue est conservee ou rattachee au favori mis a jour.

## Donnees

Le stockage reste local dans le profil Pulse Browser. `BookmarkStore.AddOrUpdateUrl` met a jour le noeud existant quand il existe, ou cree un nouveau lien sinon. Un changement de dossier attribue une nouvelle position a la fin du dossier cible.

## Limite volontaire

Cette passe ne traite pas encore la recuperation en cas de mot de passe oublie, le multi-utilisateur, les moyens de paiement ou le rangement global des menus. Ces chantiers restent separes pour eviter une modification trop large.
