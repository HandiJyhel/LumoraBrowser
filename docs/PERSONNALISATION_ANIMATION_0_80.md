# Personnalisation et animation Lumora 0.80.0-dev

## Intention

Lumora ne doit pas rester une coque de navigateur generique avec quelques
couleurs. La personnalisation doit aider l'utilisateur a se sentir a l'aise et
productif, tout en gardant l'identite locale-first du projet.

Ce premier palier ajoute une personnalite visuelle reglable sans dependance
externe et sans changer le moteur WebView2.

## Reglage ajoute

Dans `Parametres > Personnalisation`, le bloc `Personnalite Lumora` ajoute le
reglage `Animations et reactions` :

- `Discret` : entrees et transitions courtes.
- `Lumineux` : rythme par defaut, respiration douce du logo et ligne lumineuse.
- `Dynamique` : accueil plus vivant, trace lumineuse plus presente et entree
  plus marquee.

Le reglage d'accessibilite `Reduire les animations` reste prioritaire et force
un rendu statique de l'accueil.

## Surfaces touchees

- `UiSettings.PersonalizationMotionStyle` persiste le choix par profil.
- La section Personnalisation charge et sauvegarde le reglage avec le bouton
  global `Appliquer les changements`.
- `lumora://accueil` applique le rythme choisi au logo, a la ligne de lumiere,
  a la recherche et aux raccourcis.

## Limites

Ce palier cible d'abord l'accueil Lumora, car c'est la surface la plus visible
et la plus sure pour introduire du mouvement. Les animations WinUI des panneaux,
du rail et des modules pourront etre etendues ensuite avec le meme reglage.
