using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private async void SiteControlComfortZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSiteComfortUi) return;
        if (CurrentSite() is not { } site) return;
        if (sender is not ComboBox combo || combo.SelectedItem is not ComboBoxItem item) return;
        if (!int.TryParse(item.Tag?.ToString(), out var zoomPercent))
        {
            zoomPercent = SiteComfortPolicy.DefaultZoomPercent;
        }

        SiteComfortPolicy.SetZoomPercent(_uiSettings.SiteComfortRules, site.RootDomain, zoomPercent);
        SaveUiSettings();
        RefreshSiteComfortUi(site.RootDomain);
        await ApplySiteComfortAsync(CurrentTab()?.View, site.Address);
        StatusText.Text = zoomPercent == SiteComfortPolicy.DefaultZoomPercent
            ? $"{site.RootDomain} suit de nouveau le zoom standard."
            : $"{site.RootDomain} s'ouvre maintenant avec un zoom prefere de {zoomPercent} %.";
    }

    private async void SiteControlComfortLargeTextToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressSiteComfortUi) return;
        if (CurrentSite() is not { } site) return;

        SiteComfortPolicy.SetLargeText(_uiSettings.SiteComfortRules, site.RootDomain, SiteControlComfortLargeTextToggle.IsOn);
        SaveUiSettings();
        RefreshSiteComfortUi(site.RootDomain);
        await ApplySiteComfortAsync(CurrentTab()?.View, site.Address);
        StatusText.Text = SiteControlComfortLargeTextToggle.IsOn
            ? $"Texte renforce pour {site.RootDomain}."
            : $"Texte du site {site.RootDomain} revenu au confort global.";
    }

    private async void SiteControlComfortReduceMotionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressSiteComfortUi) return;
        if (CurrentSite() is not { } site) return;

        SiteComfortPolicy.SetReduceMotion(_uiSettings.SiteComfortRules, site.RootDomain, SiteControlComfortReduceMotionToggle.IsOn);
        SaveUiSettings();
        RefreshSiteComfortUi(site.RootDomain);
        await ApplySiteComfortAsync(CurrentTab()?.View, site.Address);
        StatusText.Text = SiteControlComfortReduceMotionToggle.IsOn
            ? $"Animations limitees pour {site.RootDomain}."
            : $"Animations du site {site.RootDomain} revenues au confort global.";
    }

    private async void SiteControlComfortResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSite() is not { } site) return;

        SiteComfortPolicy.Reset(_uiSettings.SiteComfortRules, site.RootDomain);
        SaveUiSettings();
        RefreshSiteComfortUi(site.RootDomain);
        await ApplySiteComfortAsync(CurrentTab()?.View, site.Address);
        StatusText.Text = $"Confort du site reinitialise pour {site.RootDomain}.";
    }

    private void RefreshSiteComfortUi(string rootDomain)
    {
        var zoomPercent = SiteComfortPolicy.ZoomPercentFor(_uiSettings.SiteComfortRules, rootDomain);
        var largeText = SiteComfortPolicy.LargeTextFor(_uiSettings.SiteComfortRules, rootDomain);
        var reduceMotion = SiteComfortPolicy.ReduceMotionFor(_uiSettings.SiteComfortRules, rootDomain);
        var hasOverrides = SiteComfortPolicy.HasOverrides(_uiSettings.SiteComfortRules, rootDomain);

        SiteControlComfortText.Text = hasOverrides
            ? BuildSiteComfortSummary(rootDomain, zoomPercent, largeText, reduceMotion)
            : "Reglage independant du Confort global (Reglages > Confort) : vous pouvez memoriser ici, pour ce site precis uniquement, un zoom, un texte plus lisible ou des animations limitees.";

        _suppressSiteComfortUi = true;
        try
        {
            SelectComboByTag(SiteControlComfortZoomCombo, zoomPercent.ToString(), SiteComfortPolicy.DefaultZoomPercent.ToString());
            SiteControlComfortLargeTextToggle.IsOn = largeText;
            SiteControlComfortReduceMotionToggle.IsOn = reduceMotion;
        }
        finally
        {
            _suppressSiteComfortUi = false;
        }

        SiteControlComfortResetButton.IsEnabled = hasOverrides;
    }

    private static string BuildSiteComfortSummary(string rootDomain, int zoomPercent, bool largeText, bool reduceMotion)
    {
        var details = new List<string>();
        if (zoomPercent != SiteComfortPolicy.DefaultZoomPercent)
        {
            details.Add($"zoom {zoomPercent} %");
        }

        if (largeText)
        {
            details.Add("texte plus lisible");
        }

        if (reduceMotion)
        {
            details.Add("animations reduites");
        }

        return details.Count == 0
            ? $"Aucun confort specifique memorise pour {rootDomain}."
            : $"{rootDomain} reutilisera : {string.Join(", ", details)}.";
    }

    private async Task ApplySiteComfortAsync(WebView2? view, string? address)
    {
        var core = view?.CoreWebView2;
        if (view is null || core is null)
        {
            return;
        }

        var zoomPercent = SiteComfortPolicy.DefaultZoomPercent;
        var largeText = false;
        var reduceMotion = false;

        if (BookmarkStore.IsWebUrl(address ?? string.Empty) &&
            Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
            !string.IsNullOrWhiteSpace(uri.Host))
        {
            var rootDomain = RootDomainOf(uri.Host);
            zoomPercent = SiteComfortPolicy.ZoomPercentFor(_uiSettings.SiteComfortRules, rootDomain);
            largeText = SiteComfortPolicy.LargeTextFor(_uiSettings.SiteComfortRules, rootDomain);
            reduceMotion = SiteComfortPolicy.ReduceMotionFor(_uiSettings.SiteComfortRules, rootDomain);
        }

        try
        {
            await core.ExecuteScriptAsync(BuildSiteComfortScript(zoomPercent, largeText, reduceMotion));
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Site comfort script skipped: {error.GetType().Name}");
        }
    }

    private static string BuildSiteComfortScript(int zoomPercent, bool largeText, bool reduceMotion)
    {
        var css = new List<string>();
        if (zoomPercent != SiteComfortPolicy.DefaultZoomPercent)
        {
            var zoomFactor = zoomPercent / 100d;
            css.Add($"html{{zoom:{zoomFactor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}!important;}}");
        }

        if (largeText)
        {
            css.Add("html{font-size:112.5%!important;-webkit-text-size-adjust:100%!important;}body{line-height:1.55!important;}");
        }

        if (reduceMotion)
        {
            css.Add("html{scroll-behavior:auto!important;}*,*::before,*::after{animation:none!important;transition:none!important;scroll-behavior:auto!important;}");
        }

        var cssText = string.Join(string.Empty, css);
        var serializedCss = JsonSerializer.Serialize(cssText);
        return $$"""
(() => {
  const id = "lumora-site-comfort-style";
  const css = {{serializedCss}};
  let style = document.getElementById(id);
  if (!css) {
    if (style) style.remove();
    return;
  }
  if (!style) {
    style = document.createElement("style");
    style.id = id;
    (document.head || document.documentElement).appendChild(style);
  }
  style.textContent = css;
})();
""";
    }
}
