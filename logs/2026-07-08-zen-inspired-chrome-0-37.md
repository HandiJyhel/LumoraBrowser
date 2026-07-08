# 2026-07-08 - Chrome integre et inspiration Zen - 0.37.0-dev

Suite au retour utilisateur sur l'effet de double barre et apres consultation de l'interface Zen Browser, nouvelle passe visuelle dans `PulseBrowser.WinUI`.

## Modifications

- Integration du contenu dans la title bar Windows avec `ExtendsContentIntoTitleBar`.
- Ajout d'une zone de drag dediee pour garder les onglets cliquables.
- Reservation de la zone des boutons systeme a droite afin d'eviter la fusion visuelle avec les onglets.
- Ajout d'un mode compact persistant inspire de Zen.
- Ajout d'un bouton compact dans la barre de navigation.
- Le mode compact masque la barre de favoris et reduit legerement la barre de navigation.
- Ajout de la personnalisation locale du nouvel onglet:
  - titre;
  - affichage des raccourcis;
  - edition des raccourcis au format `Nom | URL`.
- L'accueil interne utilise maintenant le moteur de recherche configure.
- Version passee a `0.37.0-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.

## Verification

- Premier `build-winui.cmd` bloque sous sandbox reseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- `build-winui.cmd` reussi avec 0 erreur et 0 avertissement.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.37.0-dev`, processus repondant, fermeture propre.
