# Passe du 2026-07-24 - Bloc droit du chrome recompose (0.84.0.35-dev)

## Demande

Corriger la zone la plus incoherente de la barre haute, visible sur la capture
utilisateur : le bloc droit du chrome.

## Constat

Le rendu restait bizarre meme apres le premier recentrage :
- halo droit trop ample ;
- une seule grande capsule visuelle pour des usages pourtant differents ;
- sensation de masse floue entre outils, modules, Studio et menu.

## Modifications

- reduction du halo decoratif droit et recentrage vertical ;
- separation de l'ancien grand bloc visuel en deux surfaces :
  `NavigationToolbarToolsShell` et `NavigationToolbarIdentityShell` ;
- maintien d'une continuite graphique, mais avec une lecture plus nette ;
- attenuation adaptative des surfaces droites quand la largeur disponible
  diminue.

## Verification

- build WinUI reussie via `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1` ;
- tests `UsageModeVisualIdentityTests` reussis via
  `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore --filter UsageModeVisualIdentityTests -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\` ;
- aucune generation d'installateur sur cette passe.
