# Pulse Browser - Commandes de navigation de base

Date: 2026-07-04

## Changements

- Passage de la version de `0.3.1-dev` a `0.3.2-dev`.
- Ajout de boutons provisoires dans la coque Win32:
  - `Retour`;
  - `Avancer`;
  - `Recharger`;
  - `Stop`.
- Ajout des fonctions CEF correspondantes dans `src/cef_runtime.rs`.
- Les commandes utilisent l'instance Chromium embarquee active et renvoient un statut lisible quand aucune page n'est disponible.

## Limites connues

- Les boutons ne sont pas encore une interface WinUI 3 finale.
- Les boutons ne sont pas encore des icones modernes.
- L'etat actif/inactif des boutons n'est pas encore synchronise visuellement avec l'historique CEF.

## Verifications

- `cargo fmt`: reussi.
- `cargo test`: reussi, 14 tests unitaires passes.
- `cargo build`: reussi.
- Lancement temporaire de `target\debug\pulse-browser.exe`: reussi, fermeture propre.
