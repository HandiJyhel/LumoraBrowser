# Modes Neutre et Equilibre distincts - 0.83.13-dev

## Objectif

Corriger la confusion visuelle entre le mode Neutre et le mode Equilibre.
Neutre doit rester minimal, mais pas anonyme. Equilibre doit redevenir un
accueil quotidien Lumora, plus vivant et plus utile qu'une simple recherche.

## Changements

- Le mode `balanced` est de nouveau reconnu explicitement dans la page
  d'accueil et dans les messages WebView2.
- Le mode Neutre conserve maintenant une identite Lumora discrete :
  petit logo, nom Lumora, heure locale et recherche.
- Le mode Equilibre dispose d'un rendu dedie `balanced-home`.
- L'accueil Equilibre utilise une grille plus large :
  marque Lumora + recherche a gauche, panneau quotidien a droite.
- Ajout d'actions visibles pour le quotidien :
  Favoris, Historique et Modules.
- Conservation de la presentation de mode pour Equilibre, sans imposer le gros
  workbench compagnon.
- Tests ajoutes pour empecher `balanced` de retomber sur `neutral`.

## Verification

- `dotnet test` hors sandbox apres blocage NuGet/ACL local :
  514/514 tests reussis.
- Restore MSBuild WinUI hors sandbox : 0 avertissement, 0 erreur.
- Build WinUI MSBuild x64 valide avec sortie alternative
  `artifacts\build-verify\winui-0.83.13-debug\` : 0 avertissement, 0 erreur.

## Notes

- Aucun installateur ni executable de release n'a ete genere.
- Aucun lancement automatique de Lumora n'a ete effectue.
