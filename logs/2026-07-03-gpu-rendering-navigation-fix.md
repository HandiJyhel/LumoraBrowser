# Pulse Browser - Correction navigation silencieuse

Date: 2026-07-03

## Probleme

Retour utilisateur: `tintin.fr` et `google.com` semblaient ne rien faire dans Pulse Browser.

## Diagnostic

Le log CEF montrait que `https://www.google.com/` arrivait bien jusqu'au moteur, mais que le processus GPU Chromium plantait en boucle:

- `GPU process exited unexpectedly`
- `Failed to send GpuControl.CreateCommandBuffer`
- `Failed to create shared context for virtualization`

Ce type d'erreur peut produire une page blanche ou donner l'impression que la navigation ne fait rien.

## Changements

- Passage de la version de `0.2.1-dev` a `0.2.2-dev`.
- Ajout d'une `PulseBrowserApp` CEF.
- Ajout de switches Chromium au demarrage CEF:
  - `disable-gpu`
  - `disable-gpu-compositing`
  - `disable-gpu-rasterization`
  - `disable-gpu-watchdog`
- Ajout de la navigation par touche Entree quand la barre d'adresse est active.
- Mise a jour de `AGENTS.md`, `MEMORY.md` et `docs/CEF_INTEGRATION.md`.

## Verifications

- `cargo fmt --check`: reussi.
- `cargo test`: reussi, 3 tests unitaires passes.
- `cargo build`: reussi.
- Lancement visible de `target\debug\pulse-browser.exe`: reussi.
- Test UI automatise:
  - `google.com` via Entree: statut `Navigation interne lancee vers https://google.com`.
  - `tintin.fr` via le bouton: statut `Navigation interne lancee vers https://tintin.fr`.
  - Fenetres CEF presentes: `CefBrowserWindow`, `Chrome_WidgetWin_1`, `Chrome_RenderWidgetHostHWND`.
- Verification du log CEF apres correction: aucun nouveau crash GPU observe sur le demarrage `0.2.2-dev`.

## Limites connues

- Les retours de chargement CEF ne sont pas encore branches dans l'interface.
- Une page peut encore echouer cote reseau sans message detaille dans l'UI.
- La structure d'onglets/profils n'est pas encore posee.
