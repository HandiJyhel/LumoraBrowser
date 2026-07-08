# Correction profil CEF 0.7.1-dev

## Probleme

Le log CEF `target/debug/debug.log` contenait des erreurs repetees:

`The cache_path directory (...) is not a child of the root_cache_path directory (...)`

Puis:

`The cache_path is invalid. Defaulting to in-memory storage.`

Cela signifiait que les donnees CEF pouvaient retomber en stockage memoire, contrairement a l'objectif local-first du projet.

## Correction

- Passage de la version projet a `0.7.1-dev`.
- Correction de `src/profile.rs`: `root_cache_dir` pointe maintenant vers la racine locale `%LOCALAPPDATA%\PulseBrowser`.
- `cef_cache_dir` reste dans `%LOCALAPPDATA%\PulseBrowser\profiles\default\cef-profile`.
- Mise a jour de `docs/LOCAL_PROFILE_AND_PRIVACY.md`.
- Ajout du test `profile::tests::cef_cache_path_is_inside_root_cache_path`.

## Verification

- `cargo fmt --check`: reussi.
- `cargo test`: 50 tests passes.
- `cargo build`: reussi.

## Limite

La verification visuelle automatisee a ete tentee, mais la session a produit des processus CEF sans fenetre top-level detectable. Les processus lances pendant ce test ont ete arretes. Une verification visuelle interactive reste necessaire.
