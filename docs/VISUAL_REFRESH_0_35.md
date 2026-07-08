# Alleger l'interface navigateur (0.35.0-dev)

Ce palier lance une premiere passe visuelle sur la surface active `PulseBrowser.WinUI`.

Objectif: rendre Pulse Browser plus agreable a regarder et a utiliser, avec un chrome moins lourd, sans retirer les fonctions deja disponibles.

## Changements

- Remplacement de la barre de menus permanente par un bouton menu compact dans la barre superieure.
- Ajout d'acces directs discrets pour nouvel onglet et accueil.
- Reduction des hauteurs de la barre d'onglets, de la barre d'adresse, de la barre de favoris et du statut.
- Remplacement du bouton texte `Ouvrir` par un bouton icone.
- Affinement des boutons de navigation, favoris, confidentialite et fermeture de palette.
- Barre de favoris plus dense: icone discrete, boutons plus bas, espacements reduits.
- Rail d'onglets verticaux moins large par defaut et moins rembourre.
- Barres de mots de passe/remplissage et pied de statut plus sobres.
- Palette de commande un peu plus compacte et moins opaque.

## Limites

- Cette passe ne change pas encore la structure profonde des panneaux internes comme parametres, historique, mots de passe ou centre du site.
- La validation visuelle interactive reste importante: un build ne suffit pas a juger l'equilibre final.
- Les onglets horizontaux gardent le controle `TabView` WinUI standard; une personnalisation plus poussee pourra venir plus tard si le rendu reste trop lourd.

