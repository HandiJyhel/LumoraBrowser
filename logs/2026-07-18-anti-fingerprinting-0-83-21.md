# 2026-07-18 - Anti-fingerprinting (0.83.21-dev)

Suite a la discussion sur le masquage d'IP (Tor mis de cote pour son cout
de vitesse/compatibilite, VPN maison ecarte, pas de "module magique" sans
intermediaire reseau possible), recentrage sur la meilleure protection
possible sans intermediaire ni compromis de vitesse : brouiller l'empreinte
du navigateur, un vecteur de pistage souvent plus fiable que l'IP puisqu'il
ne change pas avec le Wi-Fi/la 4G.

- Version passee a `0.83.21-dev`.
- Ajout du reglage `FingerprintProtectionEnabled` dans `UiSettings`, actif
  par defaut.
- Nouveau fichier
  `Lumora.WinUI/Privacy/FingerprintProtection/FingerprintProtectionScript.cs` :
  bruit leger sur Canvas (`getImageData`/`toDataURL`/`toBlob`), vendeur/
  renderer WebGL normalises, bruit infime sur l'AudioContext, et
  normalisation de `navigator.hardwareConcurrency`/`deviceMemory`.
- Graine de bruit tiree une fois par lancement de Lumora
  (`MainWindow._fingerprintSessionSeed`) : stable pendant toute la session,
  differente a chaque redemarrage.
- Nouvelle section "Empreinte du navigateur" dans Reglages > Confidentialite.
- S'applique immediatement aux onglets ouverts (meme mecanisme que la
  position fictive - script injecte, pas un drapeau de demarrage).
- Mise a jour du test de garde-fou d'alignement de version vers
  `0.83.21-dev` (`Lumora.Tests/UsageModeVisualIdentityTests.cs`,
  `AGENTS.md`, `scripts/build-clean-test-artifact.ps1`,
  `scripts/build-installer.ps1`).
- Documentation ajoutee : `docs/ANTI_FINGERPRINTING_0_83_21.md`.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis.
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles : page de test locale lancee sur deux
  profils distincts. Empreinte Canvas differente entre les deux lancements
  (3806 vs 3926 caracteres, contenu different) ; vendeur/renderer WebGL
  identiques et generiques sur les deux ; `hardwareConcurrency` normalise a
  4 sur les deux. Rendu visuel du canvas intact.

**Version :** `0.83.21-dev`.
