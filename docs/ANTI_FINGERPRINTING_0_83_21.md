# Anti-fingerprinting - 0.83.21-dev

## Objectif

Apres la discussion sur le masquage d'IP (Tor mis de cote pour son cout de
vitesse/compatibilite, VPN maison ecarte, pas de "module magique" possible
sans intermediaire reseau), recentrage sur la meilleure protection possible
sans intermediaire ni compromis de vitesse : brouiller l'empreinte du
navigateur (fingerprinting), un vecteur de pistage souvent plus fiable que
l'IP elle-meme puisqu'il ne change pas avec le Wi-Fi/la 4G.

## Comportement

- Nouveau reglage `FingerprintProtectionEnabled` dans `UiSettings`, actif
  par defaut (protection gratuite, meme famille que l'anti-fuite WebRTC et
  la position fictive).
- Nouvelle section "Empreinte du navigateur" dans Reglages > Confidentialite :
  bascule "Brouiller l'empreinte du navigateur".
- `Lumora.WinUI/Privacy/FingerprintProtection/FingerprintProtectionScript.cs` :
  script injecte sur chaque document (meme mecanisme que le filtre
  cosmetique et la position fictive) qui :
  - ajoute un bruit leger (bascule d'1 bit sur ~2% des pixels) a la lecture
    du Canvas (`getImageData`, `toDataURL`, `toBlob`) ;
  - normalise le vendeur/renderer WebGL renvoyes par `getParameter` vers des
    valeurs generiques ;
  - ajoute un bruit infime aux echantillons audio lus via
    `AudioBuffer.getChannelData` ;
  - normalise `navigator.hardwareConcurrency` (4) et `navigator.deviceMemory`
    (8) vers des valeurs courantes.
- Le bruit utilise une graine tiree une fois par lancement de Lumora
  (`MainWindow._fingerprintSessionSeed`) : stable pour toute la session
  (une page qui redessine son canvas plusieurs fois garde la meme empreinte
  d'une capture a l'autre), mais differente a chaque redemarrage de Lumora,
  pour ne pas devenir elle-meme un identifiant stable.
- S'applique immediatement aux onglets ouverts (pas de redemarrage
  necessaire).

## Verification

- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 517/517
  tests reussis (test de garde-fou d'alignement de version mis a jour vers
  `0.83.21-dev`).
- Build WinUI MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- Verification en conditions reelles : page de test locale (`file://`)
  dessinant un canvas et lisant `toDataURL()`, le vendeur/renderer WebGL et
  `navigator.hardwareConcurrency`, lancee deux fois avec des profils
  distincts. Resultat : empreinte Canvas differente entre les deux
  lancements (longueurs 3806 et 3926, contenu different), vendeur/renderer
  WebGL identiques et generiques ("Generic Vendor"/"Generic Renderer") sur
  les deux lancements, `hardwareConcurrency` normalise a 4 sur les deux -
  comportement exactement attendu. Rendu visuel du canvas intact (bruit
  imperceptible).

## Notes

- `dotnet build` seul reste inadapte pour WinUI sur cette machine (tache AppX
  `ExpandPriContent` absente du SDK .NET courant) : build via MSBuild Visual
  Studio x64.
- Aucun installateur ni executable de release genere.
- Aucun lancement automatique de Lumora en dehors de la verification.
