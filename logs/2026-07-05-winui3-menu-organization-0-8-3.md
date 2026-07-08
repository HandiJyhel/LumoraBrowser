# Organisation WinUI 3 0.8.3-dev

## Contexte

L'utilisateur a signale une incoherence d'organisation dans la coque WinUI 3: `A propos` etait dans le menu `Pulse`, les informations de donnees locales etaient dans une page separee confuse, et `Autres favoris` n'avait pas d'acces direct assez clair.

## Changements

- Passage de la version projet a `0.8.3-dev`.
- Deplacement de `A propos de Pulse Browser` dans le menu `Outils`.
- Suppression de l'entree et du panneau separe `Donnees locales`.
- Integration du chemin du profil local et du rappel local-first dans le panneau `A propos`.
- Ajout des entrees `Barre des favoris` et `Autres favoris` dans le menu `Favoris`.
- Ajout du raccord WinUI permettant d'ouvrir directement le gestionnaire sur `Autres favoris`.

## Verification

- `build-winui.cmd` reussi avec MSBuild Visual Studio x64, 0 avertissement et 0 erreur.
- Lancement cache court de `PulseBrowser.WinUI.exe` reussi: processus vivant avec fenetre `Pulse Browser 0.8.3-dev`.
- `cargo fmt --check` reussi.
- `cargo test` reussi: 50 tests unitaires passes.
- `cargo build` reussi en `0.8.3-dev`.
