# Position fictive (geolocalisation) - 0.83.19-dev

## Objectif

A la suite de la discussion sur les VPN, une confusion frequente a ete
clarifiee : la geolocalisation par API navigateur (`navigator.geolocation`,
utilisee par les cartes, la meteo, la recherche « pres de moi ») et la
geo-restriction par IP (utilisee par les services de streaming pour
determiner un catalogue) sont deux mecanismes independants. Cette version
traite uniquement le premier : proteger l'utilisateur des sites qui
demandent sa position GPS/Wi-Fi precise, en leur renvoyant une position
fixe a la place. Elle ne change rien a la geo-restriction par IP.

## Comportement

- Nouveaux reglages dans `UiSettings` : `GeolocationSpoofingEnabled` (actif
  par defaut), `GeolocationSpoofLatitude` / `GeolocationSpoofLongitude`
  (Paris par defaut : 48.8566 / 2.3522).
- Nouvelle section "Geolocalisation" dans Reglages > Confidentialite :
  bascule "Position fictive" + deux champs latitude/longitude + bouton
  "Appliquer".
- Un script (`GeolocationSpoofScript`) est injecte au demarrage de chaque
  document (`AddScriptToExecuteOnDocumentCreatedAsync`, comme le filtre
  cosmetique) et remplace `navigator.geolocation.getCurrentPosition`,
  `watchPosition` et `clearWatch` par la position fixe configuree, avant que
  le code de la page ne s'execute. Aucune demande de permission native n'est
  meme declenchee : le script intercepte tout en amont.
- S'applique immediatement aux onglets deja ouverts (contrairement au
  reglage WebRTC) : simple script injecte, pas un drapeau de demarrage du
  moteur.
- Un site avec une regle "Autoriser" explicite pour `geolocation` dans le
  panneau Site actuel (systeme de permissions par site deja existant,
  `SitePermissionPolicy`) est exempte de la position fictive et recoit
  toujours la vraie position : la regle par site reste prioritaire sur le
  reglage global.
- La liste des exemptions est recalculee et le script re-injecte des qu'une
  regle de permission "geolocation" change (panneau Site actuel) ou que le
  reglage/la position fictive change (Reglages > Confidentialite).

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis (test de garde-fou d'alignement de version mis a jour vers
  `0.83.19-dev`).
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles : page de test locale (`file://`)
  appelant `navigator.geolocation.getCurrentPosition` - la page affiche
  `LAT=48.8566 LON=2.3522`, confirmant la position fictive par defaut
  correctement appliquee, sans erreur ni popup de permission.

## Notes

- `dotnet build` seul reste inadapte pour WinUI sur cette machine (tache AppX
  `ExpandPriContent` absente du SDK .NET courant) : build via MSBuild Visual
  Studio x64.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.
