# 2026-07-08 - Direction visuelle Pulse - 0.38.0-dev

Nouvelle passe d'identite visuelle dans `PulseBrowser.WinUI` apres validation utilisateur pour aller plus loin que la simple comparaison avec Chrome.

## Modifications

- Version passee a `0.38.0-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.
- Ajout de ressources visuelles Pulse dans `MainWindow.xaml`.
- Palette du chrome harmonisee autour d'un charbon chaud, avec accent orange ponctuel.
- Boutons de navigation legerement reduits et moins visibles.
- Barre d'adresse raccordee a la nouvelle palette.
- Couleurs de title bar Windows synchronisees avec le chrome Pulse.
- Barre de favoris et rail vertical harmonises.
- Espacements du mode compact ajustes.
- Refonte visuelle de `pulse://accueil` avec marque compacte, ligne d'accent, recherche plus nette et raccourcis plus propres.

## Verification

- Premier `build-winui.cmd` bloque sous sandbox reseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- `build-winui.cmd` reussi avec 0 erreur et 0 avertissement.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.38.0-dev`, processus repondant, fermeture propre.
