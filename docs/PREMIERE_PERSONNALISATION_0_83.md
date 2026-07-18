# Premiere personnalisation 0.83.1-dev

## Objectif

Transformer la creation d'un profil Lumora en experience choisie plutot qu'en
interface pre-remplie.

Un nouveau profil part maintenant d'un espace volontairement epure :

- aucun module visible n'est epingle par defaut ;
- aucun raccourci de nouvel onglet n'est impose ;
- le wizard de premier lancement invite a choisir le moteur de recherche, le
  mode d'usage et les modules visibles ;
- l'accueil affiche une invitation a construire son Lumora quand le profil ne
  contient encore ni module epingle ni raccourci visible.

## Preservation des profils existants

Les anciens profils qui possedaient deja une liste `PinnedModuleIds` conservent
leur etat.

Les profils plus anciens dont le fichier de reglages ne contenait pas encore
la cle `PinnedModuleIds` gardent les modules historiques par migration, afin de
ne pas vider une interface existante par surprise.

## Comportement utilisateur

Depuis l'accueil epure, l'utilisateur peut ouvrir directement :

- `Mon Lumora` pour choisir son mode, son style, ses animations et ses
  raccourcis ;
- `Modules Lumora` pour epingler uniquement les outils qu'il veut voir dans la
  barre.

Tout reste local au profil et ne cree aucun service distant.
