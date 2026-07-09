# Correction du globe generique WebView2 — 0.48.3-dev

## Probleme

Apres l'ajout des applications web, une app installee pouvait afficher un globe
generique dans la barre des taches au lieu de l'icone Pulse validee.

Le cas observe etait l'application web `Connexion comptes Google` dans le profil
custom `C:\Users\Handi-Jyhel\Desktop\bob`.

## Cause

WebView2 peut fournir un PNG generique de 16x16 px quand Chromium n'a pas encore
de vraie favicon pour le site. Pulse considerait ce PNG comme une favicon valide,
puis l'enveloppait dans un `.ico` pour le raccourci d'application web.

Le raccourci Windows utilisait donc cette icone generique au lieu de l'icone Pulse.

## Correction

- Ajout de `FaviconQuality`, qui valide les PNG de favicon et rejette le globe
  generique WebView2 connu par son hash SHA-256.
- `CaptureFaviconForTabAsync` ignore maintenant ce PNG generique, aussi bien
  depuis `GetFaviconAsync` que depuis le repli HTTP.
- Les favoris, onglets, historiques et panneaux qui relisent le cache ne
  reutilisent plus un fichier favicon contamine.
- Les applications web n'utilisent plus une icone `.ico` generee a partir de ce
  globe generique.
- Migration au demarrage de la fenetre principale : une app web existante dont
  l'icone est invalide est reparee, son icone dediee est retiree et son
  raccourci est recree avec l'icone Pulse.

## Verification

- Le PNG generique extrait de `a8356f11fa6b4874b10729b3c06d886c.ico` etait
  exactement le globe affiche par l'utilisateur.
- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` : 98/98 verts.
- `build-winui.cmd` : 0 avertissement, 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` avec trace active : migration
  observee (`Web app generic icon repaired: a8356f11fa6b4874b10729b3c06d886c`).
- Lancement court avec `--app=a8356f11fa6b4874b10729b3c06d886c` : fenetre
  d'application activee (`PulseAppWindow activated for ...`).
- Le raccourci Menu Demarrer `Connexion comptes Google - a8356f11.lnk` pointe
  maintenant son icone vers `Assets\PulseBrowser.ico`.

**Version :** `0.48.3-dev`.
