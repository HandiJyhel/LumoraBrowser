# 2026-07-04 - Correction de l'import des favoris

## Probleme

Le bouton `Importer les favoris` de `0.5.0-dev` importait automatiquement les favoris depuis toutes les sources Chromium detectees.

Ce comportement etait trop opaque:

- aucun choix de navigateur ou de profil n'etait propose;
- un ancien profil Chrome/Chromium pouvait etre importe sans que l'utilisateur le voie;
- les favoris Pulse existants pouvaient rester melanges avec les favoris importes.

## Correction

- Passage de la version projet a `0.5.1-dev`.
- Remplacement de l'import global par une liste explicite de sources dans le menu.
- Affichage du navigateur, du profil et du nombre de favoris lisibles pour chaque source.
- Ajout d'un mode `Importer`, qui fusionne avec les favoris Pulse existants.
- Ajout d'un mode `Remplacer`, qui remplace `favorites.tsv` par la source choisie.
- Creation d'une sauvegarde locale automatique de `favorites.tsv` avant un remplacement.
- Conservation du stockage local: aucune donnee de favoris n'est envoyee a un serveur Pulse Browser.

## Limites

- L'interface Win32 reste provisoire.
- Il n'y a pas encore de fenetre de confirmation detaillee avant remplacement; la sauvegarde locale sert de filet de securite.
- Firefox reste a traiter separement.

## Verification

- `cargo fmt --check`: reussi.
- `cargo test`: 35 tests passes.
- `cargo build`: reussi apres fermeture des instances Pulse Browser qui verrouillaient `target\debug\pulse-browser.exe`.
- Lancement court de `target\debug\pulse-browser.exe`: demarrage puis fermeture avec code `0`.
