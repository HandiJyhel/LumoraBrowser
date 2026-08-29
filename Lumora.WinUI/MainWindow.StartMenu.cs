using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

// Menu Demarrer (bouton grille de la barre d'outils, ex-ModulesFlyout).
// Tuiles pilotees par StartMenuTileRegistry (id -> libelle/glyphe/section) ;
// l'execution (navigation reelle) reste ici, via ExecuteFor(id), qui
// delegue aux gestionnaires deja existants ailleurs dans l'app - aucune
// nouvelle logique de navigation, uniquement de nouveaux points d'entree
// (epingler, recent/frequent, recherche en direct) vers des destinations
// reelles.
//
// Refonte 0.94.0.0-dev ("Control panel and start menu", demande explicite
// utilisateur - "je vois comment est constitue le menu demarrer de Windows
// 11 ? Je veux a peu pres la meme chose.") : remplace le maitre/detail "a la
// Windows 7, Panneau de configuration" du 0.93.5.0-dev (rail de categories +
// volet detail) par le meme principe que le vrai menu Demarrer de Windows 11
// - "Epingle" en grille + "Recommande" par defaut, un lien "Toutes les
// tuiles" pour tout voir. Lumora avait deja un bascule Epingles/Toutes les
// applications avant le 0.93.5.0-dev, abandonne alors car il "obligeait a
// naviguer entre deux ecrans" - repris ici avec un seul lien (pas un vrai
// second ecran a chercher) et regroupe par famille de couleur plutot qu'un
// A-Z strict (confirme par l'utilisateur) : Lumora n'a que ~23 tuiles, pas
// besoin de l'A-Z qui sert Windows a gerer des centaines d'applications.
public sealed partial class MainWindow
{
    private string _startMenuQuery = string.Empty;
    // Comme un vrai menu demarrer (Windows/GNOME), on revient toujours sur
    // l'accueil (Epingle + Recommande) a chaque ouverture, meme si
    // l'utilisateur avait laisse "Toutes les tuiles" ouvert la fois d'avant.
    private bool _startMenuShowAllTiles;

    private void ModulesFlyout_Opening(object sender, object e)
    {
        StartMenuSearchBox.Text = string.Empty;
        _startMenuQuery = string.Empty;
        _startMenuShowAllTiles = false;
        // Re-rattache explicitement a chaque ouverture (idempotent, garde par
        // les HashSet internes) : le filet generique (HookAutomaticPointerFocus/
        // FlyoutPointerSupport_Opened, MainWindow.xaml.cs) le fait deja sur
        // "Opened", mais l'appeler aussi ici couvre les ScrollViewer internes
        // dont le template n'est pas forcement deja applique au premier
        // passage. Le filet molette lui-meme (hit-test, 2026-08-02) est
        // generique et couvre ModulesFlyoutRoot sans code dedie ici - voir
        // HookWheelFallbackRoot dans MainWindow.xaml.cs.
        AttachScrollViewerPointerSupport(ModulesFlyoutRoot);
        RebuildStartMenuViewModels();
    }

    private void StartMenuShowAllTiles_Click(object sender, RoutedEventArgs e)
    {
        _startMenuShowAllTiles = true;
        RebuildStartMenuViewModels();
    }

    private void StartMenuBackToHome_Click(object sender, RoutedEventArgs e)
    {
        _startMenuShowAllTiles = false;
        RebuildStartMenuViewModels();
    }

    private void StartMenuTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tileId })
        {
            OpenStartMenuTile(tileId);
        }
    }

    private void StartMenuTilePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string tileId })
        {
            TogglePinnedStartMenuTile(tileId);
        }
    }

    private void StartMenuSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _startMenuQuery = StartMenuSearchBox.Text?.Trim() ?? string.Empty;
        RebuildStartMenuViewModels();
    }

    private void OpenStartMenuTile(string tileId)
    {
        ModulesFlyout.Hide();
        RecordStartMenuTileOpen(tileId);
        ExecuteFor(tileId)();
    }

    // Alimente "Recommande" (StartMenuTileRegistry.TopTiles/UsageScore,
    // definis depuis le 0.93.x mais jamais branches a un affichage avant ce
    // lot). Une seule entree par tuile (upsert), meme motif que
    // PasskeyEntry.LastUsedAt - voir Models/StartMenuTiles.cs.
    private void RecordStartMenuTileOpen(string tileId)
    {
        var now = DateTimeOffset.Now;
        var usage = _uiSettings.StartMenuTileUsage;
        var index = usage.FindIndex(u => u.TileId == tileId);
        if (index >= 0)
        {
            usage[index] = usage[index].RecordOpen(now);
        }
        else
        {
            usage.Add(new StartMenuTileUsage(tileId, 1, now));
        }

        _uiSettings.Save(_profile.UiSettingsFile);
    }

    private void TogglePinnedStartMenuTile(string tileId)
    {
        var pinned = _uiSettings.PinnedStartMenuTileIds;
        if (!pinned.Remove(tileId))
        {
            pinned.Add(tileId);
        }

        _uiSettings.Save(_profile.UiSettingsFile);
        RebuildStartMenuViewModels();
    }

    // Point d'entree unique : bascule entre recherche / accueil (Epingle +
    // Recommande) / Toutes les tuiles. Appelee a l'ouverture du flyout, apres
    // un epingler/desepingler, a chaque frappe dans la recherche et sur les
    // 2 liens de bascule.
    private void RebuildStartMenuViewModels()
    {
        StartMenuPinnedGrid.ItemsSource = _startMenuPinnedTiles;
        StartMenuRecommendedList.ItemsSource = _startMenuRecommendedTiles;
        StartMenuFilteredList.ItemsSource = _startMenuFilteredTiles;

        var isFiltering = !string.IsNullOrEmpty(_startMenuQuery);

        _startMenuFilteredTiles.Clear();

        if (isFiltering)
        {
            var pinnedIds = _uiSettings.PinnedStartMenuTileIds;
            var matches = StartMenuTileRegistry.All
                .Select(t => (Tile: t, Score: StartMenuTileRegistry.ScoreTile(t, _startMenuQuery)))
                .Where(r => r.Score > 0)
                .OrderByDescending(r => r.Score)
                .Select(r => ToStartMenuTileViewModel(r.Tile, pinnedIds));
            foreach (var vm in matches) _startMenuFilteredTiles.Add(vm);

            StartMenuHome.Visibility = Visibility.Collapsed;
            StartMenuAllTiles.Visibility = Visibility.Collapsed;
            StartMenuFilteredScrollViewer.Visibility = Visibility.Visible;
            StartMenuFilteredList.Visibility = Visibility.Visible;
            StartMenuNoResultsText.Visibility = _startMenuFilteredTiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        StartMenuFilteredScrollViewer.Visibility = Visibility.Collapsed;
        StartMenuNoResultsText.Visibility = Visibility.Collapsed;

        StartMenuHome.Visibility = _startMenuShowAllTiles ? Visibility.Collapsed : Visibility.Visible;
        StartMenuAllTiles.Visibility = _startMenuShowAllTiles ? Visibility.Visible : Visibility.Collapsed;

        if (_startMenuShowAllTiles)
        {
            RebuildStartMenuAllTiles();
        }
        else
        {
            RebuildStartMenuHome();
        }
    }

    // Accueil : Epingle (grille carree) + Recommande (usage reel, plus
    // recent/frequent en tete). Les 2 sont independants - une tuile
    // epinglee et frequemment ouverte peut legitimement apparaitre dans les
    // deux, comme dans le vrai menu Demarrer de Windows 11.
    private void RebuildStartMenuHome()
    {
        var pinnedIds = _uiSettings.PinnedStartMenuTileIds;

        _startMenuPinnedTiles.Clear();
        foreach (var id in pinnedIds)
        {
            var def = StartMenuTileRegistry.Find(id);
            if (def is not null) _startMenuPinnedTiles.Add(ToStartMenuTileViewModel(def, pinnedIds));
        }
        StartMenuPinnedEmptyText.Visibility = _startMenuPinnedTiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _startMenuRecommendedTiles.Clear();
        var topIds = StartMenuTileRegistry.TopTiles(_uiSettings.StartMenuTileUsage, DateTimeOffset.Now, count: 4);
        foreach (var id in topIds)
        {
            var def = StartMenuTileRegistry.Find(id);
            if (def is not null) _startMenuRecommendedTiles.Add(ToStartMenuTileViewModel(def, pinnedIds));
        }
        var hasRecommended = _startMenuRecommendedTiles.Count > 0;
        StartMenuRecommendedList.Visibility = hasRecommended ? Visibility.Visible : Visibility.Collapsed;
        StartMenuRecommendedEmptyText.Visibility = hasRecommended ? Visibility.Collapsed : Visibility.Visible;
    }

    // "Toutes les tuiles" : un groupe par section du registre, chacun avec
    // son en-tete colore et sa propre grille - regroupement par famille
    // plutot qu'un A-Z strict (confirme par l'utilisateur, echelle de Lumora
    // bien plus petite que le catalogue Windows). Peuple directement en
    // StackPanel.Children (meme motif que SettingsSearchResultsPanel,
    // MainWindow.SettingsSearch.cs) : pas d'ObservableCollection unique ici,
    // chaque groupe a sa propre source figee au moment de la construction.
    private void RebuildStartMenuAllTiles()
    {
        StartMenuAllTilesPanel.Children.Clear();

        var pinnedIds = _uiSettings.PinnedStartMenuTileIds;
        var template = (DataTemplate)RootShell.Resources["StartMenuGridTileTemplate"];

        foreach (var sectionName in StartMenuTileRegistry.All.Select(t => t.Section).Distinct())
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            header.Children.Add(new FontIcon
            {
                Glyph = SectionHeaderGlyph(sectionName),
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Opacity = 0.7
            });
            header.Children.Add(new TextBlock
            {
                Text = sectionName,
                Style = (Style)RootShell.Resources["NovaPanelSectionTitleStyle"],
                FontSize = 13
            });

            var tiles = StartMenuTileRegistry.All
                .Where(t => t.Section == sectionName)
                .Select(t => ToStartMenuTileViewModel(t, pinnedIds))
                .ToList();

            var repeater = new ItemsRepeater
            {
                ItemTemplate = template,
                ItemsSource = tiles,
                Layout = new UniformGridLayout
                {
                    Orientation = Orientation.Horizontal,
                    MinItemWidth = 140,
                    MinItemHeight = 96,
                    MinColumnSpacing = 8,
                    MinRowSpacing = 8
                }
            };

            var group = new StackPanel { Spacing = 10 };
            group.Children.Add(header);
            group.Children.Add(repeater);
            StartMenuAllTilesPanel.Children.Add(group);
        }
    }

    // Couleur par famille de sens (chantier identite visuelle, 2026-08-10) :
    // le badge derriere le glyphe d'une tuile suit desormais la famille de sa
    // section plutot qu'un unique degrade identique pour toutes les tuiles -
    // Confidentialite = protection, Navigation = contenu personnel,
    // Lecture et contenu/Modules = outils, Lumora et profil = identite. La
    // resolution en couleur reelle se fait cote XAML
    // (StartMenuFamilyKeyToBrushConverter) : ToStartMenuTileViewModel reste
    // une fonction pure qui ne fait que lire StartMenuTileRegistry.
    private static StartMenuTileViewModel ToStartMenuTileViewModel(StartMenuTileDefinition t, List<string> pinnedIds) =>
        new(t.Id, t.Title, t.Subtitle, t.Glyph, pinnedIds.Contains(t.Id, StringComparer.Ordinal), StartMenuTileRegistry.ResolveFamilyKey(t.Section));

    private static string SectionHeaderGlyph(string section) => section switch
    {
        "Modules" => "\uE8A9",
        "Lecture et contenu" => "\uE736",
        "Confidentialité" => "\uE72E",
        "Navigation" => "\uE71D",
        "Lumora et profil" => "\uE713",
        _ => string.Empty
    };

    private Action ExecuteFor(string tileId) => tileId switch
    {
        StartMenuTileIds.Favoris => () => BookmarksMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Vault => () => VaultMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.AdBlocker => OpenAdBlockerSettings,
        StartMenuTileIds.ReaderMode => () => ReaderModeMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Notes => () => NotesMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Translate => () => ModulesTranslate_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.ReadingLens => () => ReadingLensButton_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Passkeys => () => PasskeysMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Sessions => () => SessionsMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Wallet => () => WalletMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.History => () => HistoryMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Downloads => () => DownloadsMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.WebApps => () => WebAppsMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Incognito => () => IncognitoWindowMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.NewWindow => () => NewWindowMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.ReopenTab => () => ReopenTabMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.TabGroups => () => SavedTabGroupsMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.SiteControl => () => SiteControlMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Settings => () => SettingsMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Profiles => OpenProfileSettings,
        StartMenuTileIds.AllModules => () => ModulesMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.About => () => AboutMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Studio => OpenWorkspaceSettings,
        _ => () => { }
    };
}
