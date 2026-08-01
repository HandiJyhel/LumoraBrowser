using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Lumora.WinUI.Sessions;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Sessions éphémères ────────────────────────────────────────────────────
    // Modèle produit : les cookies sont la mémoire du site sur l'utilisateur, le
    // coffre vault.lumora est la mémoire de l'utilisateur. Au démarrage, la mémoire
    // des sites est purgée, sauf pour les sites de confiance choisis. La purge a
    // lieu au DÉMARRAGE et non à la fermeture : garantie même après un crash ou
    // un arrêt brutal du PC. Le coffre, hors du dossier WebView2, n'est pas touché.

    private bool _sessionsPurgedThisLaunch;

    // Regroupement des cookies par domaine racine (auth.micromania.fr et
    // www.micromania.fr → micromania.fr) via la Public Suffix List officielle.
    private static string RootDomainOf(string cookieDomain) =>
        Credentials.PublicSuffixService.RootDomainOf(cookieDomain);

    private bool IsTrustedSessionSite(string rootDomain) =>
        _uiSettings.TrustedSessionSites.Contains(rootDomain, StringComparer.OrdinalIgnoreCase);

    // Purge des sessions de la visite précédente. Appelée à l'initialisation du
    // moteur, AVANT la première navigation web, pour qu'aucune page ne se charge
    // avec les anciens cookies.
    private async Task PurgeStartupSessionsAsync(CoreWebView2 core)
    {
        if (_sessionsPurgedThisLaunch || !_uiSettings.SessionPurgeEnabled) return;
        _sessionsPurgedThisLaunch = true;

        try
        {
            if (_uiSettings.TrustedSessionSites.Count == 0)
            {
                // Chemin rapide : un seul appel natif (cookies + stockage des sites),
                // aucune énumération.
                await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllSite);
                UpdateStatusText("Sessions de la visite précédente purgées.");
                WinUiRuntimeTrace.Write("Startup session purge: all site data cleared");
                ExplainSessionPurgeOnce();
                return;
            }

            var removed = await DeleteUntrustedCookiesAsync(core);
            UpdateStatusText($"Sessions purgées ({removed} cookie(s)), sites de confiance conservés.");
            WinUiRuntimeTrace.Write($"Startup session purge: {removed} cookies removed");
            if (removed > 0) ExplainSessionPurgeOnce();
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"Startup session purge skipped: {ex.GetType().Name}");
        }
    }

    // Explique la purge une seule fois dans la vie du profil : InfoBar discrète,
    // jamais republiée automatiquement une fois vue.
    private void ExplainSessionPurgeOnce()
    {
        if (_uiSettings.SessionPurgeExplained) return;
        _uiSettings.SessionPurgeExplained = true;
        _uiSettings.Save(_profile.UiSettingsFile);
        SessionPurgeInfoBar.IsOpen = true;
    }

    private void SessionPurgeInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) =>
        SessionPurgeInfoBar.IsOpen = false;

    // Supprime les cookies hors sites de confiance. Si onlyRootDomain est fourni,
    // supprime au contraire uniquement les cookies de ce domaine racine (action
    // « Oublier ce site »), qu'il soit de confiance ou non.
    private async Task<int> DeleteUntrustedCookiesAsync(CoreWebView2 core, string? onlyRootDomain = null)
    {
        var manager = core.CookieManager;
        var cookies = await manager.GetCookiesAsync(string.Empty);
        var removed = 0;
        foreach (var cookie in cookies)
        {
            var root = RootDomainOf(cookie.Domain);
            if (onlyRootDomain is not null)
            {
                if (!root.Equals(onlyRootDomain, StringComparison.OrdinalIgnoreCase)) continue;
            }
            else if (IsTrustedSessionSite(root))
            {
                continue;
            }

            manager.DeleteCookie(cookie);
            removed++;
        }

        return removed;
    }

    // ── Panneau « Sites connectés » ───────────────────────────────────────────
    // Les cookies ne sont énumérés qu'à l'ouverture du panneau (ou via Actualiser),
    // rien ne tourne en tâche de fond.

    private void SessionsMenu_Click(object sender, RoutedEventArgs e)
    {
        ShowPanel(SessionsPanel, "Sites connectés");
        _ = RefreshSessionsPanelAsync();
    }

    private void SessionsRefreshButton_Click(object sender, RoutedEventArgs e) =>
        _ = RefreshSessionsPanelAsync();

    private async void SessionsForgetAllButton_Click(object sender, RoutedEventArgs e)
    {
        var core = _browserView?.CoreWebView2;
        if (core is null) return;
        var removed = await DeleteUntrustedCookiesAsync(core);
        UpdateStatusText($"Sessions oubliées ({removed} cookie(s)), sites de confiance conservés.");
        await RefreshSessionsPanelAsync();
    }

    private async Task RefreshSessionsPanelAsync()
    {
        SessionsPanelItems.Children.Clear();
        var core = _browserView?.CoreWebView2;
        if (core is null)
        {
            SessionsPanelItems.Children.Add(new TextBlock
            {
                Text = "Moteur web non initialisé.",
                Opacity = 0.65,
                FontSize = AccessibilitySecondaryFontSize(),
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        var cookies = await core.CookieManager.GetCookiesAsync(string.Empty);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cookie in cookies)
        {
            var root = RootDomainOf(cookie.Domain);
            counts[root] = counts.TryGetValue(root, out var n) ? n + 1 : 1;
        }

        // Les sites de confiance sans cookie actif restent visibles pour pouvoir
        // retirer leur statut.
        foreach (var trusted in _uiSettings.TrustedSessionSites)
        {
            if (!counts.ContainsKey(trusted)) counts[trusted] = 0;
        }

        if (counts.Count == 0)
        {
            SessionsPanelItems.Children.Add(new TextBlock
            {
                Text = "Aucune session active. Les sites que vous visiterez apparaîtront ici.",
                Opacity = 0.65,
                FontSize = AccessibilitySecondaryFontSize(),
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var pair in counts.OrderByDescending(p => IsTrustedSessionSite(p.Key))
                                   .ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            SessionsPanelItems.Children.Add(BuildSessionCard(pair.Key, pair.Value));
        }
    }

    private UIElement BuildSessionCard(string rootDomain, int cookieCount)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock
        {
            Text = rootDomain,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = AccessibilityBodyFontSize(),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        info.Children.Add(new TextBlock
        {
            Text = cookieCount > 0 ? $"{cookieCount} cookie(s)" : "Aucun cookie actif",
            Opacity = 0.6,
            FontSize = AccessibilitySecondaryFontSize(),
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var trustToggle = new ToggleSwitch
        {
            OnContent = "Session conservée",
            OffContent = "Purgée au démarrage",
            FontSize = AccessibilitySecondaryFontSize(),
            IsOn = IsTrustedSessionSite(rootDomain),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, -6, 0, -6)
        };
        ApplyNovaControlAccessibility(trustToggle, $"Politique de session pour {rootDomain}");
        ToolTipService.SetToolTip(trustToggle,
            "Site de confiance : sa session survit à la purge du démarrage.");
        // Handler attaché après IsOn pour ne pas déclencher pendant la construction.
        trustToggle.Toggled += (_, _) => SetTrustedSessionSite(rootDomain, trustToggle.IsOn);
        Grid.SetColumn(trustToggle, 1);
        grid.Children.Add(trustToggle);

        var forgetBtn = new Button
        {
            FontSize = AccessibilitySecondaryFontSize(),
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new FontIcon
            {
                Glyph = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14
            }
        };
        ApplyNovaControlAccessibility(forgetBtn, $"Oublier les cookies du site {rootDomain}");
        ToolTipService.SetToolTip(forgetBtn, "Oublier ce site maintenant (supprime ses cookies)");
        forgetBtn.Click += async (_, _) => await ForgetSessionSiteAsync(rootDomain);
        Grid.SetColumn(forgetBtn, 2);
        grid.Children.Add(forgetBtn);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 8, 14, 8),
            Child = grid
        };
    }

    private void SetTrustedSessionSite(string rootDomain, bool trusted)
    {
        if (trusted)
        {
            if (!IsTrustedSessionSite(rootDomain))
                _uiSettings.TrustedSessionSites.Add(rootDomain);
            // L'utilisateur change d'avis : un refus antérieur au login n'a plus lieu d'être.
            _uiSettings.SessionKeepDeclinedSites.RemoveAll(
                d => string.Equals(d, rootDomain, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            _uiSettings.TrustedSessionSites.RemoveAll(
                d => string.Equals(d, rootDomain, StringComparison.OrdinalIgnoreCase));
        }

        _uiSettings.Save(_profile.UiSettingsFile);
        UpdateStatusText(trusted
            ? $"{rootDomain} : session conservée au démarrage."
            : $"{rootDomain} : session purgée au prochain démarrage.");
    }

    // ── Proposition « Rester connecté ? » au login détecté ────────────────────

    private string? _pendingSessionKeepRoot;

    private void MaybeOfferSessionKeep(string origin)
    {
        var root = RootDomainOf(origin);
        if (!SessionKeepAdvisor.ShouldOfferKeepSession(
                _uiSettings.SessionPurgeEnabled,
                _uiSettings.TrustedSessionSites,
                _uiSettings.SessionKeepDeclinedSites,
                root))
        {
            return;
        }

        _pendingSessionKeepRoot = root;
        SessionKeepText.Text = $"Rester connecté à {root} après la fermeture de Lumora ?";
        SessionKeepBar.Visibility = Visibility.Visible;
    }

    private void SessionKeepAccept_Click(object sender, RoutedEventArgs e)
    {
        SessionKeepBar.Visibility = Visibility.Collapsed;
        if (_pendingSessionKeepRoot is not { } root) return;
        _pendingSessionKeepRoot = null;
        SetTrustedSessionSite(root, trusted: true);
    }

    private void SessionKeepDismiss_Click(object sender, RoutedEventArgs e)
    {
        SessionKeepBar.Visibility = Visibility.Collapsed;
        if (_pendingSessionKeepRoot is not { } root) return;
        _pendingSessionKeepRoot = null;
        if (!_uiSettings.SessionKeepDeclinedSites.Contains(root, StringComparer.OrdinalIgnoreCase))
            _uiSettings.SessionKeepDeclinedSites.Add(root);
        _uiSettings.Save(_profile.UiSettingsFile);
    }

    private async Task ForgetSessionSiteAsync(string rootDomain)
    {
        var core = _browserView?.CoreWebView2;
        if (core is null) return;
        var removed = await DeleteUntrustedCookiesAsync(core, rootDomain);
        UpdateStatusText($"{rootDomain} oublié ({removed} cookie(s) supprimés).");
        await RefreshSessionsPanelAsync();
    }

    private void SessionPurgeSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.SessionPurgeEnabled = SessionPurgeSwitch.IsOn;
        _uiSettings.Save(_profile.UiSettingsFile);
        UpdateStatusText(_uiSettings.SessionPurgeEnabled
            ? "Purge des sessions au démarrage activée."
            : "Purge des sessions au démarrage désactivée.");
    }

    private void SessionsManageLink_Click(object sender, RoutedEventArgs e) =>
        SessionsMenu_Click(sender, e);
}
