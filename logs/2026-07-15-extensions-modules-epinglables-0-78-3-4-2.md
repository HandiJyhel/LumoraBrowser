# 2026-07-15 - Extensions Lumora epinglables (0.78.3.4.2-dev)

## Demande

Correction UX apres captures utilisateur : la barre `0.78.3.4.1-dev` etait
trop massive, trop redondante et pas presentable. L'objectif est de se rapprocher
du modele mental Chrome : quelques icones epinglees, un bouton puzzle pour tous
les modules, puis le menu `...` general.

## Changements

- Suppression de l'effet grosse capsule autour des modules epingles : la barre
  affiche maintenant des icones libres, plus proches d'une barre d'extensions.
- Le bouton puzzle ouvre un flyout `Extensions Lumora` plus propre : icone,
  nom du module, description courte, bouton d'epinglage et acces gestion.
- Ajout de l'epinglage/desepinglage persistant via `UiSettings.PinnedModuleIds`.
- Ajout d'icones epinglables pour les modules : mode lecture, notes, lecture a
  voix haute, video detachee, telechargement video, recherche assistee,
  traduction locale, applications web et dictee.
- Le panneau `Modules Lumora` devient une vraie page de gestion : liste
  structuree, description, action principale et interrupteur d'epinglage pour
  chaque module.
- Le menu `...` reste le menu general du navigateur et ne contient pas les
  modules.
- Correction finale d'espacement : les modules epingles, le bouton puzzle et le
  menu `...` ne sont plus colles. Le puzzle a sa propre colonne, un leger espace
  lateral et un separateur fin avant le menu general.
- Correction du script d'installeur : il ne tente plus de supprimer tous les
  anciens installateurs, seulement celui de la version courante. Cela evite un
  blocage si un ancien exe est verrouille.

## Verification

- `cmd /c .\build-winui.cmd` : OK, 0 avertissement, 0 erreur.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 441/441 tests
  verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.78.3.4.2-dev -NoRestore` :
  artefact propre cree.
- Artefact :
  `artifacts\clean-test\Lumora-0.78.3.4.2-dev-win-x64-clean-20260715-235813`
- Executable SHA256 :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
- DLL applicative SHA256 :
  `a82b33bf475726a2dbcd1478973af14549224a459ffe93a297da6d557761b231`
- Installeur :
  `artifacts\installer\LumoraSetup-0.78.3.4.2-dev-win-x64.exe`
- Installeur SHA256 :
  `f345f3344c5fae564217cc2224faef24344874b58a6eff735792588df548afc3`

## Version

`0.78.3.4.2-dev`
