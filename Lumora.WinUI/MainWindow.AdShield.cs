using Lumora.Privacy.NetworkBlocker;

namespace Lumora.WinUI;

// ── Renforcement anti-publicité : popups et détournements de navigation ─────
// Le bloqueur réseau filtre les requêtes DANS les pages ; ce partiel est la
// colle UI de deux politiques pures :
//  - PopupPolicy : sort des window.open / target=_blank ;
//  - NavigationHijackPolicy : sort des navigations de l'onglet lui-même
//    (domaine répertorié, clic détourné, tab-under).
// Depuis 0.78.3.1, tous les blocages sont SILENCIEUX : pas de barre, pas de
// question — ligne de statut + compteur du bouclier uniquement. Une adresse
// demandée explicitement (barre d'adresse, favori, suggestion) n'est JAMAIS
// bloquée ; la whitelist par site (paramètres) reste la soupape durable.
public sealed partial class MainWindow
{
    // Nombre de requêtes bloquées sur la page à partir duquel le site est
    // considéré « sous pression publicitaire » : ses popups cross-domaine et
    // ses redirections cross-domaine sont alors bloquées net. Aucun comptage
    // de clics : chaque tentative parasite est bloquée, une par une.
    private const int AdPressurePageBlockThreshold = 3;

    private NetworkBlockerModule? NetworkBlocker => _privacy.Get<NetworkBlockerModule>();

    private bool PageUnderAdPressure => _privacy.PageBlockedCount >= AdPressurePageBlockThreshold;

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
            openerUnderAdPressure: PageUnderAdPressure);
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
            PopupVerdict.BlockUnderAdPressure => $"Popup parasite bloquee : {DisplayTitle(popupUri ?? string.Empty)}",
            _ => "Popup automatique bloquee."
        };
        WinUiRuntimeTrace.Write($"Popup blocked ({verdict}): {popupUri}");
    }

    // ── Détournements de l'onglet ────────────────────────────────────────────

    private NavigationVerdict ClassifyNavigationForAdShield(int? tabId, string? fromUri, string toUri)
    {
        var blocker = NetworkBlocker;
        return NavigationHijackPolicy.Decide(
            fromUri,
            toUri,
            wasExplicitlyRequested: _navHealth.TakeExplicitNavigation(toUri),
            strictBlockEnabled: _uiSettings.StrictAdBlockEnabled && blocker?.IsEnabled == true,
            host => blocker?.IsBlocked(host) == true,
            host => blocker?.IsWhitelisted(host) == true,
            pageUnderAdPressure: PageUnderAdPressure,
            openedPopupRecently: tabId is int id && _navHealth.HadRecentPopup(id, DateTimeOffset.Now));
    }

    // Blocage silencieux : journal + bouclier + ligne de statut, rien d'autre.
    private void ReportBlockedNavigation(NavigationVerdict verdict, string blockedUrl, string? fromUri, bool isActiveView)
    {
        var isParasite = verdict == NavigationVerdict.BlockParasite;
        _privacy.RecordManualBlock(
            isParasite ? "parasite-block" : "strict-ad-block",
            isParasite ? "Blocage des redirections parasites" : "Blocage des redirections publicitaires",
            blockedUrl,
            fromUri ?? string.Empty);
        UpdateToolbarPrivacyIndicator();

        if (isActiveView)
        {
            var host = Uri.TryCreate(blockedUrl, UriKind.Absolute, out var parsed) ? parsed.Host : blockedUrl;
            StatusText.Text = isParasite
                ? $"Redirection parasite bloquee : {host}"
                : $"Navigation publicitaire bloquee : {host}";
        }

        WinUiRuntimeTrace.Write($"Ad navigation blocked ({verdict}): {blockedUrl}");
    }
}
