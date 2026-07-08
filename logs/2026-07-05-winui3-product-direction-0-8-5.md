# WinUI 3 direction produit - 0.8.5-dev

## Contexte

La barre de favoris et les reglages WinUI restaient trop proches d'une maquette: le bouton `Gerer` encombrait la barre, `Autres favoris` n'etait pas aligne comme dans les navigateurs courants, le toggle d'onglets verticaux ne faisait rien, et le prototype Win32/CEF pouvait encore etre lance par erreur via `run-dev.cmd`.

## Changements

- Passage de la version projet a `0.8.5-dev`.
- Ajout d'une section favoris dans `Parametres`, avec acces au gestionnaire, aux racines, a l'import et a l'export.
- Retrait du bouton `Gerer` de la barre de favoris.
- Separation visuelle de `Autres favoris` a droite de la barre.
- Activation reelle du mode onglets verticaux: rail lateral, masquage de la barre horizontale et selection d'onglet depuis la colonne.
- Ajout d'un cache local de favicons WinUI dans le profil utilisateur.
- Extension compatible du format `bookmarks.tsv` cote WinUI avec un chemin de favicon optionnel.
- Desactivation des lanceurs ambigus du prototype Win32/CEF et ajout de `archive/win32-cef-prototype/`.
- Mise a jour de `AGENTS.md`: `PulseBrowser.WinUI` est la seule interface produit active.

## Verification

- `build-winui.cmd` reussi avec restore NuGet autorise, 0 avertissement et 0 erreur.
- Lancement court cache de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.8.5-dev`, processus repondant.
- `cargo fmt --check` reussi.
- `cargo test` reussi: 50 tests passes.
- `cargo build` reussi en `0.8.5-dev`.
- `run-dev.cmd` verifie comme desactive: il sort avec le message indiquant que WinUI 3 est la seule interface produit active.
