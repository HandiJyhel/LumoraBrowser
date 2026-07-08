# Pulse Browser - Password Manager module 0.28.0-dev

Objectif: construire un vrai gestionnaire de mots de passe simple avant de continuer l'integration navigateur.

Changements:

- Creation de `PulseBrowser.WinUI/PasswordManager/PasswordManagerService.cs`.
- Creation de `PasswordManagerEntryDraft.cs`.
- Ajout d'une recherche locale dans le panneau mots de passe.
- Ajout manuel enrichi: nom, origine, URL de connexion, identifiant, mot de passe.
- Ajout d'actions directes: ouvrir la page de connexion, copier identifiant, copier mot de passe, renommer, supprimer.
- `OfferAutoFill` demande maintenant au `PasswordManagerService` de trouver le meilleur identifiant pour l'adresse courante.
- Version projet passee a `0.28.0-dev`.

Principe maintenu: `vault.pulse` reste la source de verite; Chromium/WebView2 ne stocke pas les mots de passe.

Verification:

- `node --check` OK sur `CredentialCaptureScript.js`.
- `node --check` OK sur `CredentialAutofillScript.js`.
- `build-winui.cmd` OK: 0 erreur, 4 warnings nullable preexistants dans `MainWindow.Profile.cs`.
- Lancement court de `PulseBrowser.WinUI.exe` OK: app lancee puis fermee proprement.
