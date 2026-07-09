# 2026-07-09 — Anti-télémétrie 0.44.0-dev

- Validation utilisateur `Go` sur le plan anti-télémétrie + portefeuille (options recommandées retenues : SmartScreen off par défaut, CVV jamais stocké, ajout manuel des cartes en v1).
- Ajout de `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` (crash reporter, breakpad, domain reliability, pings) dans le constructeur `MainWindow`, avant toute création de moteur.
- SmartScreen désactivé par défaut via `IsReputationCheckingRequired`, toggle dans Paramètres > Confidentialité, application immédiate à tous les moteurs vivants.
- Nouveau module `Privacy/TelemetryBlocker/` : `TelemetrySeedList` (~100 endpoints dédiés) + `TelemetryBlockerModule` (compteurs global/page, whitelist partagée), enregistré avant le bloqueur réseau.
- UiSettings : `TelemetryBlockerEnabled` (défaut true), `SmartScreenEnabled` (défaut false).
- UI : toggle anti-télémétrie, toggle SmartScreen, compteur « dont X télémétrie » (page Confidentialité + bouclier).
- Tests : `TelemetryBlockerTests.cs` (10 tests) ajouté au projet autonome via `<Compile Include>` (`IPrivacyModule.cs`, module, seed).
- Vérification : `dotnet test` 61/61 verts ; `build-winui.cmd` 0 avertissement, 0 erreur.
- AGENTS.md : version de gouvernance corrigée (restée à 0.42.0-dev) et passée à 0.44.0-dev.
