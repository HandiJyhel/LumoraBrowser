using Microsoft.UI.Xaml;
using Lumora.Privacy.NetworkBlocker;

namespace Lumora.WinUI;

// ── Renforcement anti-publicité : popups et détournements de navigation ─────
// Le bloqueur réseau filtre les requêtes DANS les pages ; ce partiel ferme les
// deux portes restantes constatées sur les sites agressifs :
//  1. les popups (window.open) publicitaires — politique pure : PopupPolicy ;
//  2. les détournements de l'onglet lui-même vers un domaine répertorié
//     publicitaire (clic capturé → location.href), avec barre « Continuer
//     quand même » pour garder l'utilisateur aux commandes. Une adresse
//     demandée explicitement (barre d'adresse, favori, suggestion) n'est
//     JAMAIS bloquée.
public sealed partial class MainWindow
{
    // L'état d'exemption (navigations explicites, « Continuer quand même ») vit
    // dans _navHealth (cf. MainWindow.SiteNotFound.cs).
    private string? _pendingAdBlockedUrl;

    private NetworkBlockerModule? NetworkBlocker => _privacy.Get<NetworkBlockerModule>();

    // ── Popups ───────────────────────────────────────────────────────────────

    // Vrai si la popup doit être bloquée ; journalise et met à jour le bouclier.
    private bool BlockPopupIfUnwanted(string? popupUri, string? openerUri, bool isUserInitiated)
    {
        var blocker = NetworkBlocker;
        var verdict = PopupPolicy.Decide(
            popupUri,
            openerUri,
            isUserInitiated,
            _uiSettings.PopupBlockerEnabled && blocker?.IsEnabled == true,
            host => blocker?.IsBlocked(host) == true,
            host => blocker?.IsWhitelisted(host) == true);

        if (verdict == PopupVerdict.Allow)
        {
            return false;
        }

        _privacy.RecordManualBlock(
            "popup-blocker",
            "Bloqueur de popups",
            string.IsNullOrWhiteSpace(popupUri) ? "about:blank" : popupUri,
            openerUri ?? string.Empty);
        UpdateToolbarPrivacyIndicator();
        StatusText.Text = verdict == PopupVerdict.BlockAdDomain
            ? $"Popup publicitaire bloquee : {DisplayTitle(popupUri ?? string.Empty)}"
            : "Popup automatique bloquee.";
        WinUiRuntimeTrace.Write($"Popup blocked ({verdict}): {popupUri}");
        return true;
    }

    // ── Détournement de navigation vers un domaine publicitaire ─────────────

    private bool ShouldStrictBlockNavigation(string uri)
    {
        // Une navigation demandée par l'UI Lumora (barre d'adresse, favori,
        // suggestion, bouton de reprise) n'est jamais bloquée : l'utilisateur
        // sait où il va. Le marqueur est consommé une seule fois.
        if (_navHealth.TakeExplicitNavigation(uri))
        {
            return false;
        }

        if (!_uiSettings.StrictAdBlockEnabled || !BookmarkStore.IsWebUrl(uri))
        {
            return false;
        }

        var blocker = NetworkBlocker;
        if (blocker is null || !blocker.IsEnabled ||
            !Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (_navHealth.IsAdContinueAllowed(SiteRelocationStore.RootOf(parsed.Host)))
        {
            return false;
        }

        // Domaine entier répertorié publicitaire uniquement : les règles de
        // sous-chaîne (chemins « /ads/ »...) produiraient des faux positifs sur
        // des pages légitimes.
        return blocker.IsBlocked(parsed.Host);
    }

    private void OfferAdBlockedContinue(string blockedUrl, string? fromUri)
    {
        _pendingAdBlockedUrl = blockedUrl;
        var host = Uri.TryCreate(blockedUrl, UriKind.Absolute, out var parsed) ? parsed.Host : blockedUrl;
        AdBlockedText.Text = $"Navigation bloquee : {host} est repertorie comme domaine publicitaire.";
        AdBlockedBar.Visibility = Visibility.Visible;

        _privacy.RecordManualBlock("strict-ad-block", "Blocage des redirections publicitaires", blockedUrl, fromUri ?? string.Empty);
        UpdateToolbarPrivacyIndicator();
        StatusText.Text = $"Navigation publicitaire bloquee : {host}";
        WinUiRuntimeTrace.Write($"Ad navigation blocked: {blockedUrl}");
    }

    private void HideAdBlockedBar()
    {
        AdBlockedBar.Visibility = Visibility.Collapsed;
        _pendingAdBlockedUrl = null;
    }

    private void AdBlockedContinue_Click(object sender, RoutedEventArgs e)
    {
        var url = _pendingAdBlockedUrl;
        HideAdBlockedBar();
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return;
        }

        // Autorisation pour la session : le domaine ne re-bloquera pas à chaque
        // clic, mais rien n'est persisté (la whitelist des paramètres reste le
        // choix durable).
        _navHealth.AllowAdContinue(SiteRelocationStore.RootOf(parsed.Host));
        _navHealth.RegisterExplicitNavigation(url);
        NavigateCurrentTab(url, DisplayTitle(url));
    }

    private void AdBlockedDismiss_Click(object sender, RoutedEventArgs e) =>
        HideAdBlockedBar();
}
