# 2026-07-11 - Pulse Browser 0.59.3-dev - Barre de favoris compacte

## Objectif

Suite a la comparaison visuelle avec Google Chrome, corriger la barre des favoris pour qu'elle ressemble davantage a une barre de navigateur classique : ligne fine, elements plats, favicons compactes, dossiers inline et debordement discret.

## Changements

- Hauteur de barre reduite a 26 px.
- Suppression de l'icone decorative fixe en debut de barre.
- Espacement reduit entre favoris.
- Boutons de favoris rendus plus plats et plus compacts :
  - hauteur 22 px ;
  - padding reduit ;
  - coins moins arrondis ;
  - largeur minimale supprimee ;
  - favoris icone seule rendus sans texte ni espace vide.
- Calcul de debordement remplace par une estimation par favori :
  - favoris icone seule : largeur compacte ;
  - dossiers/favoris nommes : largeur estimee selon le titre ;
  - reserve du bouton de debordement uniquement quand il reste des elements.
- Bouton de debordement remplace par `»`, plus proche du comportement attendu.
- Passage de version source a `0.59.3-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 180 tests reussis.
- `scripts\build-winui.ps1` : build WinUI reussi, 0 avertissement, 0 erreur.
- Artifact propre genere : `artifacts\clean-test\PulseBrowser-0.59.3-dev-win-x64-clean-20260711-181720`.
- SHA256 executable propre : `2695bcae6456a5d3f9aaed2fa4c8cc9738ac524cbd7862fd035e668144ac33a0`.
- Installateur genere : `artifacts\installer\PulseBrowserSetup-0.59.3-dev-win-x64.exe`.
- SHA256 installateur : `e7e8127b773d16e7c81861b281a92f78a3a9c2ed8b4155217077aab220635c4d`.
