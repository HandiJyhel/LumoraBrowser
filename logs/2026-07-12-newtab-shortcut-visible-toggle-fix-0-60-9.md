# 2026-07-12 - Deuxieme correctif raccourci nouvel onglet (0.60.9-dev)

## Contexte

Apres le correctif 0.60.8-dev (rafraichissement de l'onglet nouvel onglet
apres ajout d'un raccourci), l'utilisateur a reteste et confirme que le
raccourci "Google" ajoute via le bouton "+" n'apparaissait toujours pas.

## Diagnostic

Second bug independant, egalement confirme par lecture de code (pas une
hypothese) : `NewTabShortcutsHtml()` ne rend la LISTE des raccourcis existants
que si `_uiSettings.NewTabShortcutsVisible` est `true` :

```csharp
var showExisting = _uiSettings.NewTabShortcutsVisible && shortcuts.Count > 0;
```

Ce reglage ("Afficher les raccourcis", Parametres > Apparence) vaut `false`
par defaut (`UiSettings.Default()` = `new()`, valeur par defaut d'un `bool`).
Ajouter un raccourci via le bouton "+" de la page (`HandleNewTabShortcutMessageAsync`)
l'enregistrait bien dans `_uiSettings.NewTabShortcuts` (persistant sur disque),
mais ne touchait jamais `NewTabShortcutsVisible` : sur un profil ou ce toggle
n'a jamais ete active manuellement dans Parametres (cas du profil par defaut),
le raccourci restait donc invisible indefiniment, meme apres le correctif de
rafraichissement de 0.60.8-dev.

Ce comportement etait deja documente comme piege connu en 0.60.2-dev
(memoire `project_newtab_shortcuts_always_addable_0_60_2`) : le bouton "+"
avait ete rendu toujours visible independamment du toggle, precisement pour
qu'un utilisateur avec le toggle desactive puisse quand meme AJOUTER un
raccourci depuis la page — mais cette correction n'allait pas jusqu'a rendre
le raccourci ajoute visible automatiquement, laissant la moitie du piege
intacte : on peut ajouter, mais on ne voit jamais ce qu'on vient d'ajouter.

## Changements

- `HandleNewTabShortcutMessageAsync` (branche ajout, pas edition) : force
  `_uiSettings.NewTabShortcutsVisible = true` juste apres
  `_uiSettings.NewTabShortcuts.Add(shortcut)`. Un utilisateur qui vient
  d'utiliser le bouton "+" veut evidemment voir le raccourci qu'il cree.
  `SaveNewTabShortcutSettings()` synchronise deja le toggle visible dans
  Parametres avec cette valeur, donc l'UI reste coherente.
- Passage de version source a `0.60.9-dev`.

## Verification

- `dotnet test PulseBrowser.Tests\PulseBrowser.Tests.csproj` : 233 tests
  reussis.
- `scripts\build-clean-test-artifact.ps1 -Version 0.60.9-dev` : build reussi,
  0 avertissement, 0 erreur.
- `scripts\build-installer.ps1 -Version 0.60.9-dev` : installateur genere.
- Installateur :
  `artifacts\installer\PulseBrowserSetup-0.60.9-dev-win-x64.exe`.
- SHA256 installateur :
  `94a571877305d5faf4fee39b0b1efb10afc501caf7193dc932cdd76d4821ec9d`.

## Limite

- Non teste manuellement par l'IA (pas de pilotage UI disponible dans cette
  session). Les DEUX bugs identifies (rafraichissement en 0.60.8-dev + toggle
  invisible en 0.60.9-dev) ont des causes confirmees par lecture de code, mais
  seul un test reel par l'utilisateur peut confirmer qu'aucun troisieme
  obstacle ne subsiste.
