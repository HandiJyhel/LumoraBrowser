# 2026-07-08 - Passe visuelle legere - 0.35.0-dev

Suite au retour utilisateur indiquant que Pulse Browser paraissait visuellement trop lourd face a Chrome, Opera ou Zen, premiere passe d'allegement sur la surface active `PulseBrowser.WinUI`.

## Modifications

- Barre de menus permanente remplacee par une barre superieure compacte avec marque Pulse, acces nouvel onglet, accueil et menu.
- Barre d'adresse et controles de navigation reduits en hauteur, avec boutons icones plus discrets.
- Bouton texte `Ouvrir` remplace par un bouton icone.
- Barre des favoris reduite: icone discrete, espacement plus serre, boutons generes plus compacts.
- Rail des onglets verticaux reduit par defaut et ajustements des onglets generes.
- Barres d'identifiants, pied de statut et palette de commande rendus moins massifs.
- Version passee a `0.35.0-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.

## Verification

- Premier `build-winui.cmd` bloque sous sandbox reseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- Correction d'une erreur XAML: `Window.Resources` n'est pas accepte sur cette fenetre WinUI; les styles locaux ont ete deplaces dans `Grid.Resources`.
- `build-winui.cmd` reussi avec 0 erreur et 0 avertissement.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.35.0-dev`, processus repondant, fermeture propre.
