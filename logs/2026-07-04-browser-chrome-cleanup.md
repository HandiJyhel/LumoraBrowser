# Pulse Browser - Nettoyage de la chrome navigateur

Date: 2026-07-04

## Changements

- Passage de la version de `0.4.0-dev` a `0.4.1-dev`.
- Suppression des textes techniques visibles dans la zone principale de la fenetre.
- Reorganisation de la barre haute en une seule ligne de navigation.
- Ajout d'un bouton `Menu` a droite de la barre.
- Ajout d'un menu Win32 provisoire avec:
  - `Accueil`;
  - `Ajouter ou retirer le favori`;
  - `Historique local`;
  - `Favoris locaux`;
  - `Donnees du profil`;
  - `A propos de Pulse Browser`.
- Ajout de pages locales pour consulter l'historique, les favoris, les chemins de profil et les informations produit.
- Deplacement du statut en bas de fenetre pour eviter d'encombrer le haut de l'interface.
- La zone Chromium commence maintenant juste sous la barre de navigation.

## Limites connues

- L'interface reste une coque Win32 provisoire.
- Les boutons sont encore textuels et non iconographiques.
- Le menu est fonctionnel, mais pas encore le menu final WinUI 3.
- L'historique et les favoris sont consultables, mais pas encore supprimables depuis l'interface.

## Verifications

- `cargo fmt`: reussi.
- `cargo test`: reussi, 28 tests unitaires passes.
- `cargo build`: reussi.
- Lancement temporaire de `target\debug\pulse-browser.exe`: reussi, fermeture propre avec code de sortie `0`.
- Capture locale de verification: `artifacts\screenshots\pulse-browser-0.4.1-dev.png`.
