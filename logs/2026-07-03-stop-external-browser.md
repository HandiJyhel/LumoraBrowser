# Correction ouverture externe - 2026-07-03

Contexte: l'utilisateur a teste `google.com` et Pulse Browser a ouvert Google Chrome, ce qui contredit l'objectif du projet.

Actions effectuees:

- Passage de la version de `0.1.0-dev` a `0.1.1-dev`, car il s'agit d'une correction de comportement.
- Suppression de l'appel Windows `ShellExecuteW`.
- Suppression du lien direct a `shell32`.
- Le bouton `Ouvrir` ne lance plus le navigateur par defaut.
- La barre d'adresse continue de normaliser les adresses, par exemple `google.com` devient `https://google.com`.
- Une zone de rendu temporaire indique que la navigation interne est demandee et que Chromium via CEF doit etre branche ensuite.
- Ajout de `docs/CEF_INTEGRATION.md` pour noter la route technique CEF reperee.

Verifications:

- `cargo fmt --check`: OK.
- `cargo test`: 3 tests reussis.
- `cargo build`: OK.
- Lancement visible de `target\debug\pulse-browser.exe`: OK.

Limite connue: la navigation n'affiche pas encore la page web dans Pulse Browser. La correction garantit surtout que Pulse Browser ne delegue plus l'ouverture a Chrome ou au navigateur par defaut.

Donnees sensibles: aucune URL personnelle, aucun token, aucun cookie et aucun secret n'ont ete consignes dans ce log.
