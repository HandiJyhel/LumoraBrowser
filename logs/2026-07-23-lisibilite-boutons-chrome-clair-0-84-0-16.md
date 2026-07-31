# 2026-07-23 - Lisibilite des boutons du chrome clair (0.84.0.16-dev)

## Demande

L'utilisateur montre une capture de la barre haute en theme clair : les
boutons visibles a droite de la barre d'adresse restent trop pales, au point
de devenir difficiles a lire malgre la correction precedente du fond global.

## Corrections appliquees

- `Lumora.WinUI/MainWindow.xaml`
  - ajout de brosses dediees aux boutons du chrome :
    `NovaChromeButtonBackgroundBrush`,
    `NovaChromeButtonBorderBrush`,
    `NovaChromeButtonForegroundBrush`,
    `NovaChromeButtonAccentBackgroundBrush`,
    `NovaChromeButtonAccentBorderBrush`,
    `NovaChromeButtonAccentForegroundBrush` ;
  - ajout de brosses dediees aux boutons modules :
    `NovaModuleButtonBackgroundBrush`,
    `NovaModuleButtonBorderBrush`,
    `NovaModuleButtonForegroundBrush` ;
  - recalage des styles `NovaChromeIconButtonStyle`,
    `NovaChromeAccentIconButtonStyle` et
    `NovaModuleIconButtonStyle` pour utiliser de vraies surfaces visibles en
    theme clair au lieu d'un rendu trop transparent.
- `Lumora.WinUI/MainWindow.SettingsTheme.cs`
  - calcul dynamique de ces nouvelles brosses en theme clair, theme sombre et
    contraste eleve ;
  - renforcement du contraste du bouton accent principal dans la barre.
- `Lumora.WinUI/MainWindow.SearchAssist.cs`
  - opacite du bouton assombri remontee de `0.5` a `0.72`.
- `Lumora.WinUI/MainWindow.Reader.cs`
  - opacite du bouton mode lecture indisponible remontee de `0.5` a `0.72`.
- `Lumora.WinUI/MainWindow.ReadingLens.cs`
  - opacite du bouton loupe remontee de `0.5` a `0.72`.
- `Lumora.WinUI/MainWindow.ReadAloud.cs`
  - opacite du bouton lecture vocale remontee de `0.5` a `0.72`.
- montee de version en `0.84.0.16-dev` dans :
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

- artefact propre genere avec succes :
  - `artifacts/clean-test/Lumora-0.84.0.16-dev-win-x64-clean-20260723-205346`
- manifeste SHA256 artefact propre :
  - `artifacts/signatures/Lumora-0.84.0.16-dev-clean-20260723-205426.sha256`
- installateur genere avec succes :
  - `artifacts/installer/LumoraSetup-0.84.0.16-dev-win-x64.exe`
- fichier de verification installeur :
  - `artifacts/installer/LumoraSetup-0.84.0.16-dev-win-x64.VERIFICATION.txt`
- manifeste SHA256 installeur :
  - `artifacts/signatures/LumoraSetup-0.84.0.16-dev-20260723-205459.sha256`
- SHA256 installeur :
  - `42e78ff9b761729e216f1f18d1f2614ddff27596db00e27a59c04b58741de529`
- ancien installateur `0.84.0.15-dev` laisse en place sur cette passe ;
- cette iteration livre donc a la fois la correction de lisibilite du chrome
  clair et le setup associe en `0.84.0.16-dev`.

## Version

`0.84.0.16-dev`
