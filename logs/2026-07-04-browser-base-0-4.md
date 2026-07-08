# Pulse Browser - Base navigateur 0.4

Date: 2026-07-04

## Changements

- Passage de la version de `0.3.3-dev` a `0.4.0-dev`.
- Ajout de `src/browser_data.rs` pour isoler l'historique local et les favoris locaux.
- Ajout de `src/local_pages.rs` pour generer l'accueil local et la page d'erreur locale.
- Extension du profil local avec le dossier `navigation`.
- Ajout d'une page d'accueil locale chargee dans CEF au demarrage.
- Ajout des boutons provisoires `Accueil` et `Favori`.
- Raccordement du bouton `Favori` a la page web courante.
- Enregistrement local des visites web dans `history.tsv`.
- Enregistrement local des favoris web dans `favorites.tsv`.
- Exclusion des pages internes `data:` de l'historique et des favoris.
- Ajout d'une page d'erreur locale quand CEF signale un echec de chargement.
- Synchronisation de base des boutons `Retour`, `Avancer` et `Stop` avec l'etat CEF.
- Mise a jour de la barre d'adresse a partir des changements d'adresse CEF pour les pages web.

## Securite et confidentialite

- Aucune synchronisation distante n'a ete ajoutee.
- Le coffre d'identifiants reste separe des donnees d'historique et de favoris.
- Les pages internes ne sont pas stockees comme historique de navigation.
- Les statuts continuent d'eviter les secrets et les cookies.

## Limites connues

- L'interface reste une coque Win32 provisoire.
- Les favoris sont stockes mais pas encore consultables dans un panneau dedie.
- L'historique est stocke mais pas encore consultable ou supprimable depuis l'interface.
- Les onglets ne sont pas encore implementes.
- Le test automatise ne controle pas encore visuellement le contenu rendu par CEF.

## Verifications

- `cargo fmt`: reussi.
- `cargo test`: reussi, 26 tests unitaires passes.
- `cargo build`: reussi.
- Lancement temporaire de `target\debug\pulse-browser.exe`: reussi, fermeture propre avec code de sortie `0`.
