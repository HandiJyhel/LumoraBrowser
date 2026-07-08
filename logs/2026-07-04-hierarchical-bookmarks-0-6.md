# 2026-07-04 - Favoris hierarchiques 0.6

## Probleme

La barre de favoris affichait encore des favoris importes par erreur parce que Pulse Browser utilisait une liste plate `favorites.tsv`. Cette approche ne conservait ni les dossiers, ni l'ordre, ni la separation entre `Barre des favoris` et `Autres favoris`.

## Changements

- Passage de la version projet a `0.6.0-dev`.
- Ajout de `src/bookmarks.rs` pour stocker des favoris hierarchiques locaux.
- Ajout du fichier local `bookmarks.tsv`.
- Migration prudente de l'ancien `favorites.tsv` sous `Autres favoris > Anciens favoris importes`.
- Import Chromium structure: barre, autres favoris, dossiers, sous-dossiers et ordre.
- La barre Win32 lit maintenant uniquement la racine `Barre des favoris`.
- Les dossiers de la barre ouvrent un menu local.
- Le menu principal expose une section `Favoris` avec dossiers et sous-dossiers.
- La page `Favoris locaux` affiche les racines et leur arborescence.
- Le bouton `Favori` ajoute ou retire la page courante dans la racine `Barre des favoris` du nouveau modele.

## Limites

- Firefox reste detecte mais non importe.
- Les icones de favoris ne sont pas encore recuperees.
- Le renommage et le deplacement graphique ne sont pas encore disponibles.

## Verification

- `cargo fmt --check`: reussi.
- `cargo test`: 41 tests passes.
- `cargo build`: reussi.
- Lancement court de `target\debug\pulse-browser.exe`: demarrage puis fermeture avec code `0`.
