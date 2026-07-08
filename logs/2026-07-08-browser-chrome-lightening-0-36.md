# 2026-07-08 - Chrome navigateur allege - 0.36.0-dev

Suite a la comparaison entre les captures `1.NevPulse.png` et `2.NavChrome.png`, deux causes principales de lourdeur ont ete traitees dans `PulseBrowser.WinUI`:

- l'empilement de bandes visibles;
- la dominance de l'orange et des surfaces encadrees.

## Modifications

- Masquage de la bande superieure `Pulse` separee.
- Repositionnement des acces Accueil et Menu Pulse dans la barre de navigation.
- Neutralisation sombre de la title bar Windows via `AppWindow.TitleBar`.
- Barre d'adresse rendue plus arrondie, avec fond et bordure plus proches d'une pilule.
- Disparition du pied de statut permanent sur les pages web; il reste visible sur les panneaux internes pour le bouton `Retour au site`.
- Refonte de l'accueil HTML interne: suppression des cartes explicatives, ajout d'une recherche centrale et de raccourcis legers.
- Version passee a `0.36.0-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.

## Verification

- Premier `build-winui.cmd` bloque sous sandbox reseau sur NuGet (`NU1301`), puis relance hors sandbox reussie.
- `build-winui.cmd` reussi avec 0 erreur et 0 avertissement.
- Lancement court de `PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.36.0-dev`, processus repondant, fermeture propre.
