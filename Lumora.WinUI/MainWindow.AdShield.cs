using Microsoft.UI.Xaml;
using Lumora.Privacy.NetworkBlocker;

namespace Lumora.WinUI;

// ── Renforcement anti-publicité : popups et détournements de navigation ─────
// Le bloqueur réseau filtre les requêtes DANS les pages ; ce partiel ferme les
// portes restantes constatées sur les sites agressifs :
//  1. les popups (window.open) publicitaires — politique pure : PopupPolicy,
//     avec plafond « une popup par geste » et ouverture en arrière-plan sur les
//     sites sous pression publicitaire (0.78.2) ;
//  2. les détournements de l'onglet lui-même vers un domaine répertorié
//     publicitaire (clic capturé → location.href) ;
//  3. le tab-under : l'onglet qui vient d'ouvrir une popup et se redirige
//     lui-même cross-domaine dans la foulée (0.78.2).
// Dans les cas 2 et 3, barre « Continuer quand même » pour garder l'utilisateur
// aux commandes. Une adresse demandée explicitement (barre d'adresse, favori,
// suggestion) n'est JAMAIS bloquée.
public sealed partial class MainWindow
{
    // L'état d'exemption (navigations explicites, « Continuer quand même ») vit
    // dans _navHealth (cf. MainWindow.SiteNotFound.cs).
    private string? _pendingAdBlockedUrl;

    // Nombre de requêtes bloquées sur la page à partir duquel le site est
    // considéré « sous pression publicitaire » : ses popups, même sur geste,
    // s'ouvrent alors sans voler le focus.
    private const int AdPressurePageBlockThreshold = 3;

    private NetworkBlockerModule? NetworkBlocker => _privacy.Get<NetworkBlockerModule>();

    // ── Popups ───────────────────────────────────────────────────────────────

    private PopupVerdict DecidePopupVerdict(string? popupUri, string? openerUri, bool isUserInitiated, int? openerTabId)
    {
        var blocker = NetworkBlocker;
        var popupsForGesture = openerTabId is int id
            ? _navHealth.CountPopupsInGestureWindow(id, DateTimeOffset.Now)
            : 0;

        return PopupPolicy.Decide(
            popupUri,
            openerUri,
            isUserInitiated,
            _uiSettings.PopupBlockerEnabled && blocker?.IsEnabled == true,
            host => blocker?.IsBlocked(host) == true,
            host => blocker?.IsWhitelisted(host) == true,
            popupsForGesture,
            openerUnderAdPressure: _privacy.PageBlockedCount >= AdPressurePageBlockThreshold);
    }

    // Journalise un blocage de popup et met à jour le bouclier.
    private void ReportBlockedPopup(PopupVerdict verdict, string? popupUri, string? openerUri)
    {
        _privacy.RecordManualBlock(
            "popup-blocker",
            "Bloqueur de popups",
            string.IsNullOrWhiteSpace(popupUri) ? "about:blank" : popupUri,
            openerUri ?? string.Empty);
        UpdateToolbarPrivacyIndicator();
        StatusText.Text = verdict switch
        {
            PopupVerdict.BlockAdDomain => $"Popup publicitaire bloquee : {DisplayTitle(popupUri ?? string.Empty)}",
            PopupVerdict.BlockGestureFlood => "Rafale de popups bloquee (une seule fenetre par clic).",
            _ => "Popup automatique bloquee."
        };
        WinUiRuntimeTrace.Write($"Popup blocked ({verdict}): {popupUri}");
    }

    // ── Détournements de l'onglet : domaine listé et tab-under ──────────────

    private enum AdNavigationVerdict
    {
        Allow,
        BlockAdDomain,
        BlockTabUnder
    }

    private AdNavigationVerdict ClassifyNavigationForAdShield(int? tabId, string? fromUri, string toUri)
    {
        // Une navigation demandée par l'UI Lumora (barre d'adresse, favori,
        // suggestion, bouton de reprise) n'est jamais bloquée : l'utilisateur
        // sait où il va. Le marqueur est consommé une seule fois.
        if (_navHealth.TakeExplicitNavigation(toUri))
        {
            return AdNavigationVerdict.Allow;
        }

        if (!_uiSettings.StrictAdBlockEnabled || !BookmarkStore.IsWebUrl(toUri))
        {
            return AdNavigationVerdict.Allow;
        }

        var blocker = NetworkBlocker;
        if (blocker is null || !blocker.IsEnabled ||
            !Uri.TryCreate(toUri, UriKind.Absolute, out var parsed))
        {
            return AdNavigationVerdict.Allow;
        }

        var targetRoot = SiteRelocationStore.RootOf(parsed.Host);
        if (_navHealth.IsAdContinueAllowed(targetRoot) || blocker.IsWhitelisted(parsed.Host))
        {
            return AdNavigationVerdict.Allow;
        }

        // Domaine entier répertorié publicitaire uniquement : les règles de
        // sous-chaîne (chemins « /ads/ »...) produiraient des faux positifs sur
        // des pages légitimes.
        if (blocker.IsBlocked(parsed.Host))
        {
            return AdNavigationVerdict.BlockAdDomain;
        }

        // Tab-under : l'onglet a ouvert une popup il y a un instant et se
        // redirige maintenant cross-domaine — pattern publicitaire classique
        // (la popup porte le contenu, l'onglet d'origine part vers la pub).
        // Les destinations d'authentification restent libres (retour OAuth).
        if (tabId is int id &&
            _navHealth.HadRecentPopup(id, DateTimeOffset.Now) &&
            Uri.TryCreate(fromUri, UriKind.Absolute, out var from) &&
            !string.Equals(SiteRelocationStore.RootOf(from.Host), targetRoot, StringComparison.OrdinalIgnoreCase) &&
            !PopupPolicy.IsKnownIdentityProviderHost(toUri) &&
            !PopupPolicy.IsLikelyAuthenticationPopup(toUri, isUserInitiated: true) &&
            !blocker.IsWhitelisted(from.Host))
        {
            return AdNavigationVerdict.BlockTabUnder;
        }

        return AdNavigationVerdict.Allow;
    }

    private void OfferAdBlockedContinue(string blockedUrl, string? fromUri, AdNavigationVerdict verdict)
    {
        _pendingAdBlockedUrl = blockedUrl;
        var host = Uri.TryCreate(blockedUrl, UriKind.Absolute, out var parsed) ? parsed.Host : blockedUrl;
        var isTabUnder = verdict == AdNavigationVerdict.BlockTabUnder;
        AdBlockedText.Text = isTabUnder
            ? $"Redirection suspecte bloquee : la page voulait partir vers {host} juste apres avoir ouvert une popup."
            : $"Navigation bloquee : {host} est repertorie comme domaine publicitaire.";
        AdBlockedBar.Visibility = Visibility.Visible;

        _privacy.RecordManualBlock(
            isTabUnder ? "tab-under-block" : "strict-ad-block",
            isTabUnder ? "Blocage des tab-under" : "Blocage des redirections publicitaires",
            blockedUrl,
            fromUri ?? string.Empty);
        UpdateToolbarPrivacyIndicator();
        StatusText.Text = isTabUnder
            ? $"Redirection tab-under bloquee : {host}"
            : $"Navigation publicitaire bloquee : {host}";
        WinUiRuntimeTrace.Write($"Ad navigation blocked ({verdict}): {blockedUrl}");
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
