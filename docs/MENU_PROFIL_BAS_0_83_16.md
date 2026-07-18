# Menu profil bas - 0.83.16-dev

## Objectif

Transformer la zone du nom d'utilisateur en vrai menu de profil, plutot qu'en
simple lien direct vers une page.

## Changement

- Le bouton profil en bas ouvre maintenant un flyout de gestion.
- Le menu propose `Parametres utilisateur`.
- Le menu propose `Changer d'utilisateur`.
- Le menu propose `Creer un utilisateur`.
- Le contenu du flyout s'adapte au profil courant ou au mode invite.

## Intention produit

La zone basse doit devenir plus pratique et plus explicite :

- cliquer sur le nom ne doit plus surprendre par une redirection directe ;
- les actions attendues autour du profil doivent etre visibles tout de suite ;
- la gestion des utilisateurs locaux doit rester simple et locale.

## Implementation

- Ajout de `ProfileStatusFlyout` dans le bouton `ProfileStatusButton`.
- Ajout d'un resume dynamique du contexte profil dans le flyout.
- Ajout d'un point d'entree direct vers `Profils locaux` dans les parametres.
- Reutilisation d'helpers communs pour l'ouverture du selecteur et de la
  creation de profil.
- Ajout d'un test de garde-fou sur le menu profil et ses actions.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517 tests
  reussis.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : restore
  WinUI reussi, build WinUI reussi, 0 avertissement, 0 erreur.
