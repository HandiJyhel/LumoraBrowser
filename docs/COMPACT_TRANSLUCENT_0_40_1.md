# Interface compacte et translucidite (0.40.1-dev)

Cette passe corrige le comportement du mode compact et ajoute une premiere option de translucidite dans `PulseBrowser.WinUI`.

## Correction favoris

Le mode compact ne masque plus automatiquement la barre des favoris.

Avant cette correction, l'interface compacte forcait :

`Barre favoris visible = false si mode compact actif`

Ce comportement rendait les favoris inutilisables dans un mode cense ameliorer l'ergonomie. A partir de `0.40.1-dev`, la barre de favoris suit uniquement son reglage principal.

Un nouveau reglage separe existe dans `Parametres > Apparence` :

- `Masquer les favoris en interface compacte`

Il est desactive par defaut. L'utilisateur peut donc choisir explicitement un mode plus concentre sans perdre les favoris par surprise.

## Translucidite

`Parametres > Apparence` propose maintenant `Effet translucide` :

- `Desactive`
- `Mica`
- `Acrylic`

Pulse Browser applique le backdrop WinUI correspondant quand Windows le permet. Si le rendu n'est pas disponible, l'application revient au rendu solide sans bloquer le demarrage.

Les surfaces du chrome Pulse deviennent legerement transparentes pour laisser vivre l'effet, tout en conservant une lisibilite correcte.

