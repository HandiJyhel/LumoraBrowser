# Pulse Browser - Coffre transparent et autoremplissage initial

Date: 2026-07-04

## Changements

- Passage de la version de `0.3.2-dev` a `0.3.3-dev`.
- Ajout de `src/credentials.rs` pour isoler:
  - l'extraction d'origine web exacte;
  - la detection prudente de formulaires `POST` URL-encodes;
  - la construction du script d'autoremplissage local.
- Raccordement CEF:
  - capture locale des requetes `POST` compatibles sur navigation principale ou XHR;
  - rejet des captures quand l'origine de la requete et l'origine principale ne correspondent pas;
  - sauvegarde silencieuse dans `default.pbvault`;
  - injection d'un autoremplissage sur la frame principale quand un identifiant existe pour la meme origine.
- Les secrets ne sont pas envoyes dans les statuts, les logs projet ou la console.

## Limites connues

- La capture ne couvre pas encore les payloads JSON.
- La capture ne couvre pas encore tous les flux multi-etapes ou federes complexes.
- Il n'existe pas encore de surface utilisateur pour consulter, supprimer ou modifier les identifiants enregistres.

## Verifications

- `cargo fmt --check`: reussi.
- `cargo test`: reussi, 19 tests unitaires passes.
- `cargo build`: reussi.
- Lancement temporaire de `target\debug\pulse-browser.exe`: reussi, fermeture propre.
