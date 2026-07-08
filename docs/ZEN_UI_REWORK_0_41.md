# Retouche Zen UI locale (0.41.0-dev)

Cette passe repond a trois retours utilisateur sur `PulseBrowser.WinUI` :

- le rendu translucide devait repartir de l'approche deja validee dans Pulse Explorer ;
- le menu des trois points etait trop charge ;
- les raccourcis de la page d'accueil devaient devenir comprehensibles pour un utilisateur normal.

## Translucidite

Le reglage `Effet translucide` conserve les choix `Desactive`, `Mica` et `Acrylic`, mais il ne repose plus uniquement sur le backdrop WinUI.

Pulse Browser applique maintenant aussi une transparence de fenetre Win32 via :

- `WS_EX_LAYERED`
- `SetLayeredWindowAttributes`

Un curseur `Intensite de transparence` a ete ajoute dans `Parametres > Apparence`.

En contraste renforce, les effets translucides sont ignores et la fenetre revient au rendu solide.

## Menu Pulse

Le menu des trois points ne liste plus les panneaux de gestion avancée. Il garde uniquement :

- nouvel onglet ;
- accueil ;
- site actuel ;
- favoris ;
- historique ;
- telechargements ;
- parametres ;
- a propos.

Les zones comme le coffre, les sites connectes et les passkeys restent accessibles par les panneaux dedies et la palette de commande.

## Raccourcis d'accueil

`pulse://accueil` affiche maintenant des tuiles de raccourcis editables :

- ajout depuis une tuile `Ajouter` ;
- modification depuis une action de tuile ;
- suppression depuis une action de tuile ;
- persistance dans `UiSettings.NewTabShortcuts`.

Les modifications rechargent les pages d'accueil ouvertes pour refléter le changement immédiatement.

