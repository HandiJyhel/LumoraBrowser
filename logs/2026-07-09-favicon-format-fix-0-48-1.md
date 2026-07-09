# 2026-07-09 — Correction format favicons 0.48.1-dev

- Retour utilisateur après vérification des paliers précédents : certains favoris (ex. allocine.fr) n'affichent jamais leur icône, même en cliquant dessus ; demande également confirmation que les icônes des applications web (0.47.0-dev) sont bien récupérées.
- Investigation en conditions réelles : `https://www.allocine.fr/` sert son favicon via `<link rel="shortcut icon" href="https://assets.allocine.fr/favicon/allocine.ico">` — un vrai fichier ICO (signature `00-00-01-00`), pas un PNG.
- Cause trouvée dans `MainWindow.Navigation.cs::DownloadFaviconFallbackAsync` : les octets téléchargés en repli (quand `GetFaviconAsync` échoue) étaient enregistrés tels quels sous un nom `.png`, sans conversion de format. Le cache « réutiliser si < 24h » gardait ensuite ce fichier invalide, bloquant toute nouvelle tentative pendant 24h.
- `FaviconImageConverter.cs` (nouveau) : conversion en PNG réel via `Windows.Graphics.Imaging` (WIC, déjà embarqué dans Windows, aucune dépendance ajoutée), en gardant la plus grande frame d'un ICO multi-résolution.
- `CaptureFaviconForTabAsync` : le cache de réutilisation vérifie maintenant la signature PNG du fichier existant (`IsValidPngFile`) avant de le considérer valide — un fichier corrompu par l'ancien code se corrige dès la prochaine visite, sans attendre 24h.
- Effet de bord positif confirmé : ce correctif répare aussi la génération d'icône des raccourcis d'application web (`IcoWriter.WrapPngAsIco`, 0.47.0-dev), qui échouait silencieusement pour ces mêmes sites faute d'un PNG valide en entrée.
- Vérification hors application : téléchargement du vrai `allocine.ico` (4286 octets) et conversion via un projet jetable référençant `FaviconImageConverter.cs` — sortie PNG valide, image inspectée visuellement (logo Allociné correctement rendu).
- Vérification projet : `dotnet test` 94/94 verts (pas de nouvelle classe pure testable — dépendance à l'API WinRT `Windows.Graphics.Imaging`, non compilable dans le projet de tests autonome). `build-winui.cmd` 0 avertissement, 0 erreur. Lancement court : fenêtre `Pulse Browser 0.48.1-dev` répondante.

**Version :** `0.48.1-dev`.
