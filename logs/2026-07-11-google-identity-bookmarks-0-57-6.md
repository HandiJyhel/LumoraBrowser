# 2026-07-11 - Pulse Browser 0.57.6-dev

## Objectif

Corriger les retours utilisateur apres essai de `0.57.5-dev` :

- le flux Google Identity retombe sur une page blanche `accounts.google.com/gsi/transform` ;
- Pulse affiche a tort la barre `Utiliser un mot de passe fort genere ?` sur cette page Google intermediaire ;
- la barre des favoris a un rendu visuel casse/charge, avec des icones fallback peu lisibles.

## Changements

- Ajout d'une detection des pages intermediaires Google Identity (`accounts.google.com/gsi/*`, `accounts.google.com/o/oauth2/*`, `accounts.google.com/signin/oauth*`).
- Les popups Google Identity intermediaires ne volent plus l'onglet actif : si elles apparaissent dans Pulse, l'utilisateur est renvoye vers l'onglet source pendant que la transition continue.
- Les popups OAuth gardent toujours leur `CoreWebView2` rattache a `args.NewWindow`, pour conserver `window.opener` et les messages de retour vers le site d'origine.
- Le bloqueur reseau laisse passer uniquement les ressources critiques Google Identity dans ce contexte precis (`accounts.google.com` et ressources `gstatic` necessaires), sans desactiver globalement les protections.
- La capture/remplissage d'identifiants et la suggestion de mot de passe genere sont coupes sur ces pages intermediaires pour eviter les faux positifs.
- La barre des favoris est nettoyee :
  - espacement horizontal reduit ;
  - alignement vertical corrige ;
  - largeur des libelles ajustee ;
  - icones fallback remplacees par des `SymbolIcon` WinUI (`Folder`/`Link`) au lieu des glyphes MDL2 prives qui pouvaient s'afficher comme des carres.

## Limite de verification

- Le site adulte montre par l'utilisateur n'a pas ete teste avec connexion reelle. Le correctif cible le flux Google Identity observe dans la capture et doit etre confirme en usage reel.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 174/174 tests verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.57.6-dev` : reussi, 0 avertissement/erreur apres autorisation reseau NuGet.
- `scripts\build-installer.ps1 -Version 0.57.6-dev` : reussi apres autorisation reseau NuGet.

## Artefacts

- Artefact propre : `artifacts\clean-test\PulseBrowser-0.57.6-dev-win-x64-clean-20260711-024016`
- Manifeste propre : `artifacts\signatures\PulseBrowser-0.57.6-dev-clean-20260711-024044.sha256`
- SHA256 `PulseBrowser.WinUI.dll` : `895945402bb10e618a241bd1ac728ba0e59382da9f60384463393a98012bb271`
- SHA256 executable hote : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`
- Installateur : `artifacts\installer\PulseBrowserSetup-0.57.6-dev-win-x64.exe`
- Taille installateur : `35896970` octets
- SHA256 installateur : `9e7096dec1032dfd7fed701f5c39a96ee57c7ec461750ae4d3b97de72e177557`
