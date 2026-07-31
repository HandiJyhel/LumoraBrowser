using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

// ── Icône de refus de cookies dans la barre d'outils (0.84.0.10) ────────────
// ConsentManagerModule agissait jusqu'ici en silence : rien ne distinguait "le
// script a refusé les cookies" de "il n'y avait rien à refuser". Cette icône
// (œil barré) redonne un signal visible par page, comme Chrome/Edge le font
// pour leurs propres indicateurs d'état (permissions, popups...).
public sealed partial class MainWindow
{
    // Ne PAS chercher l'onglet via ReferenceEquals sur CoreWebView2 (piège connu :
    // la propriété .CoreWebView2 peut rendre un wrapper managé différent à chaque
    // accès pour le même objet natif, donc TabForCore/ReferenceEquals peut échouer
    // silencieusement même sur le bon onglet). e.Source attesté par WebView2 reste
    // fiable pour retrouver l'onglet par correspondance d'URL.
    private void HandleConsentHandledMessage(CoreWebView2? core, string? pageUri, string? method)
    {
        if (core is null || string.IsNullOrWhiteSpace(pageUri)) return;
        var tab = _tabs.FirstOrDefault(t =>
            string.Equals(t.View?.CoreWebView2?.Source, pageUri, StringComparison.Ordinal));
        if (tab is null) return;

        tab.ConsentHandledMethod = method switch
        {
            "panel" => "panel",
            _ => "direct"
        };

        _privacy.RecordManualBlock("consent-manager", "Refus automatique des cookies", pageUri, pageUri);

        if (IsActiveView(tab.View))
        {
            RefreshConsentIndicator();
            UpdateToolbarPrivacyIndicator();
        }
    }

    private void RefreshConsentIndicator()
    {
        var method = TabForView(_browserView)?.ConsentHandledMethod;
        ConsentIndicatorButton.Visibility = method is null ? Visibility.Collapsed : Visibility.Visible;

        ToolTipService.SetToolTip(
            ConsentIndicatorButton,
            method == "panel"
                ? "Cookies essentiels uniquement (préférences ajustées automatiquement)"
                : "Cookies refusés automatiquement sur ce site");
    }

    private void ConsentIndicatorFlyout_Opening(object sender, object e)
    {
        var method = TabForView(_browserView)?.ConsentHandledMethod;
        ConsentIndicatorText.Text = method == "panel"
            ? "Lumora n'a pas trouvé de bouton \"Refuser tout\" direct sur ce site : le panneau de préférences a été ouvert, les cases non essentielles décochées, puis validé."
            : "Lumora a cliqué automatiquement sur le refus des cookies non essentiels pour cette page.";
    }
}
