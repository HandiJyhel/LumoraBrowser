# Modes avec outils contextuels 0.83.7-dev

Cette etape transforme les modes d'usage Lumora en petits contextes utiles,
pas seulement en ambiances visuelles.

## Objectif

Quand l'utilisateur active un mode, l'accueil doit expliquer ce qui change et
proposer au moins une action directement utile.

## Comportement

- `Equilibre` garde une note rapide neutre.
- `Focus` affiche un objectif de maintenant.
- `Lecture` affiche un marque-page de lecture.
- `Creation` affiche un post-it local pour capturer une idee.
- `Recherche` affiche une piste ou source a verifier.
- `Nuit` affiche un rappel calme pour plus tard.

Chaque note rapide est envoyee au module Notes Lumora via le `NoteStore` local
du profil. Aucune donnee n'est envoyee a un serveur Lumora.

## Presentation de mode

Une presentation courte apparait sur l'accueil quand un mode n'a pas encore ete
presente. Le bouton `Compris` memorise le mode presente dans les preferences
locales. Si l'utilisateur change de mode, la presentation revient pour le
nouveau contexte.

## Accessibilite

Les nouveaux blocs utilisent des libelles `aria-label`, un retour `aria-live`
pour l'enregistrement de note, le focus visible existant et les regles de
reduction de mouvement deja appliquees a l'accueil Lumora.

## Version

`0.83.7-dev`
