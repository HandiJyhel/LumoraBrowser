# Protections dans le hub Modules - 0.83.22-dev

## Objectif

Retour utilisateur : les trois reglages ajoutes dans cette session
(anti-fuite WebRTC, position fictive, anti-fingerprinting) n'etaient pas
faciles a trouver, isoles dans Parametres > Vie privee locale. Demande :
les rendre visibles depuis le hub Modules, plus consulte.

## Comportement

- Nouvelle carte "Protections" dans le hub Modules (Grid ModulesPanel),
  juste apres la carte "Mode d'usage" : trois `ToggleSwitch`
  (`ModulesWebRtcSwitch`, `ModulesGeolocationSwitch`,
  `ModulesFingerprintSwitch`) reprenant les trois reglages, plus un bouton
  "Reglages avances" qui ouvre Parametres > Vie privee locale directement.
- Les bascules de Parametres et celles du hub Modules restent strictement
  synchronisees dans les deux sens : la logique de chaque reglage a ete
  extraite dans une methode partagee (`SetWebRtcLeakProtection`,
  `SetGeolocationSpoofing`, `SetFingerprintProtection`) appelee par les DEUX
  gestionnaires `Toggled` (version Parametres et version Modules), qui
  applique le changement, le sauvegarde, puis aligne l'autre bascule via
  `SyncTogglePair` (sans redeclencher son propre gestionnaire).
- Rien n'est retire de Parametres > Vie privee locale : les reglages
  detailles (description longue, champs latitude/longitude...) restent
  disponibles la-bas.
- Bug trouve et corrige pendant la verification : le bouton "Reglages
  avances" reutilisait `SettingsNavigateButton_Click`, qui ne fait que
  changer la SECTION affichee A L'INTERIEUR du panneau Parametres (suppose
  deja visible) - correct pour les liens deja situes dans Parametres, mais
  invisible depuis le hub Modules puisque le panneau Parametres n'etait
  jamais affiche. Nouveau gestionnaire dedie
  `ModulesProtectionsSettingsButton_Click` qui affiche explicitement le
  panneau Parametres (`ShowPanel(SettingsPanel, ...)`) avant de selectionner
  la section.

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis (test de garde-fou d'alignement de version mis a jour vers
  `0.83.22-dev`).
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles (pilotage UIA) : les trois bascules
  sont visibles et lisibles dans le hub Modules avec leur etat par defaut
  correct (toutes actives) ; bascule de l'une d'elles depuis le hub Modules
  -> message de statut correct ; navigation vers Parametres via "Reglages
  avances" confirmee visuellement (section "Vie privee locale" bien
  affichee) et etat du reglage confirme synchronise (Off des deux cotes
  apres le changement).

## Notes

- `dotnet build` seul reste inadapte pour WinUI sur cette machine (tache AppX
  `ExpandPriContent` absente du SDK .NET courant) : build via MSBuild Visual
  Studio x64.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.
