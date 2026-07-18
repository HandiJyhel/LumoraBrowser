# 2026-07-18 - Position fictive / geolocalisation (0.83.19-dev)

A la suite de la discussion sur les VPN, une confusion frequente a ete
clarifiee avant de coder quoi que ce soit : la geolocalisation par API
navigateur (`navigator.geolocation`) et la geo-restriction par IP
(streaming) sont deux mecanismes independants, falsifier l'un n'a aucun
effet sur l'autre. Cette version traite uniquement le premier, dans l'esprit
protection de l'utilisateur : empecher les sites de recuperer la position
GPS/Wi-Fi precise, en leur renvoyant une position fixe configurable.

- Version passee a `0.83.19-dev`.
- Ajout des reglages `GeolocationSpoofingEnabled` (actif par defaut),
  `GeolocationSpoofLatitude` et `GeolocationSpoofLongitude` (Paris par
  defaut) dans `UiSettings`.
- Nouveau fichier `Lumora.WinUI/Privacy/GeolocationSpoofing/GeolocationSpoofScript.cs` :
  construit un script qui remplace `navigator.geolocation.getCurrentPosition`,
  `watchPosition` et `clearWatch` par une position fixe, injecte via
  `AddScriptToExecuteOnDocumentCreatedAsync` (meme mecanisme que le filtre
  cosmetique).
- Nouvelle section "Geolocalisation" dans Reglages > Confidentialite :
  bascule + latitude/longitude + bouton "Appliquer". S'applique
  immediatement aux onglets ouverts (pas de redemarrage necessaire,
  contrairement au reglage WebRTC).
- Integration avec le systeme de permissions par site deja existant
  (`SitePermissionPolicy`) : un site avec une regle "Autoriser" explicite
  pour `geolocation` reste exempte et recoit la vraie position. La liste
  d'exemptions se recalcule quand une regle de site change.
- Mise a jour du test de garde-fou d'alignement de version vers
  `0.83.19-dev` (`Lumora.Tests/UsageModeVisualIdentityTests.cs`,
  `AGENTS.md`, `scripts/build-clean-test-artifact.ps1`,
  `scripts/build-installer.ps1`).
- Documentation ajoutee : `docs/GEOLOCALISATION_FICTIVE_0_83_19.md`.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.

**Verification** :

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis.
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles : page de test locale (`file://`)
  appelant `navigator.geolocation.getCurrentPosition` - la page affiche
  `LAT=48.8566 LON=2.3522`, position fictive par defaut correctement
  appliquee, aucune erreur ni popup de permission.

**Version :** `0.83.19-dev`.
