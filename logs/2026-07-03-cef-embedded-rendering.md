# Pulse Browser - Premiere vue Chromium embarquee

Date: 2026-07-03

## Changements

- Passage de la version de `0.1.1-dev` a `0.2.0-dev`.
- Ajout de la dependance Rust `cef = "149.3.0"`.
- Creation de `src/cef_runtime.rs` pour separer l'initialisation CEF, la gestion des sous-processus, le client CEF minimal et la creation de la vue navigateur.
- Integration d'une pompe de messages Win32/CEF: la boucle native appelle `CefDoMessageLoopWork`.
- Le bouton `Ouvrir` charge maintenant l'adresse dans une vue Chromium enfant de la fenetre Pulse Browser.
- Mise a jour de `scripts/run-dev.ps1` pour ajouter Rust et Ninja au `PATH` avant `cargo run`.
- Mise a jour de `AGENTS.md`, `MEMORY.md` et `docs/CEF_INTEGRATION.md`.

## Verifications

- `cargo fmt --check`: reussi.
- `cargo test`: reussi, 3 tests unitaires passes.
- `cargo build`: reussi.
- Lancement visible de `target\debug\pulse-browser.exe`: reussi.
- Test UI automatise avec `https://example.com`: reussi.

## Resultat observe

Le test UI confirme que Pulse Browser cree une vue Chromium embarquee dans sa propre fenetre:

- `CefBrowserWindow`
- `Chrome_WidgetWin_1`
- `Chrome_RenderWidgetHostHWND`

Le navigateur par defaut de Windows n'est pas ouvert.

## Limites connues

- La vue Chromium a encore une taille fixe.
- Les retours de chargement et erreurs reseau ne sont pas encore exposes proprement dans l'interface.
- La structure d'onglets/profils n'est pas encore posee.
