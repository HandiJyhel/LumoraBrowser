# Chrome visuel des modes 0.83.6-dev

Cette etape prolonge l'identite visuelle des modes au chrome permanent de
Lumora.

## Objectif

Un mode ne doit pas etre visible uniquement sur `lumora://accueil`. La barre
principale, la title bar, les onglets, le rail vertical et le bouton de mode
doivent aussi porter une ambiance identifiable.

## Comportement

- `Equilibre` conserve la signature Lumora standard.
- `Focus` resserre la perception : chrome plus direct, accent cyan net.
- `Lecture` adoucit les surfaces et les contrastes.
- `Creation` rend le chrome plus expressif sans modifier les workflows.
- `Recherche` donne une impression plus structuree et orientee collecte.
- `Nuit` baisse la presence lumineuse et calme le haut de fenetre.

## Accessibilite

Le contraste renforce reste prioritaire : il force une palette lisible noir,
blanc et accent clair. Le focus clavier reste visible via les ressources
partagees `NovaFocusBrush` et `NovaFocusInnerBrush`.

## Version

`0.83.6-dev`
