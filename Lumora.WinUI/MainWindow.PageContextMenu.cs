using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Windows.Foundation;

namespace Lumora.WinUI;

// Menu contextuel (clic droit) des pages web (chantier identite visuelle,
// 2026-08-10 - demande explicite utilisateur : "je veux qu'il soit pareil
// sur les pages web"). Le menu natif Chromium/WebView2 n'est pas stylisable
// (rendu par le moteur, hors de portee de XAML) : on le supprime
// (args.Handled = true) et on reconstruit NOTRE PROPRE menu a partir de
// args.MenuItems - la vraie liste contextuelle fournie par Chromium (lien,
// image, selection...), executee ensuite via args.SelectedCommandId. Aucune
// logique de clic droit reinventee a la main : seule la presentation change.
public sealed partial class MainWindow
{
    private void CoreWebView2_ContextMenuRequested(BrowserTabState tab, CoreWebView2ContextMenuRequestedEventArgs args)
    {
        if (!_tabs.Contains(tab) || tab.View is not { } view)
        {
            return;
        }

        args.Handled = true;
        var deferral = args.GetDeferral();

        // Pas de FlyoutPresenterStyle a poser ici : MenuFlyout n'expose pas
        // cette propriete (contrairement a Flyout) - la forme galbee vient
        // du style implicite TargetType="MenuFlyoutPresenter" (MainWindow.xaml).
        var flyout = new MenuFlyout();
        var completed = false;
        void Complete()
        {
            if (completed)
            {
                return;
            }
            completed = true;
            deferral.Complete();
        }

        RecordAndOrderContextMenuItems(args.MenuItems, out var orderedItems);

        var sawSeparator = false;
        foreach (var item in orderedItems)
        {
            var element = BuildContextMenuItem(item, args, Complete, ref sawSeparator);
            if (element is not null)
            {
                flyout.Items.Add(element);
            }
        }

        flyout.Closed += (_, _) => Complete();

        var point = new Point(args.Location.X, args.Location.Y);
        flyout.ShowAt(view, new FlyoutShowOptions { Position = point });
    }

    // Plafond du catalogue auto-decouvert (voir UiSettings.ContextMenuKnownItems) :
    // au-dela, on arrete d'apprendre de nouvelles entrees plutot que de grossir
    // sans fin - largement suffisant pour les familles page/lien/image/selection.
    private const int ContextMenuKnownItemsCap = 60;

    // Decouvre les nouvelles entrees (Name stable, jamais le Label variable)
    // pour peupler le catalogue des Reglages, puis applique masquage/ordre
    // choisis par l'utilisateur - uniquement sur les entrees de premier niveau
    // (Command/CheckBox/Radio), separateurs et sous-menus jamais masques ni
    // deplaces pour leur propre compte (voir ContextMenuOrdering).
    private void RecordAndOrderContextMenuItems(
        IList<CoreWebView2ContextMenuItem> menuItems,
        out List<CoreWebView2ContextMenuItem> orderedItems)
    {
        var known = _uiSettings.ContextMenuKnownItems;
        var knownNames = known.Select(k => k.Name).ToHashSet(StringComparer.Ordinal);
        var learnedAny = false;

        foreach (var item in menuItems)
        {
            if (item.Kind == CoreWebView2ContextMenuItemKind.Separator
                || item.Kind == CoreWebView2ContextMenuItemKind.Submenu)
            {
                continue;
            }
            if (string.IsNullOrEmpty(item.Name) || knownNames.Contains(item.Name))
            {
                continue;
            }
            if (known.Count >= ContextMenuKnownItemsCap)
            {
                break;
            }
            known.Add(new ContextMenuKnownItem(item.Name, item.Label));
            knownNames.Add(item.Name);
            learnedAny = true;
        }

        if (learnedAny)
        {
            _uiSettings.Save(_profile.UiSettingsFile);
        }

        orderedItems = ContextMenuOrdering.Apply(
            menuItems,
            static i => i.Name,
            _uiSettings.ContextMenuHiddenItems,
            _uiSettings.ContextMenuOrder);
    }

    // ── Reglages > Mon Lumora > Avance : gestion du catalogue decouvert ──────
    // Boutons Monter/Descendre plutot qu'un glisser-deposer natif de ListView :
    // ce dernier n'est pas verifiable en conditions reelles ici (glisser
    // souris synthetique refuse par l'environnement de verification, voir
    // SKILL.md verify) - des boutons simples restent, eux, entierement
    // testables (Invoke) et tout aussi fonctionnels pour reordonner.
    private void PopulateContextMenuSettingsList()
    {
        var hidden = _uiSettings.ContextMenuHiddenItems;
        var orderIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _uiSettings.ContextMenuOrder.Count; i++)
        {
            orderIndex.TryAdd(_uiSettings.ContextMenuOrder[i], i);
        }

        var sorted = _uiSettings.ContextMenuKnownItems
            .OrderBy(k => orderIndex.TryGetValue(k.Name, out var idx) ? idx : int.MaxValue)
            .ThenBy(k => k.Name, StringComparer.Ordinal)
            .ToList();

        ContextMenuItemsList.Items.Clear();
        for (var i = 0; i < sorted.Count; i++)
        {
            var known = sorted[i];
            var isVisible = !hidden.Contains(known.Name);
            ContextMenuItemsList.Items.Add(BuildContextMenuSettingsRow(known, isVisible, i, sorted.Count));
        }
        ContextMenuEmptyText.Visibility = sorted.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private ListViewItem BuildContextMenuSettingsRow(ContextMenuKnownItem known, bool isVisible, int index, int total)
    {
        var grid = new Grid { Margin = new Thickness(4, 2, 4, 2), ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkBox = new CheckBox { IsChecked = isVisible, MinWidth = 0 };
        AutomationProperties.SetName(checkBox, $"Afficher {ContextMenuFriendlyLabel(known)} dans le menu contextuel");
        checkBox.Checked += (_, _) => SetContextMenuItemVisible(known.Name, true);
        checkBox.Unchecked += (_, _) => SetContextMenuItemVisible(known.Name, false);
        Grid.SetColumn(checkBox, 0);
        grid.Children.Add(checkBox);

        var label = new TextBlock
        {
            Text = ContextMenuFriendlyLabel(known),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = isVisible ? 1.0 : 0.5
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        var upButton = new Button
        {
            Content = new SymbolIcon(Symbol.Up),
            Style = (Style)RootShell.Resources["NovaCompactButtonStyle"],
            IsEnabled = index > 0
        };
        AutomationProperties.SetName(upButton, $"Monter {ContextMenuFriendlyLabel(known)}");
        upButton.Click += (_, _) => MoveContextMenuItem(known.Name, -1);
        Grid.SetColumn(upButton, 2);
        grid.Children.Add(upButton);

        var downButton = new Button
        {
            Content = new FontIcon { Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12 },
            Style = (Style)RootShell.Resources["NovaCompactButtonStyle"],
            IsEnabled = index < total - 1
        };
        AutomationProperties.SetName(downButton, $"Descendre {ContextMenuFriendlyLabel(known)}");
        downButton.Click += (_, _) => MoveContextMenuItem(known.Name, 1);
        Grid.SetColumn(downButton, 3);
        grid.Children.Add(downButton);

        var item = new ListViewItem { Content = grid, Tag = known.Name };
        AutomationProperties.SetName(item, ContextMenuFriendlyLabel(known));
        return item;
    }

    private void SetContextMenuItemVisible(string name, bool visible)
    {
        var hidden = _uiSettings.ContextMenuHiddenItems;
        if (visible) hidden.Remove(name);
        else if (!hidden.Contains(name)) hidden.Add(name);
        _uiSettings.Save(_profile.UiSettingsFile);
        UpdateStatusText(visible ? "Action réaffichée dans le menu contextuel." : "Action masquée du menu contextuel.");
        PopulateContextMenuSettingsList();
    }

    private void MoveContextMenuItem(string name, int direction)
    {
        // L'ordre courant = l'ordre affiche a l'ecran (deja calcule dans
        // PopulateContextMenuSettingsList a partir de ContextMenuOrder +
        // reste du catalogue) - le relire depuis les lignes affichees evite
        // de dupliquer ce calcul ici.
        var currentOrder = ContextMenuItemsList.Items
            .OfType<ListViewItem>()
            .Select(i => (string)i.Tag)
            .ToList();
        var pos = currentOrder.IndexOf(name);
        var target = pos + direction;
        if (pos < 0 || target < 0 || target >= currentOrder.Count) return;
        (currentOrder[pos], currentOrder[target]) = (currentOrder[target], currentOrder[pos]);
        _uiSettings.ContextMenuOrder = currentOrder;
        _uiSettings.Save(_profile.UiSettingsFile);
        PopulateContextMenuSettingsList();
    }

    private static string ContextMenuFriendlyLabel(ContextMenuKnownItem known) =>
        ContextMenuNameLabels.TryGetValue(known.Name, out var french) ? french : known.Label;

    // Traductions maison pour les identifiants Chromium les plus courants -
    // repli sur le Label natif (potentiellement en anglais, ou une variante
    // non couverte ici) pour tout Name absent de cette table : jamais bloquant,
    // juste moins joli le temps d'etre complete.
    private static readonly Dictionary<string, string> ContextMenuNameLabels = new(StringComparer.Ordinal)
    {
        ["copy"] = "Copier",
        ["cut"] = "Couper",
        ["paste"] = "Coller",
        ["pasteAndMatchStyle"] = "Coller sans la mise en forme",
        ["selectAll"] = "Tout sélectionner",
        ["undo"] = "Annuler",
        ["redo"] = "Rétablir",
        ["openLinkNewTab"] = "Ouvrir le lien dans un nouvel onglet",
        ["openLinkNewWindow"] = "Ouvrir le lien dans une nouvelle fenêtre",
        ["copyLinkToClipboard"] = "Copier l'adresse du lien",
        ["saveLinkAs"] = "Enregistrer le lien sous…",
        ["saveImageAs"] = "Enregistrer l'image sous…",
        ["copyImage"] = "Copier l'image",
        ["copyImageLinkToClipboard"] = "Copier l'adresse de l'image",
        ["openImageInNewTab"] = "Ouvrir l'image dans un nouvel onglet",
        ["printPage"] = "Imprimer",
        ["viewSource"] = "Afficher la source de la page",
        ["inspectElement"] = "Inspecter",
        ["translatePage"] = "Traduire la page",
        ["savePage"] = "Enregistrer la page sous…",
        ["exitFullscreen"] = "Quitter le plein écran",
        ["openLinkWithDefaultBrowser"] = "Ouvrir le lien avec l'application par défaut",
    };

    private MenuFlyoutItemBase? BuildContextMenuItem(
        CoreWebView2ContextMenuItem item,
        CoreWebView2ContextMenuRequestedEventArgs args,
        Action complete,
        ref bool sawSeparator)
    {
        switch (item.Kind)
        {
            case CoreWebView2ContextMenuItemKind.Separator:
                {
                    var sep = new MenuFlyoutSeparator();
                    // Premier separateur "signature" : degrade Lumora plutot
                    // qu'un trait neutre - un seul, pour ne pas diluer l'effet
                    // (meme principe que la maquette validee).
                    if (!sawSeparator)
                    {
                        sawSeparator = true;
                        sep.Background = (Brush)RootShell.Resources["NovaIdentityMarkBrush"];
                    }
                    return sep;
                }

            case CoreWebView2ContextMenuItemKind.Submenu:
                {
                    var sub = new MenuFlyoutSubItem { Text = item.Label, IsEnabled = item.IsEnabled };
                    var sawSeparatorInSub = false;
                    foreach (var child in item.Children)
                    {
                        var childElement = BuildContextMenuItem(child, args, complete, ref sawSeparatorInSub);
                        if (childElement is not null)
                        {
                            sub.Items.Add(childElement);
                        }
                    }
                    return sub;
                }

            case CoreWebView2ContextMenuItemKind.CheckBox:
            case CoreWebView2ContextMenuItemKind.Radio:
                {
                    var toggle = new ToggleMenuFlyoutItem
                    {
                        Text = item.Label,
                        IsEnabled = item.IsEnabled,
                        IsChecked = item.IsChecked,
                    };
                    toggle.Click += (_, _) =>
                    {
                        args.SelectedCommandId = item.CommandId;
                        complete();
                    };
                    return toggle;
                }

            default: // Command
                {
                    var mfi = new MenuFlyoutItem { Text = item.Label, IsEnabled = item.IsEnabled };
                    mfi.Click += (_, _) =>
                    {
                        args.SelectedCommandId = item.CommandId;
                        complete();
                    };
                    return mfi;
                }
        }
    }
}
