# 2026-07-08 - Parametres et accessibilite - 0.39.0-dev

Passe produit dans `PulseBrowser.WinUI` pour mieux organiser les parametres, cadrer la palette `Ctrl+K` et ajouter une premiere base d'accessibilite.

## Modifications

- Version passee a `0.39.0-dev` dans `AGENTS.md` et `MainWindow.xaml.cs`.
- Ajout de champs persistants dans `UiSettings` pour la palette de commande et l'accessibilite.
- Ajout des rubriques `Apparence` et `Accessibilite` dans les parametres.
- Deplacement des reglages du nouvel onglet vers `Apparence`.
- Ajout d'options de contexte pour `Ctrl+K`.
- `Ctrl+K` ne s'ouvre plus par defaut depuis les champs texte ou les pages web.
- Ajout des options d'accessibilite contraste renforce, texte plus lisible, reduction des transitions et focus visible.
- Application immediate des options d'accessibilite au chrome Pulse et a `pulse://accueil`.

## Verification

- Premier `build-winui.cmd` bloque sous sandbox reseau sur NuGet (`NU1301`), puis relance hors sandbox.
- Compilation du code reussie, mais la copie vers le dossier Debug normal a echoue car une instance utilisateur `PulseBrowser.WinUI.exe` verrouillait l'executable.
- Build de verification vers `artifacts/winui-settings-accessibility-build/` reussi avec 0 erreur et 0 avertissement.
- Lancement court de `artifacts/winui-settings-accessibility-build/PulseBrowser.WinUI.exe` reussi: fenetre `Pulse Browser 0.39.0-dev`, processus repondant, fermeture propre.
