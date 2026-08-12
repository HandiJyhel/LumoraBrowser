using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.System;
using Windows.ApplicationModel.DataTransfer;
using Lumora.Privacy.HttpsEnforcer;
using Lumora.WinUI.Credentials;

namespace Lumora.WinUI;

// Rendu des onglets verticaux, groupes d'onglets, glisser-deposer et menus
// contextuels associes. Extrait de MainWindow.Navigation.cs (god file) :
// possede en propre _tabGroups/_collapsedGroupIds/_savedGroupIds, aucun
// changement de comportement, uniquement un deplacement de code au sein de
// la meme classe partielle.
public sealed partial class MainWindow
{
    // Palette de couleurs de groupe : reprend les teintes de l'identité visuelle
    // (lumiere/web) complétées par des teintes distinguables.
    private static readonly Windows.UI.Color[] TabGroupPalette =
    {
        UiColor(255, 185, 53),
        UiColor(67, 219, 209),
        UiColor(94, 156, 235),
        UiColor(219, 112, 147),
        UiColor(154, 140, 226),
        UiColor(255, 127, 53)
    };

    private static Windows.UI.Color TabGroupColor(TabGroup group) =>
        TabGroupPalette[((group.ColorIndex % TabGroupPalette.Length) + TabGroupPalette.Length) % TabGroupPalette.Length];

    private void RenderVerticalTabs()
    {
        if (!_verticalTabsEnabled)
        {
            VerticalTabsPanelItems.Children.Clear();
            return;
        }

        VerticalTabsPanelItems.Children.Clear();
        var current = CurrentTab();
        int? lastGroupId = null;
        // Sert a determiner si un onglet peut monter/descendre (fleches ci-dessous) :
        // meme section que MoveTab (epingle/non epingle), non epingle uniquement ici
        // puisque les onglets epingles restent toujours en icone seule (pas de place
        // pour des fleches).
        var unpinnedOrder = _tabs.Where(t => !t.Pinned).ToList();

        // Recherche d'onglet (2026-08-12) : filtre titre/adresse, insensible a la
        // casse. Verifie AVANT le bloc d'en-tete de groupe (juste apres) pour
        // qu'un groupe sans aucun onglet visible n'affiche pas son en-tete non
        // plus - meme esprit que le filtre du Menu Demarrer (rien affiche =
        // rien annonce).
        var filter = _verticalTabsFilter?.Trim();
        var hasFilter = !string.IsNullOrEmpty(filter);

        foreach (var tab in _tabs)
        {
            if (hasFilter &&
                !(tab.Title?.Contains(filter!, StringComparison.OrdinalIgnoreCase) == true) &&
                !(tab.Address?.Contains(filter!, StringComparison.OrdinalIgnoreCase) == true))
            {
                continue;
            }

            if (tab.GroupId != lastGroupId && tab.GroupId is int headerGroupId)
            {
                var headerGroup = _tabGroups.FirstOrDefault(g => g.Id == headerGroupId);
                if (headerGroup is not null)
                {
                    VerticalTabsPanelItems.Children.Add(GroupHeaderElement(headerGroup));
                }
            }
            lastGroupId = tab.GroupId;

            if (tab.GroupId is int collapsedGroupId && _collapsedGroupIds.Contains(collapsedGroupId))
            {
                continue;
            }

            var isActive = current?.Id == tab.Id || IsTabInSplitView(tab.Id);
            Button button;
            FrameworkElement content;

            if (_verticalTabsCompact || tab.Pinned)
            {
                // Bug reel signale par l'utilisateur (2026-08-07) : en rail
                // compact (icone seule), aucun moyen visible de fermer un
                // onglet - le bouton de fermeture n'existait que dans la
                // ligne "etendue" ci-dessous. Fermer restait possible par
                // clic droit > "Fermer l'onglet" (CreateTabContextFlyout,
                // pas retire) ou Ctrl+W, mais sans affordance visible c'est
                // comme si ca n'existait pas pour la plupart des gens.
                // Premiere version (reveleee au survol) jugee insuffisante -
                // aucun indice visible au repos. Deuxieme version (pastille
                // 16x16 dans le coin) jugee trop serree/petite par
                // l'utilisateur (2026-08-08), avec Google Chrome cite comme
                // reference (croix qui prend toute la tuile plutot qu'un
                // badge d'angle) - accepte pour la TAILLE de cible, mais
                // sans reprendre le "hover uniquement" de Chrome (deja
                // rejete une fois pour manque de decouvrabilite). Compromis
                // valide via maquette HTML avant implementation : croix
                // large et centree, TOUJOURS visible a opacite reduite,
                // pleine opacite au survol - meme reglage 0.6/1 que la
                // version precedente (deja prouve suffisant), juste une
                // cible bien plus grande. Une fine marge subsiste autour du
                // bouton de fermeture (tuile 44x38, bouton 24x24 centre) :
                // le bouton englobant reste cliquable pour selectionner
                // l'onglet, seul le glyphe de fermeture capture le clic sur
                // sa propre zone (meme pattern bouton-dans-bouton que la
                // ligne etendue ci-dessous). Redescendu de 30x30 a 24x24
                // (2026-08-08, meme jour) : jugee trop imposante en usage
                // reel une fois testee, comparee a la taille des boutons
                // Chrome/Edge - toujours nettement plus grande que les 20x20
                // d'origine juges trop serres, mais moins "pavee".
                // Tuile et bouton de fermeture reduits une 2e fois (round 3,
                // 2026-08-12) : retour utilisateur avec capture d'ecran d'Edge
                // a l'appui - meme la tuile 44x38 restait beaucoup plus grande
                // que des onglets reels avec ~15 onglets par session, ou
                // chaque tuile n'est guere plus large que le favicon lui-meme,
                // sans marge visible. Toutes les tailles ci-dessous reduites
                // proportionnellement (~30%) plutot que redevinees a part.
                var icon = TabIconElement(tab, 14);

                var compactClose = new Button
                {
                    Width = 18,
                    Height = 18,
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = tab.Id,
                    CornerRadius = new CornerRadius(9),
                    BorderThickness = new Thickness(0),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    // Viewbox plutot que SymbolIcon.FontSize (n'existe pas sur ce
                    // controle) : force le glyphe a une taille lisible (8x8)
                    // dans le bouton de fermeture.
                    Content = new Viewbox
                    {
                        Width = 8,
                        Height = 8,
                        Child = new SymbolIcon(Symbol.Cancel)
                    },
                    Opacity = 0.6
                };
                ToolTipService.SetToolTip(compactClose, "Fermer l'onglet");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(compactClose, $"Fermer {tab.Title}");
                compactClose.Click += VerticalTabCloseButton_Click;

                var compactContent = new Grid();
                compactContent.Children.Add(icon);
                compactContent.Children.Add(compactClose);
                content = compactContent;

                button = new Button
                {
                    Width = 30,
                    Height = 28,
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = tab.Id,
                    Margin = new Thickness(0, 1, 0, 1),
                    CornerRadius = new CornerRadius(9),
                    // Bordure retiree en compact (round 3) : l'indicatif d'onglet
                    // actif ne repose plus que sur le fond teinte (comme la
                    // capture Edge de reference - simple surbrillance discrete,
                    // pas de cadre marque), pour rester coherent avec la tuile
                    // resserree. Le contour de selection multiple ci-dessous
                    // (Ctrl/Shift+clic) reste prioritaire et se pose par-dessus.
                    BorderThickness = new Thickness(0),
                    Background = (Brush)RootShell.Resources[isActive ? "NovaTabPillActiveBackgroundBrush" : "NovaTabPillInactiveBackgroundBrush"],
                    BorderBrush = (Brush)RootShell.Resources[isActive ? "NovaTabPillActiveBorderBrush" : "NovaTabPillInactiveBorderBrush"]
                };
                button.PointerEntered += (_, _) => { compactClose.Opacity = 1; icon.Opacity = 0.3; };
                button.PointerExited += (_, _) => { compactClose.Opacity = 0.6; icon.Opacity = 1; };
            }
            else
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                row.Children.Add(TabIconElement(tab, 16));
                row.Children.Add(new TextBlock
                {
                    Text = tab.Title,
                    MaxWidth = 118,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var grid = new Grid { ColumnSpacing = 6 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(row, 0);
                grid.Children.Add(row);

                // Fleches monter/descendre (0.93.4.0) : avant cette version, reordonner
                // un onglet du rail n'etait possible qu'en glisser-deposer ou via le
                // menu contextuel - aucun bouton visible. IsEnabled desactive les
                // extremites plutot que de les masquer (position stable au survol).
                var positionInSection = unpinnedOrder.IndexOf(tab);
                var canMoveUp = positionInSection > 0;
                var canMoveDown = positionInSection >= 0 && positionInSection < unpinnedOrder.Count - 1;
                var moveStack = new StackPanel { Width = 16, Height = 26, VerticalAlignment = VerticalAlignment.Center };
                var moveUp = new Button
                {
                    Width = 16,
                    Height = 13,
                    MinWidth = 16,
                    Padding = new Thickness(0),
                    Tag = tab.Id,
                    Content = new FontIcon { Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 8 },
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    IsEnabled = canMoveUp
                };
                ToolTipService.SetToolTip(moveUp, "Monter l'onglet");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(moveUp, $"Monter {tab.Title}");
                moveUp.Click += VerticalTabMoveUpButton_Click;
                moveStack.Children.Add(moveUp);
                var moveDown = new Button
                {
                    Width = 16,
                    Height = 13,
                    MinWidth = 16,
                    Padding = new Thickness(0),
                    Tag = tab.Id,
                    Content = new FontIcon { Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 8 },
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    IsEnabled = canMoveDown
                };
                ToolTipService.SetToolTip(moveDown, "Descendre l'onglet");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(moveDown, $"Descendre {tab.Title}");
                moveDown.Click += VerticalTabMoveDownButton_Click;
                moveStack.Children.Add(moveDown);
                Grid.SetColumn(moveStack, 1);
                grid.Children.Add(moveStack);

                var close = new Button
                {
                    Width = 26,
                    Height = 26,
                    MinWidth = 26,
                    Padding = new Thickness(0),
                    Tag = tab.Id,
                    Content = new SymbolIcon(Symbol.Cancel),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0)
                };
                ToolTipService.SetToolTip(close, "Fermer l'onglet");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(close, $"Fermer {tab.Title}");
                close.Click += VerticalTabCloseButton_Click;
                Grid.SetColumn(close, 2);
                grid.Children.Add(close);
                content = grid;
                button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(10, 7, 10, 7),
                    Tag = tab.Id,
                    Margin = new Thickness(0, 2, 0, 2),
                    CornerRadius = new CornerRadius(16),
                    BorderThickness = new Thickness(1),
                    Background = (Brush)RootShell.Resources[isActive ? "NovaTabPillActiveBackgroundBrush" : "NovaTabPillInactiveBackgroundBrush"],
                    BorderBrush = (Brush)RootShell.Resources[isActive ? "NovaTabPillActiveBorderBrush" : "NovaTabPillInactiveBorderBrush"]
                };
            }

            if (tab.GroupId is int tabGroupId && _tabGroups.FirstOrDefault(g => g.Id == tabGroupId) is { } tabGroup)
            {
                var wrapper = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                wrapper.Children.Add(new Border
                {
                    Width = 3,
                    CornerRadius = new CornerRadius(1.5),
                    Background = new SolidColorBrush(TabGroupColor(tabGroup)),
                    VerticalAlignment = VerticalAlignment.Stretch
                });
                wrapper.Children.Add(content);
                button.Content = wrapper;
            }
            else
            {
                button.Content = content;
            }

            button.Foreground = new SolidColorBrush(isActive
                ? UiColor(242, 245, 250)
                : UiColor(214, 221, 229));

            // Bordure accentuée : marque la sélection multiple (Ctrl/Shift+clic),
            // distincte du fond "actif" déjà posé ci-dessus.
            if (_selectedTabIds.Contains(tab.Id))
            {
                button.BorderBrush = (Brush)RootShell.Resources["NovaAccentBrush"];
                button.BorderThickness = new Thickness(2);
            }

            ToolTipService.SetToolTip(button, tab.Title);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tab.Title);
            button.Click += VerticalTabButton_Click;
            button.ContextFlyout = CreateTabContextFlyout(tab);
            button.CanDrag = true;
            button.AllowDrop = true;
            button.DragStarting += VerticalTabButton_DragStarting;
            button.DragOver += VerticalTabButton_DragOver;
            button.Drop += VerticalTabButton_Drop;
            VerticalTabsPanelItems.Children.Add(button);
        }
    }

    private FrameworkElement GroupHeaderElement(TabGroup group)
    {
        var collapsed = _collapsedGroupIds.Contains(group.Id);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new FontIcon
        {
            Glyph = collapsed ? "\uE76C" : "\uE70D",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 10
        });
        row.Children.Add(new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(TabGroupColor(group)),
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(new TextBlock
        {
            Text = group.Name,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            MaxWidth = 130,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });

        var header = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 8, 0, 2),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(UiColor(19, 27, 39, 156)),
            BorderBrush = new SolidColorBrush(UiColor(61, 71, 88, 116)),
            Content = row,
            Tag = group.Id
        };
        header.Click += (_, _) => ToggleGroupCollapsed(group.Id);
        header.ContextFlyout = CreateGroupHeaderFlyout(group);
        ToolTipService.SetToolTip(header, group.Name);
        return header;
    }

    // Le flyout est reconstruit a chaque ouverture (Opening) plutot que fige a la
    // creation de l'onglet : sinon le menu horizontal (cree une seule fois dans
    // AddTab) affiche un etat perime des lors que l'epinglage, les groupes ou la
    // selection multiple changent apres coup. Le rail vertical reconstruit deja
    // tout a chaque RenderVerticalTabs, donc ce cout supplementaire n'y change rien.
    private MenuFlyout CreateTabContextFlyout(BrowserTabState tab)
    {
        var flyout = new MenuFlyout();
        flyout.Opening += (sender, _) => PopulateTabContextFlyout((MenuFlyout)sender!, tab);
        HookFlyoutPointerSupport(flyout);
        return flyout;
    }

    private void PopulateTabContextFlyout(MenuFlyout flyout, BrowserTabState tab)
    {
        flyout.Items.Clear();

        var selection = _selectedTabIds.Contains(tab.Id) && _selectedTabIds.Count > 1
            ? _tabs.Where(t => _selectedTabIds.Contains(t.Id)).ToList()
            : null;
        if (selection is not null)
        {
            PopulateSelectionContextFlyout(flyout, selection);
            return;
        }

        AddLumoraMenuHeader(
            flyout.Items,
            tab.Title,
            $"{(tab.Pinned ? "épinglé" : "onglet")} • {TabHeaderHost(tab.Address)}",
            tab.Pinned ? "\uE718" : "\uE8AB");

        // Case a cocher en tete de menu : seul point d'entree decouvrable de la
        // selection multiple avant cette version (Ctrl/Shift+clic n'etait suggere
        // nulle part dans l'UI). Cocher ici revient exactement a faire Ctrl+clic
        // sur cet onglet - meme bascule, meme _selectionAnchorTabId.
        var selectItem = new ToggleMenuFlyoutItem
        {
            Text = "Sélectionner",
            Tag = tab,
            IsChecked = _selectedTabIds.Contains(tab.Id)
        };
        selectItem.Click += TabContextToggleSelection_Click;
        flyout.Items.Add(selectItem);

        // Actions les plus frequentes a plat, en tete : pas de sous-menu pour ce
        // qui sert tous les jours (demande explicite - un navigateur simple doit
        // rendre la gestion d'onglets simple, meme si des sous-menus subsistent
        // ailleurs pour ce qui a une vraie raison d'en etre un).
        var pinItem = new MenuFlyoutItem { Text = tab.Pinned ? "Désépingler l'onglet" : "Épingler l'onglet", Tag = tab };
        pinItem.Click += TabContextTogglePin_Click;
        flyout.Items.Add(pinItem);

        var duplicateItem = new MenuFlyoutItem { Text = "Dupliquer l'onglet", Tag = tab };
        duplicateItem.Click += DuplicateTab_Click;
        flyout.Items.Add(duplicateItem);

        var copyAddressItem = new MenuFlyoutItem { Text = "Copier l'adresse", Tag = tab };
        copyAddressItem.Click += CopyTabAddress_Click;
        flyout.Items.Add(copyAddressItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var closeItem = new MenuFlyoutItem { Text = "Fermer l'onglet", Tag = tab };
        closeItem.Click += TabContextClose_Click;
        flyout.Items.Add(closeItem);

        // "Fermer les autres/a droite" epargnent toujours les onglets epingles,
        // comme leur fermeture individuelle (bouton grise) - coherent avec le
        // reste du menu plutot qu'une exception surprenante.
        var otherClosableCount = _tabs.Count(t => t.Id != tab.Id && !t.Pinned);
        var closeOthersItem = new MenuFlyoutItem { Text = "Fermer les autres onglets", Tag = tab, IsEnabled = otherClosableCount > 0 };
        closeOthersItem.Click += CloseOtherTabs_Click;
        flyout.Items.Add(closeOthersItem);

        var tabIndex = _tabs.IndexOf(tab);
        var hasClosableTabsToRight = tabIndex >= 0 && _tabs.Skip(tabIndex + 1).Any(t => !t.Pinned);
        var closeToRightItem = new MenuFlyoutItem { Text = "Fermer les onglets à droite", Tag = tab, IsEnabled = hasClosableTabsToRight };
        closeToRightItem.Click += CloseTabsToRight_Click;
        flyout.Items.Add(closeToRightItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        // Alternative clavier/menu au glisser-deposer du rail d'onglets
        // vertical, qui n'a aucun equivalent clavier (audit accessibilite
        // moteur/motricite, palier 0.93.x) - un tremblement ou un dispositif
        // switch ne peut pas faire un glisser-deposer precis.
        var sectionTabs = _tabs.Where(t => t.Pinned == tab.Pinned).ToList();
        var positionInSection = sectionTabs.IndexOf(tab);
        var moveUpItem = new MenuFlyoutItem { Text = "Monter", Tag = tab, IsEnabled = positionInSection > 0 };
        moveUpItem.Click += (_, _) => MoveTab(tab, -1);
        flyout.Items.Add(moveUpItem);
        var moveDownItem = new MenuFlyoutItem
        {
            Text = "Descendre", Tag = tab, IsEnabled = positionInSection >= 0 && positionInSection < sectionTabs.Count - 1
        };
        moveDownItem.Click += (_, _) => MoveTab(tab, +1);
        flyout.Items.Add(moveDownItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        if (IsTabInSplitView(tab.Id))
        {
            var restoreItem = new MenuFlyoutItem { Text = "Rétablir la vue simple", Tag = tab };
            restoreItem.Click += RestoreSingleView_Click;
            flyout.Items.Add(restoreItem);
            flyout.Items.Add(new MenuFlyoutSeparator());
        }
        else if (_tabs.Count > 1)
        {
            var splitSubItem = new MenuFlyoutSubItem { Text = "Diviser l'écran avec..." };
            foreach (var other in _tabs.Where(t => t.Id != tab.Id))
            {
                var otherItem = new MenuFlyoutItem { Text = other.Title, Tag = (tab, other) };
                otherItem.Click += SplitViewWith_Click;
                splitSubItem.Items.Add(otherItem);
            }
            flyout.Items.Add(splitSubItem);
            flyout.Items.Add(new MenuFlyoutSeparator());
        }

        var addToGroup = new MenuFlyoutSubItem { Text = "Ajouter au groupe" };
        foreach (var group in _tabGroups)
        {
            var groupItem = new MenuFlyoutItem { Text = group.Name, Tag = (tab, group) };
            groupItem.Click += AddTabToGroup_Click;
            addToGroup.Items.Add(groupItem);
        }
        if (_tabGroups.Count > 0)
        {
            addToGroup.Items.Add(new MenuFlyoutSeparator());
        }
        var newGroupItem = new MenuFlyoutItem { Text = "Nouveau groupe...", Tag = tab };
        newGroupItem.Click += CreateGroupWithTab_Click;
        addToGroup.Items.Add(newGroupItem);
        flyout.Items.Add(addToGroup);

        if (tab.GroupId is not null)
        {
            var removeItem = new MenuFlyoutItem { Text = "Retirer du groupe", Tag = tab };
            removeItem.Click += RemoveTabFromGroup_Click;
            flyout.Items.Add(removeItem);
        }
    }

    // Menu affiche quand l'onglet clic-droit fait partie d'une selection multiple
    // (Ctrl/Shift+clic sur au moins 2 onglets) : actions groupees plutot que le
    // menu par-onglet habituel.
    private void PopulateSelectionContextFlyout(MenuFlyout flyout, List<BrowserTabState> selection)
    {
        AddLumoraMenuHeader(
            flyout.Items,
            $"{selection.Count} onglets selectionnes",
            "actions groupees",
            "");

        var groupSubItem = new MenuFlyoutSubItem { Text = "Regrouper la sélection" };
        foreach (var group in _tabGroups)
        {
            var addItem = new MenuFlyoutItem { Text = group.Name, Tag = (selection, group) };
            addItem.Click += AddSelectionToGroup_Click;
            groupSubItem.Items.Add(addItem);
        }
        if (_tabGroups.Count > 0)
        {
            groupSubItem.Items.Add(new MenuFlyoutSeparator());
        }
        var newGroupItem = new MenuFlyoutItem { Text = "Nouveau groupe...", Tag = selection };
        newGroupItem.Click += CreateGroupWithSelection_Click;
        groupSubItem.Items.Add(newGroupItem);
        flyout.Items.Add(groupSubItem);

        if (selection.Count == 2)
        {
            var splitItem = new MenuFlyoutItem { Text = "Diviser l'écran avec ces 2 onglets", Tag = selection };
            splitItem.Click += SplitViewWithSelection_Click;
            flyout.Items.Add(splitItem);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        var closeItem = new MenuFlyoutItem { Text = $"Fermer les {selection.Count} onglets sélectionnés", Tag = selection };
        closeItem.Click += CloseSelection_Click;
        flyout.Items.Add(closeItem);

        flyout.Items.Add(new MenuFlyoutSeparator());
        var clearItem = new MenuFlyoutItem { Text = "Annuler la sélection" };
        clearItem.Click += (_, _) => ClearTabSelection();
        flyout.Items.Add(clearItem);
    }

    private void DuplicateTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            DuplicateTab(tab);
        }
    }

    private void DuplicateTab(BrowserTabState source)
    {
        var duplicate = AddTab(source.Title, source.Address, select: true, groupId: source.GroupId);
        PlaceTabAfter(duplicate, source);
    }

    // Insere un onglet juste apres un autre (duplication) : AddTab l'ajoute
    // toujours en fin de liste, ce petit deplacement le fait atterrir a cote de
    // son origine plutot qu'a l'autre bout de la barre.
    private void PlaceTabAfter(BrowserTabState tab, BrowserTabState afterTab)
    {
        _tabs.Remove(tab);
        var index = _tabs.IndexOf(afterTab);
        _tabs.Insert(index + 1, tab);
        RefreshTabViewOrder();
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void CopyTabAddress_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            CopyPlainTextToClipboard(tab.Address, "Adresse copiée.");
        }
    }

    // Copie neutre (contrairement a CopySecretToClipboard) : une adresse n'est
    // pas un secret, elle reste dans l'historique du presse-papiers comme un
    // copier-coller normal.
    private void CopyPlainTextToClipboard(string text, string status)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        StatusText.Text = status;
    }

    private void CloseOtherTabs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            return;
        }

        foreach (var other in _tabs.Where(t => t.Id != tab.Id && !t.Pinned).ToList())
        {
            CloseTab(other);
        }
    }

    private void CloseTabsToRight_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            return;
        }

        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        foreach (var other in _tabs.Skip(index + 1).Where(t => !t.Pinned).ToList())
        {
            CloseTab(other);
        }
    }

    private void AddSelectionToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: (List<BrowserTabState> selection, TabGroup group) })
        {
            return;
        }

        foreach (var tab in selection)
        {
            AssignTabToGroup(tab, group.Id);
        }
        ClearTabSelection();
    }

    private async void CreateGroupWithSelection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: List<BrowserTabState> selection })
        {
            return;
        }

        var name = await PromptTextAsync("Nouveau groupe", "Nom du groupe", "");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var group = new TabGroup(_nextGroupId++, name.Trim(), TabGroupOrdering.NextColorIndex(_tabGroups.Count));
        _tabGroups.Add(group);
        foreach (var tab in selection)
        {
            AssignTabToGroup(tab, group.Id);
        }
        ClearTabSelection();
    }

    private void CloseSelection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: List<BrowserTabState> selection })
        {
            return;
        }

        foreach (var tab in selection.ToList())
        {
            CloseTab(tab);
        }
        ClearTabSelection();
    }

    private MenuFlyout CreateGroupHeaderFlyout(TabGroup group)
    {
        var flyout = new MenuFlyout();
        var tabsInGroup = _tabs.Count(tab => tab.GroupId == group.Id);
        AddLumoraMenuHeader(
            flyout.Items,
            group.Name,
            $"{tabsInGroup} onglet{(tabsInGroup > 1 ? "s" : string.Empty)} dans ce groupe",
            "\uE8FD");

        var renameItem = new MenuFlyoutItem { Text = "Renommer le groupe", Tag = group };
        renameItem.Click += RenameGroup_Click;
        flyout.Items.Add(renameItem);

        var saveItem = new MenuFlyoutItem { Text = "Enregistrer le groupe", Tag = group };
        saveItem.Click += SaveGroupToLibrary_Click;
        flyout.Items.Add(saveItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var dissolveItem = new MenuFlyoutItem { Text = "Dissoudre le groupe", Tag = group };
        dissolveItem.Click += DissolveGroup_Click;
        flyout.Items.Add(dissolveItem);
        HookFlyoutPointerSupport(flyout);

        return flyout;
    }

    private void AddTabToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: (BrowserTabState tab, TabGroup group) })
        {
            AssignTabToGroup(tab, group.Id);
        }
    }

    private void TabContextClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            CloseTab(tab);
        }
    }

    private void TabContextTogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            TogglePinTab(tab);
        }
    }

    private void TabContextToggleSelection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem { Tag: BrowserTabState tab })
        {
            ToggleTabSelection(tab);
        }
    }

    // Epingler un onglet le compacte en icone seule et le regroupe au debut de la
    // liste (comme Chrome/Edge), sans toucher a l'ordre relatif des autres onglets.
    private void TogglePinTab(BrowserTabState tab)
    {
        tab.Pinned = !tab.Pinned;
        ReorderPinnedFirst();
        RefreshTabViewOrder();
        UpdateTabHeader(tab);
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void ReorderPinnedFirst()
    {
        var pinned = _tabs.Where(t => t.Pinned).ToList();
        var others = _tabs.Where(t => !t.Pinned).ToList();
        _tabs.Clear();
        _tabs.AddRange(pinned);
        _tabs.AddRange(others);
    }

    // Reconstruit l'ordre visuel de la barre horizontale pour qu'il corresponde a
    // l'ordre courant de _tabs (source de verite pour l'ordre des onglets).
    private void RefreshTabViewOrder()
    {
        _suppressTabOrderSync = true;
        try
        {
            var itemsByTag = BrowserTabs.TabItems
                .OfType<TabViewItem>()
                .Where(item => item.Tag is int)
                .ToDictionary(item => (int)item.Tag!);
            var selected = BrowserTabs.SelectedItem;

            BrowserTabs.TabItems.Clear();
            foreach (var tab in _tabs)
            {
                if (itemsByTag.TryGetValue(tab.Id, out var item))
                {
                    BrowserTabs.TabItems.Add(item);
                }
            }

            if (selected is not null)
            {
                BrowserTabs.SelectedItem = selected;
            }
        }
        finally
        {
            _suppressTabOrderSync = false;
        }
    }

    // Glisser-deposer d'onglets dans la barre horizontale (natif WinUI, TabItems est
    // observable) : on ne resynchronise _tabs qu'apres un vrai deplacement (Move),
    // pas apres un Add/Remove deja gere par AddTab/CloseTab.
    private void BrowserTabs_TabItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_suppressTabOrderSync || e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Move)
        {
            return;
        }

        var visualOrder = BrowserTabs.TabItems
            .OfType<TabViewItem>()
            .Where(item => item.Tag is int)
            .Select(item => (int)item.Tag!)
            .ToList();

        var byId = _tabs.ToDictionary(t => t.Id);
        var pinnedIds = visualOrder.Where(id => byId.TryGetValue(id, out var t) && t.Pinned).ToList();
        var otherIds = visualOrder.Where(id => !pinnedIds.Contains(id)).ToList();
        var finalOrder = pinnedIds.Concat(otherIds).ToList();

        _tabs.Clear();
        _tabs.AddRange(finalOrder.Select(id => byId[id]));

        // L'utilisateur a essaye de melanger epingles/non epingles : on corrige la
        // barre visuelle pour qu'elle respecte cet ordre force.
        if (!finalOrder.SequenceEqual(visualOrder))
        {
            RefreshTabViewOrder();
        }

        RenderVerticalTabs();
        SaveTabSession();
    }

    // Glisser-deposer d'onglets dans le rail vertical (implementation manuelle : le
    // rail est une pile de boutons construits a la main, pas un ItemsControl natif).
    private void VerticalTabButton_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is Button { Tag: int id })
        {
            args.Data.Properties.Add("novaTabId", id);
            args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }

    private void VerticalTabButton_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Properties.ContainsKey("novaTabId"))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }

    private void VerticalTabButton_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Button { Tag: int targetId } ||
            !e.DataView.Properties.TryGetValue("novaTabId", out var raw) ||
            raw is not int movingId ||
            movingId == targetId)
        {
            return;
        }

        // Glisser-deposer + Shift = diviser l'ecran entre les deux onglets, au lieu
        // du reordonnancement normal (0.93.4.0). Depose simple = comportement
        // inchange (reordonner) : le geste le plus frequent ne doit pas changer de
        // sens, la division est une variante explicite avec un modificateur.
        if (IsShiftKeyDown())
        {
            var moving = _tabs.FirstOrDefault(t => t.Id == movingId);
            var target = _tabs.FirstOrDefault(t => t.Id == targetId);
            if (moving is not null && target is not null && !IsTabInSplitView(moving.Id) && !IsTabInSplitView(target.Id))
            {
                _ = EnterSplitViewAsync(moving, target);
            }
            return;
        }

        ReorderTab(movingId, targetId);
    }

    // Un glisser-deposer ne peut pas melanger un onglet epingle et un onglet normal
    // (meme regle que pour la barre horizontale) : deposer hors de sa section n'a
    // simplement aucun effet plutot que de produire un ordre incoherent.
    private void ReorderTab(int movingId, int targetId)
    {
        var moving = _tabs.FirstOrDefault(t => t.Id == movingId);
        var target = _tabs.FirstOrDefault(t => t.Id == targetId);
        if (moving is null || target is null || moving.Pinned != target.Pinned)
        {
            return;
        }

        _tabs.Remove(moving);
        var targetIndex = _tabs.IndexOf(target);
        _tabs.Insert(targetIndex, moving);

        RefreshTabViewOrder();
        RenderVerticalTabs();
        SaveTabSession();
    }

    // Deplace un onglet d'un cran dans sa propre section (epingle/normal),
    // sans passer par un glisser-deposer - seul chemin clavier/menu pour
    // reordonner le rail d'onglets vertical.
    private void MoveTab(BrowserTabState tab, int direction)
    {
        var section = _tabs.Select((t, i) => (Tab: t, Index: i))
            .Where(x => x.Tab.Pinned == tab.Pinned)
            .ToList();
        var pos = section.FindIndex(x => x.Tab.Id == tab.Id);
        var neighborPos = pos + direction;
        if (pos < 0 || neighborPos < 0 || neighborPos >= section.Count)
        {
            return;
        }

        var neighbor = section[neighborPos].Tab;

        _tabs.Remove(tab);
        var neighborIndexAfterRemoval = _tabs.IndexOf(neighbor);
        var insertIndex = direction > 0 ? neighborIndexAfterRemoval + 1 : neighborIndexAfterRemoval;
        _tabs.Insert(insertIndex, tab);

        RefreshTabViewOrder();
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void VerticalTabCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int id } &&
            _tabs.FirstOrDefault(tab => tab.Id == id) is { } tab)
        {
            CloseTab(tab);
        }
    }

    private void VerticalTabMoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int id } &&
            _tabs.FirstOrDefault(tab => tab.Id == id) is { } tab)
        {
            MoveTab(tab, -1);
        }
    }

    private void VerticalTabMoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int id } &&
            _tabs.FirstOrDefault(tab => tab.Id == id) is { } tab)
        {
            MoveTab(tab, +1);
        }
    }

    private void RemoveTabFromGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            AssignTabToGroup(tab, null);
        }
    }

    private async void CreateGroupWithTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: BrowserTabState tab })
        {
            return;
        }

        var name = await PromptTextAsync("Nouveau groupe", "Nom du groupe", "");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var group = new TabGroup(_nextGroupId++, name.Trim(), TabGroupOrdering.NextColorIndex(_tabGroups.Count));
        _tabGroups.Add(group);
        AssignTabToGroup(tab, group.Id);
    }

    private async void RenameGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: TabGroup group })
        {
            return;
        }

        var name = await PromptTextAsync("Renommer le groupe", "Nom du groupe", group.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        group.Name = name.Trim();
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void DissolveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: TabGroup group })
        {
            return;
        }

        // Garde-fou : le groupement va etre perdu ; proposer de le ranger d'abord.
        OfferKeepGroupIfUnsaved(group);

        foreach (var tab in _tabs.Where(t => t.GroupId == group.Id))
        {
            tab.GroupId = null;
        }
        _tabGroups.Remove(group);
        _collapsedGroupIds.Remove(group.Id);
        _savedGroupIds.Remove(group.Id);
        RenderVerticalTabs();
        SaveTabSession();
    }

    private void ToggleGroupCollapsed(int groupId)
    {
        if (!_collapsedGroupIds.Remove(groupId))
        {
            _collapsedGroupIds.Add(groupId);
        }
        RenderVerticalTabs();
    }

    private void AssignTabToGroup(BrowserTabState tab, int? groupId)
    {
        var order = TabGroupOrdering.ReorderForGroup(
            _tabs.Select(t => t.Id).ToList(),
            tab.Id,
            groupId,
            _tabs.ToDictionary(t => t.Id, t => t.GroupId));

        var byId = _tabs.ToDictionary(t => t.Id);
        _tabs.Clear();
        _tabs.AddRange(order.Select(id => byId[id]));

        tab.GroupId = groupId;
        RenderVerticalTabs();
        SaveTabSession();
    }

    private FrameworkElement TabHeaderContent(BrowserTabState tab, bool compact, bool active) =>
        TabHeaderContent(tab, compact, active, selected: _selectedTabIds.Contains(tab.Id));

    // "selected" est la sélection multiple (Ctrl/Shift+clic, pour regrouper/fermer
    // plusieurs onglets d'un coup) : un marquage distinct de "active" (l'onglet
    // affiché), matérialisé par une bordure accentuée. Un onglet peut être actif
    // sans être sélectionné, et sélectionné sans être actif.
    private FrameworkElement TabHeaderContent(BrowserTabState tab, bool compact, bool active, bool selected)
    {
        var selectionBrush = selected ? (Brush)RootShell.Resources["NovaAccentBrush"] : null;

        if (compact)
        {
            var compactWrap = new Grid
            {
                Width = 30,
                Height = 30
            };
            compactWrap.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = (Brush)RootShell.Resources[active ? "NovaTabPillActiveBackgroundBrush" : "NovaTabPillInactiveBackgroundBrush"],
                BorderBrush = selectionBrush ?? (Brush)RootShell.Resources[active ? "NovaTabPillActiveBorderBrush" : "NovaTabPillInactiveBorderBrush"],
                BorderThickness = new Thickness(selected ? 2 : 1)
            });
            compactWrap.Children.Add(TabIconElement(tab, 18));
            if (active)
            {
                compactWrap.Children.Add(new Border
                {
                    Width = 6,
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 2, 2, 0),
                    Background = new SolidColorBrush(UiColor(255, 230, 104))
                });
            }

            return compactWrap;
        }

        var host = TabHeaderHost(tab.Address);
        var caption = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        caption.Children.Add(new TextBlock
        {
            Text = tab.Title,
            MaxWidth = 154,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Medium,
            Foreground = (Brush)RootShell.Resources[active ? "NovaTabTitleActiveForegroundBrush" : "NovaTabTitleInactiveForegroundBrush"]
        });
        caption.Children.Add(new TextBlock
        {
            Text = host,
            MaxWidth = 154,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 10.5,
            Opacity = active ? 0.88 : 0.72,
            Foreground = (Brush)RootShell.Resources[active ? "NovaTabSubtitleActiveForegroundBrush" : "NovaTabSubtitleInactiveForegroundBrush"]
        });

        var label = new Border
        {
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(7, 2, 7, 2),
            CornerRadius = new CornerRadius(9),
            Background = (Brush)RootShell.Resources[active ? "NovaTabBadgeActiveBackgroundBrush" : "NovaTabBadgeInactiveBackgroundBrush"],
            BorderBrush = (Brush)RootShell.Resources[active ? "NovaTabBadgeActiveBorderBrush" : "NovaTabBadgeInactiveBorderBrush"],
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = TabHeaderBadge(tab),
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)RootShell.Resources[active ? "NovaTabBadgeActiveForegroundBrush" : "NovaTabBadgeInactiveForegroundBrush"]
            }
        };

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconWrap = new Grid
        {
            Width = 24,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconWrap.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = (Brush)RootShell.Resources[active ? "NovaTabIconWrapActiveBackgroundBrush" : "NovaTabIconWrapInactiveBackgroundBrush"]
        });
        iconWrap.Children.Add(TabIconElement(tab, 14));

        Grid.SetColumn(iconWrap, 0);
        Grid.SetColumn(caption, 1);
        Grid.SetColumn(label, 2);
        row.Children.Add(iconWrap);
        row.Children.Add(caption);
        row.Children.Add(label);

        return new Border
        {
            Padding = new Thickness(10, 7, 10, 7),
            CornerRadius = new CornerRadius(16),
            Background = (Brush)RootShell.Resources[active ? "NovaTabPillActiveBackgroundBrush" : "NovaTabPillInactiveBackgroundBrush"],
            BorderBrush = selectionBrush ?? (Brush)RootShell.Resources[active ? "NovaTabPillActiveBorderBrush" : "NovaTabPillInactiveBorderBrush"],
            BorderThickness = new Thickness(selected ? 2 : 1),
            Child = row
        };
    }

    private static string TabHeaderHost(string address)
    {
        if (string.Equals(address, "lumora://accueil", StringComparison.OrdinalIgnoreCase))
        {
            return "foyer lumineux";
        }

        var domain = ExtractDomain(address);
        return string.IsNullOrWhiteSpace(domain) ? "espace local" : domain;
    }

    private static string TabHeaderBadge(BrowserTabState tab)
    {
        if (tab.Pinned)
        {
            return "EPI";
        }

        if (string.Equals(tab.Address, "lumora://accueil", StringComparison.OrdinalIgnoreCase))
        {
            return "HOME";
        }

        return "WEB";
    }

    private void RefreshHorizontalTabHeaders()
    {
        var currentId = CurrentTab()?.Id;
        foreach (var item in BrowserTabs.TabItems.OfType<TabViewItem>())
        {
            if (item.Tag is not int id)
            {
                continue;
            }

            var tab = _tabs.FirstOrDefault(candidate => candidate.Id == id);
            if (tab is null)
            {
                continue;
            }

            item.Header = TabHeaderContent(tab, compact: tab.Pinned, active: currentId == tab.Id || IsTabInSplitView(tab.Id));
            item.IsClosable = !tab.Pinned;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, tab.Title);
        }
    }

    private static FrameworkElement TabIconElement(BrowserTabState tab, double size)
    {
        if (!string.IsNullOrWhiteSpace(tab.IconPath) && FaviconQuality.IsUsablePngFile(tab.IconPath))
        {
            return new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(tab.IconUri)),
                Width = size,
                Height = size
            };
        }

        return new FontIcon
        {
            Glyph = BookmarkGlyphs.Link,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = size
        };
    }

    private void VerticalTabButton_Click(object sender, RoutedEventArgs e)
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

    // ── Sélection multiple (regrouper/fermer plusieurs onglets d'un coup) ──────
    // Se superpose à l'activation normale d'un onglet, ne la remplace jamais :
    // Ctrl+clic ajoute/retire l'onglet cliqué de la sélection, Shift+clic étend
    // depuis le dernier onglet touché (ancre), un clic simple sans modificateur
    // vide la sélection. Non persistée (RAM uniquement, remise à zéro à chaque
    // lancement).
    private void HandleTabSelectionModifiers(BrowserTabState tab)
    {
        if (IsShiftKeyDown() && _selectionAnchorTabId is int anchorId)
        {
            SelectTabRange(anchorId, tab.Id);
            return;
        }

        if (IsControlKeyDown())
        {
            ToggleTabSelection(tab);
            return;
        }

        ClearTabSelection();
        _selectionAnchorTabId = tab.Id;
    }

    // Bascule d'appartenance a la selection multiple pour un onglet : partagee
    // entre le Ctrl+clic et la case a cocher "Selectionner" du menu contextuel
    // (meme effet, deux chemins d'acces).
    private void ToggleTabSelection(BrowserTabState tab)
    {
        if (!_selectedTabIds.Remove(tab.Id))
        {
            _selectedTabIds.Add(tab.Id);
        }
        _selectionAnchorTabId = tab.Id;
        RenderVerticalTabs();
        RefreshHorizontalTabHeaders();
    }

    private void SelectTabRange(int anchorId, int targetId)
    {
        var ids = _tabs.Select(t => t.Id).ToList();
        var start = ids.IndexOf(anchorId);
        var end = ids.IndexOf(targetId);
        if (start < 0 || end < 0)
        {
            return;
        }

        foreach (var index in Enumerable.Range(Math.Min(start, end), Math.Abs(end - start) + 1))
        {
            _selectedTabIds.Add(ids[index]);
        }

        RenderVerticalTabs();
        RefreshHorizontalTabHeaders();
    }

    private void ClearTabSelection()
    {
        if (_selectedTabIds.Count == 0)
        {
            return;
        }

        _selectedTabIds.Clear();
        RenderVerticalTabs();
        RefreshHorizontalTabHeaders();
    }
}
