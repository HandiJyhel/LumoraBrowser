# 2026-07-12 - Suivi raccourcis nouvel onglet, sans correctif de code (0.60.9.1-dev)

## Contexte

L'utilisateur a confirme que 0.60.9-dev fonctionne : le raccourci "Google"
ajoute apparait desormais. Effet de bord attendu : 3 raccourcis crees dans
des sessions precedentes, jusque-la masques par le meme bug de toggle
desactive par defaut (corrige en 0.60.9-dev), sont eux aussi devenus visibles
d'un coup en meme temps que "Google".

## Analyse

Ce n'est PAS un bug de code : `_uiSettings.NewTabShortcuts` contenait deja
ces 3 entrees (donnees reelles creees par l'utilisateur lors de tests
precedents), simplement jamais affichees tant que
`NewTabShortcutsVisible` valait `false`. Une fois ce toggle active (par le
correctif 0.60.9-dev), TOUTE la liste sauvegardee redevient visible, pas
seulement le dernier ajout — c'est le comportement normal et attendu d'un
toggle "afficher/masquer la liste".

## Decision utilisateur

Demande de "repartir de 0" pour les raccourcis. Solution retenue : le bouton
"×" de suppression par raccourci, sur la page nouvel onglet elle-meme,
fonctionne deja (meme correctif de rafraichissement que l'ajout, 0.60.8-dev).
Aucun code supplementaire n'etait necessaire : l'utilisateur supprime les 3
raccourcis indesirables un par un via ce bouton, deja operationnel.

**Pourquoi pas d'edition directe du fichier de profil reel par l'IA** :
`ui-settings.pulse` du profil actif aurait pu etre ecrase silencieusement si
Pulse Browser tournait encore au moment de l'edition (l'app ne recharge/
resauvegarde pas en continu, mais toute sauvegarde explicite ulterieure cote
utilisateur aurait ecrase la modification faite hors-app). Action jugee trop
risquee sur des donnees utilisateur reelles pour un gain nul face au bouton
"×" deja fonctionnel.

## Changement

- Passage de version source a `0.60.9.1-dev` (a la demande explicite de
  l'utilisateur, purement pour le suivi — aucun changement de code
  fonctionnel dans cette entree).

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` : 233 tests
  reussis (build inchange sur le fond).
- `scripts\build-clean-test-artifact.ps1 -Version 0.60.9.1-dev` +
  `scripts\build-installer.ps1 -Version 0.60.9.1-dev` : reussis.
- Installateur :
  `artifacts\installer\PulseBrowserSetup-0.60.9.1-dev-win-x64.exe`.
- SHA256 installateur :
  `b23ab6906084bc535b862621a54961845fa6ceed8b580f9f316ae5c82a201116`.
