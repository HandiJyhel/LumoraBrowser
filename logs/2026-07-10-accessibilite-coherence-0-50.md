# 2026-07-10 - Accessibilite et coherence graphique 0.50.0-dev

Suite au `Go` utilisateur, mise en place d'un palier `0.50.0-dev` centre sur l'accessibilite visible et la coherence graphique de Pulse Browser.

## Changements

- Ressources globales de focus et de surfaces Pulse ajoutees dans `App.xaml`.
- Styles du chrome renforces dans `MainWindow.xaml` : bordure discrete, focus clavier visible, action d'adresse accentuee.
- Noms accessibles ajoutes aux boutons iconiques principaux et a la barre d'adresse.
- `ApplyAccessibilitySettings()` etend maintenant les modes contraste/texte/focus a davantage de ressources et de barres contextuelles.
- Helper runtime ajoute pour appliquer les conventions d'accessibilite aux controles generes par code.
- Boutons de favoris crees dynamiquement enrichis avec des libelles accessibles.
- Barres identifiants/autofill/portefeuille/session, panneau Parametres, barre d'etat, palette `Ctrl+K` et fenetre d'application web harmonises avec les surfaces Pulse.
- Barre d'etat exposee comme region live polie.
- Version projet passee a `0.50.0-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` : premier essai bloque par le sandbox reseau NuGet, relance autorisee reussie, 136/136 tests verts.
- `build-winui.cmd` : premier essai bloque par le sandbox reseau NuGet (`NU1301`), relance autorisee reussie avec 0 avertissement et 0 erreur.
- Lancement court de `PulseBrowser.WinUI.exe` : fenetre `Pulse Browser 0.50.0-dev`, processus repondant, fermeture propre du processus de test.

**Version :** `0.50.0-dev`.
