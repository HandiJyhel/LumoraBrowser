# 2026-07-15 - Modules Lumora, menus et A propos (0.78.3.4-dev)

## Demande

L'utilisateur a precise que la simple reorganisation du menu des trois points ne
suffisait pas. Plusieurs fonctions de Lumora doivent etre traitees comme des
modules integres, proches du modele mental des extensions dans les navigateurs
du marche, afin que l'utilisateur les trouve sans fouiller dans plusieurs
sous-menus.

## Changements

- Ajout d'un bouton accentue `Modules Lumora` dans la barre principale et dans
  la barre plein ecran.
- Ajout d'un panneau `Modules Lumora` avec tuiles d'action :
  mode lecture, notes/pages annotees, lecture a voix haute, video,
  applications web, recherche assistee, traduction locale, dictee et centre du
  site actuel.
- Ajout d'un flyout compact sur le bouton Modules pour les actions rapides :
  panneau Modules, palette Ctrl+K, mode lecture, lecture a voix haute, media.
- Reorganisation des menus `...` :
  `Navigation`, `Bibliotheque`, `Securite et donnees`, puis `Modules Lumora`,
  `Parametres` et `A propos`.
- Les anciens outils de page ne sont plus disperses dans le menu principal.
- La palette de commandes utilise maintenant la categorie `Modules Lumora` pour
  les outils concernes.
- `A propos` a ete modernise : en-tete identitaire, version, resume produit,
  onglet `Modules`, philosophie locale et informations techniques conservees.
- Les modules qui ouvraient un flyout depuis un bouton potentiellement masque
  peuvent maintenant etre lances depuis le hub Modules.

## Verification

- `cmd /c .\build-winui.cmd` : OK, 0 avertissement, 0 erreur.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 441/441 tests
  verts.
- `scripts\build-clean-test-artifact.ps1 -Version 0.78.3.4-dev -NoRestore` :
  artefact propre cree.
- Artefact :
  `artifacts\clean-test\Lumora-0.78.3.4-dev-win-x64-clean-20260715-225003`
- Executable SHA256 :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
- Installeur :
  `artifacts\installer\LumoraSetup-0.78.3.4-dev-win-x64.exe`
- Installeur SHA256 :
  `53378b29b0a7e25af9d388b8800498c7d8d99803f94cb88a5be9bab198500e52`

## Version

`0.78.3.4-dev`
