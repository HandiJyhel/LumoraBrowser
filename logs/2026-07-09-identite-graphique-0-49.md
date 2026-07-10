# 2026-07-09 - Identite graphique Pulse Browser 0.49.0-dev

## Contexte

Apres la correction de lisibilite de l'icone et des icones d'applications web, l'utilisateur a signale que l'application restait visuellement trop generique. Validation donnee par `Go` pour lancer un palier d'identite graphique.

## Travail effectue

- Version projet passee a `0.49.0-dev`.
- Accent global WinUI defini autour de l'orange Pulse.
- Ajout d'un accent secondaire menthe pour eviter une interface limitee a une seule famille orange/charcoal.
- Chrome principal renforce : barre d'identite au-dessus des onglets, navigation avec degrade discret, surfaces et bordures Pulse.
- Accueil Pulse retravaille avec une marque CSS inspiree de l'icone, recherche plus lumineuse et raccourcis plus sobres.
- Overlays de connexion et de setup alignes sur la nouvelle palette, avec `Assets/PulseBrowser.png` declare comme contenu de build.
- Fenetre d'application web harmonisee sur la barre de confinement de domaine.
- Correction de la page A propos : suppression de l'ancienne mention Rust actif, remplacée par la stack locale actuelle.

## Verification

- Premier essai de `dotnet test` et `build-winui.cmd` bloque par le sandbox reseau NuGet (`NU1301`), sans rapport avec le code.
- Relance avec acces autorise : `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` reussi, 129/129.
- `build-winui.cmd` reussi, 0 avertissement, 0 erreur.
- Lancement court de l'executable compile : `PulseBrowser.WinUI.exe` vivant apres 5 secondes, processus de verification ferme ensuite.

**Version :** `0.49.0-dev`.
