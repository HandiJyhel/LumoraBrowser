# 2026-07-18 - Protections dans le hub Modules (0.83.22-dev)

Retour utilisateur : les trois reglages ajoutes dans cette session
(anti-fuite WebRTC, position fictive, anti-fingerprinting) n'etaient pas
faciles a trouver, isoles dans Parametres > Vie privee locale. Demande de
les rendre visibles depuis le hub Modules, plus consulte, sans rien retirer
de Parametres.

- Version passee a `0.83.22-dev`.
- Nouvelle carte "Protections" dans le hub Modules : trois `ToggleSwitch`
  (`ModulesWebRtcSwitch`, `ModulesGeolocationSwitch`,
  `ModulesFingerprintSwitch`) + bouton "Reglages avances".
- Logique de chaque reglage extraite en methode partagee
  (`SetWebRtcLeakProtection`, `SetGeolocationSpoofing`,
  `SetFingerprintProtection`) appelee par les deux gestionnaires `Toggled`
  (Parametres et Modules), synchronisation via `SyncTogglePair`.
- Bug trouve et corrige pendant la verification : le bouton "Reglages
  avances" ne montrait jamais le panneau Parametres quand il etait clique
  depuis le hub Modules (le gestionnaire reutilise,
  `SettingsNavigateButton_Click`, suppose le panneau deja visible - vrai
  pour ses usages existants, faux ici). Nouveau gestionnaire dedie
  `ModulesProtectionsSettingsButton_Click` qui affiche explicitement le
  panneau avant de selectionner la section.
- Mise a jour du test de garde-fou d'alignement de version vers
  `0.83.22-dev` (`Lumora.Tests/UsageModeVisualIdentityTests.cs`,
  `AGENTS.md`, `scripts/build-clean-test-artifact.ps1`,
  `scripts/build-installer.ps1`).
- Documentation ajoutee : `docs/PROTECTIONS_HUB_MODULES_0_83_22.md`.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis.
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles (pilotage UIA) : trois bascules
  visibles dans le hub Modules avec l'etat par defaut correct ; bascule
  depuis le hub Modules confirmee (message de statut correct) ; navigation
  "Reglages avances" confirmee visuellement (section Vie privee locale
  affichee) et etat du reglage confirme synchronise entre les deux
  emplacements.

**Version :** `0.83.22-dev`.
