# Actions de chrome bas - 0.83.14-dev

## Objectif

Desengorger la barre du haut de Lumora sans affaiblir les actions de navigation.

## Changement

- `Mode d'usage` quitte la barre haute et rejoint la barre basse.
- `Lumie` quitte la barre haute et rejoint la barre basse.
- `Profil` reste en bas et devient le troisieme repere de cette zone.
- La barre du haut garde les actions de navigation et le hub Modules.

## Intention produit

La hierarchie devient plus claire :

- en haut : agir sur la page et la navigation ;
- en bas : agir sur le profil, l'ambiance et le compagnon.

La barre basse devient une vraie zone d'environnement Lumora, pas seulement une
ligne de statut.

## Implementation

- Deplacement de `ModeCompanionButton` vers `StatusBarRow`.
- Deplacement de `UsageModeButton` vers `StatusBarRow`.
- Regroupement de `ModeCompanionButton`, `UsageModeButton` et
  `ProfileStatusButton` dans une meme zone horizontale.
- Les flyouts de `Lumie` et du selecteur de mode s'ouvrent maintenant vers le
  haut depuis la barre basse.
- Le menu principal Lumora reste dans la barre haute.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 515 tests
  reussis.
- `powershell -ExecutionPolicy Bypass -File scripts\build-winui.ps1` : restore
  WinUI reussi, build WinUI reussi, 0 avertissement, 0 erreur.
