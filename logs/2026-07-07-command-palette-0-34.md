# 2026-07-07 - Palette de commande - 0.34.0-dev

## Contexte

Apres le centre du site actuel `0.33.0-dev`, l'utilisateur a valide le chantier suivant propose : une palette de commande `Ctrl+K` pour donner a Pulse Browser une interaction plus rapide et moderne.

## Changements

- Passage de version a `0.34.0-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.
- Ajout de `CommandPaletteOverlay` dans `MainWindow.xaml`.
- Ajout de `MainWindow.CommandPalette.cs`.
- Ajout d'un `KeyboardAccelerator` `Ctrl+K` dans le constructeur de `MainWindow`.
- Extension de `RootKeyDown` pour gerer le raccourci et `Echap`.
- La palette cherche dans les commandes internes, les onglets ouverts, les favoris, l'historique et une adresse/recherche saisie.
- Les resultats peuvent etre ouverts par `Entree`, double-clic ou selection clavier.

## Verification

- Build MSBuild x64 vers `artifacts/winui-commandpalette-build/` reussi avec 0 erreur et 0 avertissement.
- Pas de lancement interactif supplementaire dans cette passe.

## Limites

- Le comportement exact de `Ctrl+K` quand le focus est dans WebView2 doit etre teste en navigation reelle.
- La palette ne cherche pas encore dans les mots de passe pour eviter d'afficher des donnees sensibles dans une recherche globale.
