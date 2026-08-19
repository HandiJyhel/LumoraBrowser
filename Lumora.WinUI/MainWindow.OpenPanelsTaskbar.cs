using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

// Barre des taches Lumora (2026-08-18) : chaque panneau "application" suivi
// (OpenPanelsTaskbar.Definitions) obtient un bouton dans StatusBarRow des
// qu'il s'ouvre via ShowPanel, quel que soit son point d'entree - aucun des
// XxxMenu_Click existants n'a besoin d'etre modifie, tout passe par le hook
// unique ajoute en fin de ShowPanel (MainWindow.xaml.cs). Rien n'est
// persiste : fermer Lumora vide la liste (comportement voulu, confirme avec
// l'utilisateur).
public sealed partial class MainWindow
{
    private IReadOnlyList<string> _openTrackedPanelIds = Array.Empty<string>();
    private Dictionary<string, FrameworkElement> _trackedPanelsById = new();
    private Dictionary<FrameworkElement, string> _trackedPanelIdsByPanel = new();

    // Appele une fois depuis le constructeur, juste apres InitializeComponent
    // (les elements nommes du XAML - VaultPanel, BookmarksPanel... - ne sont
    // accessibles qu'a partir de ce moment). A propos, l'assistant d'import
    // et le Centre du site actuel sont volontairement absents de cette table :
    // un ShowPanel sur l'un de ces 3 panneaux ne cree donc jamais de bouton
    // (decision utilisateur du 2026-08-18).
    private void InitializeOpenPanelsTaskbar()
    {
        _trackedPanelsById = new Dictionary<string, FrameworkElement>
        {
            [StartMenuTileIds.Vault] = VaultPanel,
            [StartMenuTileIds.Favoris] = BookmarksPanel,
            [StartMenuTileIds.History] = HistoryPanel,
            [StartMenuTileIds.Settings] = SettingsPanel,
            [StartMenuTileIds.Notes] = NotesPanel,
            [OpenPanelsTaskbar.RssId] = RssPanel,
            [StartMenuTileIds.Downloads] = DownloadsPanel,
            [StartMenuTileIds.TabGroups] = SavedTabGroupsPanel,
            [StartMenuTileIds.Sessions] = SessionsPanel,
            [StartMenuTileIds.Wallet] = WalletPanel,
            [StartMenuTileIds.WebApps] = WebAppsPanel,
            [StartMenuTileIds.ReadingLens] = ReadingLensPanel,
            [StartMenuTileIds.AllModules] = ModulesPanel,
        };
        _trackedPanelIdsByPanel = _trackedPanelsById.ToDictionary(kv => kv.Value, kv => kv.Key);
    }

    // Point d'entree unique, appele en fin de ShowPanel pour CHAQUE panneau
    // affiche (suivi ou non) : si visiblePanel est suivable et pas deja dans
    // la liste des applications ouvertes, on l'y ajoute ; dans tous les cas
    // on reaffiche la barre, pour que les autres boutons deja presents
    // repassent correctement en etat arriere-plan (leur Visibility vient de
    // changer aussi, cf. ShowPanel).
    private void SyncOpenPanelsTaskbar(FrameworkElement visiblePanel)
    {
        if (_trackedPanelIdsByPanel.TryGetValue(visiblePanel, out var id))
        {
            _openTrackedPanelIds = OpenPanelsTaskbar.AddIfMissing(_openTrackedPanelIds, id);
        }

        RenderOpenPanelsTaskbar();
    }

    private void RenderOpenPanelsTaskbar()
    {
        OpenPanelsTaskbarPanel.Children.Clear();
        foreach (var id in _openTrackedPanelIds)
        {
            if (!_trackedPanelsById.TryGetValue(id, out var panel))
            {
                continue;
            }

            OpenPanelsTaskbarPanel.Children.Add(CreateOpenPanelsTaskbarItem(id, panel));
        }
    }

    // Un item = 2 boutons cote a cote (jamais un Button imbrique dans un
    // Button - peu fiable en WinUI pour recevoir 2 clics distincts) : le
    // premier active/ramene le panneau au premier plan, le second (la croix)
    // ferme uniquement ce module, jamais Lumora.
    private Grid CreateOpenPanelsTaskbarItem(string id, FrameworkElement panel)
    {
        var definition = OpenPanelsTaskbar.Find(id);
        var title = definition?.Title ?? id;
        var isActive = panel.Visibility == Visibility.Visible;
        var stateLabel = isActive ? "au premier plan" : "en arrière-plan";

        var dot = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = isActive
                ? ResolveNovaBrush("NovaSuccessBrush", UiColor(61, 214, 136))
                : ResolveNovaBrush("NovaTextMutedBrush", UiColor(120, 113, 102)),
            VerticalAlignment = VerticalAlignment.Center
        };

        var icon = new FontIcon
        {
            Glyph = string.IsNullOrEmpty(definition?.Glyph) ? "" : definition!.Glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        var label = new TextBlock
        {
            Text = title,
            FontSize = 11.5,
            FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var activateContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        activateContent.Children.Add(dot);
        activateContent.Children.Add(icon);
        activateContent.Children.Add(label);

        var activateButton = new Button
        {
            Content = activateContent,
            Tag = id,
            Style = RootShell.Resources["NovaTaskbarItemButtonStyle"] as Style,
            Background = isActive
                ? ResolveNovaBrush("NovaChromeSurfaceBrush", UiColor(34, 42, 58))
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Opacity = isActive ? 1.0 : 0.62
        };
        activateButton.Click += OpenPanelsTaskbarItem_Click;
        ApplyNovaControlAccessibility(activateButton, $"{title}, ouvert, {stateLabel}");
        ToolTipService.SetToolTip(activateButton, title);

        var closeButton = new Button
        {
            Tag = id,
            Width = 20,
            Height = 20,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = new FontIcon
            {
                Glyph = "\uE711",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 9,
                Opacity = 0.6
            }
        };
        closeButton.Click += OpenPanelsTaskbarItemClose_Click;
        ApplyNovaControlAccessibility(closeButton, $"Fermer {title}");
        ToolTipService.SetToolTip(closeButton, $"Fermer {title}");

        var container = new Grid();
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(closeButton, 1);
        container.Children.Add(activateButton);
        container.Children.Add(closeButton);
        return container;
    }

    // Meme motif defensif que ApplyNovaControlAccessibility
    // (MainWindow.WindowChrome.cs) : certains brushes semantiques
    // (NovaSuccessBrush...) vivent dans App.xaml plutot que RootShell.Resources
    // (contrairement aux brushes de theme mutables comme NovaChromeSurfaceBrush,
    // voir SetBrush dans MainWindow.SettingsTheme.cs) - on essaie les deux
    // dictionnaires avant de retomber sur une couleur litterale.
    //
    // BUG REEL CORRIGE (2026-08-18) : l'indexeur ResourceDictionary[key] ne
    // renvoie PAS null pour une cle absente comme un Dictionary<> ordinaire -
    // il LEVE une COMException ("Cannot find a resource with the given key").
    // L'ancien code (`RootShell.Resources[key] as Brush ?? ...`) plantait donc
    // AVANT meme d'atteindre le `??` des que "NovaSuccessBrush" (absent de
    // RootShell.Resources, uniquement dans App.xaml) etait demande - crash
    // natif reel a l'ouverture du Coffre (le seul panneau qui declenchait ce
    // chemin avec isActive=true au bon moment), trouve via winui-runtime-trace.log
    // apres signalement utilisateur. TryGetValue ne leve jamais, c'est le seul
    // moyen sur pour sonder un ResourceDictionary sans certitude sur la cle.
    private Brush ResolveNovaBrush(string key, Windows.UI.Color fallback)
    {
        if (RootShell.Resources.TryGetValue(key, out var local) && local is Brush localBrush)
        {
            return localBrush;
        }

        if (Application.Current.Resources.TryGetValue(key, out var app) && app is Brush appBrush)
        {
            return appBrush;
        }

        return new SolidColorBrush(fallback);
    }

    private void OpenPanelsTaskbarItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id } || !_trackedPanelsById.TryGetValue(id, out var panel))
        {
            return;
        }

        var title = OpenPanelsTaskbar.Find(id)?.Title ?? id;
        ShowPanel(panel, title);
    }

    // Ferme UNIQUEMENT ce module - jamais Lumora (decision utilisateur du
    // 2026-08-18). Si le panneau ferme etait au premier plan, on revient a la
    // navigation (meme fallback que BackToPageButton_Click,
    // MainWindow.xaml.cs) ; ShowPanel se charge alors lui-meme de reafficher
    // la barre via SyncOpenPanelsTaskbar. Sinon on se contente de retirer le
    // bouton et de reporter le focus sur l'item suivant (ou ModulesButton si
    // la barre redevient vide).
    private void OpenPanelsTaskbarItemClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id } || !_trackedPanelsById.TryGetValue(id, out var panel))
        {
            return;
        }

        var wasVisible = panel.Visibility == Visibility.Visible;
        _openTrackedPanelIds = OpenPanelsTaskbar.Remove(_openTrackedPanelIds, id);

        if (wasVisible)
        {
            var tab = CurrentTab();
            ShowPanel(BrowserPanel, tab?.Title ?? "Accueil Lumora");
            return;
        }

        RenderOpenPanelsTaskbar();
        if (OpenPanelsTaskbarPanel.Children.Count > 0)
        {
            // CreateOpenPanelsTaskbarItem renvoie un Grid (Panel), pas un Control : le
            // cast direct `as Control` renvoyait toujours null et le focus ne se
            // reportait jamais nulle part en silence (bug réel trouvé en audit le
            // 2026-08-19). Le vrai bouton focusable est le 1er enfant du Grid
            // (activateButton, voir CreateOpenPanelsTaskbarItem).
            var container = OpenPanelsTaskbarPanel.Children[0] as Grid;
            var activateButton = container is { Children.Count: > 0 } ? container.Children[0] : null;
            (activateButton as Control)?.Focus(FocusState.Programmatic);
        }
        else
        {
            ModulesButton.Focus(FocusState.Programmatic);
        }
    }
}
