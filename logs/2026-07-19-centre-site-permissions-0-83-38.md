# Permissions du Centre du site - 0.83.38-dev

## Contexte

Le lot `0.83.37-dev` avait corrige un bouton muet dans `Confort`.
La passe suivante sur l'accessibilite du `Centre du site` a mis en evidence
un angle mort plus discret : les permissions dynamiques etaient nommees, mais
pas encore suffisamment accompagnees pour un usage confortable au lecteur
d'ecran.

## Probleme releve

- Le titre de permission (`Camera`, `Microphone`, etc.) et son texte
  d'explication n'etaient pas alignes sur `Texte plus lisible`.
- Le `ComboBox` annoncait bien `"{descriptor.Label} pour {rootDomain}"`,
  mais pas l'explication de la permission ni son etat courant.

Consequence : un utilisateur Narrator pouvait atteindre le controle et
comprendre l'intitule global, sans beneficier du contexte detaille que la
carte affiche visuellement juste a cote.

## Correctif

- `MainWindow.SiteControl.cs`
  - titre de permission branche sur `AccessibilityBodyFontSize()`;
  - texte d'explication branche sur `AccessibilitySecondaryFontSize()`;
  - ajout d'un `AutomationProperties.HelpText` sur chaque `ComboBox` avec :
    - l'explication de la permission ;
    - l'etat courant (`Demander`, `Autoriser`, `Bloquer`).

## Tests

- `Lumora.Tests/AccessibilityRegressionTests.cs`
  - verification de l'usage des tailles mutualisees dans `MainWindow.SiteControl.cs`;
  - verification de la presence de `AutomationProperties.SetHelpText(...)`.

## Impact

Ce n'est pas seulement une correction cosmetique :

- le `Centre du site` suit mieux les reglages de lisibilite de Lumora ;
- les permissions deviennent plus auto-explicatives sans exiger une lecture
  visuelle de toute la ligne ;
- l'experience se distingue davantage d'un navigateur "standard" en donnant
  du contexte vocal utile, pas seulement un nom de champ.
