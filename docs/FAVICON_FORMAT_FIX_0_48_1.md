# Correction favicons — 0.48.1-dev

## Symptôme rapporté

Certains favoris (ex. allocine.fr) n'affichaient jamais leur icône, même
après plusieurs visites/clics — remplacés en permanence par le glyphe
générique.

## Cause

`DownloadFaviconFallbackAsync` (méthode de repli utilisée quand
`CoreWebView2.GetFaviconAsync` échoue) téléchargeait l'icône trouvée via
`<link rel="icon">` ou `/favicon.ico` et l'enregistrait **telle quelle** sous
un nom de fichier `.png` — sans vérifier ni convertir son format réel.
Beaucoup de sites (Allociné compris) servent encore un vrai fichier `.ico`
(voire `.bmp`) à cet endroit. Vérifié en conditions réelles :
`https://assets.allocine.fr/favicon/allocine.ico` répond avec
`Content-Type: image/vnd.microsoft.icon`, signature `00-00-01-00` (ICO), pas
`89-50-4E-47` (PNG).

Un fichier `.png` contenant en réalité des octets ICO peut être refusé par le
contrôle `Image`/`BitmapImage` de WinUI selon le décodeur sollicité, laissant
le favori sans icône. Aggravant : le cache « réutiliser si < 24h » gardait ce
fichier invalide et empêchait toute nouvelle tentative pendant 24h, y compris
en recliquant sur le favori.

## Correction

- `FaviconImageConverter.cs` (nouveau) : convertit n'importe quelle image
  (ICO, BMP, JPEG...) en PNG réel via les décodeurs WIC déjà embarqués dans
  Windows (`Windows.Graphics.Imaging`, aucune dépendance supplémentaire). Pour
  un `.ico` multi-résolution, garde la plus grande frame disponible plutôt que
  la première (souvent 16×16).
- `DownloadFaviconFallbackAsync` : les octets téléchargés passent par cette
  conversion avant écriture sur disque.
- `CaptureFaviconForTabAsync` : le cache « réutiliser si < 24h » vérifie
  maintenant aussi la signature PNG du fichier existant (`IsValidPngFile`).
  Un fichier invalide écrit par l'ancien code est donc corrigé dès la
  prochaine visite/clic, sans attendre l'expiration du cache.

## Effet de bord positif

Le générateur d'icône de raccourci pour les applications web (0.47.0-dev,
`IcoWriter.WrapPngAsIco`) exige un PNG valide en entrée et échouait donc
silencieusement pour ces mêmes sites (repli sur l'icône Pulse générique). Ce
correctif répare aussi l'icône des raccourcis d'application pour les sites
concernés.

## Vérification

- Reproduction confirmée hors application : téléchargement du vrai
  `allocine.ico` (4286 octets, signature ICO) et conversion via
  `FaviconImageConverter.ToPngAsync` dans un projet jetable référençant le
  fichier — sortie : PNG valide (signature `89-50-4E-47...`), image
  correctement décodée et visuellement correcte (logo Allociné).
- `dotnet test` : 94/94 verts (aucune nouvelle classe pure ajoutée au projet
  de tests — la conversion dépend de `Windows.Graphics.Imaging`, une API
  WinRT non compilable dans le projet de tests `net8.0-windows` autonome).
- `build-winui.cmd` : 0 avertissement, 0 erreur.
- Lancement court : fenêtre `Pulse Browser 0.48.1-dev` répondante.
