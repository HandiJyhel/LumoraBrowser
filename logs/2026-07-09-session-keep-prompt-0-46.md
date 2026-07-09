# 2026-07-09 — Sessions éphémères expliquées 0.46.0-dev

- Retour utilisateur : perte de connexion Google au redémarrage, causée par la purge de sessions au démarrage (`SessionPurgeEnabled`, actif par défaut depuis `0.10.0-dev`), fonction oubliée par l'utilisateur lui-même faute d'explication au bon moment.
- Décision : garder la purge active par défaut (différenciateur produit), la rendre compréhensible et pilotable plutôt que de l'inverser.
- `Sessions/SessionKeepAdvisor.cs` (logique pure, testée) : décide si Pulse doit proposer « Rester connecté ? » après un login détecté (pas si purge désactivée, site déjà de confiance, ou déjà refusé).
- `MainWindow.Sessions.cs` : `MaybeOfferSessionKeep`, barre `SessionKeepBar` (Accepter → ajoute aux sites de confiance ; Refuser → `SessionKeepDeclinedSites`, mémorisé par domaine racine). `SetTrustedSessionSite(trusted: true)` nettoie aussi un refus antérieur.
- `MainWindow.Vault.cs` : `CredentialService_CredentialCaptured` déclenche la proposition à chaque login capturé, indépendamment de l'offre d'enregistrement du mot de passe.
- InfoBar `SessionPurgeInfoBar` : explication affichée une seule fois dans la vie du profil, à la première purge réelle (`SessionPurgeExplained`), avec lien vers *Sites connectés*.
- `Models/UiSettings.cs` : nouveaux champs `SessionPurgeExplained` et `SessionKeepDeclinedSites`, rétro-compatibles.
- Libellé du toggle *Sessions éphémères* dans les paramètres mis à jour pour mentionner la proposition au login.
- `MainWindow.xaml` : restructuration des lignes de `BrowserPanel` (ajout de l'InfoBar et de la barre `SessionKeepBar`, `BrowserHost` décalé).
- Ajout de `docs/SESSION_KEEP_PROMPT_0_46.md` et de ce log.
- Vérification : `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` → 80/80 verts (75 existants + 5 nouveaux `SessionKeepAdvisorTests`). `build-winui.cmd` → 0 avertissement, 0 erreur. Lancement court de `PulseBrowser.WinUI.exe` : fenêtre `Pulse Browser 0.46.0-dev`, processus vivant et répondant, arrêté ensuite pour ne pas verrouiller l'exécutable.

**Version :** `0.46.0-dev`.
