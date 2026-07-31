# 2026-07-24 - Barre d'adresse nettoyee et chrome recentre

## Contexte

L'utilisateur a pointe une incoherence franche du chrome principal : un repere
`accueil` se retrouvait visible dans la barre d'adresse alors que cette zone
doit rester dediee a la navigation et a la recherche. Le probleme etait autant
graphique que structurel, car il venait d'un badge interne ajoute dans le champ
lui-meme.

## Corrections appliquees

- suppression du `AddressContextBadge` dans `Lumora.WinUI/MainWindow.xaml` ;
- simplification de `UpdateAddressIdentityChrome` dans
  `Lumora.WinUI/MainWindow.xaml.cs` pour ne plus injecter de libelle contextuel
  dans le champ d'adresse ;
- conservation du seul marqueur visuel identitaire gauche, afin de garder la
  signature Lumora sans polluer la saisie ;
- alignement de la version projet sur `0.84.0.36-dev` dans les fichiers de
  gouvernance et de verification.

## Verification prevue

- build WinUI via `scripts/build-winui.ps1` ;
- test cible `UsageModeVisualIdentityTests` apres montee de version.

## Livraison

- aucun installateur ni executable de release genere sur cette passe.
