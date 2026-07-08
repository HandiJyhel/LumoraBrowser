# Navigation WinUI 3 0.8.2-dev

## Contexte

L'utilisateur a signale une regression majeure: la coque WinUI 3 ne permettait plus d'acceder a Internet, et les clics sur les favoris ne chargeaient pas les sites. Les dossiers de la barre de favoris ouvraient aussi le gestionnaire au lieu d'un menu de navigateur.

## Changements

- Passage de la version projet a `0.8.2-dev`.
- Ajout d'une zone hote dans `PulseBrowser.WinUI/MainWindow.xaml` et creation dynamique du `WebView2` dans `MainWindow.xaml.cs`.
- Raccordement de la barre d'adresse a la zone web.
- Raccordement des boutons retour, avancer, recharger et stop a la zone web.
- Raccordement des clics sur favoris a la navigation reelle dans la fenetre WinUI.
- Mise a jour des onglets depuis les adresses et titres de pages chargees.
- Transformation des dossiers de la barre de favoris en menus deroulants avec liens et sous-dossiers.
- Correction du crash WinUI `0xC000027B`: la premiere navigation est maintenant mise en attente jusqu'a `CoreWebView2Initialized`, puis executee via `CoreWebView2`.
- Ajout d'une reference NuGet explicite a `Microsoft.Web.WebView2`.
- Documentation du statut de WebView2: pont temporaire de migration, pas remplacement de la cible CEF/Rust.

## Etat

La coque WinUI redevient utilisable pour naviguer sur Internet. Le moteur final du projet reste Chromium via CEF raccorde au coeur Rust local; WebView2 sert seulement a eviter que l'interface WinUI soit une coquille sans navigation pendant la migration.

## Verification

- `build-winui.cmd` reussi avec MSBuild Visual Studio x64.
- La build WinUI 3 termine avec 0 avertissement et 0 erreur.
- Lancement visible court de `PulseBrowser.WinUI.exe` reussi: processus vivant avec fenetre `Pulse Browser 0.8.2-dev`.
- Lancement cache court de `PulseBrowser.WinUI.exe` reussi: processus vivant, plus de crash `Microsoft.UI.Xaml.dll`.
