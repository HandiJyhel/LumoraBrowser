using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace PulseBrowser.WinUI;

// Applications web Pulse : un site épinglé, ouvert dans une fenêtre dédiée
// (PulseAppWindow) sans onglets ni barre d'adresse, comme le mode « application »
// de Chrome/Edge. Synergie avec les sessions éphémères (0.46.0-dev) : installer
// une app marque automatiquement son domaine comme site de confiance.
public sealed partial class MainWindow
{
    // ── Panneau Applications ──────────────────────────────────────────────────

    private void WebAppsMenu_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(WebAppsPanel, "Applications");
        RefreshWebAppsPanel();
    }

    private void RefreshWebAppsPanel()
    {
        WebAppsPanelItems.Children.Clear();

        var apps = _webApps.All();
        if (apps.Count == 0)
        {
            WebAppsPanelItems.Children.Add(new TextBlock
            {
                Text = "Aucune application installée. Utilisez « Installer la page active » ci-dessus, ou depuis le menu Pulse.",
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var app in apps)
        {
            WebAppsPanelItems.Children.Add(BuildWebAppElement(app));
        }
    }

    private UIElement BuildWebAppElement(PulseWebApp app)
    {
        var headerGrid = new Grid { ColumnSpacing = 8 };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconPngPath = Path.Combine(_profile.FaviconsDir, $"{HashOrigin(app.Url)}.png");
        FrameworkElement icon = FaviconQuality.IsUsablePngFile(iconPngPath)
            ? new Image
            {
                Source = new BitmapImage(new Uri(iconPngPath)),
                Width = 22,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center
            }
            : new FontIcon
            {
                Glyph = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center
            };
        Grid.SetColumn(icon, 0);
        headerGrid.Children.Add(icon);

        var titleBlock = new TextBlock
        {
            Text = app.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(titleBlock, 1);
        headerGrid.Children.Add(titleBlock);

        var renameBtn = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            Content = new FontIcon { Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 }
        };
        ToolTipService.SetToolTip(renameBtn, "Renommer");
        renameBtn.Click += async (_, _) => await RenameWebAppAsync(app);
        Grid.SetColumn(renameBtn, 2);
        headerGrid.Children.Add(renameBtn);

        var deleteBtn = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            Content = new FontIcon { Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 }
        };
        ToolTipService.SetToolTip(deleteBtn, "Désinstaller");
        deleteBtn.Click += async (_, _) => await DeleteWebAppAsync(app);
        Grid.SetColumn(deleteBtn, 3);
        headerGrid.Children.Add(deleteBtn);

        var body = new StackPanel();
        body.Children.Add(headerGrid);
        body.Children.Add(new TextBlock
        {
            Text = app.RootDomain,
            Opacity = 0.6,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var alwaysOnTop = new ToggleSwitch
        {
            OnContent = "Toujours au premier plan",
            OffContent = "Fenêtre normale",
            IsOn = app.AlwaysOnTop,
            Margin = new Thickness(0, 8, 0, -6)
        };
        alwaysOnTop.Toggled += (_, _) =>
        {
            app.AlwaysOnTop = alwaysOnTop.IsOn;
            _webApps.Upsert(app);
        };
        body.Children.Add(alwaysOnTop);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
        var openBtn = new Button { Content = "Ouvrir", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        openBtn.Click += (_, _) => LaunchWebAppWindow(app);
        actions.Children.Add(openBtn);
        body.Children.Add(actions);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = body
        };
    }

    private void LaunchWebAppWindow(PulseWebApp app)
    {
        var appWindow = new PulseAppWindow(app, _profile);
        appWindow.Activate();
        appWindow.InitializeBrowserSurface();
    }

    private async Task RenameWebAppAsync(PulseWebApp app)
    {
        var nameBox = new TextBox { Header = "Nom de l'application", Text = app.Title, MaxLength = 60, MinWidth = 320 };
        var dialog = new ContentDialog
        {
            Title = "Renommer l'application",
            Content = nameBox,
            PrimaryButtonText = "Renommer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var title = nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title) || title == app.Title) return;

        app.Title = title;
        try { InstallShortcutsForApp(app, app.HasDesktopShortcut); }
        catch (Exception ex) { WinUiRuntimeTrace.Write($"Shortcut rename failed: {ex.GetType().Name}"); }
        _webApps.Upsert(app);
        RefreshWebAppsPanel();
        StatusText.Text = $"Application renommée en {title}.";
    }

    private async Task DeleteWebAppAsync(PulseWebApp app)
    {
        var confirm = new ContentDialog
        {
            Title = "Désinstaller cette application ?",
            Content = $"{app.Title}\n{app.RootDomain}\n\nLe raccourci sera supprimé. La session et les favoris ne sont pas affectés.",
            PrimaryButtonText = "Désinstaller",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        if (!string.IsNullOrWhiteSpace(app.ShortcutFileName))
        {
            ShellShortcut.Delete(Path.Combine(ShellShortcut.StartMenuAppsFolder(), app.ShortcutFileName));
            ShellShortcut.Delete(Path.Combine(ShellShortcut.DesktopFolder(), app.ShortcutFileName));
        }

        if (!string.IsNullOrWhiteSpace(app.IconFile))
        {
            try { File.Delete(Path.Combine(_profile.WebAppIconsDir, app.IconFile)); } catch { }
        }

        _webApps.Remove(app.Id);
        RefreshWebAppsPanel();
        StatusText.Text = $"{app.Title} désinstallée.";
    }

    // ── Installation depuis la page active ────────────────────────────────────

    private async void InstallAppMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode)
        {
            StatusText.Text = "Applications indisponibles en mode invité.";
            return;
        }

        var tab = CurrentTab();
        if (tab is null || !BookmarkStore.IsWebUrl(tab.Address))
        {
            StatusText.Text = "Aucune page web active à installer.";
            return;
        }

        if (string.IsNullOrWhiteSpace(tab.IconPath))
        {
            await CaptureFaviconForTabAsync(tab);
        }

        var rootDomain = RootDomainOf(tab.Address);
        var existing = _webApps.FindByRootDomain(rootDomain);

        var nameBox = new TextBox
        {
            Header = "Nom de l'application",
            Text = existing?.Title ?? (string.IsNullOrWhiteSpace(tab.Title) ? DisplayTitle(tab.Address) : tab.Title),
            MaxLength = 60,
            MinWidth = 340
        };
        var desktopCheck = new CheckBox
        {
            Content = "Ajouter aussi un raccourci sur le Bureau",
            IsChecked = existing?.HasDesktopShortcut ?? false
        };
        var info = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.65,
            FontSize = 12,
            Text = $"Ouvre {rootDomain} dans une fenêtre dédiée, sans onglets ni barre d'adresse. La session de ce site sera automatiquement conservée après fermeture de Pulse Browser. Les protections (bloqueur de pubs/trackers, anti-télémétrie, HTTPS) restent actives."
        };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(nameBox);
        panel.Children.Add(desktopCheck);
        panel.Children.Add(info);

        var dialog = new ContentDialog
        {
            Title = existing is null ? "Installer comme application" : "Mettre à jour l'application",
            Content = panel,
            PrimaryButtonText = existing is null ? "Installer" : "Mettre à jour",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var title = nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title)) title = rootDomain;

        var app = existing ?? new PulseWebApp();
        app.Title = title;
        app.Url = tab.Address;
        app.RootDomain = rootDomain;

        try
        {
            if (!string.IsNullOrWhiteSpace(tab.IconPath) &&
                FaviconQuality.IsUsablePngFile(tab.IconPath))
            {
                Directory.CreateDirectory(_profile.WebAppIconsDir);
                var pngBytes = await File.ReadAllBytesAsync(tab.IconPath);
                var icoBytes = IcoWriter.WrapPngAsIco(pngBytes);
                var iconFile = $"{app.Id}.ico";
                await File.WriteAllBytesAsync(Path.Combine(_profile.WebAppIconsDir, iconFile), icoBytes);
                app.IconFile = iconFile;
            }
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Web app icon generation skipped: {ex.GetType().Name}");
        }

        _webApps.Upsert(app);

        // Synergie sessions éphémères (0.46.0-dev) : installer une app suppose
        // vouloir y rester connecté après fermeture de Pulse Browser.
        SetTrustedSessionSite(app.RootDomain, trusted: true);

        try
        {
            InstallShortcutsForApp(app, desktopCheck.IsChecked == true);
            StatusText.Text = $"{title} installée comme application.";
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Shortcut creation failed: {ex.GetType().Name}");
            StatusText.Text = $"Application enregistrée, mais le raccourci n'a pas pu être créé : {ex.Message}";
        }

        ShowPanel(WebAppsPanel, "Applications");
        RefreshWebAppsPanel();
    }

    // Crée/replace le raccourci Menu Démarrer (et Bureau si demandé). Supprime
    // l'ancien fichier d'abord si le nom (donc le titre) a changé, pour éviter
    // de laisser un raccourci obsolète à côté du nouveau.
    private void InstallShortcutsForApp(PulseWebApp app, bool wantDesktop)
    {
        if (!string.IsNullOrWhiteSpace(app.ShortcutFileName))
        {
            ShellShortcut.Delete(Path.Combine(ShellShortcut.StartMenuAppsFolder(), app.ShortcutFileName));
            ShellShortcut.Delete(Path.Combine(ShellShortcut.DesktopFolder(), app.ShortcutFileName));
        }

        var fileName = SanitizeShortcutFileName(app.Title, app.Id) + ".lnk";
        app.ShortcutFileName = fileName;

        var exePath = Path.Combine(AppContext.BaseDirectory, "PulseBrowser.WinUI.exe");
        var iconPath = ResolveShortcutIconPath(app);
        var description = $"{app.Title} (application Pulse)";

        var startMenuPath = Path.Combine(ShellShortcut.StartMenuAppsFolder(), fileName);
        ShellShortcut.Create(startMenuPath, exePath, $"--app={app.Id}", iconPath, description);

        if (wantDesktop)
        {
            var desktopPath = Path.Combine(ShellShortcut.DesktopFolder(), fileName);
            ShellShortcut.Create(desktopPath, exePath, $"--app={app.Id}", iconPath, description);
        }

        app.HasDesktopShortcut = wantDesktop;
        _webApps.Upsert(app);
    }

    private void RepairInvalidWebAppIconsAndShortcuts()
    {
        foreach (var app in _webApps.All())
        {
            if (string.IsNullOrWhiteSpace(app.IconFile)) continue;

            var customIcon = Path.Combine(_profile.WebAppIconsDir, app.IconFile);
            if (FaviconQuality.IsUsablePngBackedIcoFile(customIcon)) continue;

            try { File.Delete(customIcon); } catch { }
            app.IconFile = null;
            _webApps.Upsert(app);

            try
            {
                InstallShortcutsForApp(app, app.HasDesktopShortcut);
                WinUiRuntimeTrace.Write($"Web app generic icon repaired: {app.Id}");
            }
            catch (Exception ex)
            {
                WinUiRuntimeTrace.Write($"Web app icon repair shortcut skipped: {ex.GetType().Name}");
            }
        }
    }

    private string? ResolveShortcutIconPath(PulseWebApp app)
    {
        if (!string.IsNullOrWhiteSpace(app.IconFile))
        {
            var custom = Path.Combine(_profile.WebAppIconsDir, app.IconFile);
            if (FaviconQuality.IsUsablePngBackedIcoFile(custom)) return custom;
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "Assets", "PulseBrowser.ico");
        return File.Exists(fallback) ? fallback : null;
    }

    private static string SanitizeShortcutFileName(string title, string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "Application Pulse";
        if (cleaned.Length > 50) cleaned = cleaned[..50];
        return $"{cleaned} - {id[..Math.Min(8, id.Length)]}";
    }
}
