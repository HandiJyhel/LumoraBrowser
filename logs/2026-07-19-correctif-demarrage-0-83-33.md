# Correctif de demarrage apres regression accessibilite - 0.83.33-dev

## Contexte

Apres les ajustements d'accessibilite du lot `0.83.33-dev`, Lumora ne
demarrait plus cote WinUI.

## Cause

Le build complet a revele une erreur de compilation dans
`Lumora.WinUI/MainWindow.SiteControl.cs`.

Dans `BuildSitePermissionRow(...)`, le code utilisait
`descriptor.Title` pour enrichir le nom accessible d'un `ComboBox`, alors
que `SitePermissionDescriptor` expose `Key`, `Label` et `Detail`.

## Correctif

- remplacement de `descriptor.Title` par `descriptor.Label` ;
- conservation du libelle accessible attendu pour les permissions du
  `Centre du site`, sans changer le comportement fonctionnel du panneau.

## Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
  : build WinUI complet reussi, `0 avertissement`, `0 erreur`.
- lancement de verification de
  `Lumora.WinUI\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\Lumora.WinUI.exe`
  : processus demarre correctement (`START_OK`), puis ferme apres le test.

## Fichiers touches

- `Lumora.WinUI/MainWindow.SiteControl.cs`
- `MEMORY.md`
