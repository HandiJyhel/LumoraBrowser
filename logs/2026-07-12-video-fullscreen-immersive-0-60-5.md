# 2026-07-12 - Plein ecran video immersif (0.60.5-dev)

## Contexte

Le retour utilisateur montrait une video YouTube dite "plein ecran" avec encore
la barre d'adresse, les favoris et le rail lateral visibles. Le plein ecran
contenu WebView2 n'etait pas relie au mode immersif Pulse.

## Changements

- Ajout d'un etat plein ecran contenu (`_contentFullScreenCore`) distinct du
  plein ecran Pulse.
- Branchement de `CoreWebView2.ContainsFullScreenElementChanged`.
- Quand une page contient un element plein ecran, Pulse bascule la fenetre en
  `AppWindowPresenterKind.FullScreen`.
- Le layout immersif cache la chrome haute, les favoris, les onglets et le rail
  lateral permanent.
- Les colonnes du rail vertical passent a zero en immersif ; le rail revient
  seulement au survol gauche, comme overlay.
- Le dock haut revient au survol haut et affiche une adresse compacte.
- `Echap` utilise la sortie immersive commune.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` :
  233 tests reussis.
- `build-winui.cmd` : 0 avertissement, 0 erreur apres autorisation reseau NuGet.
- Artifact propre :
  `artifacts\clean-test\PulseBrowser-0.60.5-dev-win-x64-clean-20260712-142233`.
- SHA256 exe hote :
  `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- Installateur :
  `artifacts\installer\PulseBrowserSetup-0.60.5-dev-win-x64.exe`.
- SHA256 installateur :
  `4f4552dcdb1010843b6a097eb5bc4b03b1d76834206fe3dd9a920ede3a81271b`.

## Limite

- La compilation valide le branchement WebView2, mais le rendu YouTube plein
  ecran doit etre confirme visuellement apres installation sur le poste
  utilisateur.
