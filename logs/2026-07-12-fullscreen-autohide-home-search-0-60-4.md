# 2026-07-12 - Plein ecran auto-masque et champ d'accueil (0.60.4-dev)

## Contexte

Retour utilisateur apres installation de `0.60.3-dev` :

- le champ de recherche de `pulse://accueil` pouvait encore afficher `9` apres
  connexion ;
- le mode plein ecran restait trop proche du mode fenetre classique ;
- le reglage accessibilite lie aux animations etait trop flou ;
- le chantier personnalisation devait devenir plus visible.

## Changements

- Verrouillage defensif du champ de recherche d'accueil au chargement :
  `value=""`, `readonly` temporaire, `autocomplete="new-password"`, purge JS
  sur `DOMContentLoaded`/`pageshow`, puis purge cote WebView apres navigation.
- L'overlay de connexion desactive les interactions du `BrowserHost` tant qu'il
  est visible et nettoie la recherche d'accueil apres saisie PIN/fermeture.
- Nouveau reglage `NewTabFocusSearchOnOpen`, desactive par defaut, pour eviter
  qu'une saisie sensible parte vers la page d'accueil.
- Nouveau reglage `FullScreenAutoHideChrome`, active par defaut : en plein
  ecran, les barres se masquent et reviennent au survol du haut/gauche.
- Section Parametres > Apparence renommee en Parametres > Personnalisation,
  avec controles dedies au plein ecran et au focus du nouvel onglet.
- Libelle accessibilite clarifie : "Limiter les transitions visuelles".

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` :
  233 tests reussis.
- `build-winui.cmd` : 0 avertissement, 0 erreur apres autorisation reseau NuGet.
- `scripts\build-clean-test-artifact.ps1 -Version 0.60.4-dev` : artifact propre
  genere.
- Artifact :
  `artifacts\clean-test\PulseBrowser-0.60.4-dev-win-x64-clean-20260712-135943`.
- SHA256 exe hote :
  `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- `scripts\build-installer.ps1 -Version 0.60.4-dev` : installateur genere.
- Installateur :
  `artifacts\installer\PulseBrowserSetup-0.60.4-dev-win-x64.exe`.
- SHA256 installateur :
  `277ec3ade9b1ffb15cca17114c74a8b6669c5eb260ba9614d8a632c81ea5dd2b`.

## Limites

- Rendu visuel plein ecran et comportement exact au survol a confirmer sur le
  poste utilisateur avec l'installeur `0.60.4-dev`.
- Le chantier personnalisation est amorce, pas termine : palette/theme,
  densite, barre laterale, accueil et comportements de chrome peuvent ensuite
  etre organises en vraie page de personnalisation.
