# Passkeys - bouton Windows explicite en UIA - 0.83.39-dev

## Contexte

Apres validation live du `Centre du site`, des `Sessions` et du coffre
rapide, la passe d'audit a ete etendue au panneau `Clés d'accès`.

Le bloc `Gestion complète via Windows` affichait bien l'action
`Paramètres Windows — Clés d'accès`, mais la lecture UIA remontait surtout
des `TextBlock` et pas un bouton clairement nomme.

## Risque

Pour un utilisateur Narrator ou un pilotage clavier outille :

- l'action pouvait sembler purement informative ;
- la cible interactive dependait trop du contenu visuel imbrique ;
- le contrat d'accessibilite etait fragile face a de futurs refactors XAML.

## Correctif

- `MainWindow.xaml`
  - ajout de `x:Name="PasskeysWindowsSettingsButton"`
  - ajout de
    `AutomationProperties.Name="Ouvrir les paramètres Windows des clés d'accès"`

## Verification attendue

Le bouton devient une action UIA explicite et stable, independante du simple
texte affiche dans son `StackPanel`.

## Tests

- `Lumora.Tests/AccessibilityRegressionTests.cs`
  - verification de la presence du `x:Name`
  - verification du `AutomationProperties.Name`
