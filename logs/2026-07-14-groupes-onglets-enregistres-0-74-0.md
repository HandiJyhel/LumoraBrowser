# Groupes d'onglets enregistres - 0.74.0-dev

## Contexte

Demande utilisateur : les groupes d'onglets des navigateurs actuels marchent mal,
surtout parce qu'une fois le groupe fait, si on quitte, on perd ses groupements.
Il veut « un petit moyen pour que l'utilisateur puisse retrouver ses groupements
d'onglets s'il en a besoin ».

Etat existant : Lumora avait deja des groupes vivants (creer/renommer/dissoudre/
replier, couleurs), sauvegardes dans la session d'onglets (`tabs.lumora`) — donc
persistes tant que les onglets restent ouverts. Le trou : des qu'on ferme le
groupe, il disparait pour toujours ; aucune bibliotheque pour le ressortir.

## Decision de conception

Bibliotheque de « groupes enregistres » : ranger un groupe (archive : nom, couleur,
titres + URL) et le rouvrir quand on veut, meme apres l'avoir ferme. Question posee
a l'utilisateur sur le mode d'enregistrement -> reponse : **manuel + garde-fou**
(action explicite « Enregistrer », plus proposition de garder un groupe non
enregistre avant qu'il disparaisse). 100% local, chiffre comme le reste du profil.
Logique isolee en classe pure testable.

## Changements

- `Tabs/SavedTabGroup.cs` : `SavedTabGroup`, `SavedTabGroupTab`, et
  `SavedTabGroupStore` (pur). Persistance chiffree injectee par delegues
  lecture/ecriture. `Save` (filtre `lumora://`/sans URL, titre vide -> URL, null
  si vide), `Remove`, `Find`, `Groups` (recents d'abord), `IsSavableUrl`,
  `SetGuestMode`.
- `Models/ProfilePaths.cs` : `SavedTabGroupsFile`
  (`navigation/saved-tab-groups.lumora`).
- `MainWindow.xaml.cs` : champ `_savedTabGroups` initialise au constructeur ;
  `_savedGroupIds` (groupes vivants deja ranges, pour le garde-fou) ;
  `SavedTabGroupsPanel` ajoute a `ShowPanel`.
- `MainWindow.SavedTabGroups.cs` (nouveau partiel) : action « Enregistrer le
  groupe », garde-fou `SaveGroupBar` (accept/dismiss), panneau (cartes en code
  avec pastille couleur/ nb onglets/ date/ apercu, boutons Ouvrir/Supprimer),
  `OpenSavedGroup` (recree un groupe vivant via `AddTab` + `groupId`).
- `MainWindow.Navigation.cs` : entree « Enregistrer le groupe » dans l'en-tete de
  groupe ; garde-fou branche dans `DissolveGroup_Click` et `CloseTab` (fermeture
  du dernier onglet d'un groupe -> nettoyage du groupe vide + proposition).
- `MainWindow.xaml` : panneau `SavedTabGroupsPanel`, barre `SaveGroupBar`
  (nouvelle ligne du BrowserPanel, BrowserHost passe en Row 8), entrees
  « Groupes enregistres » dans les deux menus (Naviguer / Navigation).
- `MainWindow.CommandPalette.cs` : entree « Groupes enregistres ».
- `MainWindow.Profile.cs` : `SetGuestMode` du store au passage en invite.

## Verification (skill verify)

- Build WinUI OK ; `dotnet test` : 315/315 verts, dont 15 nouveaux
  (`SavedTabGroupStoreTests`) couvrant : enregistrement + relecture disque,
  filtrage pages internes, dedoublonnage titre vide, tri par recence, suppression
  + persistance, `Find`, mode invite (memoire videe, aucune ecriture disque),
  `IsSavableUrl` (theorie).
- Verification live de l'UI **non aboutie** : l'app de test s'arrete de facon
  aleatoire (au demarrage, a l'inactivite) dans l'environnement — instabilite
  WebView2 deja documentee en 0.73, aucune exception non geree loggee, confirmee
  non liee a cette fonction. De plus, les commandes de pilotage souris/clavier ont
  ete refusees. Decision (accord utilisateur) : finaliser la version, l'utilisateur
  teste la fonction lui-meme (clic droit onglet > Nouveau groupe ; clic droit
  en-tete > Enregistrer ; menu Naviguer > Groupes enregistres). Le round-trip
  enregistrement/relecture/reouverture est couvert par les tests ; les chemins UI
  restants reprennent des patrons existants (cartes de panneau, `AddTab`).
