# Lumora 0.83.45-dev - Repere contextuel et recentrage de focus

## Intent

Continuer l'accessibilite distinctive de Lumora sans tomber dans
l'accumulation de toggles. Ce lot vise surtout la desorientation :
quand une personne se perd dans le shell, Lumora doit pouvoir lui relire
ou elle se trouve et la ramener vite vers le bon point.

## Changements

- Ajout du fichier `Lumora.WinUI/MainWindow.AccessibilityContext.cs`.
- Nouveaux raccourcis globaux :
  - `Ctrl+Alt+F` : annonce du repere courant.
  - `Ctrl+Alt+R` : recentrage du focus sur la zone utile.
- Le repere courant combine maintenant :
  - la grande zone shell active ;
  - la surface visible (page, panneau, section Parametres) ;
  - le controle cible quand il possede un nom exploitable.
- Le flyout `Confort rapide` affiche aussi ce resume contextuel et expose
  deux actions directes :
  - `Ou suis-je maintenant ?`
  - `Recentrer le focus`
- `MainWindow.AccessibilityNavigation.cs` accepte maintenant un recentrage
  silencieux (`announce: false`) pour reutiliser la logique de zones sans
  spammer les annonces UIA.
- La carte `Raccourcis Lumora` dans les Parametres > `Confort` et son
  annonce associee mentionnent aussi `Ctrl+Alt+F` et `Ctrl+Alt+R`.

## Pourquoi

- Les options basiques d'accessibilite traitent surtout l'affichage et les
  lecteurs d'ecran, mais pas assez la perte de repere.
- Un navigateur riche en panneaux, modules et modes a besoin d'un filet
  anti-desorientation, surtout quand le focus saute entre page web,
  panneaux internes et barre basse.
- Ce comportement est plus identitaire pour Lumora qu'un simple
  "agrandir le texte" deja couvert ailleurs.

## Verification prevue

- `dotnet test .\Lumora.Tests\Lumora.Tests.csproj --no-restore -p:BaseIntermediateOutputPath=artifacts\tmp\tests\obj\ -p:MSBuildProjectExtensionsPath=artifacts\tmp\tests\obj\`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-winui.ps1`
