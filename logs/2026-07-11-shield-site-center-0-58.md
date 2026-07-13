# 2026-07-11 - Pulse Browser 0.58.0-dev - Bouclier et Centre du site v2

## Objectif

Rendre les protections par site plus lisibles et plus actionnables, surtout apres
les correctifs recents autour des connexions cassees par un blocage trop opaque.

## Changements

- Ajout d'une trace locale en memoire des derniers blocages privacy par page.
- La trace conserve uniquement le module, le domaine requete, le chemin sans
  parametres, le domaine de page et l'instant du blocage.
- Le bouton bouclier affiche maintenant une recommandation courte et les derniers
  blocages recents de la page.
- Le panneau `Site actuel` affiche une recommandation plus explicite :
  - blocage probablement lie a la connexion ;
  - compatibilite connexion deja active ;
  - blocage surtout lie a la telemetrie ;
  - protection standard sans signal de casse.
- Ajout d'un bouton `Autoriser seulement le flux de connexion` dans `Site actuel`
  lorsque Pulse detecte un blocage ressemblant a un login et que la compatibilite
  connexion n'est pas encore active.
- Ajout de tests purs pour `PrivacyEngine` afin de verifier la trace locale et
  l'absence de valeurs de requete sensibles dans le chemin affiche.

## Version

- `AGENTS.md` et `MainWindow.xaml.cs` passes a `0.58.0-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj --no-restore` :
  176/176 tests verts.
- Premier `build-winui.cmd` bloque par le sandbox reseau NuGet (`NU1301`).
- `build-winui.cmd` relance avec autorisation reseau : reussi, 0 avertissement,
  0 erreur.

## Limite

- Pas de nouvel installateur genere dans cette etape : la demande portait sur le
  palier produit `Bouclier / Site actuel v2`, pas sur une livraison setup.
