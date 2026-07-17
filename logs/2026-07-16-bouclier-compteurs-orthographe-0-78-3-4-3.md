# 2026-07-16 - Bouclier avec compteurs et correction orthographique (0.78.3.4.3-dev)

## Demande

Dernière petite mise à jour de la journée : corriger l'orthographe visible et
améliorer le bouclier de confidentialité. Le bouclier doit afficher combien de
publicités et de trackers sont bloqués par page/site, expliquer ce qui a été
bloqué, rester performant, et ne pas réintroduire le blocage trop agressif
constaté précédemment.

## Changements

- Passage de la version à `0.78.3.4.3-dev`.
- Correction de libellés visibles dans les menus, le panneau confidentialité,
  le panneau modules et plusieurs messages de statut.
- Ajout d'un badge numérique sur le bouclier : il indique le nombre de blocages
  sur la page visible, plafonné visuellement à `99+`.
- `PrivacyEngine` conserve maintenant des compteurs structurés :
  total, publicités, trackers et autres blocages, séparés entre page courante,
  total de session et statistiques par site.
- Les détails récents du bouclier indiquent le type de blocage, le domaine, le
  chemin non sensible et la raison : publicité réseau, télémétrie, CNAME
  cloaking, popup publicitaire, redirection parasite, etc.
- Les statistiques par site sont gardées en mémoire et bornées : aucun stockage
  persistant inutile, aucune journalisation massive, pas de recalcul coûteux.
- Aucune règle de blocage agressive n'a été ajoutée : les décisions existantes
  restent inchangées, seul le comptage et l'explication ont été enrichis.
- Ajout de tests unitaires pour vérifier la séparation pubs/trackers, les stats
  par site, le reset de page et l'absence de paramètres sensibles dans les
  détails affichés.

## Vérification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 442/442 tests
  verts.
- `cmd /c .\build-winui.cmd` : OK, 0 avertissement, 0 erreur.
- `scripts\build-clean-test-artifact.ps1 -Version 0.78.3.4.3-dev -NoRestore` :
  artefact propre créé.
- Artefact :
  `artifacts\clean-test\Lumora-0.78.3.4.3-dev-win-x64-clean-20260716-002359`
- Exécutable SHA256 :
  `e94f299f13279fb76de4f892e136a1152d93c37d10e4621eac7616fbd3bce67f`
- DLL applicative SHA256 :
  `b76179a975403b229323be9f2626b5c7a8ba0bf3cbd20638e4e3b8cba2627a70`
- Installeur :
  `artifacts\installer\LumoraSetup-0.78.3.4.3-dev-win-x64.exe`
- Installeur SHA256 :
  `0c07227f227b099c4747a4877d5a0345d4a9fbca12bb7af52fb402ab11128106`

## Version

`0.78.3.4.3-dev`
