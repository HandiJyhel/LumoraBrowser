# 2026-07-16 - Grille d'installateur sans chevauchement

## Version

`0.78.3.4.9-dev`

## Demande

Correction apres retour utilisateur : la passe precedente avait grossi le logo
et l'en-tete sans recalculer correctement la grille. Des textes etaient
masques, certains elements se chevauchaient et le nom complet de l'application
n'etait pas affiche.

## Changements

- Passage en `0.78.3.4.9-dev`.
- Affichage du nom complet `Lumora Browser` dans la barre de titre et l'en-tete.
- Suppression des badges compacts de l'en-tete qui entraient en collision avec
  la version et le titre.
- Retour a une typographie stable `Segoe UI` dans le setup WinForms.
- Desactivation du comportement DPI qui amplifiait les dimensions sans que la
  grille soit adaptee.
- Version affichee sur deux lignes pour rester lisible.
- En-tete, avertissement, sections, options, progression et boutons recales.
- Panneau `Options` agrandi et colonnes reprises pour eviter les textes coupes.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 442/442 tests
  verts.
- `cmd /c .\build-winui.cmd` : OK apres relance autorisee hors sandbox
  (premiere tentative bloquee par `NU1301` et acces temp refuse).
- Artefact propre :
  `artifacts\clean-test\Lumora-0.78.3.4.9-dev-win-x64-clean-20260716-040144`
- SHA256 executable Lumora :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
- Installateur :
  `artifacts\installer\LumoraSetup-0.78.3.4.9-dev-win-x64.exe`
- Taille installateur : `78040682` octets.
- SHA256 installateur :
  `4a8c889483a81858395ef4f47a0bf6cedfbadaa342b76374f2861e92edea839d`
- Verification installateur :
  `artifacts\installer\LumoraSetup-0.78.3.4.9-dev-win-x64.VERIFICATION.txt`

## Note

L'installateur a ete ouvert pour controle visuel sans lancer l'installation.
