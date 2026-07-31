# 2026-07-23 - Bouton favoris visible (0.84.0.19-dev)

## Demande

L'utilisateur rappelle que le theme clair et le theme sombre doivent offrir la
meme qualite de lisibilite, puis montre que le bouton favoris reste encore
quasi invisible en clair.

## Corrections appliquees

- `Lumora.WinUI/MainWindow.xaml`
  - ajout des ressources :
    `NovaBookmarkButtonBackgroundBrush`,
    `NovaBookmarkButtonBorderBrush`,
    `NovaBookmarkButtonForegroundBrush`,
    `NovaBookmarkButtonActiveBackgroundBrush`,
    `NovaBookmarkButtonActiveBorderBrush`,
    `NovaBookmarkButtonActiveForegroundBrush` ;
  - ajout des styles :
    `NovaBookmarkIconButtonStyle` et `NovaBookmarkFontIconStyle` ;
  - le bouton favoris utilise maintenant cette palette dediee ;
  - l'etoile est rendue avec un glyphe plein, plus lisible.
- `Lumora.WinUI/MainWindow.SettingsTheme.cs`
  - calcul dynamique de la palette favoris en clair, sombre et contraste
    eleve.
- `Lumora.WinUI/MainWindow.Bookmarks.cs`
  - l'etat non favori utilise explicitement la palette favoris neutre ;
  - l'etat favori applique explicitement la palette active sur l'icone, le
    fond et la bordure du bouton.
- montee de version en `0.84.0.19-dev` dans :
  - `MainWindow.xaml.cs`
  - `AGENTS.md`
  - `scripts/build-installer.ps1`
  - `scripts/build-clean-test-artifact.ps1`
  - `Lumora.Tests/UsageModeVisualIdentityTests.cs`

## Verification

- test cible passe :
  - `dotnet test Lumora.Tests\Lumora.Tests.csproj --filter UsageModeVisualIdentityTests`
  - resultat : `11/11` tests reussis

## Livraison

- aucune regeneration d'artefact ou d'installateur sur cette passe ;
- cette iteration se concentre sur le bouton favoris et la parite de
  visibilite entre theme clair et theme sombre.

## Version

`0.84.0.19-dev`
