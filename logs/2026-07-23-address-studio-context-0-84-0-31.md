# 2026-07-23 - Barre d'adresse orbitale et menus Lumora simplifies

## Objectif

Finir la passe d'identite graphique sans laisser les zones les plus visibles en
retard : barre d'adresse, suggestions, Studio Lumora et menus contextuels.

## Modifications

- `Lumora.WinUI/MainWindow.xaml`
  - reorganisation du `StudioLumoraFlyout` en trois blocs plus courts :
    presets, position des onglets/favoris, ambiance et bascules essentielles ;
  - transformation de la barre d'adresse en capsule plus profonde, avec
    liseres lumineux, badge `LUMORA` retravaille et panneau de contexte a
    droite ;
  - refonte de `AddressSuggestionsPopup` avec entete locale, cartes d'icones
    et badges de nature de suggestion.

- `Lumora.WinUI/MainWindow.xaml.cs`
  - ajout de `UpdateAddressIdentityChrome`, `CompactAddressHost` et
    `CompactAddressDraft` pour faire vivre le repere de contexte dans la barre
    d'adresse ;
  - version passee a `0.84.0.31-dev`.

- `Lumora.WinUI/MainWindow.AddressSuggestions.cs`
  - mise a jour du repere de contexte a chaque frappe, y compris pendant une
    recherche locale.

- `Lumora.WinUI/MainWindow.LayoutStudio.cs`
  - creation d'un en-tete commun pour les menus Lumora ;
  - ajout de `CurrentWorkspaceSummary` pour reutiliser un resume court et
    coherent ;
  - renommage du sous-menu `Presets rapides` en `Disposition rapide`.

- `Lumora.WinUI/MainWindow.TabGroups.cs`
  - en-tete contextuel dans les menus d'onglet et de groupe ;
  - ajout d'un acces `Studio Lumora` depuis le clic droit des onglets.

- `Lumora.WinUI/MainWindow.BookmarksFlyouts.cs`
  - en-tetes contextuels pour la constellation et pour les favoris individuels
    ou dossiers.

- `Lumora.WinUI/MainWindow.History.cs`
  - en-tete contextuel sur le clic droit d'une entree d'historique ;
  - ajout d'une action `Ouvrir dans un nouvel onglet`.

- `scripts/build-installer.ps1`
- `scripts/build-clean-test-artifact.ps1`
- `Lumora.Tests/UsageModeVisualIdentityTests.cs`
  - alignement de la version `0.84.0.31-dev`.

## Verification

- Build WinUI :
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
  - resultat : succes, `0 avertissement`, `0 erreur`

- Tests :
  - `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore --filter UsageModeVisualIdentityTests -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
  - resultat : succes

## Livraison

- aucune generation d'installateur ;
- `MEMORY.md` mis a jour ;
- version courante : `0.84.0.31-dev`.
