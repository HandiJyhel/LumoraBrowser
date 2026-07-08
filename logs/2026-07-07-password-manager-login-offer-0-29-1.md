# Pulse Browser - Password Manager login offer 0.29.1-dev

## Objet

Correction du comportement reel attendu: un identifiant deja present dans le gestionnaire doit etre propose a la connexion du site.

## Changements

- Ajout du type de decision `UsernameFillAvailable`.
- `PasswordManagerInteractionService.EvaluatePage` propose maintenant le remplissage si un champ identifiant est visible, meme sans champ mot de passe.
- `CredentialAutofillScript.js` sait remplir l'identifiant seul et retourne un message dedie.
- `CredentialCaptureScript.js` filtre mieux les faux champs identifiant comme recherche, promo, code ou quantite.
- `PasswordManagerService.FindExistingLogin` permet de retrouver une entree existante par identifiant et domaine/URL.
- `BuildSaveOffer` ignore les captures deja presentes avec le meme mot de passe.
- Si le mot de passe differe pour un identifiant deja connu, la barre affiche une proposition de mise a jour.

## Verification

- `node --check PulseBrowser.WinUI\Credentials\CredentialCaptureScript.js` : OK.
- `node --check PulseBrowser.WinUI\Credentials\CredentialAutofillScript.js` : OK.
- Premier `build-winui.cmd` sous sandbox : echec NuGet attendu (`NU1301`).
- `build-winui.cmd` hors sandbox : OK, 0 erreur. Warnings nullable preexistants dans `MainWindow.Profile.cs`.
- Lancement court de `PulseBrowser.WinUI.exe` : OK, ouverture puis fermeture propre.
