# Pulse Browser - Vue Chromium redimensionnable

Date: 2026-07-03

## Changements

- Passage de la version de `0.2.0-dev` a `0.2.1-dev`.
- Ajout d'un layout Win32 reactif pour la coque de developpement.
- Redimensionnement de la barre d'adresse, du bouton `Ouvrir`, du statut et de la zone de rendu.
- Recuperation du handle natif de la fenetre CEF via `BrowserHost::window_handle`.
- Redimensionnement de la fenetre enfant CEF avec `MoveWindow`, puis notification `was_resized` au moteur.
- Mise a jour de `AGENTS.md`, `MEMORY.md` et `docs/CEF_INTEGRATION.md`.

## Verifications

- `cargo fmt --check`: reussi.
- `cargo test`: reussi, 3 tests unitaires passes.
- `cargo build`: reussi.
- Lancement visible de `target\debug\pulse-browser.exe`: reussi.
- Test UI automatise avec `https://example.com`: reussi.

## Resultat observe

Le test UI confirme que la vue `CefBrowserWindow` suit la taille de la fenetre Pulse Browser:

- avant agrandissement: `607x204`
- apres agrandissement: `1173x571`

Le statut affiche `Navigation interne lancee vers https://example.com`.

## Limites connues

- Les retours de chargement CEF ne sont pas encore branches.
- Les erreurs reseau ne sont pas encore exposees proprement dans l'interface.
- La structure d'onglets/profils n'est pas encore posee.
