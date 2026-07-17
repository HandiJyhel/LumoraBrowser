# 2026-07-16 - Detection WebView2 corrigee et verifiee pour de vrai (0.78.3.4.13-dev)

## Contexte

Le correctif de la 0.78.3.4.12-dev (GUID `{F3017226-FE2A-4295-8BDF-00C3A9C7C2BF}`)
ne resolvait pas le probleme : l'utilisateur a reteste l'installeur et a
obtenu exactement la meme erreur "WebView2 n'est toujours pas detecte apres
installation."

## Diagnostic

Le GUID utilise dans le correctif precedent etait invente/mal memorise, pas
verifie contre une source reelle. Inspection directe du registre de la
machine de test :

- `HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients` contient en realite
  `{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}` (pv `150.0.4078.65`) — different a
  la fois du GUID tronque d'origine et du GUID invente du correctif precedent.
- Ce GUID de client EdgeUpdate n'est pas un identifiant documente de facon
  fiable et verifiable : mieux vaut ne pas en dependre du tout.
- En revanche, l'entree de desinstallation Windows
  `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView`
  a un `DisplayName` = "Microsoft Edge WebView2 Runtime" — c'est exactement ce
  que Windows affiche dans "Applications installees", stable et lisible.

## Changement

- `scripts/installer/Program.cs.template` : `IsWebView2RuntimeInstalled()`
  ne cherche plus un GUID de client EdgeUpdate. Elle parcourt les entrees de
  desinstallation (`HKLM` avec et sans `WOW6432Node`, plus `HKCU`) et detecte
  toute entree dont le `DisplayName` contient "WebView2".

## Verification (cette fois faite pour de vrai avant de conclure)

- Reproduction en PowerShell de la logique de detection : `True` sur la
  machine de test, en trouvant bien l'entree "Microsoft Edge WebView2
  Runtime".
- **Compilation et execution d'un petit programme C# autonome** reprenant
  exactement le code ajoute dans `Program.cs.template` (meme methodes, meme
  logique, meme API `Microsoft.Win32.Registry`) : execute avec succes,
  affiche `Trouve : Microsoft Edge WebView2 Runtime (cle: Microsoft EdgeWebView)`
  puis `IsWebView2RuntimeInstalled() = True`. Contrairement au correctif
  precedent, le code reellement modifie a ete verifie en conditions reelles
  avant d'etre considere comme fonctionnel.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 456/456 (aucun
  code applicatif touche, seulement l'installeur).
- Test complet du flux d'installation (case a cocher WebView2 + clic
  "Installer" reel) laisse a l'utilisateur, car il modifie reellement les
  programmes installes sur sa machine.

## Artefacts

- Installateur : `artifacts\installer\LumoraSetup-0.78.3.4.13-dev-win-x64.exe`
- SHA256 installateur :
  `70ea327d979cb1ec64a37f2dc171ee3ae338106c75aa296a61c75d0f0fc92aa0`
- Reutilise l'artefact application propre de la 0.78.3.4.12-dev (aucun code
  applicatif modifie dans ce correctif) :
  `artifacts\clean-test\Lumora-0.78.3.4.12-dev-win-x64-clean-20260716-223911`
