# Lumora 0.74.0-dev - Groupes d'onglets enregistres

## Objectif

Regler le vrai defaut des groupes d'onglets des navigateurs actuels : un groupe
patiemment constitue disparait des qu'on ferme ses onglets. Dans Lumora, on peut
maintenant **ranger un groupe dans une bibliotheque locale** et le **rouvrir plus
tard**, meme apres avoir quitte. 100% local, fichier chiffre du profil.

## Ce qui change

- **Enregistrer un groupe** : depuis l'en-tete d'un groupe d'onglets, action
  « Enregistrer le groupe ». Le nom, la couleur et la liste des pages (titre +
  URL) sont stockes dans la bibliotheque.
- **Garde-fou** : quand on ferme le dernier onglet d'un groupe, ou qu'on dissout
  un groupe non enregistre, une barre discrete propose « Garder ce groupe ? »
  avant qu'il disparaisse. Ne se represente pas pour un groupe deja range.
- **Retrouver** : panneau « Groupes enregistres » (menu Naviguer + palette de
  commandes). Chaque groupe montre sa pastille de couleur, le nombre d'onglets,
  la date d'enregistrement et un apercu des premieres pages. Un clic sur
  « Ouvrir » reconstitue tout le groupe (un onglet par page). « Supprimer »
  retire l'entree.
- Fermer les onglets d'un groupe rouvert depuis la bibliotheque ne supprime pas
  l'entree : elle reste retrouvable.

## Architecture

- `SavedTabGroup` / `SavedTabGroupStore` : classe **pure** (aucune dependance UI),
  compilee aussi dans `Lumora.Tests`. La persistance chiffree est injectee via des
  delegues lecture/ecriture (`LumoraFile.TryReadAllText` / `WriteAllText`), ce qui
  la rend testable hors WinUI. `Save` filtre les pages internes (`lumora://`) et
  sans URL web, dedoublonne le titre vide en URL, et retourne null si rien
  d'enregistrable. `SetGuestMode` vide la memoire et coupe l'ecriture disque.
- `MainWindow.SavedTabGroups.cs` : partiel UI. Action d'enregistrement, garde-fou
  (barre `SaveGroupBar`), rendu du panneau (cartes construites en code, comme
  les telechargements), reouverture (`OpenSavedGroup` reutilise `AddTab` avec un
  `groupId`), suppression.
- Nouveau chemin `LumoraProfilePaths.SavedTabGroupsFile`
  (`navigation/saved-tab-groups.lumora`).
- Garde-fou branche dans `CloseTab` (dernier onglet d'un groupe) et
  `DissolveGroup_Click`.

## Limites connues

- Mode invite : pas de bibliotheque (rien n'est ecrit sur le disque du profil,
  comme l'historique et les favoris).
- L'enregistrement est une photo a l'instant T : rouvrir un groupe recree ses
  onglets a partir des URL memorisees, il ne restaure pas l'historique de
  navigation interne de chaque onglet (comme pour « rouvrir l'onglet ferme »).
- Un groupe enregistre puis modifie (onglets ajoutes/retires) n'est pas mis a
  jour automatiquement : il faut le reenregistrer pour capturer le nouvel etat.

## Verification

- Build WinUI OK ; 315/315 tests verts dont 15 nouveaux
  (`SavedTabGroupStoreTests` : enregistrement + relecture disque, filtrage des
  pages internes, dedoublonnage titre vide, tri par recence, suppression,
  recherche par id, mode invite sans ecriture, `IsSavableUrl`).
- Verification live de l'UI (creation d'un groupe -> enregistrement -> reouverture
  depuis le panneau) reportee : l'app de test s'arrete de facon aleatoire dans
  l'environnement (instabilite WebView2 deja constatee en 0.73, sans exception
  loggee, non liee a cette fonction) et le pilotage souris/clavier a ete refuse.
  Le coeur (store, round-trip enregistrement/relecture/reouverture) est couvert
  par les tests ; les chemins UI restants reprennent des patrons existants
  (cartes de panneau, `AddTab`).

Details : `logs/2026-07-14-groupes-onglets-enregistres-0-74-0.md`.
