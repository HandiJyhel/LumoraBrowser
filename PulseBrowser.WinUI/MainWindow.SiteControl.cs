using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using PulseBrowser.WinUI.PasswordManager;

namespace PulseBrowser.WinUI;

public sealed partial class MainWindow
{
    // ── Centre du site actuel ────────────────────────────────────────────────
    // Vue locale qui réunit ce que Pulse sait du domaine visible : protection,
    // session/cookies, identifiants du coffre et historique.

    private sealed record CurrentSiteInfo(string Address, string Host, string RootDomain);
    private sealed record SitePermissionSelection(string RootDomain, string Kind);

    private void SiteControlMenu_Click(object sender, RoutedEventArgs e) =>
        ShowSiteControlForCurrentPage();

    private void ShowSiteControlForCurrentPage()
    {
        if (CurrentSite() is not { } site)
        {
            StatusText.Text = "Aucun site web actif.";
            return;
        }

        ShowPanel(SiteControlPanel, $"Site actuel: {site.RootDomain}");
        _ = RefreshSiteControlAsync(site);
    }

    private async void SiteControlRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSite() is { } site)
        {
            await RefreshSiteControlAsync(site);
        }
    }

    private void SiteControlReturnButton_Click(object sender, RoutedEventArgs e)
    {
        var tab = CurrentTab();
        ShowPanel(BrowserPanel, tab?.Title ?? "Page web");
    }

    private async void SiteControlForgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSite() is not { } site) return;
        await ForgetSessionSiteAsync(site.RootDomain);
        await RefreshSiteControlAsync(site);
    }

    private void SiteControlTrustToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressSiteControlTrustToggle) return;
        if (CurrentSite() is not { } site) return;

        SetTrustedSessionSite(site.RootDomain, SiteControlTrustToggle.IsOn);
        _ = RefreshSiteControlAsync(site);
    }

    private async void SiteControlOpenPasswordsButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSite() is not { } site) return;
        if (_isGuestMode)
        {
            StatusText.Text = "Coffre indisponible en mode invite.";
            return;
        }

        if (!await RequireVaultAccessAsync())
        {
            StatusText.Text = "Acces au coffre refuse : code incorrect ou annule.";
            return;
        }

        PasswordManagerSearchBox.Text = site.RootDomain;
        ShowPanel(VaultPanel, $"Identifiants: {site.RootDomain}");
        SyncFromBrowserStore();
        RefreshVaultPanel();
    }

    private void SiteControlOpenHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSite() is not { } site) return;

        _historyPanel.SearchTerm = site.RootDomain;
        HistorySearchBox.Text = site.RootDomain;
        RenderHistory();
        ShowPanel(HistoryPanel, $"Historique: {site.RootDomain}");
    }

    private async Task RefreshSiteControlAsync(CurrentSiteInfo site)
    {
        SiteControlTitleText.Text = site.RootDomain;
        SiteControlAddressText.Text = site.Address;

        var blocked = _privacy.PageBlockedCount;
        var isWhitelisted = _uiSettings.PrivacyWhitelist.Contains(site.Host, StringComparer.OrdinalIgnoreCase) ||
                            _uiSettings.PrivacyWhitelist.Contains(site.RootDomain, StringComparer.OrdinalIgnoreCase);
        SiteControlPrivacyText.Text = isWhitelisted
            ? "Ce site est exclu du bloqueur de contenu."
            : blocked == 0
                ? "Protection active. Aucune requete bloquee sur la page visible."
                : $"Protection active. {blocked} requete{(blocked > 1 ? "s" : "")} bloquee{(blocked > 1 ? "s" : "")} sur la page visible.";

        var cookieCount = await CountCookiesForRootDomainAsync(site.RootDomain);
        SiteControlSessionText.Text = cookieCount == 0
            ? "Aucun cookie de session detecte pour ce domaine."
            : $"{cookieCount} cookie(s) detecte(s) pour ce domaine.";

        _suppressSiteControlTrustToggle = true;
        try
        {
            SiteControlTrustToggle.IsOn = IsTrustedSessionSite(site.RootDomain);
        }
        finally
        {
            _suppressSiteControlTrustToggle = false;
        }

        SiteControlForgetButton.IsEnabled = cookieCount > 0;

        var passwordCount = PasswordCountForRootDomain(site.RootDomain);
        SiteControlPasswordText.Text = passwordCount switch
        {
            null => "Coffre verrouille ou indisponible.",
            0 => "Aucun identifiant local connu pour ce domaine.",
            1 => "1 identifiant local connu pour ce domaine.",
            _ => $"{passwordCount} identifiants locaux connus pour ce domaine."
        };
        SiteControlOpenPasswordsButton.IsEnabled = !_isGuestMode;

        var historyEntries = HistoryEntriesForRootDomain(site.RootDomain).ToList();
        SiteControlHistoryText.Text = historyEntries.Count == 0
            ? "Aucune page de ce domaine dans l'historique local."
            : $"{historyEntries.Count} visite(s) locale(s) retrouvee(s).";

        RenderSitePermissions(site.RootDomain);
        RenderSiteRecentHistory(historyEntries.Take(5).ToList());
        StatusText.Text = $"Centre du site: {site.RootDomain}";
    }

    private void RenderSitePermissions(string rootDomain)
    {
        var allowed = 0;
        var blocked = 0;
        foreach (var descriptor in SitePermissionPolicy.KnownPermissions)
        {
            var state = SitePermissionPolicy.StateFor(_uiSettings.SitePermissions, rootDomain, descriptor.Key);
            if (state == SitePermissionPolicy.Allow) allowed++;
            if (state == SitePermissionPolicy.Block) blocked++;
        }

        SiteControlPermissionsText.Text = allowed == 0 && blocked == 0
            ? "Pulse demandera confirmation quand ce site réclame une permission sensible."
            : $"{allowed} permission(s) autorisee(s), {blocked} permission(s) bloquee(s) pour ce site.";

        _suppressSitePermissionUi = true;
        try
        {
            SiteControlPermissionsPanel.Children.Clear();
            foreach (var descriptor in SitePermissionPolicy.KnownPermissions)
            {
                SiteControlPermissionsPanel.Children.Add(BuildSitePermissionRow(rootDomain, descriptor));
            }
        }
        finally
        {
            _suppressSitePermissionUi = false;
        }
    }

    private UIElement BuildSitePermissionRow(string rootDomain, SitePermissionDescriptor descriptor)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labels = new StackPanel { Spacing = 1 };
        labels.Children.Add(new TextBlock
        {
            Text = descriptor.Label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        labels.Children.Add(new TextBlock
        {
            Text = descriptor.Detail,
            FontSize = 12,
            Opacity = 0.66,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(labels, 0);
        row.Children.Add(labels);

        var combo = new ComboBox
        {
            Width = 150,
            Tag = new SitePermissionSelection(rootDomain, descriptor.Key),
            VerticalAlignment = VerticalAlignment.Center
        };
        combo.Items.Add(new ComboBoxItem { Content = "Demander", Tag = SitePermissionPolicy.Ask });
        combo.Items.Add(new ComboBoxItem { Content = "Autoriser", Tag = SitePermissionPolicy.Allow });
        combo.Items.Add(new ComboBoxItem { Content = "Bloquer", Tag = SitePermissionPolicy.Block });
        combo.SelectionChanged += SitePermissionCombo_SelectionChanged;

        var state = SitePermissionPolicy.StateFor(_uiSettings.SitePermissions, rootDomain, descriptor.Key);
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), state, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                break;
            }
        }

        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);

        return row;
    }

    private void SitePermissionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSitePermissionUi) return;
        if (sender is not ComboBox combo ||
            combo.Tag is not SitePermissionSelection selection ||
            combo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var state = item.Tag?.ToString() ?? SitePermissionPolicy.Ask;
        SitePermissionPolicy.SetState(_uiSettings.SitePermissions, selection.RootDomain, selection.Kind, state);
        SaveUiSettings();
        RenderSitePermissions(selection.RootDomain);
        StatusText.Text = $"{SitePermissionPolicy.StateLabel(state)} : {selection.Kind} pour {selection.RootDomain}.";
    }

    private void CoreWebView2_PermissionRequested(CoreWebView2 sender, CoreWebView2PermissionRequestedEventArgs args)
    {
        if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return;
        }

        var rootDomain = RootDomainOf(uri.Host);
        var permissionKey = SitePermissionPolicy.NormalizeKind(args.PermissionKind.ToString());
        var state = SitePermissionPolicy.StateFor(_uiSettings.SitePermissions, rootDomain, permissionKey);

        if (state == SitePermissionPolicy.Allow)
        {
            args.State = CoreWebView2PermissionState.Allow;
        }
        else if (state == SitePermissionPolicy.Block)
        {
            args.State = CoreWebView2PermissionState.Deny;
        }
        else
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            StatusText.Text = $"{SitePermissionPolicy.StateLabel(state)} : {permissionKey} pour {rootDomain}.";
        });
    }

    private string BuildShieldSiteSummary(string domain)
    {
        var root = RootDomainOf(domain);
        var trust = IsTrustedSessionSite(root)
            ? "session conservee"
            : "session ephemere";
        var passwordCount = PasswordCountForRootDomain(root);
        var passwordPart = passwordCount is null
            ? "coffre verrouille"
            : passwordCount == 0
                ? "aucun identifiant"
                : $"{passwordCount} identifiant(s)";
        return $"{root} : {trust}, {passwordPart}.";
    }

    private CurrentSiteInfo? CurrentSite()
    {
        var address = _browserView?.Source?.ToString();
        if (!BookmarkStore.IsWebUrl(address ?? string.Empty))
        {
            address = CurrentTab()?.Address;
        }

        if (!BookmarkStore.IsWebUrl(address ?? string.Empty) ||
            !Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
        var root = RootDomainOf(host);
        return new CurrentSiteInfo(address!, host, root);
    }

    private async Task<int> CountCookiesForRootDomainAsync(string rootDomain)
    {
        var core = _browserView?.CoreWebView2;
        if (core is null) return 0;

        try
        {
            var cookies = await core.CookieManager.GetCookiesAsync(string.Empty);
            return cookies.Count(cookie =>
                RootDomainOf(cookie.Domain).Equals(rootDomain, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Site control cookie count skipped: {error.GetType().Name}");
            return 0;
        }
    }

    private int? PasswordCountForRootDomain(string rootDomain)
    {
        if (_isGuestMode || _vault.IsLocked) return null;

        try
        {
            return _passwordManager.List(new PasswordManagerSearchOptions(rootDomain)).Count;
        }
        catch
        {
            return null;
        }
    }

    private IEnumerable<HistoryEntry> HistoryEntriesForRootDomain(string rootDomain)
    {
        return _historyPanel.Store.AllEntries().Where(entry =>
        {
            if (!Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri)) return false;
            return RootDomainOf(uri.Host).Equals(rootDomain, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void RenderSiteRecentHistory(IReadOnlyList<HistoryEntry> entries)
    {
        SiteControlRecentHistoryPanel.Children.Clear();
        SiteControlRecentHistoryPanel.Children.Add(new TextBlock
        {
            Text = "Dernieres pages du site",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0)
        });

        if (entries.Count == 0)
        {
            SiteControlRecentHistoryPanel.Children.Add(new TextBlock
            {
                Text = "Aucune page recente.",
                Opacity = 0.65
            });
            return;
        }

        foreach (var entry in entries)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(entry.Title) ? entry.Url : entry.Title,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 0);
            row.Children.Add(title);

            var open = new Button
            {
                Content = "Ouvrir",
                Padding = new Thickness(10, 3, 10, 3),
                Tag = entry
            };
            open.Click += (_, _) =>
            {
                NavigateCurrentTab(entry.Url, entry.Title);
                ShowPanel(BrowserPanel, entry.Title);
            };
            Grid.SetColumn(open, 1);
            row.Children.Add(open);

            SiteControlRecentHistoryPanel.Children.Add(new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"],
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Child = row
            });
        }
    }
}
