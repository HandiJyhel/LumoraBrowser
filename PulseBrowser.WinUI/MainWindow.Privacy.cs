using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using PulseBrowser.Privacy;
using PulseBrowser.Privacy.NetworkBlocker;
using PulseBrowser.Privacy.ParameterCleaner;
using PulseBrowser.Privacy.HttpsEnforcer;
using PulseBrowser.Privacy.CnameUncloaker;
using PulseBrowser.Privacy.CosmeticFilter;
using PulseBrowser.Privacy.ConsentManager;

namespace PulseBrowser.WinUI;

public sealed partial class MainWindow
{
    // ── Privacy ───────────────────────────────────────────────────────────────

    private void InitPrivacyEngine()
    {
        _networkBlocker = new NetworkBlockerModule { IsEnabled = _uiSettings.NetworkBlockerEnabled };
        _networkBlocker.SetUserWhitelist(_uiSettings.PrivacyWhitelist);
        _networkBlocker.StatusChanged += msg => DispatcherQueue.TryEnqueue(() =>
        {
            if (PrivacyStatusText is not null)
                PrivacyStatusText.Text = msg;
        });

        _privacy.Register(_networkBlocker);
        _privacy.Register(new ParameterCleanerModule { IsEnabled = _uiSettings.ParameterCleanerEnabled });
        _privacy.Register(new HttpsEnforcerModule    { IsEnabled = _uiSettings.HttpsEnforcerEnabled });
        _privacy.Register(new CnameUncloakerModule(_networkBlocker) { IsEnabled = _uiSettings.CnameUncloakerEnabled });

        _cosmeticFilter = new CosmeticFilterModule { IsEnabled = _uiSettings.CosmeticFilterEnabled };
        _consentModule  = new ConsentManagerModule { IsEnabled = _uiSettings.ConsentManagerEnabled };

        _ = InitCosmeticAsync();
    }

    private void ApplyPrivacySettings()
    {
        if (_networkBlocker is not null)
        {
            _networkBlocker.IsEnabled = _uiSettings.NetworkBlockerEnabled;
            _networkBlocker.SetUserWhitelist(_uiSettings.PrivacyWhitelist);
        }

        var cleaner = _privacy.Get<ParameterCleanerModule>();
        if (cleaner is not null) cleaner.IsEnabled = _uiSettings.ParameterCleanerEnabled;

        var enforcer = _privacy.Get<HttpsEnforcerModule>();
        if (enforcer is not null) enforcer.IsEnabled = _uiSettings.HttpsEnforcerEnabled;

        var cname = _privacy.Get<CnameUncloakerModule>();
        if (cname is not null) cname.IsEnabled = _uiSettings.CnameUncloakerEnabled;

        UpdatePrivacyUi();
    }

    private void UpdatePrivacyUi()
    {
        if (PrivacyBlockedCountText is not null)
            PrivacyBlockedCountText.Text = $"{_privacy.BlockedCount:N0} requêtes bloquées";

        var cnameModule = _privacy.Get<CnameUncloakerModule>();
        var cnameCount  = cnameModule?.DetectedCount ?? 0;
        if (PrivacyRuleCountText is not null && _networkBlocker is not null)
            PrivacyRuleCountText.Text = $"{_networkBlocker.RuleCount:N0} règles · {cnameCount} CNAME cloakés détectés";

        var lu = _networkBlocker?.LastListUpdate();
        if (PrivacyLastUpdateText is not null)
            PrivacyLastUpdateText.Text = lu.HasValue
                ? $"Dernière mise à jour : {lu.Value.ToLocalTime():dd/MM/yyyy}"
                : "Listes non téléchargées";
    }

    private void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (_privacy.ShouldBlock(args.Request.Uri, sender.Source ?? string.Empty))
            args.Response = sender.Environment.CreateWebResourceResponse(null, 200, "OK", string.Empty);
    }

    private void NetworkBlockerSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.NetworkBlockerEnabled = NetworkBlockerSwitch.IsOn;
        SaveUiSettings();
        ApplyPrivacySettings();
    }

    private void ParameterCleanerSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.ParameterCleanerEnabled = ParameterCleanerSwitch.IsOn;
        SaveUiSettings();
        ApplyPrivacySettings();
    }

    private void HttpsEnforcerSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.HttpsEnforcerEnabled = HttpsEnforcerSwitch.IsOn;
        SaveUiSettings();
        ApplyPrivacySettings();
    }

    private void CnameUncloakerSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.CnameUncloakerEnabled = CnameUncloakerSwitch.IsOn;
        SaveUiSettings();
        ApplyPrivacySettings();
    }

    private async void UpdatePrivacyListsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_networkBlocker is null) return;
        UpdatePrivacyListsButton.IsEnabled = false;
        PrivacyListsUpdateBar.IsOpen = false;
        PrivacyStatusText.Text = "Mise à jour en cours...";
        await _networkBlocker.ForceUpdateAsync();
        await _networkBlocker.LoadAsync();
        UpdatePrivacyUi();
        PrivacyListsUpdateBar.IsOpen = true;
        UpdatePrivacyListsButton.IsEnabled = true;
    }

    private void PrivacyWhitelistAddButton_Click(object sender, RoutedEventArgs e)
    {
        var input = PrivacyWhitelistBox.Text.Trim();
        string domain;
        if (Uri.TryCreate(input.Contains("://") ? input : "https://" + input, UriKind.Absolute, out var parsed))
            domain = parsed.Host.ToLowerInvariant();
        else
            domain = input.ToLowerInvariant().Split('/')[0];

        if (string.IsNullOrWhiteSpace(domain) || !domain.Contains('.')) return;
        if (!_uiSettings.PrivacyWhitelist.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            _uiSettings.PrivacyWhitelist.Add(domain);
            SaveUiSettings();
            ApplyPrivacySettings();
            RenderPrivacyWhitelist();
        }
        PrivacyWhitelistBox.Text = string.Empty;
    }

    private void PrivacyWhitelistRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string domain)
        {
            _uiSettings.PrivacyWhitelist.Remove(domain);
            SaveUiSettings();
            ApplyPrivacySettings();
            RenderPrivacyWhitelist();
        }
    }

    private void RenderPrivacyWhitelist()
    {
        PrivacyWhitelistPanel.Children.Clear();
        foreach (var domain in _uiSettings.PrivacyWhitelist)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = domain, VerticalAlignment = VerticalAlignment.Center });
            var removeBtn = new Button { Content = "Retirer", Tag = domain, Padding = new(6, 2, 6, 2) };
            removeBtn.Click += PrivacyWhitelistRemoveButton_Click;
            row.Children.Add(removeBtn);
            PrivacyWhitelistPanel.Children.Add(row);
        }
    }

    // ── Cosmetic Filter ───────────────────────────────────────────────────────

    // Tous les moteurs vivants (un WebView2 par onglet).
    private IEnumerable<CoreWebView2> AttachedCores() =>
        _tabs.Select(tab => tab.View?.CoreWebView2).OfType<CoreWebView2>();

    private async Task InitCosmeticAsync()
    {
        // Charge seed + listes en cache puis enregistre les scripts sur chaque moteur
        await _networkBlocker!.LoadAsync();

        if (_cosmeticFilter is not null)
            await _cosmeticFilter.LoadAsync();

        await RegisterCosmeticScriptsAsync();
        await RegisterConsentScriptsAsync();

        DispatcherQueue.TryEnqueue(UpdatePrivacyUi);
    }

    private async Task InjectSiteCosmeticAsync(CoreWebView2? core, string pageUri)
    {
        if (core is null || _cosmeticFilter is null || !_cosmeticFilter.IsEnabled) return;
        var script = _cosmeticFilter.BuildSiteInjectionScript(pageUri);
        if (script is null) return;
        try { await core.ExecuteScriptAsync(script); }
        catch { }
    }

    private async Task RegisterCosmeticScriptsAsync()
    {
        foreach (var core in AttachedCores().ToList())
            await RegisterCosmeticScriptOnCoreAsync(core);
    }

    private async Task RegisterCosmeticScriptOnCoreAsync(CoreWebView2 core)
    {
        if (_cosmeticFilter is null) return;

        // Retirer l'ancien script s'il existait sur ce moteur
        if (_cosmeticScriptIds.TryGetValue(core, out var oldId))
        {
            try { core.RemoveScriptToExecuteOnDocumentCreated(oldId); } catch { }
            _cosmeticScriptIds.Remove(core);
        }

        if (_cosmeticFilter.IsEnabled)
        {
            _cosmeticScriptIds[core] = await core.AddScriptToExecuteOnDocumentCreatedAsync(
                _cosmeticFilter.BuildGenericInjectionScript());
        }
        else
        {
            // Retirer le CSS déjà injecté dans la page courante
            try { await core.ExecuteScriptAsync(CosmeticFilterModule.BuildRemovalScript()); } catch { }
        }
    }

    private async void CosmeticFilterSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.CosmeticFilterEnabled = CosmeticFilterSwitch.IsOn;
        SaveUiSettings();
        if (_cosmeticFilter is not null)
            _cosmeticFilter.IsEnabled = _uiSettings.CosmeticFilterEnabled;
        await RegisterCosmeticScriptsAsync();
        UpdatePrivacyUi();
    }

    // ── Consent Manager ───────────────────────────────────────────────────────

    private async Task RegisterConsentScriptsAsync()
    {
        foreach (var core in AttachedCores().ToList())
            await RegisterConsentScriptOnCoreAsync(core);
    }

    private async Task RegisterConsentScriptOnCoreAsync(CoreWebView2 core)
    {
        if (_consentModule is null) return;

        if (_consentScriptIds.TryGetValue(core, out var oldId))
        {
            try { core.RemoveScriptToExecuteOnDocumentCreated(oldId); } catch { }
            _consentScriptIds.Remove(core);
        }

        if (_consentModule.IsEnabled)
            _consentScriptIds[core] = await core.AddScriptToExecuteOnDocumentCreatedAsync(
                _consentModule.BuildInjectionScript());
    }

    private async Task InjectConsentRetryAsync(CoreWebView2? core)
    {
        if (core is null || _consentModule is not { IsEnabled: true }) return;
        try { await core.ExecuteScriptAsync(ConsentManagerModule.RetryScript); } catch { }
    }

    private async void ConsentManagerSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        _uiSettings.ConsentManagerEnabled = ConsentManagerSwitch.IsOn;
        SaveUiSettings();
        if (_consentModule is not null)
            _consentModule.IsEnabled = _uiSettings.ConsentManagerEnabled;
        await RegisterConsentScriptsAsync();
    }

    // ── Bouton bouclier (confidentialité par-site) ────────────────────────────

    private void ShieldFlyout_Opening(object sender, object e)
    {
        var domain = _currentPageDomain;
        if (string.IsNullOrEmpty(domain))
        {
            ShieldDomainText.Text       = "Aucune page active";
            ShieldBlockedCountText.Text = string.Empty;
            ShieldSiteSummaryText.Text  = string.Empty;
            ShieldSiteExcludeToggle.IsEnabled = false;
            ShieldSiteCenterButton.IsEnabled = false;
            return;
        }

        ShieldDomainText.Text = domain;
        var count = _privacy.PageBlockedCount;
        ShieldBlockedCountText.Text = count == 0
            ? "Aucune requete bloquee sur cette page"
            : $"{count} requete{(count > 1 ? "s" : "")} bloquee{(count > 1 ? "s" : "")} sur cette page";
        ShieldSiteSummaryText.Text = BuildShieldSiteSummary(domain);

        ShieldSiteExcludeToggle.IsEnabled = true;
        ShieldSiteCenterButton.IsEnabled = true;
        _suppressShieldToggle = true;
        try
        {
            ShieldSiteExcludeToggle.IsOn =
                _uiSettings.PrivacyWhitelist.Contains(domain, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _suppressShieldToggle = false;
        }
    }

    private void ShieldSiteExcludeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressShieldToggle) return;
        var domain = _currentPageDomain;
        if (string.IsNullOrEmpty(domain)) return;

        if (ShieldSiteExcludeToggle.IsOn)
        {
            if (!_uiSettings.PrivacyWhitelist.Contains(domain, StringComparer.OrdinalIgnoreCase))
                _uiSettings.PrivacyWhitelist.Add(domain);
        }
        else
        {
            _uiSettings.PrivacyWhitelist.RemoveAll(
                d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase));
        }

        _networkBlocker?.SetUserWhitelist(_uiSettings.PrivacyWhitelist);
        SaveUiSettings();
        RenderPrivacyWhitelist();
    }

    private void ShieldSettingsLink_Click(object sender, RoutedEventArgs e)
    {
        ShieldFlyout.Hide();
        StorageCurrentFolderText.Text = _profile.ProfileDir;
        ShowPanel(SettingsPanel, "Parametres");
        SettingsSectionNavigation.Visibility = Visibility.Collapsed;
        SettingsSectionStartup.Visibility    = Visibility.Collapsed;
        SettingsSectionVault.Visibility      = Visibility.Collapsed;
        SettingsSectionProfile.Visibility    = Visibility.Collapsed;
        SettingsSectionStorage.Visibility    = Visibility.Collapsed;
        SettingsSectionPrivacy.Visibility    = Visibility.Visible;
        SettingsNavPrivacy.IsChecked         = true;
        UpdatePrivacyUi();
    }

    private void ShieldSiteCenterButton_Click(object sender, RoutedEventArgs e)
    {
        ShieldFlyout.Hide();
        ShowSiteControlForCurrentPage();
    }
}
