# 2026-07-16 - Installeur reecrit en templates propres (0.78.3.4.10-dev)

## Contexte

Apres plusieurs passes rapides sur l'interface de l'installateur, le script
`scripts/build-installer.ps1` contenait trop de logique UI, de packaging et de
generation C# au meme endroit. La demande etait de repartir proprement pour
alleger le code et eviter les couches accumulees.

## Changements

- Version passee a `0.78.3.4.10-dev`.
- `scripts/build-installer.ps1` remplace par un orchestrateur plus court :
  validation de l'artefact propre, staging, zip, injection de version, publish,
  verification et hash.
- Ajout de `scripts/installer/Lumora.Setup.csproj.template`.
- Ajout de `scripts/installer/Program.cs.template` pour isoler le code WinForms
  de l'installateur.
- Conservation du rendu corrige de l'installateur :
  - nom complet `Lumora Browser` ;
  - logo reel `LumoraApp.png` embarque ;
  - rendu haute qualite via `LogoImageControl` ;
  - grille sans badges compacts dans l'en-tete ;
  - typographie `Segoe UI` stable ;
  - `AutoScaleMode.None` pour eviter les collisions DPI ;
  - options et progression recalees.
- Conservation du comportement d'installation :
  - aucun profil utilisateur embarque ;
  - dossier par defaut dans `LOCALAPPDATA` ;
  - option WebView2 telechargee depuis Microsoft uniquement si le runtime est
    absent ;
  - raccourci Bureau, lancement apres installation et installation propre.
- Correction de la generation des templates pour lire explicitement les sources
  en UTF-8 et conserver les accents dans le C# genere.

## Verification

- Analyse PowerShell de `scripts/build-installer.ps1` : OK.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 442/442 tests
  verts.
- `cmd /c .\build-winui.cmd` : OK apres relance autorisee hors sandbox
  (premiere tentative bloquee par `NU1301` et acces temp refuse).
- Artefact propre :
  `artifacts\clean-test\Lumora-0.78.3.4.10-dev-win-x64-clean-20260716-041901`.
- SHA256 executable Lumora :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`.
- Installeur :
  `artifacts\installer\LumoraSetup-0.78.3.4.10-dev-win-x64.exe`.
- Taille installeur : `78040682` octets.
- SHA256 installeur :
  `c77cedc93e60a5de820c551e798f5f937645f31717692db061b15830d0bb296f`.
- Manifeste SHA256 :
  `artifacts\signatures\LumoraSetup-0.78.3.4.10-dev-20260716-042446.sha256`.
- Controle visuel par capture ciblee de la fenetre du setup :
  `artifacts\installer\LumoraSetup-0.78.3.4.10-dev-window-fixed.png`.
  Resultat : accents lisibles, nom complet visible, logo correct, sections et
  options sans chevauchement apparent.
