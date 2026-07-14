# Lumora 0.73.0-dev - Suggestions de la barre d'adresse

## Objectif

Offrir la fonction la plus attendue au quotidien d'un navigateur et absente de
Lumora jusqu'ici : pendant la frappe dans la barre d'adresse, proposer les pages
que l'utilisateur cherche probablement, pour lui eviter de retaper une URL
complete. Le tout 100% local, fidele a la philosophie du projet : ce qui est tape
n'est jamais envoye a un service de suggestions distant.

## Ce qui change

- En tapant dans la barre d'adresse, une liste de suggestions s'ouvre juste
  en dessous. Trois sources locales sont fusionnees :
  - les **onglets deja ouverts** (pour y basculer sans creer de doublon) ;
  - les **favoris** ;
  - les **pages de l'historique** du profil.
- Fleches haut/bas pour parcourir la liste (l'adresse selectionnee se recopie
  dans la barre, comme Chrome/Firefox), `Entree` pour ouvrir, clic souris pour
  ouvrir directement, `Echap` pour revenir au texte tape.
- Choisir un onglet deja ouvert **bascule** vers cet onglet au lieu de recharger
  la page.
- Le classement privilegie : d'abord la correspondance (prefixe de domaine >
  debut de mot du titre > sous-chaine), puis onglet ouvert > favori > historique,
  puis la frequence et la fraicheur de visite pour l'historique.
- Les accents sont ignores (`meteo` trouve « Meteo France »).
- Les URL internes `lumora://` ne sont jamais suggerees.
- Reglage dedie dans `Parametres > Navigation > Recherche` :
  « Suggestions dans la barre d'adresse » (active par defaut, desactivable).

## Architecture

- `AddressSuggestionEngine` : classe **pure** (aucune dependance UI), compilee
  aussi dans `Lumora.Tests`. Elle prend les candidats fournis par l'appelant,
  filtre, note, dedoublonne par URL canonique et classe. `AggregateHistory`
  regroupe les visites brutes de l'historique par URL avec compteur et date de
  derniere visite.
- `MainWindow.AddressSuggestions.cs` : partiel UI. Collecte les candidats depuis
  `_tabs`, `_allBookmarkNodes` et `_historyPanel.Store`, gere le popup, le clavier
  et la souris. L'onglet courant est exclu des candidats.
- Le popup est ancre sous la barre via `PlacementTarget` (couche popup, rendu
  au-dessus du WebView2). `AddressSuggestionsList.ItemsSource` est lie a la
  collection observable dans le constructeur, comme la palette de commandes.
- Nouveau champ `UiSettings.AddressBarSuggestionsEnabled` (defaut `true`).

## Limites connues

- Fenetre de navigation privee : pas de suggestions (comme le filtre cosmetique
  et le refus des bannieres cookies, reserves a la fenetre principale ; une
  session privee n'a de toute facon ni historique ni favoris a proposer).
- Pas de suggestion de recherche « en ligne » (autocompletion du moteur) : ce
  serait un appel reseau a chaque touche, exactement ce que le projet refuse.

## Verification

- Build WinUI OK ; 300/300 tests verts dont 11 nouveaux
  (`AddressSuggestionEngineTests`).
- Verifie en conditions reelles (mode invite) : le popup s'ouvre sous la barre en
  tapant, affiche l'onglet ouvert correspondant avec libelle « Onglet ouvert » ;
  `Fleche bas` selectionne et recopie l'URL dans la barre ; `Entree` bascule vers
  l'onglet. Un bug de branchement a ete trouve et corrige pendant la verification :
  la `ListView` du popup n'etait pas liee a sa collection (`ItemsSource`), le popup
  s'ouvrait donc vide/invisible.
- Ajout d'un journal des exceptions non gerees (`App.UnhandledException` vers
  `WinUiRuntimeTrace`, actif seulement si `LUMORA_TRACE_STARTUP=1`) : utile au
  diagnostic, ne masque rien.

Details : `logs/2026-07-14-suggestions-barre-adresse-0-73-0.md`.
