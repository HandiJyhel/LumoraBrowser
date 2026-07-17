# 2026-07-17 - Refonte du coffre en module autonome (0.79.1-dev)

## Contexte

Troisième volet des priorités annoncées (0.78.2 = anti-pub + étoile de
favori, 0.78.3 = bloc-notes) : refonte du coffre de mots de passe en vrai
petit module, avec ergonomie revue et deux options nouvelles choisies avec
l'utilisateur : générateur de mot de passe configurable et authentification
à deux facteurs (TOTP) locale. Session lancée depuis
`docs/PROMPT_REFONTE_COFFRE_0_78_4.md` (version cible retarée à
`0.79.1-dev` : le dépôt était déjà passé à `0.79.0-dev` entre-temps).

Avant de commencer, rattrapage git nécessaire : le dépôt contenait un gros
backlog de travail déjà fait mais jamais commité (`0.78.3.3` →
`0.79.0-dev`). Reconstitué en 8 commits thématiques distincts de la
présente refonte (voir historique de la branche `main`).

## Changements

**Étape 1 — Extraction en module.** `MainWindow.Vault.cs` (1351 lignes,
mélange capture d'identifiants/autofill/accès/import-export/rendu du
panneau/passkeys/dispatch de messages web sans rapport) découpé en
partiels à responsabilité unique : `MainWindow.VaultCapture.cs`,
`MainWindow.VaultAccess.cs`, `MainWindow.VaultImportExport.cs`,
`MainWindow.VaultPanel.cs`, `MainWindow.Passkeys.cs` (sujet distinct sorti
du fichier coffre) et `MainWindow.WebMessaging.cs` (dispatch générique qui
n'a jamais été propre au coffre). Aucune logique modifiée.

**Étape 2 — Ergonomie du panneau.** Nouveau `VaultGroupingService` pur et
testé (regroupement par site, sous-domaines fusionnés via
`PublicSuffixService`, tri Récent/Alphabétique). Panneau reconstruit :
liste compacte groupée par site avec favicon (cache local existant en
lecture seule, aucun téléchargement) à gauche, volet de détail à droite
alimenté par la sélection courante (actions : ouvrir, copier identifiant,
copier mot de passe, renommer, supprimer), plutôt que des cartes empilées
avec tous les boutons visibles en permanence.

**Étape 3 — Générateur configurable.** `PasswordGenerator` gagne
`GeneratePassphrase` (mode phrase de passe, liste locale d'environ 270
mots courants, tirage crypto-sûr, aucune ressource externe). Réglages
(mode, longueur, symboles, nombre de mots) persistés dans `UiSettings` et
partagés entre le dialogue "Nouvel identifiant" (bascule Aléatoire /
Phrase de passe ajoutée) et la barre de suggestion automatique sur les
champs "nouveau mot de passe" détectés (création de compte, mot de passe
oublié), qui utilisait jusqu'ici une longueur fixe de 20 caractères sans
option.

**Étape 4 — TOTP local.** Nouveau `TotpService` pur (RFC 6238 / HOTP RFC
4226, SHA-1, décodeur base32 local, saisie unifiée secret collé ou URI
`otpauth://`), vérifié contre les vecteurs de test publiés par la RFC
6238 Annexe B. `VaultCredential` gagne un TOTP optionnel (secret chiffré
comme le reste du coffre, digits, period) ; `VaultStore` préserve
désormais ce champ dans toutes les reconstructions d'entrées existantes
(upsert, renommage, import) au lieu de l'écraser silencieusement. Volet
de détail : section dédiée avec bouton d'ajout (validation du secret/URI
avant enregistrement) ou code à 6 chiffres rotatif rafraîchi chaque
seconde avec décompte, copie et suppression. Saisie manuelle uniquement
pour cette version — pas de scan de QR code (nécessiterait une nouvelle
dépendance de décodage image, différée volontairement, à discuter
séparément si voulue).

**Étape 5 — Finition.** Version passée à `0.79.1-dev`
(`MainWindow.xaml.cs`, `AGENTS.md`, `scripts/build-clean-test-artifact.ps1`,
`scripts/build-installer.ps1`).

## Vérification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 507/507
  tests verts (38 nouveaux : 6 `VaultGroupingServiceTests`, 9
  `PasswordGeneratorTests` phrase de passe, 23 `TotpServiceTests`).
- `scripts\build-winui.ps1` : build MSBuild réussi à chaque étape, 0
  avertissement / 0 erreur.
- Vérification visuelle live tentée via le skill verify (profil de test
  dédié `TestCoffre`, distinct du profil réel `H.J.`) : la sélection de
  profil entre dans une boucle de redémarrage sous pilotage UIA
  (écran non touché par cette refonte), non résolue malgré plusieurs
  approches. Vérification manuelle laissée à l'utilisateur ; build et
  suite de tests restent l'évidence de correction disponible.

## Artefacts

- Artefact propre :
  `artifacts\clean-test\Lumora-0.79.1-dev-win-x64-clean-20260717-170345`
- SHA256 exécutable hôte :
  `0f7499bdb73769fd56d67e7b078ef056ee960e5f29d1b67507246d686038742e`
- Installateur : `artifacts\installer\LumoraSetup-0.79.1-dev-win-x64.exe`
- SHA256 installateur :
  `b63e63f64c2d052b523f0215fba41ae2fae9dd41d84c13298442e0e5d773f4c2`

## Note

Version de développement sur la branche dédiée
`feature/refonte-coffre-0-79-1`, pas encore fusionnée sur `main`.
