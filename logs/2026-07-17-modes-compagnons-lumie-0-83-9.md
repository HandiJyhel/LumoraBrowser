# 2026-07-17 - Modes compagnons Lumie (0.83.9-dev)

## Contexte

Retour utilisateur sur `0.83.8-dev` :

- les boutons du chrome etaient trop ressemblants et peu agreables ;
- l'accueil restait trop compacte malgre le deplacement en deux zones ;
- le changement de mode semblait avoir disparu ;
- l'objectif ou le post-it enregistre depuis l'accueil devenait penible a
  retrouver parce qu'il fallait repasser par le module Notes ;
- l'idee produit attendue est plutot un compagnon persistant type petit
  assistant local, inspire de l'ancien trombone Word, mais sans service distant
  ni question-reponse imposee.

## Changements

- Passage de la version projet a `0.83.9-dev`.
- Ajout de memoires locales par mode dans `UiSettings` :
  `CompanionBalancedMemo`, `CompanionFocusObjective`,
  `CompanionReadingNote`, `CompanionCreativePostIt`,
  `CompanionResearchTrail` et `CompanionNightReminder`.
- Remplacement du bouton compagnon rond par un bouton pilule distinct nomme
  `Lumie`.
- Ajout d'un libelle visible au bouton de mode : `Mode Equilibre`,
  `Mode Focus`, `Mode Lecture`, etc.
- Ajout d'une zone de memoire dans le flyout Lumie, avec sauvegarde locale.
- Raccordement des notes rapides de `lumora://accueil` a la memoire du
  compagnon au lieu d'un enregistrement cache uniquement dans Notes.
- Reprise de l'accueil pour une composition plus large et plus aeree.
- Ajout des controles Lumie au traitement d'accessibilite existant.
- Mise a jour des tests de regression des modes.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 512/512 tests
  reussis.
- Restore MSBuild WinUI hors sandbox : 0 avertissement, 0 erreur.
- Build WinUI Debug hors sandbox vers
  `artifacts\build-verify\winui-0.83.9-debug\` : 0 avertissement, 0 erreur.

## Notes

- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora effectue.
- La verification est compile/build/tests ; l'inspection visuelle en fenetre
  n'a pas ete lancee automatiquement conformement a la regle projet.
