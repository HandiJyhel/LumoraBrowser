# Mode Neutre - 0.83.12-dev

## Objectif

Ajouter un mode de base clairement distinct des modes compagnons : une page
d'accueil qui ne cherche pas a guider, produire ou accompagner, mais qui laisse
seulement l'heure, la recherche et les outils essentiels de navigation.

## Comportement

- Le mode `neutral` devient le mode d'usage par defaut des nouveaux profils.
- L'accueil neutre affiche uniquement l'heure locale et la barre de recherche.
- Les panneaux de presentation, de post-it et d'invitation a personnaliser sont
  absents en mode neutre.
- Le compagnon Lumie est masque en mode neutre pour eviter l'effet assistant
  permanent sur le mode de base.
- Le preset neutre garde les fonctions essentielles accessibles :
  coffre de mots de passe, favoris et blocage publicitaire.
- Les modules optionnels epingles sont retires du mode neutre sans supprimer les
  donnees utilisateur.
- Le mode reste disponible dans le selecteur principal, les parametres, le hub
  Modules et l'assistant de premiere configuration.

## Verification

- `dotnet test` hors sandbox apres blocage NuGet/ACL local :
  513/513 tests reussis.
- Restore MSBuild WinUI hors sandbox : 0 avertissement, 0 erreur.
- Build WinUI MSBuild x64 valide avec sortie alternative
  `artifacts\build-verify\winui-0.83.12-debug\` : 0 avertissement, 0 erreur.

## Notes

- `dotnet build` seul reste inadapte pour WinUI sur cette machine : il echoue
  sur la tache AppX `ExpandPriContent` absente du SDK .NET courant. La
  validation WinUI doit passer par MSBuild Visual Studio x64.
- Aucun installateur ni executable de release n'a ete genere.
- Aucun lancement automatique de Lumora n'a ete effectue.
