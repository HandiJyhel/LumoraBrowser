# 2026-07-13 - Onglets recemment fermes (Ctrl+Shift+T) 0.68.0-dev

## Changements

- Ajout de `Tabs/ClosedTabHistory.cs` : pile bornee (20) des onglets fermes, classe pure sans dependance UI (`ClosedTabRecord`), plus recent en tete ; les onglets restes sur `lumora://accueil` ne sont pas memorises.
- Ajout de `MainWindow.ClosedTabs.cs` : capture dans `CloseTab` (avant retrait), restauration avec groupe (si encore existant) et etat epingle.
- Accelerateur `Ctrl+Shift+T`, menu `Naviguer > Rouvrir l'onglet ferme` (2 menus), palette de commandes : commande `Rouvrir l'onglet ferme` + une entree par onglet ferme recent (categorie `Onglet ferme`) pour restaurer un onglet precis.
- Passage en mode invite : pile videe (pas de restauration inter-profils).
- Pile en memoire uniquement, choix documente (pas de trace disque supplementaire).

## Verification

- `dotnet test Lumora.Tests` : 276/276 tests verts (dont 6 nouveaux `ClosedTabHistoryTests` : ordre LIFO, capacite, filtrage accueil, retrait cible, vidage).
- `build-winui.cmd` : 0 avertissement, 0 erreur.
- Test manuel restant : fermer un onglet groupe, supprimer le groupe, `Ctrl+Shift+T` doit restaurer l'onglet hors groupe.
