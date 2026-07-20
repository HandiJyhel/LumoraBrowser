# Navigation clavier par zones - 2026-07-19

## Contexte

Lumora avait deja gagne en annonces UIA, en confort de lecture et en focus
automatique a l'ouverture des panneaux, mais il manquait encore une
traversee clavier globale du shell qui sorte du schema "tabulation longue
ou clic".

## Ajouts

- Nouveau fichier `Lumora.WinUI/MainWindow.AccessibilityNavigation.cs`.
- 5 zones shell exposees :
  - `Onglets`
  - `Barre d'adresse`
  - `Contenu actif`
  - `Outils et navigation`
  - `Compagnon et statut`
- Raccourcis :
  - `F6` : zone suivante
  - `Shift+F6` : zone precedente
  - `Ctrl+Alt+1..5` : saut direct vers une zone
- A chaque deplacement reussi, Lumora annonce :
  `Zone clavier : <zone>. <indice d'usage>`

## Details produit

- La zone `Contenu actif` cible la page web si aucun panneau interne n'est
  ouvert.
- Si un panneau interne est visible, Lumora cherche son premier controle
  tabbable et y replace le focus.
- La navigation par zones est desactivee pendant les overlays bloquants
  (`LoginOverlay`, `SetupWizardOverlay`, `CommandPaletteOverlay`) pour ne
  pas casser les parcours modaux.

## Garde-fous

- Test source ajoute dans
  `Lumora.Tests/AccessibilityRegressionTests.cs` pour verifier :
  - l'enregistrement des accelerateurs ;
  - les touches `F6` et `Ctrl+Alt+1..5` ;
  - l'annonce `Zone clavier : ...` ;
  - la presence des zones clefs et des garde-fous overlay.

## Verification prevue

- `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
