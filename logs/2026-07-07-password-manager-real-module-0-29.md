# Pulse Browser - Password Manager real module 0.29.0-dev

## Objet

Refonte du gestionnaire de mots de passe pour eviter l'empilement de correctifs autour de `MainWindow`.

## Changements

- Ajout de `PasswordManagerInteractionService`.
- Ajout de `PasswordManagerPageDecision`, `PasswordManagerPromptKind` et `PasswordManagerSaveOffer`.
- Ajout de `CredentialPageState`.
- `CredentialService` expose maintenant `PageStateChanged` en plus de `CredentialCaptured`.
- `CredentialCaptureScript.js` passe en observateur de page: champs visibles, mutations DOM, focus, clics, entree, chargement.
- `MainWindow.Vault.cs` consomme des decisions du module au lieu de choisir directement quand afficher la barre de remplissage.
- Suppression de l'ancien `CredentialMatcher.cs`.
- Suppression de l'ancien `RegisterCredentialMonitorAsync` inline.
- `BrowserCore_WebMessageReceived` reste limite aux passkeys et ne traite plus l'ancien chemin `{t:"cred"}`.
- Lecture des messages WebView2 passkeys rendue robuste aux messages envoyes comme chaine JSON.

## Comportement vise

Pour un site en deux etapes comme Micromania:

1. Pulse reconnait l'identifiant du domaine racine.
2. Si seul le champ e-mail est visible, il n'affiche pas un faux echec de remplissage.
3. Quand le champ mot de passe devient visible, le module peut proposer le remplissage.

## Verification

- `node --check PulseBrowser.WinUI\Credentials\CredentialCaptureScript.js` : OK.
- `node --check PulseBrowser.WinUI\Credentials\CredentialAutofillScript.js` : OK.
- `build-winui.cmd` : OK, 0 erreur. Warnings nullable preexistants dans `MainWindow.Profile.cs`.
- Lancement court de `PulseBrowser.WinUI.exe` : OK, ouverture puis fermeture propre.
