# Identite Lumora 0.79.0-dev

## Intention

La nouvelle identite graphique de Lumora doit rendre visible le sens du nom :
lumiere, clarte et navigation web. Le logo n'est plus un simple marqueur
abstrait ; il associe un coeur lumineux, des arcs de globe et une trajectoire
de navigation.

## Signe

- Coeur lumineux : represente la lumiere et l'idee d'un navigateur qui eclaire
  sans rendre la securite pesante.
- Arcs de globe : rendent l'internet lisible sans copier les icones de
  navigateurs existants.
- Trajectoire locale : rappelle que Lumora garde ses donnees sur l'ordinateur
  de l'utilisateur.
- Palette : fond bleu-profond, lumiere jaune, cyan de navigation et accent
  chaud secondaire.

## Surfaces raccordees

- `Lumora.WinUI/Assets/LumoraApp.png`
- `Lumora.WinUI/Assets/LumoraApp.ico`
- page d'accueil locale `lumora://accueil`
- page `A propos`
- barre plein ecran
- ecrans de connexion et de premier lancement
- fenetre d'application web Lumora
- page d'accueil de navigation privee
- installateur WinForms actif

## Notes techniques

Le logo reste genere par `scripts/generate-app-icon.ps1` afin de garder une
source reproductible pour le PNG et l'ICO. Les rendus simplifies de l'ICO
conservent seulement les elements les plus lisibles aux petites tailles :
soleil central et arcs principaux.
