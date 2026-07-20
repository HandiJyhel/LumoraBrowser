# Confort rapide footer - 2026-07-19

## Contexte

Les aides d'accessibilite avancees de Lumora devenaient solides, mais
l'acces restait encore trop profond pour un usage quotidien : il fallait
ouvrir les parametres puis retrouver la bonne section avant d'ajuster le
confort.

## Ajouts

- Nouveau bouton `Confort rapide` dans la barre basse.
- Flyout dedie avec :
  - profil courant ;
  - resume d'etat ;
  - presets `Equilibre`, `Mode calme`, `Vision fatiguee`, `Lecture profonde` ;
  - toggles directs pour :
    - `Guide de lecture`
    - `Loupe de lecture`
    - `Lecture a voix haute`
  - action `Faire relire l'etat de confort`.

## Raccourcis

- `Ctrl+Alt+6` : `Equilibre`
- `Ctrl+Alt+7` : `Mode calme`
- `Ctrl+Alt+8` : `Vision fatiguee`
- `Ctrl+Alt+9` : `Lecture profonde`
- `Ctrl+Alt+0` : relire l'etat courant

## Technique

- Nouveau fichier : `Lumora.WinUI/MainWindow.AccessibilityQuickActions.cs`
- Le texte du bouton footer suit automatiquement le profil detecte.
- L'etat annonce les aides actives : texte lisible, contraste, transitions,
  guide de lecture, loupe et lecture vocale.
- Les toggles clees utilisent maintenant `UpdateStatusText(...)` pour
  pousser une annonce active au lieu d'un simple changement visuel.

## Verification prevue

- `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
