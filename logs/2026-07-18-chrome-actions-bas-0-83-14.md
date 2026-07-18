# 2026-07-18 - Actions de chrome bas (0.83.14-dev)

## Contexte

Retour utilisateur : la barre du haut devenait trop chargee apres les ajouts
de la veille. L'idee validee etait de reutiliser la zone basse, deja amorcee
avec le profil, pour y placer des options d'environnement plutot que de
navigation.

## Actions

- Version passee a `0.83.14-dev`.
- Deplacement du bouton `Mode d'usage` de la barre haute vers la barre basse.
- Deplacement du bouton `Lumie` de la barre haute vers la barre basse.
- Regroupement de `Lumie`, `Mode d'usage` et `Profil` dans `StatusBarRow`.
- Conservation du hub `Modules` et du menu principal dans la barre haute.
- Ajout d'un test de garde-fou pour verifier que `Lumie` et `Mode` ne sont plus
  dans `NavigationToolbar` mais bien dans `StatusBarRow`.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 515 tests
  reussis.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : restore
  WinUI reussi, build WinUI reussi, 0 avertissement, 0 erreur.

## Notes

- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora effectue.
