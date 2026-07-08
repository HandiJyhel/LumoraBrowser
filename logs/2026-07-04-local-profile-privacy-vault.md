# Pulse Browser - Profil local, cookies et coffre transparent

Date: 2026-07-04

## Changements

- Passage de la version de `0.2.4-dev` a `0.3.0-dev`.
- Ajout de `src/profile.rs` pour definir le profil local `default` sous `%LOCALAPPDATA%\PulseBrowser\profiles\default`.
- Raccordement de CEF a un `root_cache_path` Pulse Browser et a un `cache_path` de profil persistant.
- Activation de la persistance des cookies de session cote CEF.
- Ajout de `src/privacy.rs` pour centraliser la decision cookies first-party/same-site contre contexte tiers.
- Ajout d'un `CookieAccessFilter` CEF: les cookies first-party/same-site sont autorises, les cookies tiers ou au contexte first-party inconnu sont bloques.
- Ajout de `src/vault.rs` pour initialiser un coffre local transparent `default.pbvault`, chiffre avec Windows DPAPI pour l'utilisateur courant.
- Reduction des nouveaux statuts de navigation: affichage du domaine plutot que de l'URL complete.
- Ajout de `docs/LOCAL_PROFILE_AND_PRIVACY.md`.
- Mise a jour de `docs/CEF_INTEGRATION.md`, `AGENTS.md`, `MEMORY.md` et `Cargo.toml`.

## Verifications

- `cargo fmt`: reussi.
- `cargo test`: reussi, 10 tests unitaires passes.
- `cargo build`: reussi.
- Lancement temporaire de `target\debug\pulse-browser.exe`: reussi, fermeture propre.
- Verification dans `%LOCALAPPDATA%\PulseBrowser\profiles\default`: profil present, dossier `cef-profile` present, coffre `vault\default.pbvault` present.

## Limites connues

- Le coffre local est initialise et chiffre, mais ne stocke pas encore de vrais mots de passe utilisateur.
- Le blocage des cookies tiers est volontairement strict; certains flux de connexion tiers pourront demander une politique plus nuancee ou partitionnee.
- La navigation reelle avec connexion a un compte et validation du comportement cookies devra etre testee dans une passe dediee.
