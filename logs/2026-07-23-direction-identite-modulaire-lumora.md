# 2026-07-23 - Direction identite modulaire Lumora (0.84.0.20-dev)

## Contexte

Apres les ajustements successifs de lisibilite entre theme clair et theme
sombre, un nouveau cadrage produit etait necessaire : sortir Lumora d'une
simple logique "clair/sombre" et poser une vraie identite graphique propre,
coherente avec son nom et distincte des navigateurs existants.

Le besoin exprime etait double :

- creer une signature visuelle Lumora fondee sur l'idee de lumiere ;
- permettre une personnalisation structurelle utile, notamment pour les
  onglets et les favoris.

## Travail realise

- relecture du contexte projet via `MEMORY.md` ;
- verification de la structure UI WinUI actuelle ;
- confirmation de l'existence des briques deja presentes :
  `BookmarksBarRow`, `VerticalTabsRail`, preferences persistantes de layout,
  moteur de palette `Nova*` ;
- redaction de `docs/DIRECTION_IDENTITE_MODULAIRE_0_84.md` pour formaliser la
  direction produit/UI recommandee.

## Decisions de cadrage retenues

- `Lumora` designe l'identite visuelle de base, pas seulement un mode sombre ;
- `clair` et `sombre` deviennent deux ambiances de luminosite de cette meme
  identite ;
- la modularite doit rester encadree : structure libre sur quelques zones
  fortes, pas refonte totale de l'interface ;
- les onglets sont recommandes en `haut` ou `gauche` dans un premier temps ;
- les favoris peuvent raisonnablement vivre en `haut`, `gauche`, `droite`,
  `bas` ou etre masques ;
- des presets d'espace de travail sont preferables a une personnalisation trop
  granulaire des le debut.

## Effet attendu

Ce cadrage donne une base claire pour les prochaines etapes UI :

- renforcer la signature visuelle Lumora sans casser la lisibilite ;
- separer proprement l'identite graphique du simple reglage de luminosite ;
- preparer une future implementation WinUI modulaire sans improvisation.

## Verification

- pas de test lance : aucun comportement runtime ni code produit modifies sur
  cette passe ;
- sortie concentree sur le cadrage, la documentation et l'alignement produit.

## Version

- `0.84.0.20-dev` - quatrieme chiffre uniquement, palier `0.84.0` inchange.
