# 2026-07-04 - Import favoris, barre favoris et base onglets

## Objectif

Commencer la transition de Pulse Browser vers un navigateur plus quotidien apres `0.4.1-dev`:

- importer des favoris depuis les navigateurs locaux les plus probables;
- afficher une barre de favoris utilisable;
- preparer une base interne pour les onglets et les onglets verticaux.

## Changements

- Passage de la version projet a `0.5.0-dev`.
- Ajout de `src/bookmarks_import.rs`.
- Import direct des fichiers `Bookmarks` Chromium locaux pour Chrome, Edge, Brave et Chromium.
- Ajout d'un import en lot dans `src/browser_data.rs`, avec detection des doublons et rejet des URLs non web.
- Ajout d'une barre de favoris Win32 sous la barre d'adresse.
- Ajout de boutons de favoris directs, rafraichis apres import ou ajout/retrait de favori.
- Ajout d'une commande de menu pour importer les favoris.
- Ajout d'une commande de menu pour afficher ou masquer la barre des favoris pendant la session.
- Ajout de `src/tabs.rs`, modele interne teste pour la future gestion des onglets horizontaux et verticaux.

## Limites

- Firefox n'est pas encore importe directement.
- La barre de favoris n'est pas encore persistante dans les preferences UI.
- Les onglets ne sont pas encore visibles ni raccordes a plusieurs vues CEF.

## Verification

- `cargo fmt --check`: reussi.
- `cargo test`: 34 tests passes.
- `cargo build`: reussi.
- Lancement court de `target\debug\pulse-browser.exe`: demarrage puis fermeture avec code `0`.
