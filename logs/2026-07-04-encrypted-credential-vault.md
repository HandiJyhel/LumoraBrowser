# Pulse Browser - Coffre d'identifiants chiffre

Date: 2026-07-04

## Changements

- Passage de la version de `0.3.0-dev` a `0.3.1-dev`.
- Transformation de `src/vault.rs`: le coffre `default.pbvault` n'est plus seulement un marqueur chiffre, mais un conteneur local d'identifiants.
- Ajout d'un payload interne versionne pour stocker des identifiants par origine.
- Ajout d'une API interne pour:
  - charger le coffre;
  - sauvegarder le coffre;
  - remplacer ou ajouter un identifiant pour un couple origine/nom d'utilisateur;
  - rechercher les identifiants d'une origine.
- Ajout du dechiffrement DPAPI Windows via `CryptUnprotectData`.
- Migration automatique de l'ancien marqueur `0.3.0-dev` vers un coffre structure vide au prochain demarrage.
- Verification du coffre au demarrage CEF: Pulse Browser s'assure qu'il peut creer et relire le coffre local.

## Securite

- Le fichier `.pbvault` reste protege par Windows DPAPI pour l'utilisateur Windows courant.
- Les secrets ne sont pas affiches dans l'interface et ne doivent pas etre journalises.
- L'extension `.pbvault` identifie le format Pulse Browser; elle ne remplace pas le chiffrement.

## Limites connues

- L'interface ne detecte pas encore les formulaires de connexion.
- L'enregistrement automatique et le remplissage automatique ne sont pas encore branches au moteur web.
- La gestion d'un mot de passe maitre, d'une recuperation ou d'un export/import n'est pas encore traitee.

## Verifications

- `cargo fmt`: reussi.
- `cargo fmt --check`: reussi.
- `cargo test`: reussi, 14 tests unitaires passes.
- `cargo build`: reussi.
- Lancement temporaire de `target\debug\pulse-browser.exe`: reussi, fermeture propre.
- Verification non sensible dans `%LOCALAPPDATA%\PulseBrowser\profiles\default\vault`: `default.pbvault` present.
