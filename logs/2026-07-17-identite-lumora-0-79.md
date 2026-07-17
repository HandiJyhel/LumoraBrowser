# Identite graphique Lumora - 0.79.0-dev

Retour utilisateur : le nom Lumora evoque la lumiere, mais l'identite visuelle
du navigateur ne symbolisait pas assez la lumiere ni internet.

- Passage de version a `0.79.0-dev`.
- Refonte de `scripts/generate-app-icon.ps1` : nouveau signe Lumora avec fond
  bleu-profond, coeur lumineux, arcs de globe et trajectoire de navigation.
- Regeneration de `Lumora.WinUI/Assets/LumoraApp.png` et
  `Lumora.WinUI/Assets/LumoraApp.ico`.
- Nouvelle palette WinUI : accent lumiere jaune, contrepoint cyan, surfaces
  bleu-profond et focus plus lumineux.
- Raccordement du logo reel dans la barre plein ecran, la page A propos, les
  ecrans de connexion et le wizard.
- Page d'accueil `lumora://accueil` harmonisee avec le nouveau logo, une ligne
  lumineuse et un fond plus coherent avec l'idee lumiere + web.
- Fenetre d'application web, accueil navigation privee et palette de
  l'installateur actif alignes sur la nouvelle charte.
- Documentation ajoutee : `docs/IDENTITE_LUMORA_0_79.md`.

Verification locale :

- Generation des assets reussie via `scripts/generate-app-icon.ps1`.
- Inspection visuelle de `LumoraApp.png` : logo lisible, symbole lumiere +
  internet clairement present.
- Controle textuel : plus de `0.78.3.4.20-dev` ni d'anciennes couleurs ciblees
  dans les fichiers actifs touches.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 469/469 tests
  verts.
- `scripts\build-winui.ps1` : restore/build reussis hors sandbox, 0
  avertissement, 0 erreur.
- Artefact propre :
  `artifacts\clean-test\Lumora-0.79.0-dev-win-x64-clean-20260717-122451`.
- SHA256 executable hote :
  `2b61456e1034b9498c562cf65369748da6cfbd707192b6045e7ba66586cb84ad`.
- Installateur :
  `artifacts\installer\LumoraSetup-0.79.0-dev-win-x64.exe`.
- SHA256 installateur :
  `61a85e75757a67a4fdebe4842b9ddeba54366dc1bea5e696fbb181b6a2a445e1`.
- SHA256 `LumoraApp.png` :
  `2815A01358A8A39650A0EEC1CCFA93AA97D2FC184CC9D796AA3F724D14F8A40F`.
- SHA256 `LumoraApp.ico` :
  `28B4103EB20528713B94D474BFC75870D3E179E21EB2D87D60D61C8D03FDA00C`.
