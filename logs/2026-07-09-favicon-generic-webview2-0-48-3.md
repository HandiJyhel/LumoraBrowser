# 2026-07-09 — Correction globe generique WebView2 0.48.3-dev

## Diagnostic

- Le raccourci `Connexion comptes Google - a8356f11.lnk` lancait
  `PulseBrowser.WinUI.exe --app=a8356f11fa6b4874b10729b3c06d886c`.
- Son icone pointait vers
  `C:\Users\Handi-Jyhel\Desktop\bob\navigation\webapp-icons\a8356f11fa6b4874b10729b3c06d886c.ico`.
- Extraction du PNG embarque : globe WebView2 generique 16x16 px, hash SHA-256
  `959A80AA9A16AD7B306D7895B34083F3817CC61FB6E8B676B05D5DD59AC89F15`.
- Plusieurs fichiers du cache `navigation\favicons` avaient le meme hash, signe
  que le fallback generique avait ete memorise comme favicon.

## Correction

- Ajout de `FaviconQuality` pour rejeter le PNG generique WebView2 connu.
- Raccordement de la validation dans la capture favicon, les lectures du cache,
  l'affichage des favoris/onglets, le panneau Applications et les icones
  d'applications web.
- Ajout d'une migration de demarrage qui repare les apps web existantes avec
  icone invalide et recrée leurs raccourcis avec l'icone Pulse.
- Version passee a `0.48.3-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` : 98/98 verts.
- `build-winui.cmd` : 0 avertissement, 0 erreur.
- Lancement court de l'executable WinUI : trace
  `Web app generic icon repaired: a8356f11fa6b4874b10729b3c06d886c`.
- Lancement court avec `--app=a8356f11fa6b4874b10729b3c06d886c` : trace
  `PulseAppWindow activated for a8356f11fa6b4874b10729b3c06d886c`.
- Apres migration, `navigation\webapp-icons` ne contient plus l'icone fautive.
- Le raccourci Menu Demarrer pointe maintenant vers
  `G:\DevelopmentProjects\AppsDepots\PulseBrowser\PulseBrowser.WinUI\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\Assets\PulseBrowser.ico,0`.

**Version :** `0.48.3-dev`.
