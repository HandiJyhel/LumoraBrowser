# 2026-07-16 - Logo LumoraApp.png dans l'installateur

## Version

`0.78.3.4.7-dev`

## Demande

Correction apres retour utilisateur : l'installateur devait afficher le vrai logo
Lumora utilise dans le navigateur et sur le nouvel onglet, pas une icone extraite
de l'executable d'installation.

## Changements

- `scripts/build-installer.ps1` exige maintenant `Assets\LumoraApp.png` dans
  l'artefact propre.
- Le PNG est copie dans le projet temporaire de l'installateur et embarque comme
  ressource `LumoraApp.png`.
- `LoadInstallerLogo()` charge cette ressource embarquee via
  `Assembly.GetExecutingAssembly().GetManifestResourceStream("LumoraApp.png")`.
- `LumoraApp.ico` reste utilise comme icone Windows de l'executable, mais le
  visuel affiche dans l'interface de l'installateur vient du PNG du navigateur.
- Le fichier de verification de l'installateur mentionne explicitement le logo
  `LumoraApp.png` embarque.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 442/442 tests
  verts.
- `cmd /c .\build-winui.cmd` : OK apres relance autorisee hors sandbox
  (restauration NuGet bloquee en sandbox).
- Artefact propre :
  `artifacts\clean-test\Lumora-0.78.3.4.7-dev-win-x64-clean-20260716-033626`
- SHA256 executable Lumora :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
- Installateur :
  `artifacts\installer\LumoraSetup-0.78.3.4.7-dev-win-x64.exe`
- Taille installateur : `78039146` octets.
- SHA256 installateur :
  `b2a10e984db91d9907e854c97d39425b0f8640492509a93943e136459cab963a`
- Verification installateur :
  `artifacts\installer\LumoraSetup-0.78.3.4.7-dev-win-x64.VERIFICATION.txt`

## Note

L'installateur n'a pas ete lance interactivement pendant cette verification
pour eviter une installation locale non demandee.
