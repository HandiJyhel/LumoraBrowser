# Pulse Browser - Correction statut CEF et affichage de la vue

Date: 2026-07-03

## Probleme

Retour utilisateur: `google.com` donnait encore l'impression de ne rien faire dans Pulse Browser.

## Diagnostic

Deux indices differents ont ete observes:

- le log CEF contenait un `Timeout of new browser info response`, ce qui indiquait que la boucle hote devait mieux cooperer avec la pompe CEF;
- apres correction de la pompe, Google atteignait bien CEF, mais il fallait encore forcer l'affichage et le focus de la fenetre enfant pour eviter une navigation chargee mais invisible.

## Changements

- Passage de la version a `0.2.3-dev`, puis `0.2.4-dev`.
- Activation de `external_message_pump` dans les settings CEF.
- Ajout de handlers CEF de chargement et d'affichage.
- Remontee dans le statut des evenements CEF: debut de chargement, fin de chargement, erreurs reseau, adresse et titre.
- Remplacement du repositionnement de la vue CEF par `SetWindowPos` avec affichage force.
- Appel a `was_resized` et focus explicite du navigateur embarque apres creation, navigation et redimensionnement.
- Mise a jour de `AGENTS.md`, `MEMORY.md`, `docs/CEF_INTEGRATION.md` et `Cargo.toml`.

## Verifications

- `cargo fmt --check`: reussi.
- `cargo test`: reussi, 3 tests unitaires passes.
- `cargo build`: reussi.
- Lancement visible de `target\debug\pulse-browser.exe`: reussi.
- Test UI automatise:
  - adresse saisie: `google.com`;
  - bouton `Ouvrir`: clique;
  - statut obtenu: `Statut CEF: Page chargee: Google`;
  - fenetres CEF presentes: `CefBrowserWindow`, `Chrome_WidgetWin_1`, `Chrome_RenderWidgetHostHWND`.
- Verification du log CEF apres correction: une ligne console provient de `https://www.google.com/`, ce qui confirme que la page charge dans le moteur embarque.

## Limites connues

- La coque Win32 reste un prototype de travail.
- La surface d'erreur reseau doit encore devenir une vraie page interne lisible.
- La structure d'onglets/profils n'est pas encore posee.
