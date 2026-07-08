# Squelette Rust - 2026-07-03

Contexte: creation du premier socle lancable de Pulse Browser apres validation par `Go`.

Actions effectuees:

- Creation de `Cargo.toml` pour le binaire `pulse-browser`.
- Version initiale du paquet: `0.0.0-dev`.
- Creation de `src/main.rs` avec un affichage minimal du nom, de la version, du moteur cible et du statut de lancement.
- Creation de `scripts/run-dev.ps1` pour lancer le projet avec `cargo run`.
- Creation de `run-dev.cmd` pour faciliter le lancement sous Windows.
- Verification avec `run-dev.cmd`: compilation dev reussie et execution de `target\debug\pulse-browser.exe`.
- Verification du formatage avec `cargo fmt --check`.

Note: ce socle ne contient pas encore de vraie interface graphique. Il sert a verifier que la base Rust compile et se lance avant d'ajouter l'UI et l'integration Chromium via CEF.

Note Windows: l'appel direct a `scripts\run-dev.ps1` peut etre bloque par la politique d'execution PowerShell. Le lancement valide passe par `run-dev.cmd`, qui utilise `ExecutionPolicy Bypass` pour ce script de developpement local.

Donnees sensibles: aucune donnee personnelle, aucun token, aucun cookie et aucun secret n'ont ete consignes dans ce log.
