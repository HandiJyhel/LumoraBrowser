# 2026-07-13 - Fenetre de navigation privee 0.67.0-dev

## Changements

- Ajout de `LumoraPrivateWindow.xaml(.cs)` : fenetre minimale (retour/suivant/recharger + barre d'adresse) sur profil WebView2 InPrivate.
- Environnement WebView2 explicite (`CoreWebView2Environment.CreateWithOptionsAsync`) sur le meme dossier de donnees que le process, avec `CoreWebView2ControllerOptions.IsInPrivateModeEnabled = true` et profil nomme `lumora-prive` (toutes les fenetres privees partagent la meme session ephemere, purgee par le moteur a la liberation).
- Protections reseau identiques aux fenetres d'application web : TelemetryBlocker, NetworkBlocker, ParameterCleaner, HttpsEnforcer, CnameUncloaker, avec la liste blanche utilisateur.
- `NewWindowRequested` ouvre une nouvelle fenetre privee (`args.Handled = true`) : pas d'echappement vers le profil normal.
- Page d'accueil privee en `NavigateToString` (explique ce qui est conserve ou non), autofill/autosave Chromium coupes, SmartScreen suivant le reglage utilisateur.
- Extraction de `AddressNormalizer` (classe pure) depuis `MainWindow.NormalizeAddress`/`SearchUrl`, partagee par les deux fenetres.
- Entrees : menu `Naviguer > Nouvelle fenetre privee` (2 menus), palette de commandes, accelerateur `Ctrl+Shift+N`. Bloque tant que l'overlay de connexion ou l'assistant de demarrage est affiche.

## Verification

- `dotnet test Lumora.Tests` : 276/276 tests verts (dont 12 nouveaux `AddressNormalizerTests`).
- `build-winui.cmd` : 0 avertissement, 0 erreur.
- Lancement court de `Lumora.WinUI.exe` : processus vivant apres 12 s, arret propre.
- Test manuel restant : ouvrir une fenetre privee, verifier qu'un login n'apparait pas dans `Sites connectes` de la fenetre principale, fermer et rouvrir pour confirmer la purge.
