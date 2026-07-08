# Audit et onglets 0.7.0-dev

## Contexte

Un audit de reprise a ete effectue apres la validation utilisateur `go`, car le projet contenait une incoherence de version: `Cargo.toml` indiquait deja `0.7.0-dev`, tandis que `AGENTS.md` et `MEMORY.md` indiquaient encore `0.6.4-dev`.

## Constats

- Le code courant contient deja une premiere implementation visible des onglets.
- `src/tabs.rs` gere le modele interne d'onglets.
- `src/ui_tabs.rs` affiche une barre d'onglets Win32 provisoire.
- `src/cef_runtime.rs` sait creer, masquer, restaurer et fermer des instances CEF associees aux onglets.
- `src/main.rs` raccorde l'ouverture, le changement, la fermeture et l'ouverture de favoris dans un nouvel onglet.
- `src/settings.rs` conserve la preference de disposition horizontale ou verticale.

## Corrections effectuees

- Mise en forme Rust appliquee avec `cargo fmt`.
- Mise a jour de la version courante dans `AGENTS.md` vers `0.7.0-dev`.
- Ajout de `docs/TABS_0_7.md` pour documenter le palier d'onglets.
- Mise a jour de `MEMORY.md` pour garder l'historique chronologique coherent.

## Correction 0.7.1-dev

La verification runtime a revele une erreur CEF persistante dans `target/debug/debug.log`: `cache_path` etait refuse parce qu'il n'etait pas enfant de `root_cache_path`. CEF retombait donc en stockage memoire, ce qui contredisait l'objectif de profil local persistant.

La correction de `src/profile.rs` fait maintenant pointer `root_cache_path` vers la racine locale Pulse Browser, tandis que `cache_path` reste dans le profil local `profiles/default/cef-profile`. Un test verifie desormais que `cache_path` reste bien sous `root_cache_path`.

La tentative de verification visuelle automatisee dans cette session a lance des processus CEF sans fenetre top-level detectable. Ces processus ont ete arretes. La verification visuelle longue reste donc a faire directement dans une session interactive Windows.

## Validation

- `cargo fmt --check`: reussi apres formatage.
- `cargo test`: 50 tests passes.
- `cargo build`: compilation reussie.

## Limites

- L'audit confirme la compilation et les tests, mais pas encore une session visuelle longue.
- La barre d'onglets reste provisoire en Win32.
- La persistance/restauration des onglets entre lancements n'est pas encore implementee.
- Le deplacement des onglets n'est pas encore disponible.
