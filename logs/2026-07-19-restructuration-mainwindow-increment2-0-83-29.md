# 2026-07-19 - Restructuration des god files de MainWindow, increment 2/2 (0.83.29-dev)

Suite et fin (pour l'instant) du point 3 de l'audit initial. L'increment 1
(0.83.28-dev) avait reduit `Navigation.cs`, `Settings.cs` et `Bookmarks.cs`,
en reportant volontairement `Profile.cs` (1292 lignes) et `xaml.cs` (1285
lignes) - identifies comme plus risques sans exploration approfondie a
l'epoque.

## Decouverte : `Profile.cs` n'a pas besoin d'extraction

L'increment 1 avait flagge `RootKeyDown` (routeur clavier global, lignes
~981-1026) comme un corps etranger a extraire de `Profile.cs`, parce qu'il
gere Ctrl+K (palette de commandes) et Echap (sortie plein ecran) en plus du
PIN. Relecture complete de la methode cette session :

```csharp
private void RootKeyDown(object sender, KeyRoutedEventArgs e)
{
    ResetSessionTimer();
    if (e.Key == Windows.System.VirtualKey.K && IsControlKeyDown() && ...)
    { ToggleCommandPalette(); return; }
    if (CommandPaletteOverlay.Visibility == Visibility.Visible && e.Key == Escape)
    { HideCommandPalette(); return; }
    if (IsImmersiveFullScreenActive() && e.Key == Escape)
    { ExitImmersiveFullScreenFromKeyboard(); return; }
    if (LoginPinPanel.Visibility != Visibility.Visible) return;
    // ... dispatch vers ProcessPinInputAsync, qui utilise directement
    // _pinBuffer/_pinFailCount (champs exclusifs a Profile.cs)
}
```

La branche PIN (la derniere, apres les retours anticipes) est la raison
d'etre principale de la methode et utilise des champs que `Profile.cs` est
seul a utiliser dans tout le projet. Ce n'est donc pas un corps etranger a
deplacer : c'est un routeur multi-usage qui vit legitimement ici. Le reste
du fichier (14 champs exclusifs, deja confirme a l'increment 1) n'a pas de
couplage problematique non plus - juste de la taille, sans urgence.
**Conclusion : `Profile.cs` laisse tel quel cette session**, contrairement
a l'hypothese de depart.

## Extraction realisee sur `xaml.cs`

`xaml.cs` (1285 -> 652 lignes) :

- **`MainWindow.UsageMode.cs`** (553 l.) : fonctionnalite Mode d'usage /
  Compagnon Lumie complete (`ApplyUsageModeFromUi`,
  `UpdateUsageModeButtonUi`, `UpdateModeCompanionUi`,
  `ModeCompanionButton_Click`, `RunModeCompanionActionAsync`, le switch
  `ModeCompanion(...)`, `ApplyUsageModePreset`, `AddPinnedModule`,
  `ModulePinToggle_Click`, `UpdateModulesPinUi`, `UsageModeLabel`) + le
  record `ModeCompanionDefinition` (declare a la toute fin du fichier
  original, loin de son usage - meme situation orpheline que
  `NewTabModeContext`/`ModeChromePalette`/`BookmarkFolderChoice` a
  l'increment 1). Ne depend que de `_uiSettings`.
- **`MainWindow.WindowChrome.cs`** (165 l.) : couleurs de la barre de
  titre, icone, zone de securite, region de glisser-deplacer
  (`ApplyWindowTitleBarColors`, `UiColor`, `BrushColor`, `WithAlpha`,
  `ApplyNovaControlAccessibility`, `ApplyAppIcon`, `ApplyTitleBarSafeArea`,
  `UpdateTitleBarDragRegion`). Ne depend que de `_appWindow`/ressources
  XAML.
- **Reste dans `xaml.cs` sans y toucher** : le manifeste d'etat partage
  (champs consommes par 7 a 19 autres fichiers) + le constructeur a l'ordre
  d'initialisation critique (`UiSettings.Load` avant
  `WebView2Bootstrap.ConfigureOnce`), le bloc menu/navigation-panneaux
  (dont `OpenProfileSettings()`, utilise par un test), `ShowPanel` (utilise
  par 18 fichiers), et les aides statiques generiques (`NormalizeAddress`,
  `SearchUrl`, `HashUrl`, `OriginOf`, `DisplayTitle`, `DisplayAddressForBar`)
  utilisees par de nombreux autres fichiers.

Meme methode qu'a l'increment 1 : sauvegarde des plages exactes via
`sed -n`, assemblage des nouveaux fichiers par concatenation shell directe
(`cat`), verification octet-a-octet (`diff`) avant de considerer chaque
extraction faite.

## Lecon de l'increment 1 appliquee : tests verifies AVANT extraction

Cette fois, chaque assertion de `UsageModeVisualIdentityTests.cs` contre
`MainWindow.xaml.cs` a ete localisee ligne par ligne (`grep -n`) avant de
couper quoi que ce soit, plutot que de decouvrir les tests casses apres
coup (comme c'etait arrive deux fois a l'increment 1). Resultat : 4 tests
identifies comme necessitant une redirection de variable
(`Chrome_lumora_applique_la_palette_du_mode_actif` ->
`MainWindow.WindowChrome.cs` ;
`Accueil_lumora_affiche_un_outil_et_une_presentation_par_mode`,
`Modes_lumora_ont_un_compagnon_permanent_et_un_accueil_aere`,
`Mode_neutre_reste_la_base_avec_raccourcis_et_acces_rapide` ->
`MainWindow.UsageMode.cs`), corriges avant meme de lancer les tests. Aucune
surprise cette fois : `dotnet test` est passe du premier coup a 534/534
apres l'extraction.

## Documentation et version

- `docs/PROCHAINES_ETAPES.md` : point 3 de l'audit initial marque traite.
  Retrait de la liste "priorite haute" (les deux increments sont clos).
- Version passee a `0.83.29-dev`.

**Verification** :

- Build MSBuild x64 (Debug) : 0 avertissement, 0 erreur.
- `dotnet test Lumora.Tests\Lumora.Tests.csproj --no-restore` : 534/534
  des le premier passage (aucune correction de test necessaire apres
  coup, contrairement a l'increment 1).
- Fidelite des deux extractions verifiee par comparaison octet-a-octet
  (`diff`).
- Verification en conditions reelles : profil de test isole, mode invite,
  pilotage UIA. Changement de mode d'usage "Neutre" -> "Focus" via le
  bouton de mode : "Mode d'usage : Focus" et "Compagnon du mode Focus"
  confirmes appliques correctement (valide `UsageMode.cs` -
  `ApplyUsageModeFromUi`/`UpdateUsageModeButtonUi`/`UpdateModeCompanionUi`/
  `ApplyUsageModePreset`). Ce changement declenche aussi
  `ApplyUsageModeChrome` (dans `SettingsTheme.cs`), qui appelle
  `ApplyWindowTitleBarColors` (dans `WindowChrome.cs`) - aucune exception,
  fenetre restee pleinement fonctionnelle apres le changement, validant les
  deux extractions dans la meme interaction. Nettoyage effectue.

**Version :** `0.83.29-dev`.
