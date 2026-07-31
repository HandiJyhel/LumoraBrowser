# Passe du 2026-07-24 - Centre du site, Parametres et Modules (0.84.0.33-dev)

## Demande

Pousser l'identite graphique de Lumora plus loin sur trois surfaces encore trop
proches d'un panneau standard, sans alourdir l'usage :
- `SettingsPanel`
- `ModulesPanel`
- `SiteControlPanel`

La demande implicite restait la meme sur toute la sequence : donner du relief,
de la personnalite et une meilleure lisibilite a l'interface, tout en gardant
des manipulations courtes pour l'utilisateur.

## Modifications

### Styles partages

- ajout du style `NovaPanelActionButtonStyle` pour unifier les actions
  secondaires avec plus de relief, un contour plus present et une assise
  visuelle plus nette ;
- evolution du style `NovaModulePinButtonStyle` pour agrandir et materialiser
  les boutons d'epinglage au lieu de simples icones flottantes.

### Parametres

- transformation de la colonne laterale en navigation par cartes, avec un hero
  de tete et des groupes plus lisibles ;
- ajout d'une entree hero au debut du contenu pour rappeler que Lumora propose
  une personnalisation d'ambiance et d'ergonomie, pas un simple empilement
  d'options ;
- passage des cartes de la vue d'ensemble sur une surface plus marquee ;
- rehausse du bandeau d'application des changements pour garder une continuité
  visuelle jusqu'en bas du panneau.

### Modules

- remplacement de l'en-tete simple par une surface hero plus identitaire ;
- regroupement des actions de tete dans une vraie carte "Actions essentielles" ;
- mise en avant du mode d'usage comme carte hero ;
- passage des blocs Protections et Modules epinglables sur des cartes plus
  consistantes ;
- application du nouveau style de bouton a toutes les actions de module pour
  renforcer la perception de commandes locales tangibles.

### Centre du site

- remplacement de l'entree standard par un hero qui explique tout de suite la
  portee du panneau ;
- passage des sections Protection, Session, Confort, Mots de passe, Historique
  et Permissions sur des cartes plus fortes ;
- harmonisation des actions secondaires pour qu'elles restent visibles sans
  rivaliser avec l'action primaire "Retour au site".

## Verification

- build WinUI reussie via `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1` ;
- tests `UsageModeVisualIdentityTests` reussis via
  `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore --filter UsageModeVisualIdentityTests -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\` ;
- aucune generation d'installateur sur cette passe.
