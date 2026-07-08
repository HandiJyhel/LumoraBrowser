# Pulse Browser 0.8.6-dev - Onglets verticaux et favoris sous Outils

Date: 2026-07-05

## Contexte

Apres essai de la version WinUI recente, plusieurs points restaient trop fragiles pour un usage navigateur normal:

- le rail d'onglets verticaux etait active, mais pas redimensionnable ni vraiment compact;
- les favicons etaient rattachees aux signets, mais pas assez visibles dans les onglets;
- la gestion des favoris dans `Parametres` n'etait pas l'emplacement le plus logique;
- le menu de favoris devait plutot vivre dans `Outils`;
- les dossiers affiches dans `Autres favoris` n'exposaient pas assez clairement les actions attendues.

## Changements

- Passage de la version projet a `0.8.6-dev`.
- Suppression du menu principal `Favoris`.
- Ajout de `Outils > Favoris` avec acces a:
  - `Barre des favoris`;
  - `Autres favoris`;
  - `Gerer les favoris`;
  - `Importer des favoris`;
  - `Exporter les favoris`;
  - affichage/masquage de la barre de favoris.
- Retrait de la section de gestion des favoris dans `Parametres`.
- Ajout d'une poignee de redimensionnement pour le rail d'onglets verticaux.
- Ajout d'un mode compact du rail vertical pour afficher les onglets sous forme d'icones.
- Reutilisation des favicons locales dans les entetes d'onglets horizontaux et verticaux.
- Mise a jour immediate de l'icone de l'onglet courant quand WebView2 fournit une favicon.
- Ajout d'actions directes dans les sous-menus de dossiers de favoris:
  - `Ouvrir le dossier`;
  - `Renommer`;
  - `Supprimer`;
  - contenu du dossier ensuite.

## Verification

- `build-winui.cmd` reussi apres restore NuGet autorise, avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.8.6-dev`, processus repondant.
- `cargo fmt --check` reussi.
- `cargo test` reussi: 50 tests passes.
- `cargo build` reussi.
- `run-dev.cmd` reste desactive et renvoie vers `run-winui.cmd`.

## Notes

- Les commandes Rust signalent encore des avertissements de code mort existants (`move_node`, `ImportFromFile`, `search`, champs/methodes non utilises). Ils ne sont pas lies au palier WinUI `0.8.6-dev`.
