# Pulse Browser 0.8.7-dev - Reglages UI persistants

Date: 2026-07-05

## Contexte

Les options visibles dans `Parametres`, notamment les onglets verticaux, ne devaient pas etre perdues a chaque redemarrage. Un parametre qui se reactive manuellement a chaque lancement n'est pas un vrai parametre utilisateur.

Les favicons cachees localement devaient aussi etre reutilisees par les onglets quand elles existent deja, au lieu d'attendre uniquement un nouvel evenement WebView2.

## Changements

- Passage de la version projet a `0.8.7-dev`.
- Ajout de `ui-settings.json` dans le profil local, sous `navigation/`.
- Sauvegarde automatique de:
  - la barre de favoris visible ou masquee;
  - l'activation des onglets verticaux;
  - le mode compact du rail vertical;
  - la largeur du rail vertical.
- Rechargement des reglages UI au demarrage.
- Protection contre la sauvegarde intempestive pendant l'initialisation WinUI.
- Separation entre largeur reduite en mode texte et mode compact en icones.
- Reconstruction du cache de favicons depuis les favoris deja charges.
- Recherche deterministe d'une favicon locale par hash d'URL avant d'afficher l'icone generique d'un onglet.

## Verification

- `build-winui.cmd` reussi apres restore NuGet autorise, avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.8.7-dev`, processus repondant.
- `cargo fmt --check` reussi.
- `cargo test` reussi: 50 tests passes.
- `cargo build` reussi.
- `run-dev.cmd` reste desactive et renvoie vers `run-winui.cmd`.

## Notes

- Les commandes Rust signalent encore des avertissements de code mort existants (`move_node`, `ImportFromFile`, `search`, champs/methodes non utilises). Ils ne sont pas lies au palier WinUI `0.8.7-dev`.
