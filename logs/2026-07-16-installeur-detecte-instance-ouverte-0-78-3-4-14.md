# 2026-07-16 - L'installeur detecte une instance de Lumora ouverte (0.78.3.4.14-dev)

## Contexte

Apres correction de la detection WebView2 (0.78.3.4.13-dev), l'installeur a
echoue differemment : "Access to the path 'clrjit.dll' is denied." Ce n'etait
pas un bug de la correction WebView2 (qui a bien fonctionne cette fois, plus
aucune plainte a ce sujet) mais un cas different : Lumora Browser tournait
encore depuis le dossier d'installation cible
(`C:\Users\Handi-Jyhel\AppData\Local\Programs\Lumora\app\Lumora.WinUI.exe`,
confirme via `Get-Process`), donc Windows verrouillait ses fichiers quand
l'installeur tentait de supprimer/ecraser l'ancien dossier `app`.

## Changement

- `scripts/installer/Program.cs.template` : ajout de `IsLumoraRunning()`
  (`Process.GetProcessesByName("Lumora.WinUI")`) et d'une verification en
  tout debut de `Install(...)`, avant toute preparation de dossier. Si Lumora
  tourne encore, l'installateur leve une `InvalidOperationException` avec un
  message clair ("Lumora Browser est actuellement ouvert. Ferme completement
  l'application, puis relance l'installateur.") au lieu de tenter la copie et
  d'echouer avec une erreur .NET brute.

## Verification

- Build de l'installeur reussi.
- Verification du nom de processus reel (`Get-Process` / `GetProcessesByName`)
  contre l'instance de Lumora effectivement lancee sur la machine de test :
  correspondance confirmee (`Lumora.WinUI`).
- Aucun code applicatif touche : reutilise l'artefact propre de la
  0.78.3.4.12-dev.
- Test reel du flux d'installation (Lumora ouvert puis ferme) laisse a
  l'utilisateur.

## Artefacts

- Installateur : `artifacts\installer\LumoraSetup-0.78.3.4.14-dev-win-x64.exe`
- SHA256 installateur :
  `081c7e6b60d24c735b2de03bfeb2f7b8877d5584aac3208112aa81104f0b3b80`
