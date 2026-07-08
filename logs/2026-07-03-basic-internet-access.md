# Acces Internet basique - 2026-07-03

Contexte: ajout d'un premier acces Internet simple et logique apres validation par `Go`.

Actions effectuees:

- Passage de la version de `0.0.0-dev` a `0.1.0-dev`.
- Ajout d'identifiants de controles pour la barre d'adresse, le bouton `Ouvrir` et le statut.
- Lecture de la barre d'adresse au clic sur `Ouvrir`.
- Normalisation d'adresse: une saisie comme `example.com` devient `https://example.com`.
- Conservation des adresses qui possedent deja un schema, par exemple `http://example.com`.
- Refus d'une adresse vide avec un statut lisible dans la fenetre.
- Ouverture temporaire de l'adresse via Windows dans le navigateur par defaut.
- Ajout de tests unitaires pour la normalisation d'adresse.

Verifications:

- `cargo fmt --check`: OK.
- `cargo test`: 3 tests reussis.
- `cargo build`: OK.

Limite connue: l'affichage web ne se fait pas encore dans Pulse Browser. Cette etape donne un acces Internet temporaire en attendant l'integration Chromium via CEF.

Donnees sensibles: aucune URL utilisateur reelle, aucun token, aucun cookie et aucun secret n'ont ete consignes dans ce log.
