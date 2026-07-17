# 2026-07-15 - Barre Modules type extensions (0.78.3.4.1-dev)

## Demande

Correction de la passe `0.78.3.4-dev` : le besoin n'etait pas seulement un
grand panneau Modules, mais une petite barre de modules epingles, visible comme
la barre d'extensions de Chrome, avec une icone puzzle pour ouvrir la gestion
complete. Derniere correction utilisateur : ne pas multiplier les menus. Le
menu `...` doit rester le menu general du navigateur, et les modules doivent
vivre a part dans leur barre/puzzle.

## Changements

- Transformation des anciens boutons disperses en une capsule `ModulesQuickBar`
  avant le menu `...`.
- Ajout d'icones epinglees : mode lecture, notes/annotations, lecture a voix
  haute, telechargement video et recherche assistee.
- Ajout d'une icone puzzle `Tous les modules Lumora`, qui ouvre un flyout de
  gestion rapide : modules media, traduction locale, applications web, dictee,
  gestion complete et actions rapides Ctrl+K.
- Le menu `...` reste un menu navigateur complet : navigation, bibliotheque,
  securite/donnees, parametres et a propos. Les entrees `Modules epingles` et
  `Modules Lumora` ont ete retirees pour eviter la redondance.
- Les boutons de modules restent visibles comme des extensions. Les modules
  optionnels prennent une opacite reduite quand ils sont desactives, au lieu de
  disparaitre de la barre.
- Le script d'installeur accepte maintenant les versions produit a cinq
  segments comme `0.78.3.4.1-dev` : l'affichage produit garde la version
  complete, tandis que le projet technique Setup utilise une version compatible
  NuGet/MSBuild.

## Verification

- `cmd /c .\build-winui.cmd` : OK, 0 avertissement, 0 erreur.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 441/441 tests
  verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.78.3.4.1-dev -NoRestore` :
  artefact propre cree.
- Artefact :
  `artifacts\clean-test\Lumora-0.78.3.4.1-dev-win-x64-clean-20260715-231946`
- Executable SHA256 :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
- Installeur :
  `artifacts\installer\LumoraSetup-0.78.3.4.1-dev-win-x64.exe`
- Installeur SHA256 :
  `cdac8cb8564b0759f1bb11ade5f7517d1a1e5542ca5053043331c9987ee0d7d2`

## Version

`0.78.3.4.1-dev`
