# Pulse Browser - Credentials clean module 0.27.1-dev

Objectif: repartir sur une base propre pour le gestionnaire de mots de passe sans remettre le stockage Chromium.

Changements:

- Creation du dossier `PulseBrowser.WinUI/Credentials/`.
- Ajout de `CredentialService`, `CredentialMatcher`, `PublicSuffixService` et des modeles de capture.
- Ajout de scripts separes pour la capture et l'autofill.
- Ajout d'un banc de test local `Credentials/TestPages/credential-lab.html`.
- Branchement de `MainWindow` sur le nouveau service.
- Gel de l'ancien sniff POST: le code reste comme reference legacy, mais n'est plus appele.
- Passage de version a `0.27.1-dev`.

Principe maintenu: `vault.pulse` reste le seul coffre actif; WebView2/Chromium ne stocke ni ne remplit les mots de passe.

Verification:

- `node --check` OK sur `CredentialCaptureScript.js`.
- `node --check` OK sur `CredentialAutofillScript.js`.
- `build-winui.cmd` OK: 0 erreur, 4 warnings nullable preexistants dans `MainWindow.Profile.cs`.
- Lancement court de `PulseBrowser.WinUI.exe` OK: app lancee puis fermee proprement.
