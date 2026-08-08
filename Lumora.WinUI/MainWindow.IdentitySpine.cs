using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Lumora.WinUI;

// Colonne verticale identitaire (style de disposition "identitySpine") :
// remplace visuellement le tabstrip horizontal + toolbar classiques par une
// colonne teintee du Mode d'usage, coexiste avec le chrome classique (aucune
// suppression, l'utilisateur choisit via ChromeLayoutStyleCombo). Nouveau
// fichier plutot qu'un ajout a MainWindow.Settings.cs/SettingsTheme.cs (deja
// volumineux), meme logique de decoupage que MainWindow.TabGroups.cs.
public sealed partial class MainWindow
{
    private static readonly string[] ModeAccentColorKeys =
    {
        "neutral", "focus", "reading", "creative", "research", "night"
    };

    private readonly Dictionary<string, string> _pendingModeAccentColors = new(StringComparer.OrdinalIgnoreCase);

    // ── Style de disposition ─────────────────────────────────────────────────

    private static string NormalizeChromeLayoutStyle(string? value) =>
        string.Equals(value, "identitySpine", StringComparison.OrdinalIgnoreCase) ? "identitySpine" : "classic";

    private string SelectedChromeLayoutStyle() =>
        NormalizeChromeLayoutStyle((ChromeLayoutStyleCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? _chromeLayoutStyle);

    private void ChromeLayoutStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave)
        {
            return;
        }

        ApplyChromeLayoutStyleImmediate(SelectedChromeLayoutStyle());
    }

    private void ApplyChromeLayoutStyleImmediate(string style)
    {
        _chromeLayoutStyle = NormalizeChromeLayoutStyle(style);

        SetWorkspaceControlsSilently(() =>
        {
            SelectComboByTag(ChromeLayoutStyleCombo, _chromeLayoutStyle, "classic");
        });

        ApplyChromeLayoutStyle();
        SaveWorkspaceUiSettings();
        UpdateStatusText(_chromeLayoutStyle == "identitySpine"
            ? "Style Lumora activé."
            : "Disposition classique rétablie.");
    }

    // Appelee depuis ApplyUiSettings() (comme ApplyVerticalTabsLayout()) :
    // idempotente via _identitySpineActive, pour ne reparenter les elements
    // qu'une seule fois meme si la fonction est appelee plusieurs fois de suite
    // (meme motif que le reste de la cascade ApplyUiSettings/ApplyCompactModeLayout).
    // Largeur de la colonne identitaire (IdentitySpineHost, MainWindow.xaml,
    // Width="64") dupliquee ici en constante : un Margin de contenu doit
    // exister avant le premier passage de mesure, ActualWidth n'est pas
    // fiable a ce moment-la. A garder synchronisee si la largeur XAML change.
    private const double IdentitySpineContentInset = 64d;

    // Corrige le chevauchement remonte par capture d'ecran (2026-08-07) :
    // IdentitySpineHost est un OVERLAY (Grid.RowSpan="7" dans RootShell, ne
    // participe a aucune colonne de ContentHost) - le rendre visible ne
    // redimensionnait jamais la zone de contenu en dessous, qui gardait sa
    // pleine largeur et se faisait donc simplement recouvrir sur ses 64
    // premiers pixels (menu/nav propre du site web masque, pas seulement en
    // plein ecran mais aussi au survol du masquage automatique). Applique un
    // Margin gauche a ContentHost (ancetre commun a BrowserPanel/
    // SettingsPanel/ModulesPanel/... - tous Grid.Column="3" du meme Grid) en
    // miroir exact de la visibilite de la colonne : le contenu recule
    // vraiment au lieu d'etre seulement recouvert. Appelee a chaque endroit
    // qui bascule IdentitySpineHost.Visibility (ApplyChromeLayoutStyle,
    // Enter/ExitIdentitySpineLayout, Show/HideIdentitySpineChrome pour le
    // masquage automatique, ApplyFullScreenLayout pour le plein ecran).
    private void UpdateIdentitySpineContentInset()
    {
        ContentHost.Margin = IdentitySpineHost.Visibility == Visibility.Visible
            ? new Thickness(IdentitySpineContentInset, 0, 0, 0)
            : new Thickness(0);
    }

    private void ApplyChromeLayoutStyle()
    {
        var wantsSpine = _chromeLayoutStyle == "identitySpine";
        if (wantsSpine == _identitySpineActive)
        {
            IdentitySpineHost.Visibility = wantsSpine ? Visibility.Visible : Visibility.Collapsed;
            UpdateIdentitySpineContentInset();
            return;
        }

        if (wantsSpine)
        {
            EnterIdentitySpineLayout();
        }
        else
        {
            ExitIdentitySpineLayout();
        }

        _identitySpineActive = wantsSpine;
        UpdateTitleBarDragRegion();
    }

    private void EnterIdentitySpineLayout()
    {
        if (!_navigationToolbarCapsuleMarginCaptured)
        {
            _navigationToolbarCapsuleClassicMargin = NavigationToolbarCapsule.Margin;
            _navigationToolbarCapsuleMarginCaptured = true;
        }

        IdentitySpineHost.Visibility = Visibility.Visible;
        IdentitySpineAddressHost.Visibility = Visibility.Visible;
        UpdateIdentitySpineContentInset();

        // Reparenting, pas duplication : la meme AddressBox/Popup de
        // suggestions et le meme ModulesButton/flyout profil continuent de
        // fonctionner sans modification, juste deplaces d'affichage.
        // IdentitySpineAddressHost flotte desormais a part (overlay centre,
        // decale de la largeur de la colonne) plutot que d'etre confine dans
        // les 64px de la colonne - c'est ce confinement qui faisait deborder
        // la capsule dans la premiere version. Pas de contrainte de largeur
        // ici : NavigationToolbarCapsule garde sa taille naturelle. Reparente
        // dans IdentitySpineCapsuleSlot (pas IdentitySpineAddressHost
        // directement) pour laisser une place fixe a la ligne de favoris
        // juste en dessous (IdentitySpineBookmarksTop).
        RootShell.Children.Remove(NavigationToolbarCapsule);
        NavigationToolbarCapsule.Margin = new Thickness(0);
        IdentitySpineCapsuleSlot.Children.Add(NavigationToolbarCapsule);

        RootShell.Children.Remove(ModulesButton);
        ModulesButton.Margin = new Thickness(0);
        ModulesButton.HorizontalAlignment = HorizontalAlignment.Center;
        IdentitySpineFooterHost.Children.Add(ModulesButton);

        // Chrome classique neutralise a plat pendant que la colonne est
        // active, pour eviter d'afficher les deux systemes d'onglets a la
        // fois. La barre de favoris (quelle que soit sa position choisie)
        // est geree separement par ApplyBookmarksBarVisibility() plus bas,
        // qui route vers RenderIdentitySpineBookmarks() plutot que les
        // conteneurs classiques.
        //
        // TopTabsRow garde 52px (bug reel signale par capture d'ecran,
        // 2026-08-07) : ce n'est PAS juste la bande des onglets classiques,
        // c'est aussi la bande de fond derriere les boutons systeme (min/max/
        // fermer, ExtendsContentIntoTitleBar) - meme raison deja documentee
        // pour le mode Classique + onglets verticaux
        // (ApplyVerticalTabsLayout, MainWindow.Settings.cs : "comme Edge/
        // Arc"). La mettre a 0 ici (BrowserTabs reste Collapsed, c'est le
        // bon reglage) laissait les boutons systeme flotter directement
        // au-dessus du contenu web, sans aucune bande derriere - la colonne
        // identitaire elle-meme (IdentitySpineHost, overlay RowSpan="7")
        // n'est pas affectee par cette hauteur, elle continue de se dessiner
        // par-dessus normalement.
        TopTabsRow.Height = new GridLength(52);
        NavigationRow.Height = new GridLength(0);
        BrowserTabs.Visibility = Visibility.Collapsed;
        VerticalTabsRail.Visibility = Visibility.Collapsed;
        VerticalTabsResizeThumb.Visibility = Visibility.Collapsed;
        WorkspaceLeftTabsColumn.Width = new GridLength(0);
        WorkspaceLeftTabsResizeColumn.Width = new GridLength(0);
        WorkspaceRightTabsResizeColumn.Width = new GridLength(0);
        WorkspaceRightTabsColumn.Width = new GridLength(0);

        RenderIdentitySpineTabs();
        UpdateIdentitySpineHomeHeroVisibility(CurrentTab());
        ApplyBookmarksBarVisibility();
    }

    private void ExitIdentitySpineLayout()
    {
        _identitySpineHideTimer?.Stop();
        IdentitySpineLeftRevealZone.Visibility = Visibility.Collapsed;
        IdentitySpineTopRevealZone.Visibility = Visibility.Collapsed;

        IdentitySpineHost.Visibility = Visibility.Collapsed;
        IdentitySpineAddressHost.Visibility = Visibility.Collapsed;
        IdentitySpineHomeHero.Visibility = Visibility.Collapsed;
        IdentitySpineTabItems.Children.Clear();
        HideIdentitySpineBookmarks();
        UpdateIdentitySpineContentInset();

        IdentitySpineCapsuleSlot.Children.Remove(NavigationToolbarCapsule);
        NavigationToolbarCapsule.Margin = _navigationToolbarCapsuleClassicMargin;
        RootShell.Children.Add(NavigationToolbarCapsule);

        IdentitySpineFooterHost.Children.Remove(ModulesButton);
        ModulesButton.Margin = new Thickness(18, 0, 0, 0);
        ModulesButton.HorizontalAlignment = HorizontalAlignment.Left;
        RootShell.Children.Add(ModulesButton);

        // Reconstruit l'etat classique a partir de _tabStripPosition/
        // _compactModeEnabled plutot que de restaurer chaque propriete a la
        // main : ApplyCompactModeLayout() (qui appelle deja
        // ApplyVerticalTabsLayout()/ApplyBookmarksBarVisibility() en cascade)
        // reste la seule source de verite du chrome classique.
        ApplyCompactModeLayout();
    }

    // ── Masquage automatique (colonne + capsule), reveles au survol ──────────
    // Reutilise a l'identique le patron deja existant pour le plein ecran
    // immersif (FullScreenAutoHideChrome/FullScreenAutoHideDelay,
    // MainWindow.Settings.cs) : un seul DispatcherTimer, des bandes
    // transparentes de 10px en bord d'ecran qui revelent au survol, des
    // bascules Visibility directes (pas d'animation). La colonne et la
    // capsule apparaissent/disparaissent ensemble - reglage opt-in,
    // desactive par defaut (_uiSettings.IdentitySpineAutoHide).

    private DispatcherTimer? _identitySpineHideTimer;

    private void ShowIdentitySpineChrome()
    {
        _identitySpineHideTimer?.Stop();
        IdentitySpineHost.Visibility = Visibility.Visible;
        IdentitySpineAddressHost.Visibility = Visibility.Visible;
        IdentitySpineLeftRevealZone.Visibility = Visibility.Collapsed;
        IdentitySpineTopRevealZone.Visibility = Visibility.Collapsed;
        UpdateIdentitySpineContentInset();
    }

    private void ScheduleIdentitySpineHide()
    {
        if (!_uiSettings.IdentitySpineAutoHide || _chromeLayoutStyle != "identitySpine")
        {
            return;
        }

        _identitySpineHideTimer ??= new DispatcherTimer();
        _identitySpineHideTimer.Tick -= IdentitySpineHideTimer_Tick;
        _identitySpineHideTimer.Tick += IdentitySpineHideTimer_Tick;
        _identitySpineHideTimer.Interval = FullScreenAutoHideDelay;
        _identitySpineHideTimer.Stop();
        _identitySpineHideTimer.Start();
    }

    private void IdentitySpineHideTimer_Tick(object? sender, object e)
    {
        _identitySpineHideTimer?.Stop();
        if (!_uiSettings.IdentitySpineAutoHide || _chromeLayoutStyle != "identitySpine")
        {
            return;
        }

        IdentitySpineHost.Visibility = Visibility.Collapsed;
        IdentitySpineAddressHost.Visibility = Visibility.Collapsed;
        IdentitySpineLeftRevealZone.Visibility = Visibility.Visible;
        IdentitySpineTopRevealZone.Visibility = Visibility.Visible;
        UpdateIdentitySpineContentInset();
    }

    private void IdentitySpineChrome_PointerEntered(object sender, PointerRoutedEventArgs e) => ShowIdentitySpineChrome();

    private void IdentitySpineChrome_PointerExited(object sender, PointerRoutedEventArgs e) => ScheduleIdentitySpineHide();

    private void IdentitySpineRevealZone_PointerEntered(object sender, PointerRoutedEventArgs e) => ShowIdentitySpineChrome();

    private void IdentitySpineAutoHideSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave)
        {
            return;
        }

        _uiSettings.IdentitySpineAutoHide = IdentitySpineAutoHideSwitch.IsOn;
        if (!_uiSettings.IdentitySpineAutoHide)
        {
            // Ne jamais rester coince replie si l'utilisateur desactive le
            // reglage pendant que tout est cache.
            ShowIdentitySpineChrome();
        }

        SaveWorkspaceUiSettings();
        UpdateStatusText(_uiSettings.IdentitySpineAutoHide
            ? "Masquage automatique du Style Lumora activé."
            : "Masquage automatique du Style Lumora désactivé.");
    }

    // ── Pastilles d'onglets de la colonne ────────────────────────────────────

    private void RenderIdentitySpineTabs()
    {
        IdentitySpineTabItems.Children.Clear();
        var current = CurrentTab();

        foreach (var tab in _tabs)
        {
            var isActive = current?.Id == tab.Id || IsTabInSplitView(tab.Id);

            // Croix de fermeture TOUJOURS visible mais discrete (meme motif
            // que le rail compact classique, RenderVerticalTabs -
            // MainWindow.TabGroups.cs). Deuxieme iteration (2026-08-08) :
            // premiere version en pastille 16x16 d'angle jugee trop serree
            // par l'utilisateur (Chrome cite en reference pour la TAILLE de
            // cible, pas pour son "hover uniquement" - deja rejete une fois
            // pour manque de decouvrabilite). Troisieme iteration, meme jour :
            // 32x32 juge trop imposant en usage reel, redescendu a 26x26 -
            // toujours nettement plus grand que les 20x20 d'origine juges
            // trop serres, mais moins "pave", plus proche des tailles
            // Chrome/Edge. TOUJOURS visible a opacite reduite (0.6/1, meme
            // reglage que la version precedente), fine marge autour ou le
            // bouton englobant reste cliquable pour selectionner l'onglet.
            var icon = TabIconElement(tab, 20);

            var closeButton = new Button
            {
                Width = 26,
                Height = 26,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = tab.Id,
                CornerRadius = new CornerRadius(13),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                // Viewbox plutot que SymbolIcon.FontSize (n'existe pas sur ce
                // controle) : force le glyphe a une taille lisible (12x12).
                Content = new Viewbox
                {
                    Width = 12,
                    Height = 12,
                    Child = new SymbolIcon(Symbol.Cancel)
                },
                Opacity = 0.6
            };
            ToolTipService.SetToolTip(closeButton, "Fermer l'onglet");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(closeButton, $"Fermer {tab.Title}");
            closeButton.Click += VerticalTabCloseButton_Click;

            var content = new Grid();
            content.Children.Add(icon);
            content.Children.Add(closeButton);

            var button = new Button
            {
                Width = 44,
                Height = 44,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = tab.Id,
                Margin = new Thickness(0, 2, 0, 2),
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(isActive ? 2 : 1),
                Background = (Brush)RootShell.Resources[isActive ? "NovaTabPillActiveBackgroundBrush" : "NovaTabPillInactiveBackgroundBrush"],
                BorderBrush = (Brush)RootShell.Resources[isActive ? "NovaTabPillActiveBorderBrush" : "NovaTabPillInactiveBorderBrush"],
                Content = content
            };
            button.PointerEntered += (_, _) => { closeButton.Opacity = 1; icon.Opacity = 0.3; };
            button.PointerExited += (_, _) => { closeButton.Opacity = 0.6; icon.Opacity = 1; };

            if (tab.GroupId is int tabGroupId && _tabGroups.FirstOrDefault(g => g.Id == tabGroupId) is { } tabGroup)
            {
                button.BorderBrush = new SolidColorBrush(TabGroupColor(tabGroup));
                button.BorderThickness = new Thickness(3);
            }

            ToolTipService.SetToolTip(button, tab.Title);
            ApplyNovaControlAccessibility(button, $"Onglet {tab.Title}");
            button.Click += IdentitySpineTabButton_Click;
            button.ContextFlyout = CreateTabContextFlyout(tab);
            button.CanDrag = true;
            button.AllowDrop = true;
            button.DragStarting += VerticalTabButton_DragStarting;
            button.DragOver += VerticalTabButton_DragOver;
            button.Drop += VerticalTabButton_Drop;

            IdentitySpineTabItems.Children.Add(button);
        }
    }

    private void IdentitySpineTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int id })
        {
            return;
        }

        var tab = _tabs.FirstOrDefault(candidate => candidate.Id == id);
        if (tab is null)
        {
            return;
        }

        HandleTabSelectionModifiers(tab);

        var item = BrowserTabs.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(candidate => candidate.Tag is int tabId && tabId == id);
        if (item is not null)
        {
            _suppressTabNavigation = true;
            BrowserTabs.SelectedItem = item;
            _suppressTabNavigation = false;
        }

        ActivateTab(tab);
        ReturnToBrowserIfHidden();
    }

    // ── Favoris integres selon la position choisie ───────────────────────────
    // Meme patron que les onglets : appelee depuis RenderBookmarksBar()
    // (MainWindow.Bookmarks.cs), pas un site d'appel a part - tout ajout/
    // suppression/reordonnancement de favori resynchronise automatiquement
    // les deux presentations. Reutilise CreateBookmarkBarButton(node) tel
    // quel (contenu, clic, flyout de dossier, menu contextuel, nom
    // accessible deja geres) - seul l'habillage visuel est change pour
    // rester coherent avec le verre de la capsule/des pastilles d'onglets
    // plutot que les teintes chaudes du bandeau classique.

    private void RenderIdentitySpineBookmarks()
    {
        HideIdentitySpineBookmarks();

        var hiddenByCompactChoice = _compactModeEnabled && CompactModeHideBookmarksSwitch.IsOn;
        if (!BookmarksBarSwitch.IsOn || hiddenByCompactChoice)
        {
            return;
        }

        var toolbarNodes = _allBookmarkNodes
            .Where(node => node.ParentId == BookmarkStore.ToolbarRootId)
            .OrderBy(node => node.Position)
            .ToList();
        // Meme racine "Autres favoris" que RenderBookmarksBar() (Classique) -
        // toujours presente structurellement (BookmarkStore la seme des la
        // creation du profil), donc affichee inconditionnellement des que la
        // racine existe, meme vide. Oubliee lors du premier passage a la
        // capsule de verre pleine largeur (retour utilisateur du 2026-08-05 :
        // "il manque toujours le dossier autres favoris") - la ligne
        // integree ne montrait jusque-la que la racine ToolbarRootId.
        var otherRoot = _allBookmarkNodes.FirstOrDefault(node => node.Id == BookmarkStore.OtherRootId);
        if (toolbarNodes.Count == 0 && otherRoot is null)
        {
            return;
        }

        var compact = _bookmarksBarPosition is "left" or "right";
        const int maxVisible = 4;
        var visibleNodes = toolbarNodes.Take(maxVisible).ToList();
        var overflowCount = toolbarNodes.Count - visibleNodes.Count;

        Panel target = _bookmarksBarPosition switch
        {
            "bottom" => IdentitySpineBookmarksBottomHost,
            "right" => IdentitySpineBookmarksRightHost,
            "left" => IdentitySpineFavoritesSection,
            _ => IdentitySpineBookmarksTop
        };

        foreach (var node in visibleNodes)
        {
            target.Children.Add(StyleAsIdentitySpineBookmarkChip(CreateBookmarkBarButton(node), compact));
        }

        if (overflowCount > 0)
        {
            target.Children.Add(StyleAsIdentitySpineBookmarkChip(CreateIdentitySpineBookmarksOverflowButton(overflowCount), compact));
        }

        if (otherRoot is not null)
        {
            // Positions "haut"/"bas" : "Autres favoris" va dans la colonne
            // Auto dediee (IdentitySpineBookmarksTopOtherHost /
            // ...BottomOtherHost), ancree a droite de la ligne pleine
            // largeur - meme resultat visuel que OtherBookmarksBarHost /
            // OtherBookmarksBottomHost en Classique. Le separateur y est
            // systematique (comme la barre verticale, toujours presente,
            // du mode Classique), pas conditionne au nombre de puces a
            // gauche. Positions gauche/droite : rails verticaux compacts,
            // pas d'axe gauche-droite equivalent - le dossier reste ajoute
            // a la suite des puces (fin de liste), inchange.
            if (ReferenceEquals(target, IdentitySpineBookmarksTop))
            {
                IdentitySpineBookmarksTopOtherHost.Children.Add(CreateIdentitySpineBookmarksDivider(compact));
                IdentitySpineBookmarksTopOtherHost.Children.Add(StyleAsIdentitySpineBookmarkChip(CreateBookmarkBarButton(otherRoot), compact));
            }
            else if (ReferenceEquals(target, IdentitySpineBookmarksBottomHost))
            {
                IdentitySpineBookmarksBottomOtherHost.Children.Add(CreateIdentitySpineBookmarksDivider(compact));
                IdentitySpineBookmarksBottomOtherHost.Children.Add(StyleAsIdentitySpineBookmarkChip(CreateBookmarkBarButton(otherRoot), compact));
            }
            else
            {
                if (target.Children.Count > 0)
                {
                    target.Children.Add(CreateIdentitySpineBookmarksDivider(compact));
                }

                target.Children.Add(StyleAsIdentitySpineBookmarkChip(CreateBookmarkBarButton(otherRoot), compact));
            }
        }

        target.Visibility = Visibility.Visible;

        // IdentitySpineBookmarksTop/...BottomHost ne sont que des conteneurs
        // de puces desormais - l'habillage propre a chaque position
        // (capsule de verre en haut, simple ligne flottante en bas) vit sur
        // leur Grid englobante et doit suivre la meme visibilite.
        if (ReferenceEquals(target, IdentitySpineBookmarksTop))
        {
            IdentitySpineBookmarksTopCard.Visibility = Visibility.Visible;
        }
        else if (ReferenceEquals(target, IdentitySpineBookmarksBottomHost))
        {
            IdentitySpineBookmarksBottomRow.Visibility = Visibility.Visible;
        }
    }

    private void HideIdentitySpineBookmarks()
    {
        IdentitySpineBookmarksTop.Children.Clear();
        IdentitySpineBookmarksTopOtherHost.Children.Clear();
        IdentitySpineBookmarksTopCard.Visibility = Visibility.Collapsed;
        IdentitySpineBookmarksBottomHost.Children.Clear();
        IdentitySpineBookmarksBottomOtherHost.Children.Clear();
        IdentitySpineBookmarksBottomRow.Visibility = Visibility.Collapsed;
        IdentitySpineBookmarksRightHost.Children.Clear();
        IdentitySpineBookmarksRightHost.Visibility = Visibility.Collapsed;
        IdentitySpineFavoritesSection.Children.Clear();
        IdentitySpineFavoritesSection.Visibility = Visibility.Collapsed;
    }

    private Button StyleAsIdentitySpineBookmarkChip(Button button, bool compact)
    {
        button.Background = (Brush)RootShell.Resources["NovaTabPillInactiveBackgroundBrush"];
        button.BorderBrush = (Brush)RootShell.Resources["NovaTabPillInactiveBorderBrush"];
        button.CornerRadius = new CornerRadius(compact ? 10 : 12);
        button.Height = compact ? 30 : 32;
        button.MinHeight = compact ? 30 : 32;
        if (compact)
        {
            button.MaxWidth = 62;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        return button;
    }

    private Button CreateIdentitySpineBookmarksOverflowButton(int overflowCount)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = $"+{overflowCount}",
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            },
            Padding = new Thickness(10, 0, 10, 0)
        };
        button.Click += (_, _) => ShowPanel(BookmarksPanel, "Favoris");
        ApplyNovaControlAccessibility(button, $"{overflowCount} autres favoris");
        ToolTipService.SetToolTip(button, "Voir tous les favoris");
        return button;
    }

    // Meme repere visuel que le separateur devant "Autres favoris" dans
    // BookmarksBarRow (Classique, MainWindow.xaml) - adapte a l'orientation
    // de la pile hote (verticale en position gauche/droite de la colonne,
    // horizontale en haut/bas).
    private Border CreateIdentitySpineBookmarksDivider(bool compact) => compact
        ? new Border
        {
            Height = 1,
            Margin = new Thickness(4, 4, 4, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = (Brush)RootShell.Resources["NovaChromeStrokeBrush"]
        }
        : new Border
        {
            Width = 1,
            Height = 18,
            Margin = new Thickness(2, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = (Brush)RootShell.Resources["NovaChromeStrokeBrush"]
        };

    // ── Ecran natif "Compagnon du mode" (home hero) ──────────────────────────
    // Recouvre BrowserHost uniquement quand la colonne identitaire est active
    // ET que l'onglet actif est la page d'accueil - la page HTML continue de
    // se charger dessous, inchangee. Contenu entierement derive de
    // GetCurrentModeCompanion()/CompanionMemory (MainWindow.UsageMode.cs) :
    // aucun texte de mode duplique ici.

    private void UpdateIdentitySpineHeroUi(string mode, string label, ModeCompanionDefinition companion)
    {
        IdentitySpineHeroEyebrowText.Text = "COMPAGNON";
        IdentitySpineHeroTitleText.Text = label;
        IdentitySpineHeroBodyText.Text = companion.Body;
        IdentitySpineHeroBodyText.FontSize = AccessibilityBodyFontSize();
        IdentitySpineHeroMemoryTitleText.Text = companion.MemoryTitle;
        IdentitySpineHeroObjectiveBox.PlaceholderText = companion.MemoryPlaceholder;
        IdentitySpineHeroObjectiveBox.Text = CompanionMemory(mode);
        IdentitySpineHeroPrimaryIcon.Glyph = companion.PrimaryIcon;
        IdentitySpineHeroPrimaryTitleText.Text = companion.PrimaryTitle;
        IdentitySpineHeroPrimaryHintText.Text = companion.PrimaryHint;
        IdentitySpineHeroPrimaryHintText.FontSize = AccessibilitySecondaryFontSize();
        IdentitySpineHeroSecondaryIcon.Glyph = companion.SecondaryIcon;
        IdentitySpineHeroSecondaryTitleText.Text = companion.SecondaryTitle;
        IdentitySpineHeroSecondaryHintText.Text = companion.SecondaryHint;
        IdentitySpineHeroSecondaryHintText.FontSize = AccessibilitySecondaryFontSize();

        IdentitySpineHeroSearchBox.Text = string.Empty;

        ApplyNovaControlAccessibility(IdentitySpineHeroObjectiveBox, "Objectif du compagnon");
        ApplyNovaControlAccessibility(IdentitySpineHeroSaveButton, "Garder l'objectif dans Lumie");
        ApplyNovaControlAccessibility(IdentitySpineHeroSearchBox, "Rechercher ou saisir une URL");
        ApplyNovaControlAccessibility(IdentitySpineHeroPrimaryButton, companion.PrimaryTitle);
        ApplyNovaControlAccessibility(IdentitySpineHeroSecondaryButton, companion.SecondaryTitle);
    }

    // Deux call-sites precis (ActivateTab, UpdateTab) - la ou l'adresse de
    // l'onglet actif est deja comparee a "lumora://accueil" aujourd'hui,
    // plutot qu'une accroche generique sur toute navigation.
    private void UpdateIdentitySpineHomeHeroVisibility(BrowserTabState? tab)
    {
        var isHome = tab is not null && tab.Address.Equals("lumora://accueil", StringComparison.OrdinalIgnoreCase);
        // Retour utilisateur (2026-08-08) : le mode Neutre doit rester
        // "vraiment neutre" meme en Style Lumora - logo + recherche, comme
        // l'accueil Classique, sans le Compagnon. Les autres modes (Focus,
        // Lecture...) gardent le Compagnon, seul Neutre en est exempte.
        var isNeutralMode = string.Equals(_uiSettings.UsageMode, "neutral", StringComparison.OrdinalIgnoreCase);
        IdentitySpineHomeHero.Visibility = _chromeLayoutStyle == "identitySpine" && isHome && !isNeutralMode
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void IdentitySpineHeroSaveButton_Click(object sender, RoutedEventArgs e) =>
        SaveCompanionMemoryAndNotify(IdentitySpineHeroObjectiveBox.Text);

    // Meme logique de navigation que la vraie barre d'adresse
    // (AddressBox_KeyDown/NavigateFromAddressBox, MainWindow.Navigation.cs) :
    // on y relaie le texte plutot que de dupliquer la normalisation
    // d'adresse. Vide apres soumission - ce champ est un point d'entree, pas
    // un miroir permanent de l'adresse courante (contrairement a AddressBox).
    private void IdentitySpineHeroSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        var text = IdentitySpineHeroSearchBox.Text?.Trim() ?? string.Empty;
        if (text.Length == 0) return;

        AddressBox.Text = text;
        NavigateFromAddressBox();
        IdentitySpineHeroSearchBox.Text = string.Empty;
        e.Handled = true;
    }

    private async void IdentitySpineHeroPrimaryButton_Click(object sender, RoutedEventArgs e) =>
        await RunModeCompanionActionAsync(GetCurrentModeCompanion().PrimaryAction);

    private async void IdentitySpineHeroSecondaryButton_Click(object sender, RoutedEventArgs e) =>
        await RunModeCompanionActionAsync(GetCurrentModeCompanion().SecondaryAction);

    // ── Couleur personnalisee par mode d'usage ───────────────────────────────

    private string GetModeAccentColorHex(string mode) => mode switch
    {
        "neutral" => _uiSettings.ModeAccentColorNeutral,
        "focus" => _uiSettings.ModeAccentColorFocus,
        "reading" => _uiSettings.ModeAccentColorReading,
        "creative" => _uiSettings.ModeAccentColorCreative,
        "research" => _uiSettings.ModeAccentColorResearch,
        "night" => _uiSettings.ModeAccentColorNight,
        _ => string.Empty
    };

    private void SetModeAccentColorHex(string mode, string hex)
    {
        switch (mode)
        {
            case "neutral": _uiSettings.ModeAccentColorNeutral = hex; break;
            case "focus": _uiSettings.ModeAccentColorFocus = hex; break;
            case "reading": _uiSettings.ModeAccentColorReading = hex; break;
            case "creative": _uiSettings.ModeAccentColorCreative = hex; break;
            case "research": _uiSettings.ModeAccentColorResearch = hex; break;
            case "night": _uiSettings.ModeAccentColorNight = hex; break;
        }
    }

    private ColorPicker? ModeAccentColorPicker(string mode) => mode switch
    {
        "neutral" => ModeColorPickerNeutral,
        "focus" => ModeColorPickerFocus,
        "reading" => ModeColorPickerReading,
        "creative" => ModeColorPickerCreative,
        "research" => ModeColorPickerResearch,
        "night" => ModeColorPickerNight,
        _ => null
    };

    private Button? ModeAccentColorSwatchButton(string mode) => mode switch
    {
        "neutral" => ModeColorSwatchNeutralButton,
        "focus" => ModeColorSwatchFocusButton,
        "reading" => ModeColorSwatchReadingButton,
        "creative" => ModeColorSwatchCreativeButton,
        "research" => ModeColorSwatchResearchButton,
        "night" => ModeColorSwatchNightButton,
        _ => null
    };

    private Windows.UI.Color ResolveDefaultModeAccentColor(string mode) =>
        ResolveModeChromePalette(mode, LumoraTheme.ResolveIsDarkTheme(_uiSettings), 255).Accent;

    // Initialise chaque ColorPicker/pastille depuis _uiSettings au moment ou
    // les Reglages sont (re)affiches - meme moment que les autres
    // SelectComboByTag(...) de ApplyUiSettings(), sous _suppressUiSettingsSave.
    private void InitializeModeAccentColorPickers()
    {
        _pendingModeAccentColors.Clear();

        foreach (var mode in ModeAccentColorKeys)
        {
            var hex = GetModeAccentColorHex(mode);
            var color = !string.IsNullOrWhiteSpace(hex) && TryParseHexColor(hex, out var custom)
                ? custom
                : ResolveDefaultModeAccentColor(mode);

            var picker = ModeAccentColorPicker(mode);
            if (picker is not null)
            {
                picker.Color = color;
            }

            var swatch = ModeAccentColorSwatchButton(mode);
            if (swatch is not null)
            {
                swatch.Background = new SolidColorBrush(color);
            }
        }
    }

    private void ModeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_suppressUiSettingsSave || sender.Tag is not string mode)
        {
            return;
        }

        _pendingModeAccentColors[mode] = ToHexColor(args.NewColor);
        var swatch = ModeAccentColorSwatchButton(mode);
        if (swatch is not null)
        {
            swatch.Background = new SolidColorBrush(args.NewColor);
        }

        MarkAppearanceOptionsPending();
    }

    private void ModeColorResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string mode })
        {
            return;
        }

        _pendingModeAccentColors[mode] = string.Empty;
        var defaultColor = ResolveDefaultModeAccentColor(mode);

        var picker = ModeAccentColorPicker(mode);
        if (picker is not null)
        {
            picker.Color = defaultColor;
        }

        var swatch = ModeAccentColorSwatchButton(mode);
        if (swatch is not null)
        {
            swatch.Background = new SolidColorBrush(defaultColor);
        }

        MarkAppearanceOptionsPending();
    }

    // Appele depuis ApplySettingsChangesButton_Click, comme
    // ApplyPendingAvatarChange()/ApplyPendingWallpaperChange() : les couleurs
    // choisies ne sont commises dans _uiSettings (et sauvegardees) qu'a la
    // validation explicite, jamais en direct pendant le glisser du picker
    // (ColorChanged se declenche a tres haute frequence - un re-theme complet
    // a chaque tick serait couteux, en plus de rompre la coherence "rien ne
    // change avant Appliquer" du reste de cette section Personnalisation).
    private bool ApplyPendingModeAccentColorChanges()
    {
        if (_pendingModeAccentColors.Count == 0)
        {
            return false;
        }

        foreach (var (mode, hex) in _pendingModeAccentColors)
        {
            SetModeAccentColorHex(mode, hex);
        }

        _pendingModeAccentColors.Clear();
        _uiSettings.Save(_profile.UiSettingsFile);
        return true;
    }

    private void ResetPendingModeAccentColorChanges() => _pendingModeAccentColors.Clear();

    // ── Derivation automatique du degrade (couleur unique choisie par mode) ─

    // Point d'injection appele depuis ApplyUsageModeChrome (MainWindow.SettingsTheme.cs),
    // STRICTEMENT apres son court-circuit "if (highContrast) { ...; return; }" :
    // le contraste eleve continue donc d'ecraser toute couleur personnalisee
    // exactement comme avant cette fonctionnalite.
    private static ModeChromePalette ApplyCustomModeAccent(ModeChromePalette basePalette, Windows.UI.Color accent, bool isDark)
    {
        var (cool, warm) = DeriveModeAccentTones(accent, isDark);
        return basePalette with
        {
            Accent = accent,
            AccentSoft = WithAlpha(accent, isDark ? (byte)32 : (byte)38),
            CoolAccent = cool,
            CoolAccentSoft = WithAlpha(cool, isDark ? (byte)28 : (byte)30),
            WarmAccent = warm,
            Focus = DeriveFocusTone(accent, isDark)
        };
    }

    private static (Windows.UI.Color Cool, Windows.UI.Color Warm) DeriveModeAccentTones(Windows.UI.Color accent, bool isDark)
    {
        RgbToHsl(accent, out var h, out var s, out var l);
        var cool = HslToRgb(h - 32, s, Math.Clamp(l + (isDark ? 0.12 : -0.08), 0, 1));
        var warm = HslToRgb(h + 32, Math.Clamp(s + 0.05, 0, 1), Math.Clamp(l + (isDark ? -0.05 : 0.05), 0, 1));
        return (cool, warm);
    }

    private static Windows.UI.Color DeriveFocusTone(Windows.UI.Color accent, bool isDark)
    {
        RgbToHsl(accent, out var h, out var s, out _);
        return HslToRgb(h, Math.Clamp(s * 0.65, 0, 1), isDark ? 0.86 : 0.30);
    }

    private static void RgbToHsl(Windows.UI.Color color, out double h, out double s, out double l)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;

        if (max - min < 0.0001)
        {
            h = 0;
            s = 0;
            return;
        }

        var d = max - min;
        s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

        if (max == r)
            h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g)
            h = (b - r) / d + 2;
        else
            h = (r - g) / d + 4;

        h *= 60;
    }

    private static Windows.UI.Color HslToRgb(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);

        if (s <= 0.0001)
        {
            var gray = (byte)Math.Round(l * 255);
            return Windows.UI.Color.FromArgb(255, gray, gray, gray);
        }

        static double Channel(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        var hk = h / 360.0;

        return Windows.UI.Color.FromArgb(255,
            (byte)Math.Round(Math.Clamp(Channel(p, q, hk + 1.0 / 3), 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(Channel(p, q, hk), 0, 1) * 255),
            (byte)Math.Round(Math.Clamp(Channel(p, q, hk - 1.0 / 3), 0, 1) * 255));
    }

    private static bool TryParseHexColor(string? hex, out Windows.UI.Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var value = hex.Trim().TrimStart('#');
        if (value.Length != 6
            || !byte.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(value[2..4], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(value[4..6], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = Windows.UI.Color.FromArgb(255, r, g, b);
        return true;
    }

    private static string ToHexColor(Windows.UI.Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
