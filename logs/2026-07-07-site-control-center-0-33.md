# 2026-07-07 - Centre du site actuel - 0.33.0-dev

## Contexte

Apres validation du gestionnaire de mots de passe et la mise en place d'un WebView2 par onglet, l'utilisateur a demande une fonction "stylee de fou". Le choix retenu apres proposition a ete le centre de controle par site.

## Changements

- Ajout de l'entree `Outils > Site actuel`.
- Extension du flyout du bouclier avec un resume de site et un bouton `Centre du site`.
- Ajout du panneau `SiteControlPanel` dans `PulseBrowser.WinUI/MainWindow.xaml`.
- Ajout de `MainWindow.SiteControl.cs`.
- Le panneau affiche le domaine racine, l'adresse courante, l'etat privacy, le nombre de cookies du domaine, le statut session de confiance, le nombre d'identifiants locaux et le nombre d'entrees d'historique.
- Ajout d'actions rapides : retour au site, actualiser, conserver la session au demarrage, oublier le site maintenant, ouvrir les identifiants filtres, ouvrir l'historique filtre et rouvrir des pages recentes.
- Passage de version a `0.33.0-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.

## Verification

- `build-winui.cmd` a d'abord echoue sous sandbox reseau sur NuGet (`NU1301`), comportement deja connu.
- Relance avec reseau autorise : restore OK.
- Le build standard a ensuite echoue uniquement a la copie finale de `PulseBrowser.WinUI.exe`, verrouille par une instance deja ouverte (`PulseBrowser.WinUI (26164)`).
- Build de verification vers `artifacts/winui-sitecontrol-build/` reussi avec MSBuild x64 : 0 erreur, 0 avertissement.

## Limite

Pas de lancement interactif supplementaire dans cette passe, car une instance Pulse Browser etait deja ouverte et verrouillait l'executable Debug.
