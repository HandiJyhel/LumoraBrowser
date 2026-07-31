using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Palette de commande Ctrl+K ───────────────────────────────────────────

    private sealed record CommandPaletteItem(
        string Kind,
        string Title,
        string Detail,
        string Glyph,
        Action Execute)
    {
        // Nom accessible unique : sans ca, un lecteur d'ecran lit Title et
        // Detail comme deux TextBlock separes, sans la categorie (Kind).
        public string AccessibleName => string.IsNullOrWhiteSpace(Detail)
            ? $"{Kind} : {Title}"
            : $"{Kind} : {Title}, {Detail}";
    }

    private void ToggleCommandPalette()
    {
        if (CommandPaletteOverlay.Visibility == Visibility.Visible)
        {
            HideCommandPalette();
            return;
        }

        ShowCommandPalette();
    }

    private void ShowCommandPalette()
    {
        CommandPaletteSearchBox.Text = string.Empty;
        CommandPaletteOverlay.Visibility = Visibility.Visible;
        RenderCommandPalette();
        CommandPaletteSearchBox.Focus(FocusState.Programmatic);
        CommandPaletteSearchBox.SelectAll();
    }

    private void HideCommandPalette()
    {
        CommandPaletteOverlay.Visibility = Visibility.Collapsed;
    }

    private void CommandPaletteCloseButton_Click(object sender, RoutedEventArgs e) =>
        HideCommandPalette();

    private void CommandPaletteAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!CanUseCommandPaletteFromCurrentFocus())
        {
            return;
        }

        args.Handled = true;
        ToggleCommandPalette();
    }

    private void CommandPaletteSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        RenderCommandPalette();

    private void CommandPaletteSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            HideCommandPalette();
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            ExecuteSelectedCommandPaletteItem();
            return;
        }

        if (e.Key == VirtualKey.Down)
        {
            e.Handled = true;
            if (CommandPaletteList.Items.Count > 0)
            {
                CommandPaletteList.SelectedIndex = Math.Max(0, CommandPaletteList.SelectedIndex);
                CommandPaletteList.Focus(FocusState.Programmatic);
            }
        }
    }

    private void CommandPaletteList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            HideCommandPalette();
            CommandPaletteSearchBox.Focus(FocusState.Programmatic);
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            ExecuteSelectedCommandPaletteItem();
            return;
        }

        if (e.Key == VirtualKey.Up && CommandPaletteList.SelectedIndex <= 0)
        {
            e.Handled = true;
            CommandPaletteSearchBox.Focus(FocusState.Programmatic);
            CommandPaletteSearchBox.Select(CommandPaletteSearchBox.Text.Length, 0);
        }
    }

    private void CommandPaletteList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        ExecuteSelectedCommandPaletteItem();

    private void ExecuteSelectedCommandPaletteItem()
    {
        var item = CommandPaletteList.SelectedItem as CommandPaletteItem
                   ?? _commandPaletteItems.FirstOrDefault();
        if (item is null) return;

        HideCommandPalette();
        item.Execute();
    }

    private void RenderCommandPalette()
    {
        var query = CommandPaletteSearchBox?.Text?.Trim() ?? string.Empty;
        var items = BuildCommandPaletteItems(query)
            .Take(40)
            .ToList();

        _commandPaletteItems.Clear();
        foreach (var item in items)
        {
            _commandPaletteItems.Add(item);
        }

        CommandPaletteList.SelectedIndex = _commandPaletteItems.Count > 0 ? 0 : -1;
        CommandPaletteHintText.Text = _commandPaletteItems.Count == 0
            ? "Aucun resultat."
            : "Entree pour ouvrir, Echap pour fermer.";
    }

    private IEnumerable<CommandPaletteItem> BuildCommandPaletteItems(string query)
    {
        var results = new List<(CommandPaletteItem Item, int Score)>();

        void Add(CommandPaletteItem item, int baseScore)
        {
            var score = ScoreCommandPaletteItem(item, query, baseScore);
            if (score > 0)
            {
                results.Add((item, score));
            }
        }

        AddSearchOrAddressItem(query, results);

        Add(new("Naviguer", "Nouvel onglet", "Ouvre un nouvel onglet Lumora (Ctrl+T)", "\uE710",
            () => AddTab("Nouvel onglet", "lumora://accueil", select: true)), 100);
        Add(new("Naviguer", "Fermer l'onglet", "Ferme l'onglet actif (Ctrl+W)", "\uE711",
            CloseCurrentTabAccelerator), 99);
        Add(new("Naviguer", "Incognito", "Session ephemere, IP masquee en option via Tor (Ctrl+Shift+N)", "\uE727",
            () => OpenIncognitoWindow()), 98);
        Add(new("Naviguer", "Rouvrir l'onglet ferme", "Restaure le dernier onglet ferme (Ctrl+Shift+T)", "\uE7A7",
            () => ReopenLastClosedTab()), 97);
        Add(new("Naviguer", "Groupes enregistres", "Retrouver et rouvrir vos groupes d'onglets ranges", "\uE7C1",
            () => SavedTabGroupsMenu_Click(this, new RoutedEventArgs())), 96);
        Add(new("Naviguer", "Accueil", "Retourne a la page d'accueil Lumora", "\uE80F",
            () => HomeMenu_Click(this, new RoutedEventArgs())), 95);
        Add(new("Controle du site", "Site actuel", "Ouvre le centre du site visible", "\uE774",
            () => ShowSiteControlForCurrentPage()), 94);
        Add(new("Lumora", "Modules Lumora", "Ouvre les outils integres : lecture, media, traduction, actions rapides", "\uE7B8",
            () => ModulesMenu_Click(this, new RoutedEventArgs())), 93);
        Add(new("Lumora", "Parametres", "Ouvre les parametres de Lumora", "\uE713",
            () => SettingsMenu_Click(this, new RoutedEventArgs())), 90);
        Add(new("Coffre local", "Gestionnaire de mots de passe", "Ouvre le coffre local vault.lumora", "\uE72E",
            () => VaultMenu_Click(this, new RoutedEventArgs())), 88);
        Add(new("Coffre local", "Importer des mots de passe", "Depuis un CSV (Proton Pass, Bitwarden...) ou un navigateur installe (Chrome, Edge, Brave...)", "\uE8B5",
            () => ImportPasswordsMenu_Click(this, new RoutedEventArgs())), 88);
        Add(new("Coffre local", "Portefeuille", "Cartes de paiement locales (vault.lumora)", "\uE8C7",
            () => WalletMenu_Click(this, new RoutedEventArgs())), 87);
        Add(new("Coffre local", "Bilan de sante des mots de passe", "Reutilises, faibles ou anciens - analyse 100% locale", "\uE9D9",
            () => VaultHealthButton_Click(this, new RoutedEventArgs())), 85);
        Add(new("Donnees locales", "Historique", "Ouvre l'historique local", "\uE81C",
            () => HistoryMenu_Click(this, new RoutedEventArgs())), 86);
        Add(new("Donnees locales", "Telechargements", "Ouvre les telechargements de cette session", "\uE896",
            () => DownloadsMenu_Click(this, new RoutedEventArgs())), 84);
        Add(new("Donnees locales", "Notes", "Notes libres et pages annotees en mode lecture", "\uE70B",
            () => NotesMenu_Click(this, new RoutedEventArgs())), 83);
        Add(new("Modules Lumora", "Mode lecture", "Article epure - surligner et commenter la page active", "\uE736",
            () => ReaderModeMenu_Click(this, new RoutedEventArgs())), 83);
        Add(new("Controle du site", "Sites connectes", "Gere les sessions et cookies conserves", "\uE8D4",
            () => SessionsMenu_Click(this, new RoutedEventArgs())), 82);
        Add(new("Modules Lumora", "Applications", "Sites installes en fenetre dediee", "\uE71D",
            () => WebAppsMenu_Click(this, new RoutedEventArgs())), 81);
        Add(new("Modules Lumora", "Installer comme application", "Epingle la page active dans sa propre fenetre", "\uE710",
            () => InstallAppMenu_Click(this, new RoutedEventArgs())), 79);
        Add(new("Modules Lumora", "Detacher la video", "Picture-in-Picture pour la video active de la page", "\uE8B9",
            () => DetachVideoMenu_Click(this, new RoutedEventArgs())), 77);
        Add(new("Coffre local", "Cles d'acces", "Ouvre les passkeys locales connues", "\uE8D7",
            () => PasskeysMenu_Click(this, new RoutedEventArgs())), 80);
        Add(new("Donnees locales", "Ajouter aux favoris", "Ajoute la page active a la barre des favoris", "\uE734",
            () => AddBookmarkButton_Click(this, new RoutedEventArgs())), 78);
        Add(new("Donnees locales", "Importer des favoris", "Importe des favoris depuis un navigateur ou un fichier HTML", "\uE8B5",
            () => ImportMenu_Click(this, new RoutedEventArgs())), 76);
        Add(new("Lumora", "A propos", "Informations sur Lumora", "\uE946",
            () => AboutMenu_Click(this, new RoutedEventArgs())), 60);

        foreach (var tab in _tabs)
        {
            Add(new("Onglet", tab.Title, tab.Address, "\uE8A7", () => SelectTab(tab)), 76);
        }

        foreach (var closed in _closedTabs.Items)
        {
            Add(new("Onglet ferme", closed.Title, closed.Address, "\uE7A7",
                () => ReopenClosedTab(closed)), 74);
        }

        foreach (var node in _allBookmarkNodes.Where(n => n.Kind == BookmarkKind.Url))
        {
            Add(new("Favori", node.Title, node.Url, "\uE734",
                () =>
                {
                    NavigateCurrentTab(node.Url, node.Title);
                    ShowPanel(BrowserPanel, node.Title);
                }), 70);
        }

        foreach (var entry in _historyPanel.Store.AllEntries().Take(300))
        {
            Add(new("Historique",
                string.IsNullOrWhiteSpace(entry.Title) ? DisplayTitle(entry.Url) : entry.Title,
                entry.Url,
                "\uE81C",
                () =>
                {
                    NavigateCurrentTab(entry.Url, entry.Title);
                    ShowPanel(BrowserPanel, entry.Title);
                }), 50);
        }

        return results
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Item.Kind, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(result => result.Item.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(result => result.Item);
    }

    private void AddSearchOrAddressItem(string query, List<(CommandPaletteItem Item, int Score)> results)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        var normalized = NormalizeAddress(query);
        var isSearch = !query.Contains('.') && !query.Contains("://", StringComparison.OrdinalIgnoreCase) ||
                       query.Contains(' ');
        var title = isSearch ? $"Rechercher \"{query}\"" : $"Ouvrir {query}";
        results.Add((new CommandPaletteItem("Navigation", title, normalized, "\uE721",
            () =>
            {
                NavigateCurrentTab(normalized, DisplayTitle(normalized));
                ShowPanel(BrowserPanel, DisplayTitle(normalized));
            }), 120));
    }

    private static int ScoreCommandPaletteItem(CommandPaletteItem item, string query, int baseScore)
    {
        if (string.IsNullOrWhiteSpace(query)) return baseScore;

        var q = query.Trim();
        var score = 0;
        if (item.Title.Equals(q, StringComparison.CurrentCultureIgnoreCase)) score += 150;
        if (item.Title.StartsWith(q, StringComparison.CurrentCultureIgnoreCase)) score += 100;
        if (item.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase)) score += 65;
        if (item.Detail.Contains(q, StringComparison.OrdinalIgnoreCase)) score += 45;
        if (item.Kind.Contains(q, StringComparison.CurrentCultureIgnoreCase)) score += 25;
        return score == 0 ? 0 : score + baseScore;
    }

    private void SelectTab(BrowserTabState tab)
    {
        var item = BrowserTabs.TabItems
            .OfType<TabViewItem>()
            .FirstOrDefault(candidate => candidate.Tag is int id && id == tab.Id);
        if (item is null) return;

        BrowserTabs.SelectedItem = item;
        ActivateTab(tab);
    }

    private static bool IsControlKeyDown()
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        return (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    }

    private static bool IsShiftKeyDown()
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        return (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    }

    // AltGr (touche Alt de droite, presente sur la plupart des claviers europeens)
    // est rapporte par Windows comme Ctrl+Alt enfonces simultanement - exactement
    // les combinaisons Ctrl+Alt+chiffre/lettre utilisees par les raccourcis
    // d'accessibilite (zones, confort rapide, secours, contexte). Sans cette
    // detection, taper un caractere compose via AltGr (ex. "@" via AltGr+0 en
    // clavier francais AZERTY) dans un champ de saisie declenchait le raccourci
    // au lieu de produire le caractere. Trouve en usage reel le 2026-07-22
    // (mot de passe de creation de profil qui refusait "@").
    private static bool IsRightAltKeyDown()
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightMenu);
        return (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    }

    private bool CanUseCommandPaletteFromCurrentFocus()
    {
        if (CommandPaletteOverlay.Visibility == Visibility.Visible)
        {
            return true;
        }

        if (!_uiSettings.CommandPaletteEnabled ||
            LoginOverlay.Visibility == Visibility.Visible ||
            SetupWizardOverlay.Visibility == Visibility.Visible)
        {
            return false;
        }

        var focused = FocusManager.GetFocusedElement(Content.XamlRoot);
        if (ReferenceEquals(focused, AddressBox))
        {
            AddressBox.Focus(FocusState.Programmatic);
            AddressBox.SelectAll();
            return false;
        }

        if (focused is DependencyObject dependencyObject &&
            IsDescendantOf(dependencyObject, CommandPaletteOverlay))
        {
            return true;
        }

        if (!_uiSettings.CommandPaletteOpenFromTextFields && IsTextInputElement(focused))
        {
            return false;
        }

        if (!_uiSettings.CommandPaletteOpenFromWebPages && IsWebContentElement(focused))
        {
            return false;
        }

        return true;
    }

    private static bool IsTextInputElement(object? focused) =>
        focused is TextBox ||
        focused is PasswordBox ||
        focused is RichEditBox ||
        focused is AutoSuggestBox ||
        focused is ComboBox;

    private static bool IsWebContentElement(object? focused) =>
        focused is WebView2;

    private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
    {
        var current = child;
        while (current is not null)
        {
            if (ReferenceEquals(current, parent))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
