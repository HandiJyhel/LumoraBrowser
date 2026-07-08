# Pulse Browser - Password Manager target field 0.29.2-dev

## Objet

Correction du faux succes "Identifiant rempli" quand le champ de connexion reste vide.

## Changements

- `CredentialAutofillScript.js` choisit seulement des champs topmost/actionnables.
- Verification apres `setValue`: le succes n'est retourne que si la valeur est reellement presente dans le champ.
- Penalisation forte des contextes newsletter/footer/marketing/paiement/recherche/promo/code.
- Bonus pour les contextes de connexion/auth/compte/continuer/mot de passe.
- `CredentialCaptureScript.js` applique la meme logique topmost/contexte pour eviter de declencher la barre sur un champ newsletter couvert par un overlay.

## Verification

- `node --check PulseBrowser.WinUI\Credentials\CredentialCaptureScript.js` : OK.
- `node --check PulseBrowser.WinUI\Credentials\CredentialAutofillScript.js` : OK.
- Premier `build-winui.cmd` sous sandbox : echec NuGet attendu (`NU1301`).
- `build-winui.cmd` hors sandbox : OK, 0 erreur. Warnings nullable preexistants dans `MainWindow.Profile.cs`.
- Lancement court de `PulseBrowser.WinUI.exe` : OK, ouverture puis fermeture propre.
