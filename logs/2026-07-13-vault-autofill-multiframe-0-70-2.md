# Coffre : remplissage multi-frames et regles assouplies - 0.70.2-dev

## Contexte

L'utilisateur importe correctement ses mots de passe dans le coffre (ils sont
justes, ils viennent de son gestionnaire), mais quand Lumora propose de remplir
sur un site et qu'il accepte, le remplissage echoue souvent : la barre
s'affiche, le clic sur « Remplir » ne remplit rien.

## Diagnostic

Asymetrie structurelle entre la detection et le remplissage :

- La detection (`CredentialCaptureScript.js`) est injectee via
  `AddScriptToExecuteOnDocumentCreatedAsync`, donc presente dans TOUTES les
  frames (iframes comprises). Le remplissage passait par
  `ExecuteScriptAsync` sur le `CoreWebView2`, qui ne touche QUE la frame
  principale : tout formulaire de connexion loge dans une iframe etait
  detecte mais irremplissable (« Aucun champ de connexion visible »).
- Le script de remplissage (`CredentialAutofillScript.js`, supprime)
  exigeait `elementFromPoint` (premier plan) et la presence dans le
  viewport : un formulaire sous un bandeau cookies ou plus bas dans la page
  faisait echouer le remplissage explicite.
- Les champs `readonly` (astuce anti-autofill courante, deverrouillage au
  focus) etaient rejetes d'office.
- Les shadow roots n'etaient pas traverses.
- Le script de remplissage dupliquait les helpers du script de capture et
  les deux avaient deja diverge.

## Changements

- `CredentialCaptureScript.js` : nouvelle section « remplissage a la
  demande » — `window.__novaFillCredential(payload)` definie dans chaque
  frame. Recherche des champs via `queryAllDeep` (document + shadow roots
  ouverts + iframes same-origin), visibilite assouplie au remplissage
  (pas d'exigence de premier plan ni de viewport), `scrollIntoView` avant
  ecriture, deverrouillage `readonly` (focus puis retrait force), setter
  natif + evenements `input`/`change`. Verification differee a ~300 ms :
  si un framework SPA efface la valeur, nouvel essai, puis message
  `nova.credential.fill-report` vers l'app si l'echec persiste.
- `CredentialService.cs` : suivi des `CoreWebView2Frame` (FrameCreated /
  Destroyed) ; `FillAsync` appelle `__novaFillCredential` dans la frame
  principale puis dans chaque iframe cross-origin suivie, en n'envoyant que
  ce qui manque (pas de double remplissage), et agrege les resultats
  partiels (identifiant dans la page, mot de passe dans une iframe).
  `FillGeneratedPasswordAsync` tente aussi les iframes. Nouvel evenement
  `FillReported` (rapport differe), route depuis la frame principale et les
  iframes ; meme garde onglet-actif que le page-state. Les page-states des
  iframes ne sont volontairement PAS routes vers l'UI (une frame tierce sans
  champ masquerait la barre).
- `MainWindow.Vault.cs` / `MainWindow.xaml.cs` : abonnement `FillReported`
  → `StatusText`.
- `CredentialAutofillScript.js` supprime (logique unifiee dans le script de
  capture, fin de la duplication divergente).
- `credential-lab.html` : 4 nouveaux cas de test manuels — champs readonly
  anti-autofill, formulaire hors viewport, connexion dans une iframe
  same-origin (srcdoc), connexion dans un shadow DOM ouvert.

## Verification

- `node --check Lumora.WinUI\Credentials\CredentialCaptureScript.js` : OK.
- Build via MSBuild.exe (vswhere) : reussi, 0 erreur (rappel : `dotnet build`
  seul echoue toujours sur ce projet, MSB4062 PriGen, probleme
  d'environnement documente).
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-build` :
  276/276 tests verts.
- Artefact propre Release (0 avertissement, 0 erreur) :
  `artifacts\clean-test\Lumora-0.70.2-dev-win-x64-clean-20260713-135646`.
- SHA256 de `Lumora.WinUI.exe` :
  `01aa7368b91a8e063ceb572e212e1dc7e0ff17f911cf08e1e9a6de89860f6441`
  (identique a 0.70.1 : c'est le stub apphost generique, le code vit dans
  `Lumora.WinUI.dll`).
- Contenu de l'artefact verifie : `CredentialAutofillScript.js` absent,
  `__novaFillCredential` present dans `CredentialCaptureScript.js`, nouveaux
  cas dans `credential-lab.html`, chaine `0.70.2-dev` (UTF-16) presente dans
  `Lumora.WinUI.dll`.
- Manifeste artefact :
  `artifacts\signatures\Lumora-0.70.2-dev-clean-20260713-135711.sha256`.
- Installateur :
  `artifacts\installer\LumoraSetup-0.70.2-dev-win-x64.exe`.
- SHA256 installateur :
  `376b43c7855afa9c417485594e0abbe0ef1a48373441824fec0ad2a32188c49d`.
- Fichier de verification :
  `artifacts\installer\LumoraSetup-0.70.2-dev-win-x64.VERIFICATION.txt`.
- Manifeste installateur :
  `artifacts\signatures\LumoraSetup-0.70.2-dev-20260713-135847.sha256`.
- Pas de validation manuelle interactive dans cette passe : a verifier par
  l'utilisateur sur ses sites quotidiens et via
  `Lumora.WinUI\Credentials\TestPages\credential-lab.html` (sections iframe,
  readonly, hors viewport, shadow DOM).

**Version :** `0.70.2-dev`.
