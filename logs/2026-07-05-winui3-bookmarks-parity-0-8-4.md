# WinUI 3 favoris - 0.8.4-dev

## Contexte

La coque WinUI avait restaure une partie de l'interface, mais la gestion des favoris restait inferieure a l'ancienne version: icones absentes, `Autres favoris` trop cache, dossiers qui ne s'ouvraient pas comme dans un navigateur, absence de menu contextuel et import navigateur insuffisamment explicite.

## Changements

- Passage de la version projet a `0.8.4-dev`.
- Ajout de glyphes WinUI pour distinguer dossiers et liens dans les listes et la barre de favoris.
- Ajout d'un bouton permanent `Autres favoris` dans la barre de favoris.
- Raccordement d'une ouverture normale des dossiers depuis le gestionnaire et les menus contextuels.
- Raccordement des liens favoris a la navigation WebView2 WinUI.
- Ajout de menus contextuels sur les listes de dossiers/favoris avec ouverture, renommage et suppression selon les droits de l'element.
- Separation de l'import depuis navigateur installe et de l'import HTML.
- Ajout du mode `Remplacer par le navigateur selectionne`, avec sauvegarde du fichier de favoris avant remplacement.

## Verification

- `build-winui.cmd` reussi apres restore NuGet autorise, avec 0 erreur. MSBuild a emis 5 avertissements de copie car un ancien processus `PulseBrowser.WinUI.exe` verrouillait temporairement l'executable, puis la build a produit la DLL et l'executable.
- Lancement court cache de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.8.4-dev`, processus repondant.
- `cargo fmt --check` reussi.
- `cargo test` reussi: 50 tests passes.
- `cargo build` reussi en `0.8.4-dev`.
