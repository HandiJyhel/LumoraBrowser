# 2026-07-04 - Gestion utilisable des favoris et import elargi

## Probleme

Le correctif `0.5.1-dev` rendait les sources d'import plus explicites, mais la couche restait insuffisante cote produit:

- les favoris deja importes par erreur restaient difficiles a retirer;
- l'import pouvait sembler ne rien faire car la page des favoris n'etait pas ouverte apres l'action;
- les sources etaient encore trop centrees sur Chrome/Edge/Brave/Chromium;
- Firefox etait ignore au lieu d'etre signale comme non encore pris en charge.

## Correction

- Passage de la version projet a `0.5.2-dev`.
- Ajout de la suppression individuelle de favoris depuis le menu.
- Ajout du vidage complet des favoris Pulse avec sauvegarde locale automatique.
- Ouverture automatique de la page `Favoris locaux` apres import, remplacement, suppression ou vidage.
- Extension des sources importables a Vivaldi, Opera et Opera GX.
- Detection des profils Firefox locaux comme sources non encore importables, afin que leur absence d'import soit visible et explicite.

## Verification

- `cargo fmt --check`: reussi.
- `cargo test`: 37 tests passes.
- `cargo build`: reussi.
- Lancement court de `target\debug\pulse-browser.exe`: demarrage puis fermeture avec code `0`.
