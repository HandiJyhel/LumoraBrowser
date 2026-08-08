using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Lumora.WinUI;

// Menu Demarrer (bouton grille de la barre d'outils, ex-ModulesFlyout).
// Tuiles pilotees par StartMenuTileRegistry (id -> libelle/glyphe/section) ;
// l'execution (navigation reelle) reste ici, via ExecuteFor(id), qui
// delegue aux gestionnaires deja existants ailleurs dans l'app - aucune
// nouvelle logique de navigation, uniquement de nouveaux points d'entree
// (epingler, recent/frequent, recherche en direct) vers des destinations
// reelles.
//
// Refonte 0.93.5.0-dev : rail de categories (Epingles + sections) a gauche,
// volet detail a droite - maitre/detail a la Windows 7 (Panneau de
// configuration en vue categories), demande explicite de l'utilisateur pour
// remplacer l'ancien bascule "Epingles par defaut" / "Toutes les
// applications" qui obligeait a naviguer entre deux ecrans.
public sealed partial class MainWindow
{
    private const string PinnedCategoryKey = "__pinned__";

    private string _startMenuQuery = string.Empty;
    // Comme un vrai menu demarrer (Windows/GNOME), on revient toujours sur
    // "Epingles" a chaque ouverture, meme si l'utilisateur avait laisse une
    // autre categorie selectionnee la fois d'avant.
    private string _startMenuSelectedCategoryKey = PinnedCategoryKey;
    private bool _suppressStartMenuCategorySelection;

    private void ModulesFlyout_Opening(object sender, object e)
    {
        StartMenuSearchBox.Text = string.Empty;
        _startMenuQuery = string.Empty;
        _startMenuSelectedCategoryKey = PinnedCategoryKey;
        // Re-rattache explicitement a chaque ouverture (idempotent, garde par
        // les HashSet internes) : le filet generique (HookAutomaticPointerFocus/
        // FlyoutPointerSupport_Opened, MainWindow.xaml.cs) le fait deja sur
        // "Opened", mais l'appeler aussi ici couvre le rail (StartMenuCategoryList,
        // un ListView avec son propre ScrollViewer interne dont le template
        // n'est pas forcement deja applique au premier passage) sans dependre
        // de l'ordre exact Opening/Opened entre les deux mecanismes. Le filet
        // molette lui-meme (hit-test, 2026-08-02) est generique et couvre
        // ModulesFlyoutRoot sans code dedie ici - voir HookWheelFallbackRoot
        // dans MainWindow.xaml.cs.
        AttachScrollViewerPointerSupport(ModulesFlyoutRoot);
        RebuildStartMenuViewModels();
    }

    private void StartMenuCategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressStartMenuCategorySelection ||
            StartMenuCategoryList.SelectedItem is not StartMenuCategoryViewModel category)
        {
            return;
        }

        _startMenuSelectedCategoryKey = category.Key;
        RebuildStartMenuDetail();
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
        ExecuteFor(tileId)();
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

    // Reconstruit le rail de categories (Epingles + sections) et, a partir de
    // la categorie courante, le volet detail - ou la liste plate filtree
    // pendant une recherche active. Appelee a l'ouverture du flyout, apres un
    // epingler/desepingler, et a chaque frappe dans la recherche.
    private void RebuildStartMenuViewModels()
    {
        // Liaison des ItemsSource : meme motif que CommandPaletteList (ItemsSource
        // assigne une fois vers l'ObservableCollection, puis Clear()/Add() a chaque
        // reconstruction) - reassignation idempotente, sans effet si deja en place.
        StartMenuCategoryList.ItemsSource = _startMenuCategories;
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

            StartMenuRailAndDetail.Visibility = Visibility.Collapsed;
            StartMenuFilteredScrollViewer.Visibility = Visibility.Visible;
            StartMenuFilteredList.Visibility = Visibility.Visible;
            StartMenuNoResultsText.Visibility = _startMenuFilteredTiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        StartMenuRailAndDetail.Visibility = Visibility.Visible;
        StartMenuFilteredScrollViewer.Visibility = Visibility.Collapsed;
        StartMenuNoResultsText.Visibility = Visibility.Collapsed;

        _startMenuCategories.Clear();
        _startMenuCategories.Add(new StartMenuCategoryViewModel(PinnedCategoryKey, "Épinglés", "\uE718"));
        foreach (var sectionName in StartMenuTileRegistry.All.Select(t => t.Section).Distinct())
        {
            _startMenuCategories.Add(new StartMenuCategoryViewModel(sectionName, sectionName, SectionHeaderGlyph(sectionName)));
        }

        // Reassignation de l'ItemsSource ci-dessus reinitialise la selection de la
        // ListView : on la repose explicitement sur la categorie courante, sous
        // garde pour ne pas redeclencher RebuildStartMenuDetail en boucle.
        _suppressStartMenuCategorySelection = true;
        StartMenuCategoryList.SelectedItem = _startMenuCategories.FirstOrDefault(c => c.Key == _startMenuSelectedCategoryKey);
        _suppressStartMenuCategorySelection = false;

        RebuildStartMenuDetail();
    }

    // Reconstruit uniquement le volet detail (a droite) pour la categorie
    // courante - appelee a chaque changement de selection du rail, sans
    // reconstruire le rail lui-meme.
    private void RebuildStartMenuDetail()
    {
        StartMenuPinnedRow.ItemsSource = _startMenuPinnedTiles;
        StartMenuDetailList.ItemsSource = _startMenuDetailTiles;

        var pinnedIds = _uiSettings.PinnedStartMenuTileIds;
        var isPinnedCategory = _startMenuSelectedCategoryKey == PinnedCategoryKey;

        _startMenuPinnedTiles.Clear();
        _startMenuDetailTiles.Clear();

        var category = _startMenuCategories.FirstOrDefault(c => c.Key == _startMenuSelectedCategoryKey);
        StartMenuDetailTitleText.Text = category?.Title ?? "Épinglés";
        StartMenuDetailTitleIcon.Glyph = category?.Glyph ?? "";

        if (isPinnedCategory)
        {
            foreach (var id in pinnedIds)
            {
                var def = StartMenuTileRegistry.Find(id);
                if (def is not null) _startMenuPinnedTiles.Add(ToStartMenuTileViewModel(def, pinnedIds));
            }

            StartMenuPinnedEmptyText.Visibility = _startMenuPinnedTiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StartMenuPinnedSection.Visibility = Visibility.Visible;
            StartMenuDetailList.Visibility = Visibility.Collapsed;
        }
        else
        {
            var tiles = StartMenuTileRegistry.All
                .Where(t => t.Section == _startMenuSelectedCategoryKey)
                .Select(t => ToStartMenuTileViewModel(t, pinnedIds));
            foreach (var vm in tiles) _startMenuDetailTiles.Add(vm);

            StartMenuPinnedSection.Visibility = Visibility.Collapsed;
            StartMenuDetailList.Visibility = Visibility.Visible;
        }
    }

    private static StartMenuTileViewModel ToStartMenuTileViewModel(StartMenuTileDefinition t, List<string> pinnedIds) =>
        new(t.Id, t.Title, t.Subtitle, t.Glyph, pinnedIds.Contains(t.Id, StringComparer.Ordinal));

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
        StartMenuTileIds.ReopenTab => () => ReopenTabMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.TabGroups => () => SavedTabGroupsMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.SiteControl => () => SiteControlMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Settings => () => SettingsMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Profiles => OpenProfileSettings,
        StartMenuTileIds.AllModules => () => ModulesMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.About => () => AboutMenu_Click(this, new RoutedEventArgs()),
        StartMenuTileIds.Studio => OpenWorkspaceSettings,
        StartMenuTileIds.ChromeStyle => OpenChromeStyleSettings,
        _ => () => { }
    };
}
