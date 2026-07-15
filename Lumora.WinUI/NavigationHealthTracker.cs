namespace Lumora.WinUI;

// ── Suivi de l'état de navigation par onglet ─────────────────────────────────
// Classe PURE : aucune dépendance UI ni WebView2, compilée aussi dans
// Lumora.Tests. Elle possède les dictionnaires par-onglet qui étaient
// auparavant des champs de MainWindow (et dont l'ordre d'arrivée non garanti
// des événements WebView2 avait causé le bug de la 0.76). MainWindow lui confie
// l'état et les décisions, et se contente d'agir sur les verdicts rendus
// (afficher une barre, naviguer, apprendre un déménagement).
//
// Rien n'est persisté : ce suivi est éphémère, à la durée d'une navigation.

public enum MainDocumentSignalKind
{
    None,
    PermanentRedirect,
    HttpError
}

// Verdict rendu par l'observation d'une réponse du document principal.
//  - PermanentRedirect : 301/308 vers une autre adresse (à apprendre).
//  - HttpError : 5xx sur le document (site en panne). TriggerPendingFailure
//    indique qu'un échec « Unknown » avait été armé en attente de cette réponse
//    (cf. l'ordre non garanti des événements), et que la barre de reprise doit
//    donc être proposée immédiatement.
public readonly record struct MainDocumentSignal(
    MainDocumentSignalKind Kind,
    int TabId = 0,
    int Status = 0,
    string? FromUri = null,
    string? ToUri = null,
    bool TriggerPendingFailure = false,
    string? PendingFailedAddress = null)
{
    public static readonly MainDocumentSignal None = new(MainDocumentSignalKind.None);
}

public sealed class NavigationHealthTracker
{
    // URI du document principal en cours de navigation → id d'onglet. Indexé par
    // URI (et par id d'onglet ci-dessous), JAMAIS par instance CoreWebView2 :
    // les wrappers .NET d'un même objet WinRT n'ont pas d'identité de référence
    // fiable (vérifié en pratique, cf. commentaire d'origine en 0.76).
    private readonly Dictionary<string, int> _navigatingDocumentUris = new(StringComparer.OrdinalIgnoreCase);

    // Erreur HTTP (5xx) observée sur le document principal, par onglet.
    private readonly Dictionary<int, (string Uri, int Status)> _mainDocumentHttpErrors = new();

    // Échec « Unknown » armé en attente de la réponse 5xx retardataire, par onglet.
    private readonly Dictionary<int, string> _pendingUnknownFailures = new();

    // Adresses demandées explicitement par l'utilisateur via l'UI Lumora :
    // jamais bloquées par le filtre anti-redirection publicitaire.
    private readonly HashSet<string> _explicitNavigationUris = new(StringComparer.OrdinalIgnoreCase);

    // Horodatages des popups OUVERTES (autorisées) par onglet opener : sert au
    // plafond « une popup par geste » et à la détection de tab-under. L'heure
    // est injectée par l'appelant pour rester pur et testable.
    private readonly Dictionary<int, List<DateTimeOffset>> _openedPopupsByTab = new();

    // Deux window.open à moins d'une seconde d'écart relèvent du même geste
    // utilisateur : aucun humain ne clique deux liens en moins d'une seconde
    // avec l'INTENTION d'ouvrir deux fenêtres.
    private static readonly TimeSpan PopupGestureWindow = TimeSpan.FromSeconds(1);

    // Après une popup, l'opener qui se redirige lui-même dans la foulée exécute
    // le pattern tab-under (streaming/pub). Les redirections légitimes après
    // login arrivent bien plus tard (le temps que l'utilisateur s'authentifie).
    private static readonly TimeSpan TabUnderWindow = TimeSpan.FromSeconds(3);

    // Budget de trace diagnostic : les premières réponses de chaque navigation.
    private int _responseTraceBudget;

    // ── Suivi du document principal ──────────────────────────────────────────

    // isRedirect : un saut de redirection prolonge la navigation en cours — la
    // chaîne d'URIs est conservée, car la réponse 301 du saut précédent peut
    // arriver APRÈS le NavigationStarting suivant et doit encore être reconnue.
    public void TrackNavigationStart(int? tabId, string uri, bool isRedirect)
    {
        if (tabId is not int id) return;
        if (!isRedirect)
        {
            RemoveNavigatingUrisOfTab(id);
            _mainDocumentHttpErrors.Remove(id);
            _pendingUnknownFailures.Remove(id);
        }

        _navigatingDocumentUris[DocumentUriKey(uri)] = id;
        _responseTraceBudget = 5;
    }

    public void ForgetTab(int tabId)
    {
        RemoveNavigatingUrisOfTab(tabId);
        _mainDocumentHttpErrors.Remove(tabId);
        _pendingUnknownFailures.Remove(tabId);
        _openedPopupsByTab.Remove(tabId);
    }

    public bool IsMainDocument(string? uri, out int tabId) =>
        _navigatingDocumentUris.TryGetValue(DocumentUriKey(uri), out tabId);

    // Diagnostic : vrai pour les premières réponses d'une navigation, décrémente
    // le budget. Réservé au journal de trace.
    public bool ShouldTraceResponse()
    {
        if (_responseTraceBudget <= 0) return false;
        _responseTraceBudget--;
        return true;
    }

    // Classe une réponse du document principal en verdict typé. status et
    // locationHeader sont extraits par l'appelant (WebView2), passés en
    // primitifs pour garder cette classe pure et testable.
    public MainDocumentSignal ClassifyMainDocumentResponse(string? requestUri, int? status, string? locationHeader)
    {
        if (!IsMainDocument(requestUri, out var tabId) || status is not int code)
        {
            return MainDocumentSignal.None;
        }

        if (code is 301 or 308)
        {
            if (!string.IsNullOrWhiteSpace(locationHeader) &&
                Uri.TryCreate(requestUri, UriKind.Absolute, out var req) &&
                Uri.TryCreate(req, locationHeader, out var target))
            {
                return new MainDocumentSignal(
                    MainDocumentSignalKind.PermanentRedirect,
                    FromUri: requestUri,
                    ToUri: target.ToString());
            }

            return MainDocumentSignal.None;
        }

        if (code is >= 500 and < 600)
        {
            _mainDocumentHttpErrors[tabId] = (requestUri ?? string.Empty, code);

            // Consommer l'échec « Unknown » en attente, qu'il corresponde ou non
            // (une navigation ne peut avoir qu'un échec en vol) ; la barre n'est
            // proposée que si l'URI correspond bien à cette réponse.
            var pendingRemoved = _pendingUnknownFailures.Remove(tabId, out var failed);
            var matches = pendingRemoved && UrisRoughlyEqual(failed, requestUri);

            return new MainDocumentSignal(
                MainDocumentSignalKind.HttpError,
                TabId: tabId,
                Status: code,
                TriggerPendingFailure: matches,
                PendingFailedAddress: matches ? failed : null);
        }

        return MainDocumentSignal.None;
    }

    // Appelé quand un NavigationCompleted échoue en « Unknown » : arme l'échec
    // pour que la réponse 5xx retardataire déclenche la barre.
    public void ArmPendingUnknownFailure(int tabId, string address) =>
        _pendingUnknownFailures[tabId] = address;

    // Consomme l'erreur HTTP du document principal pour cet onglet, s'il y en a une.
    public int? TakeMainDocumentHttpError(int tabId) =>
        _mainDocumentHttpErrors.Remove(tabId, out var error) ? error.Status : null;

    // ── Navigations explicites ───────────────────────────────────────────────

    public void RegisterExplicitNavigation(string address) =>
        _explicitNavigationUris.Add(DocumentUriKey(address));

    // Consomme (une seule fois) le marqueur « demandé explicitement » pour cette URI.
    public bool TakeExplicitNavigation(string? uri) =>
        _explicitNavigationUris.Remove(DocumentUriKey(uri));

    // ── Popups ouvertes et tab-under ─────────────────────────────────────────

    // À appeler quand une popup est réellement OUVERTE (verdict Allow*).
    public void RegisterPopupOpened(int openerTabId, DateTimeOffset now)
    {
        if (!_openedPopupsByTab.TryGetValue(openerTabId, out var stamps))
        {
            stamps = new List<DateTimeOffset>();
            _openedPopupsByTab[openerTabId] = stamps;
        }

        stamps.RemoveAll(stamp => now - stamp > TabUnderWindow);
        stamps.Add(now);
    }

    // Nombre de popups déjà ouvertes par cet onglet dans la fenêtre du geste
    // courant : ≥ 1 signifie que le geste a déjà « consommé » sa popup.
    public int CountPopupsInGestureWindow(int openerTabId, DateTimeOffset now)
    {
        return _openedPopupsByTab.TryGetValue(openerTabId, out var stamps)
            ? stamps.Count(stamp => now - stamp <= PopupGestureWindow)
            : 0;
    }

    // Vrai si cet onglet a ouvert une popup il y a moins de TabUnderWindow :
    // sa propre navigation cross-domaine immédiate est alors suspecte.
    public bool HadRecentPopup(int openerTabId, DateTimeOffset now)
    {
        return _openedPopupsByTab.TryGetValue(openerTabId, out var stamps) &&
               stamps.Any(stamp => now - stamp <= TabUnderWindow);
    }

    // ── Interne ──────────────────────────────────────────────────────────────

    private void RemoveNavigatingUrisOfTab(int tabId)
    {
        foreach (var key in _navigatingDocumentUris
                     .Where(pair => pair.Value == tabId)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _navigatingDocumentUris.Remove(key);
        }
    }

    private static string DocumentUriKey(string? uri) => (uri ?? string.Empty).TrimEnd('/');

    private static bool UrisRoughlyEqual(string? a, string? b) =>
        a is not null && b is not null &&
        a.TrimEnd('/').Equals(b.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
