# 2026-07-11 - Pulse Browser 0.57.5-dev

## Objectif

Corriger deux retours utilisateur apres `0.57.4-dev` :

- le rail vertical compact reste impossible a identifier clairement sur capture ;
- un site tiers semble connecter l'utilisateur via Google, mais la session ne revient pas au site d'origine et le bouton `Connexion` ne reagit plus.

## Changements

- Le rail vertical compact passe de 60 px a 64 px.
- Les marges compactes du rail sont reduites pour laisser plus de surface aux actions.
- Les deux actions du rail compact utilisent maintenant une icone avec mini-libelle :
  - `Onglet` pour creer un nouvel onglet ;
  - `Liste` pour agrandir/afficher la liste des onglets.
- L'ancien symbole compact `>>`, trop proche d'un prompt technique et mal rendu visuellement, est supprime.
- Toutes les popups WebView2 sont maintenant raccordees a `args.NewWindow`, y compris celles qui arrivent deja avec une URL directe.
- Les flux OAuth/Google conservent ainsi le lien WebView2 attendu entre la popup et l'onglet d'origine (`window.opener`, `postMessage`, fermeture de popup).
- Ajout de la fermeture d'onglet sur `WindowCloseRequested`, pour que les popups qui appellent `window.close()` puissent se refermer proprement.

## Limite de verification

- Le site adulte signale par l'utilisateur n'a pas ete teste en interaction reelle. Le correctif vise le probleme structurel des popups OAuth detachees ; la confirmation finale doit etre faite sur le site concerne.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 174/174 tests verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.57.5-dev` : reussi, 0 avertissement/erreur apres autorisation reseau NuGet.
- `scripts\build-installer.ps1 -Version 0.57.5-dev` : reussi apres autorisation reseau NuGet.

## Artefacts

- Artefact propre : `artifacts\clean-test\PulseBrowser-0.57.5-dev-win-x64-clean-20260711-022717`
- Manifeste propre : `artifacts\signatures\PulseBrowser-0.57.5-dev-clean-20260711-022742.sha256`
- SHA256 `PulseBrowser.WinUI.dll` : `c2db86eeb76763050cf69495cf5879a2e6ebdd94333b376c1964542129257746`
- SHA256 executable hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`
- Installateur : `artifacts\installer\PulseBrowserSetup-0.57.5-dev-win-x64.exe`
- Taille installateur : `35895946` octets
- SHA256 installateur : `eec3da27f6acc0bb23646c8d453af66297418dc05c2bcc1eed5c26f36ef390a4`
