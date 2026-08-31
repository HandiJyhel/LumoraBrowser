using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private void TabStripPositionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave)
        {
            return;
        }

        ApplyTabStripPositionImmediate(SelectedTabStripPosition());
    }

    private void BookmarksBarPositionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave)
        {
            return;
        }

        ApplyBookmarksBarPositionImmediate(SelectedBookmarksBarPosition());
    }

    private void WorkspaceLayoutSurface_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement anchor)
        {
            return;
        }

        // Actions d'onglets (muet global, recherche) ajoutees uniquement quand
        // le clic droit vient du rail d'onglets verticaux lui-meme (round 3,
        // 2026-08-12) - ce menu est partage par 3 autres ancrages (favoris,
        // zone de contenu, barre de statut) ou ces actions n'auraient pas de
        // sens.
        var includeVerticalTabsActions = ReferenceEquals(sender, VerticalTabsRail);
        CreateWorkspaceLayoutContextFlyout(includeVerticalTabsActions).ShowAt(anchor, e.GetPosition(anchor));
        e.Handled = true;
    }

    private void ExecuteStudioQuickAction(string actionTag)
    {
        if (string.IsNullOrWhiteSpace(actionTag))
        {
            return;
        }

        if (actionTag.StartsWith("tabs:", StringComparison.OrdinalIgnoreCase))
        {
            ApplyTabStripPositionImmediate(actionTag["tabs:".Length..]);
            return;
        }

        if (actionTag.StartsWith("bookmarks:", StringComparison.OrdinalIgnoreCase))
        {
            ApplyBookmarksBarPositionImmediate(actionTag["bookmarks:".Length..]);
            return;
        }

        if (actionTag.StartsWith("theme:", StringComparison.OrdinalIgnoreCase))
        {
            ApplyThemeModeImmediate(actionTag["theme:".Length..]);
            return;
        }

        switch (actionTag)
        {
            case "toggle:bookmarks":
                BookmarksBarSwitch.IsOn = !BookmarksBarSwitch.IsOn;
                SaveWorkspaceUiSettings();
                UpdateStatusText(BookmarksBarSwitch.IsOn ? "Barre de favoris visible." : "Barre de favoris masquée.");
                return;
            case "toggle:compact":
                CompactModeSwitch.IsOn = !CompactModeSwitch.IsOn;
                SaveWorkspaceUiSettings();
                UpdateStatusText(_compactModeEnabled ? "Interface compacte activée." : "Interface compacte désactivée.");
                return;
            // Muet global + recherche du rail reduit (round 3, 2026-08-12) :
            // reutilisent directement les memes methodes que les anciens
            // boutons dedies (VerticalTabsMuteAllButton_Click,
            // ExpandVerticalTabsRailAndFocusSearch), juste invoquees depuis ce
            // menu plutot que depuis un clic direct.
            case "railaction:muteall":
                VerticalTabsMuteAllButton_Click(this, new RoutedEventArgs());
                return;
            case "railaction:search":
                ExpandVerticalTabsRailAndFocusSearch();
                return;
        }
    }

    private void ApplyTabStripPositionImmediate(string position, string? statusOverride = null)
    {
        _tabStripPosition = NormalizeTabStripPosition(position);
        _verticalTabsEnabled = UsesVerticalTabRail(_tabStripPosition);

        SetWorkspaceControlsSilently(() =>
        {
            SelectComboByTag(TabStripPositionCombo, _tabStripPosition, "top");
            VerticalTabsSwitch.IsOn = _verticalTabsEnabled;
        });

        ApplyVerticalTabsLayout();
        SaveWorkspaceUiSettings();
        UpdateStatusText(statusOverride ?? $"Onglets placés à {WorkspacePositionLabel(_tabStripPosition)}.");
    }

    private void ApplyBookmarksBarPositionImmediate(string position, string? statusOverride = null)
    {
        _bookmarksBarPosition = NormalizeBookmarksBarPosition(position);

        SetWorkspaceControlsSilently(() =>
        {
            SelectComboByTag(BookmarksBarPositionCombo, _bookmarksBarPosition, "top");
        });

        ApplyBookmarksBarVisibility();
        SaveWorkspaceUiSettings();
        UpdateStatusText(statusOverride ?? $"Favoris placés à {WorkspacePositionLabel(_bookmarksBarPosition)}.");
    }

    private void ApplyThemeModeImmediate(string mode)
    {
        _uiSettings.ThemeMode = NormalizeThemeMode(mode);

        SetWorkspaceControlsSilently(() =>
        {
            SelectComboByTag(ThemeModeCombo, _uiSettings.ThemeMode, "dark");
        });

        _uiSettings.Save(_profile.UiSettingsFile);
        ApplyAccessibilitySettings();
        RefreshNovaHomePages();
        UpdateStatusText($"Lumora {ThemeModeLabel(_uiSettings.ThemeMode).ToLowerInvariant()} activé.");
    }

    private void SetWorkspaceControlsSilently(Action updateControls)
    {
        var previous = _suppressUiSettingsSave;
        _suppressUiSettingsSave = true;
        try
        {
            updateControls();
        }
        finally
        {
            _suppressUiSettingsSave = previous;
        }
    }

    private void SaveWorkspaceUiSettings()
    {
        if (_suppressUiSettingsSave)
        {
            return;
        }

        _uiSettings.BookmarksBarVisible = BookmarksBarSwitch.IsOn;
        _uiSettings.BookmarksBarPosition = _bookmarksBarPosition;
        _uiSettings.BookmarkViewMode = _bookmarkViewMode;
        _uiSettings.VerticalTabsEnabled = _verticalTabsEnabled;
        _uiSettings.TabStripPosition = _tabStripPosition;
        _uiSettings.UiDensity = _uiDensity;
        _uiSettings.VerticalTabsCompact = _verticalTabsCompact;
        _uiSettings.VerticalTabsWidth = Math.Clamp(_verticalTabsExpandedWidth, VerticalTabsMinExpandedWidth, VerticalTabsMaxWidth);
        _uiSettings.CompactModeEnabled = _compactModeEnabled;
        _uiSettings.CompactModeHidesBookmarks = CompactModeHideBookmarksSwitch.IsOn;
        _uiSettings.FullScreenAutoHideChrome = FullScreenAutoHideChromeSwitch.IsOn;
        _uiSettings.Save(_profile.UiSettingsFile);
    }

    private MenuFlyout CreateWorkspaceLayoutContextFlyout(bool includeVerticalTabsActions = false)
    {
        var flyout = new MenuFlyout();
        AddLumoraMenuHeader(
            flyout.Items,
            "Studio Lumora",
            CurrentWorkspaceSummary(),
            "\uE790");
        // Muet global + recherche (round 3, 2026-08-12) : le rail reduit
        // (40px) n'a plus la place pour un bouton dedie a chacune - ce menu,
        // deja ouvert au clic droit sur le rail, est le point d'acces de
        // remplacement plutot qu'une icone permanente. Uniquement sur cet
        // ancrage precis (voir WorkspaceLayoutSurface_RightTapped), separe du
        // reste (disposition, favoris, theme) par une ligne.
        if (includeVerticalTabsActions)
        {
            AddVerticalTabsActionMenuItems(flyout.Items);
            flyout.Items.Add(new MenuFlyoutSeparator());
        }
        AddWorkspaceLayoutMenuItems(flyout.Items, includeTheme: true);
        HookFlyoutPointerSupport(flyout);
        return flyout;
    }

    private void AddVerticalTabsActionMenuItems(IList<MenuFlyoutItemBase> items)
    {
        items.Add(CreateWorkspaceMenuItem(
            _allTabsMuted ? "R\u00E9tablir le son de tous les onglets" : "Couper le son de tous les onglets",
            "railaction:muteall",
            CreateMenuGlyphIcon("\uE995")));
        items.Add(CreateWorkspaceMenuItem(
            "Rechercher un onglet\u2026",
            "railaction:search",
            new SymbolIcon(Symbol.Find)));
    }

    private void AddWorkspaceLayoutMenuItems(IList<MenuFlyoutItemBase> items, bool includeTheme)
    {
        var presets = new MenuFlyoutSubItem
        {
            Text = "Disposition rapide",
            Icon = CreateMenuGlyphIcon("\uE7FC")
        };
        presets.Items.Add(CreateWorkspaceMenuItem("Halo", "preset:top|top", new SymbolIcon(Symbol.Home)));
        presets.Items.Add(CreateWorkspaceMenuItem("Atelier", "preset:left|right", new SymbolIcon(Symbol.Setting)));
        presets.Items.Add(CreateWorkspaceMenuItem("Flux", "preset:bottom|left", CreateMenuGlyphIcon("\uE8AB")));
        items.Add(presets);

        var tabs = new MenuFlyoutSubItem
        {
            Text = "Onglets",
            Icon = CreateMenuGlyphIcon("\uE8AB")
        };
        tabs.Items.Add(CreateWorkspaceMenuItem("Placer en haut", "tabs:top", new SymbolIcon(Symbol.Up)));
        tabs.Items.Add(CreateWorkspaceMenuItem("Placer en bas", "tabs:bottom", CreateMenuGlyphIcon("\uE74B")));
        tabs.Items.Add(CreateWorkspaceMenuItem("Placer à gauche", "tabs:left", new SymbolIcon(Symbol.Back)));
        tabs.Items.Add(CreateWorkspaceMenuItem("Placer à droite", "tabs:right", new SymbolIcon(Symbol.Forward)));
        items.Add(tabs);

        var bookmarks = new MenuFlyoutSubItem
        {
            Text = "Favoris",
            Icon = CreateMenuGlyphIcon("\uE735")
        };
        bookmarks.Items.Add(CreateWorkspaceMenuItem("Placer en haut", "bookmarks:top", new SymbolIcon(Symbol.Up)));
        bookmarks.Items.Add(CreateWorkspaceMenuItem("Placer en bas", "bookmarks:bottom", CreateMenuGlyphIcon("\uE74B")));
        bookmarks.Items.Add(CreateWorkspaceMenuItem("Placer à gauche", "bookmarks:left", new SymbolIcon(Symbol.Back)));
        bookmarks.Items.Add(CreateWorkspaceMenuItem("Placer à droite", "bookmarks:right", new SymbolIcon(Symbol.Forward)));
        bookmarks.Items.Add(new MenuFlyoutSeparator());
        bookmarks.Items.Add(CreateWorkspaceMenuItem(
            BookmarksBarSwitch.IsOn ? "Masquer la barre de favoris" : "Afficher la barre de favoris",
            "toggle:bookmarks",
            CreateMenuGlyphIcon("\uE734")));
        items.Add(bookmarks);

        items.Add(CreateWorkspaceMenuItem(
            _compactModeEnabled ? "Quitter le mode compact" : "Activer le mode compact",
            "toggle:compact",
            new SymbolIcon(Symbol.FullScreen)));

        if (!includeTheme)
        {
            return;
        }

        var theme = new MenuFlyoutSubItem
        {
            Text = "Luminosité Lumora",
            Icon = CreateMenuGlyphIcon("\uE706")
        };
        theme.Items.Add(CreateWorkspaceMenuItem("Sombre", "theme:dark", CreateMenuGlyphIcon("\uE708")));
        theme.Items.Add(CreateWorkspaceMenuItem("Clair", "theme:light", CreateMenuGlyphIcon("\uE706")));
        theme.Items.Add(CreateWorkspaceMenuItem("Système", "theme:system", CreateMenuGlyphIcon("\uE770")));
        items.Add(theme);
    }

    private MenuFlyoutItem CreateWorkspaceMenuItem(string text, string actionTag, IconElement icon)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Tag = actionTag,
            Icon = icon
        };
        item.Click += WorkspaceLayoutMenuItem_Click;
        return item;
    }

    private void WorkspaceLayoutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string actionTag })
        {
            return;
        }

        if (actionTag.StartsWith("preset:", StringComparison.OrdinalIgnoreCase))
        {
            var payload = actionTag["preset:".Length..].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (payload.Length == 2)
            {
                ApplyWorkspacePresetImmediate(payload[0], payload[1], "Preset d'espace Lumora appliqué.");
            }
            return;
        }

        ExecuteStudioQuickAction(actionTag);
    }

    private void ApplyWorkspacePresetImmediate(string tabPosition, string bookmarksPosition, string status)
    {
        _tabStripPosition = NormalizeTabStripPosition(tabPosition);
        _bookmarksBarPosition = NormalizeBookmarksBarPosition(bookmarksPosition);
        _verticalTabsEnabled = UsesVerticalTabRail(_tabStripPosition);

        SetWorkspaceControlsSilently(() =>
        {
            SelectComboByTag(TabStripPositionCombo, _tabStripPosition, "top");
            SelectComboByTag(BookmarksBarPositionCombo, _bookmarksBarPosition, "top");
            VerticalTabsSwitch.IsOn = _verticalTabsEnabled;
        });

        ApplyVerticalTabsLayout();
        ApplyBookmarksBarVisibility();
        SaveWorkspaceUiSettings();
        UpdateStatusText(status);
    }

    private string CurrentWorkspaceSummary() =>
        $"Onglets {WorkspacePositionLabel(_tabStripPosition)} • Favoris {WorkspacePositionLabel(_bookmarksBarPosition)} • {ThemeModeLabel(_uiSettings.ThemeMode)}";

    private void AddLumoraMenuHeader(IList<MenuFlyoutItemBase> items, string title, string subtitle, string glyph)
    {
        items.Add(new MenuFlyoutItem
        {
            Text = title,
            IsEnabled = false,
            Icon = CreateMenuGlyphIcon(glyph)
        });

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            items.Add(new MenuFlyoutItem
            {
                Text = subtitle,
                IsEnabled = false,
                Icon = CreateMenuGlyphIcon("\uE946")
            });
        }

        items.Add(new MenuFlyoutSeparator());
    }

    private static string WorkspacePositionLabel(string position) =>
        NormalizeTabStripPosition(position) switch
        {
            "left" => "gauche",
            "right" => "droite",
            "bottom" => "bas",
            _ => "haut"
        };

    private static string NormalizeThemeMode(string? mode) =>
        mode?.Trim().ToLowerInvariant() switch
        {
            "light" => "light",
            "system" => "system",
            _ => "dark"
        };

    private static string ThemeModeLabel(string? mode) =>
        NormalizeThemeMode(mode) switch
        {
            "light" => "Lumora clair",
            "system" => "Lumora système",
            _ => "Lumora sombre"
        };

    private static FontIcon CreateMenuGlyphIcon(string glyph) =>
        new()
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets")
        };
}
