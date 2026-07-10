# Chrome solide - 0.50.1-dev

Ce palier retire l'option utilisateur d'effet translucide dans `Parametres > Apparence`.

## Decision

Pulse Browser garde maintenant un chrome solide par defaut et ne propose plus les choix `Mica` ou `Acrylic`.

## Pourquoi

- L'identite Pulse est plus nette avec des surfaces solides et maitrisees.
- Le contraste, le focus clavier et la lisibilite sont plus previsibles.
- Le contenu WebView2 doit rester opaque et jamais teinte par une transparence de fenetre.
- Le reglage ajoutait une complexite visuelle sans apporter de vraie fonction de navigation.

## Comportement

- Les profils qui avaient `WindowBackdrop` sur `mica` ou `acrylic` sont normalises en `solid` au chargement et a la sauvegarde des reglages.
- `SystemBackdrop` est force a `null`.
- Le garde-fou Win32 qui retire l'eventuel style layered reste en place pour eviter toute transparence residuelle du contenu web.

## Verification

- `Parametres > Apparence` ne montre plus `Effet translucide`.
- La fenetre demarre avec le titre `Pulse Browser 0.50.1-dev`.
- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` : 136/136 verts.
- `build-winui.cmd` : premier essai bloque par le sandbox reseau NuGet (`NU1301`), relance autorisee reussie avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` : processus repondant, fermeture propre du processus de test.

**Version :** `0.50.1-dev`.
