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

        // Pression publicitaire : mesurée par le bouclier réseau (comptage de
        // requêtes bloquées), par une navigation déjà détournée sur ce même
        // onglet (0.84.0.4), OU par un popup déjà ouvert avant sur ce même
        // onglet (0.84.0.5) — ce troisième signal cible le site qui rouvre un
        // onglet à CHAQUE clic séparé : chaque clic étant un geste neuf, aucun
        // des deux premiers signaux ne s'accumule jamais sur ce schéma précis.
        var openerUnderAdPressure = PageUnderAdPressure ||
            (openerTabId is int pressureTabId &&
                (_navHealth.HadBlockedNavigation(pressureTabId) || _navHealth.HasOpenedPopupBefore(pressureTabId)));

        return PopupPolicy.Decide(
            popupUri,
            openerUri,
            isUserInitiated,
            _uiSettings.PopupBlockerEnabled && blocker?.IsEnabled == true,
            host => blocker?.IsBlocked(host) == true,
            host => blocker?.IsWhitelisted(host) == true,
            popupsForGesture,
            openerUnderAdPressure: openerUnderAdPressure);
    }

    // Journalise un blocage de popup et met à jour le bouclier. Les verdicts
    // « confiants » (domaine publicitaire, automatique, rafale, pression)
    // restent silencieux comme depuis 0.78.3.1 ; BlockPendingUserChoice est un
    // jugement, pas une certitude — il alimente en plus l'icône de
    // récupération (0.84.0.6) au lieu de disparaître sans recours.
    private void ReportBlockedPopup(PopupVerdict verdict, string? popupUri, string? openerUri, int? openerTabId)
    {
        _privacy.RecordManualBlock(
            "popup-blocker",
            "Bloqueur de popups",
            string.IsNullOrWhiteSpace(popupUri) ? "about:blank" : popupUri,
            openerUri ?? string.Empty);
        UpdateToolbarPrivacyIndicator();
        StatusText.Text = verdict switch
        {
            PopupVerdict.BlockAdDomain => $"Popup publicitaire bloquée : {DisplayTitle(popupUri ?? string.Empty)}",
            PopupVerdict.BlockGestureFlood => "Rafale de popups bloquée (une seule fenêtre par clic).",
            PopupVerdict.BlockUnderAdPressure => $"Popup parasite bloquée : {DisplayTitle(popupUri ?? string.Empty)}",
            PopupVerdict.BlockPendingUserChoice => $"Popup en attente : {DisplayTitle(popupUri ?? string.Empty)} (icône de récupération)",
            _ => "Popup automatique bloquée."
        };
        WinUiRuntimeTrace.Write($"Popup blocked ({verdict}): {popupUri}");

        if (verdict == PopupVerdict.BlockPendingUserChoice && openerTabId is int tabId)
        {
            _navHealth.RecordPendingPopup(tabId, string.IsNullOrWhiteSpace(popupUri) ? "about:blank" : popupUri);
            RefreshPopupRecoveryIndicator();
        }
    }

    // ── Détournements de l'onglet ────────────────────────────────────────────

    private NavigationVerdict ClassifyNavigationForAdShield(int? tabId, string? fromUri, string toUri, bool isUserInitiated)
    {
        var blocker = NetworkBlocker;
        var userInitiatedChain = tabId is int navigationTabId &&
                                 _navHealth.IsUserInitiatedNavigationChain(navigationTabId);
        var verdict = NavigationHijackPolicy.Decide(
            fromUri,
            toUri,
            wasExplicitlyRequested: _navHealth.TakeExplicitNavigation(toUri),
            isUserInitiated: isUserInitiated || userInitiatedChain,
            strictBlockEnabled: _uiSettings.StrictAdBlockEnabled && blocker?.IsEnabled == true,
            host => blocker?.IsBlocked(host) == true,
            host => blocker?.IsWhitelisted(host) == true,
            openedPopupRecently: tabId is int id && _navHealth.HadRecentPopup(id, DateTimeOffset.Now));

        // Mémorisé pour durcir les popups suivants du même onglet (0.84.0.4),
        // même si le réseau publicitaire en cause échappe au bouclier réseau.
        if (verdict != NavigationVerdict.Allow && tabId is int blockedTabId)
        {
            _navHealth.RecordBlockedNavigation(blockedTabId);
        }

        return verdict;
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
                ? $"Redirection parasite bloquée : {host}"
                : $"Navigation publicitaire bloquée : {host}";
        }

        WinUiRuntimeTrace.Write($"Ad navigation blocked ({verdict}): {blockedUrl}");
    }
}
