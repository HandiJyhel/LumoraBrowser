using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI.Credentials;

internal sealed class CredentialService
{
    // Un WebView2 par onglet : le service observe tous les moteurs attachés, mais
    // seul le moteur de l'onglet ACTIF pilote l'interface (page-state, autofill).
    //
    // L'onglet propriétaire de chaque moteur est capturé une fois pour toutes à
    // l'attache (AttachAsync(core, tabId)), et comparé à l'ID de l'onglet actif lu
    // EN DIRECT via ActiveTabIdProvider. Aucune comparaison de référence d'objet
    // WebView2/CoreWebView2 : ce sont deux entiers qu'on compare, la même source
    // d'ID que celle qui pilote déjà la barre d'adresse et le titre de fenêtre.
    //
    // Tout est indexe par ID d'onglet, capture dans les gestionnaires a
    // l'attache : l'objet CoreWebView2 recu en "sender" d'un evenement n'est PAS
    // le meme wrapper que celui passe a AttachAsync (confirme en direct le
    // 2026-09-24 : found=False, ReferenceEquals=False). L'ancien
    // Dictionary<CoreWebView2, int> rejetait donc silencieusement TOUS les
    // page-states et rapports de remplissage, et ne suivait aucune iframe.
    private readonly Dictionary<int, TabAttachment> _attachments = new();
    private readonly Dictionary<int, List<TrackedFrame>> _framesByTab = new();

    private sealed record TabAttachment(
        CoreWebView2 Core,
        Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2WebMessageReceivedEventArgs> MessageHandler,
        Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2FrameCreatedEventArgs> FrameHandler);

    // Iframe suivie et adresse du document qu'elle affiche (voir
    // CredentialFillTargetPolicy) : CurrentUrl reste null tant qu'une
    // navigation est en cours ou que rien n'a encore ete charge - aucun secret
    // n'est envoye dans ce cas. Un seul objet par iframe, stocke dans la liste
    // de l'onglet : aucun dictionnaire indexe par wrapper WinRT.
    private sealed class TrackedFrame(CoreWebView2Frame frame)
    {
        public CoreWebView2Frame Frame { get; } = frame;
        public string? CurrentUrl { get; set; }
        public string? PendingUrl { get; set; }
    }
    private readonly Dictionary<string, string> _recentUsersByRoot = new(StringComparer.OrdinalIgnoreCase);

    public event Action<CredentialCapture>? CredentialCaptured;
    public event Action<CredentialPageState>? PageStateChanged;
    // Correction differee d'un remplissage : le script re-verifie la valeur ~300 ms
    // apres coup (les SPA peuvent l'effacer au re-render) et previent si elle a saute.
    public event Action<CredentialFillResult>? FillReported;

    public Func<CoreWebView2?>? ActiveCoreProvider { get; set; }
    public Func<int?>? ActiveTabIdProvider { get; set; }

    public async Task AttachAsync(CoreWebView2 core, int tabId)
    {
        // Un onglet reveille (veille) ou dont le moteur a ete recree recoit un
        // NOUVEAU CoreWebView2 sous le meme ID : l'ancien branchement est
        // remplace, jamais conserve (sinon le Coffre restait muet sur cet
        // onglet jusqu'a la fin de la session - relecture 2026-09-24).
        Detach(tabId);

        var attachment = new TabAttachment(
            core,
            (_, e) => Core_WebMessageReceived(tabId, e),
            (_, e) => Core_FrameCreated(tabId, e));
        _attachments[tabId] = attachment;
        _framesByTab[tabId] = new List<TrackedFrame>();
        core.WebMessageReceived += attachment.MessageHandler;
        core.FrameCreated += attachment.FrameHandler;

        var script = await LoadCredentialCaptureScriptAsync();
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        Lumora.WinUI.WinUiRuntimeTrace.Write($"CredentialService: script de capture attache (onglet {tabId})");
    }

    public void Detach(int tabId)
    {
        if (!_attachments.Remove(tabId, out var attachment))
        {
            return;
        }

        attachment.Core.WebMessageReceived -= attachment.MessageHandler;
        attachment.Core.FrameCreated -= attachment.FrameHandler;
        _framesByTab.Remove(tabId);
    }

    // Suivi des iframes de premier niveau : le script de capture y est injecte
    // d'office (AddScriptToExecuteOnDocumentCreatedAsync couvre toutes les frames),
    // mais ExecuteScriptAsync sur le CoreWebView2 ne touche QUE la frame
    // principale. Pour remplir un formulaire de connexion loge dans une iframe
    // cross-origin, il faut appeler chaque CoreWebView2Frame individuellement.
    private void Core_FrameCreated(int tabId, CoreWebView2FrameCreatedEventArgs e)
    {
        if (!_framesByTab.TryGetValue(tabId, out var frames))
        {
            return;
        }

        var frame = e.Frame;
        var tracked = new TrackedFrame(frame);
        frames.Add(tracked);
        frame.Destroyed += (_, _) => frames.Remove(tracked);
        frame.NavigationStarting += (_, args) =>
        {
            tracked.CurrentUrl = null;
            tracked.PendingUrl = args.Uri;
        };
        frame.ContentLoading += (_, _) => tracked.CurrentUrl = tracked.PendingUrl;
        // Seul le rapport differe de remplissage est ecoute ici : router les
        // page-states des iframes vers l'UI ferait clignoter/masquer la barre au
        // gre des frames tierces (pubs, widgets) qui publient un etat sans champ.
        frame.WebMessageReceived += (_, args) => HandleFrameMessage(tabId, args.WebMessageAsJson);
    }

    private void HandleFrameMessage(int tabId, string webMessageAsJson)
    {
        var message = ParseMessage(webMessageAsJson);
        if (message is null)
        {
            return;
        }

        var type = GetString(message, "t") ?? GetString(message, "type");
        if (type == "nova.credential.fill-report")
        {
            PublishFillReport(tabId, message);
        }
    }

    private void PublishFillReport(int ownerTabId, JsonObject message)
    {
        // Meme garde que le page-state : seul l'onglet visible pilote l'UI.
        if (ownerTabId != ActiveTabIdProvider?.Invoke())
        {
            return;
        }

        var result = new CredentialFillResult(
            GetBool(message, "success") ?? false,
            GetString(message, "message") ?? string.Empty);
        Lumora.WinUI.WinUiRuntimeTrace.Write(
            $"CredentialService: fill-report success={result.Success} message={result.Message}");
        FillReported?.Invoke(result);
    }

    // Remplissage multi-frames : la fonction __novaFillCredential (definie par
    // CredentialCaptureOrchestrator.js dans CHAQUE frame) est appelee d'abord dans la
    // frame principale — qui couvre elle-meme ses iframes same-origin et ses
    // shadow roots — puis dans chaque iframe cross-origin suivie, jusqu'a ce que
    // tout soit rempli. Les resultats partiels s'agregent (ex. identifiant dans
    // la page, mot de passe dans une iframe).
    public async Task<CredentialFillResult> FillAsync(VaultCredential credential)
    {
        var activeCore = ActiveCoreProvider?.Invoke();
        if (activeCore is null)
        {
            return new CredentialFillResult(false, "Moteur web indisponible.");
        }

        // Page principale : elle a pu naviguer entre l'offre et le clic.
        if (!CredentialFillTargetPolicy.IsAllowedTarget(activeCore.Source, credential.Origin, credential.LoginUrl))
        {
            Lumora.WinUI.WinUiRuntimeTrace.Write(
                $"FillAsync refuse: page {PublicSuffixService.OriginOf(activeCore.Source)} hors du site de l'identifiant {credential.Origin}");
            return new CredentialFillResult(false, "Remplissage refusé : cette page n'appartient pas au site de cet identifiant.");
        }

        var wantsUsername = !string.IsNullOrEmpty(credential.Username);
        var filledUsername = false;
        var filledPassword = false;
        var foundAnyField = false;
        string? scriptError = null;

        var targets = new List<Func<string, Task<string>>>
        {
            script => activeCore.ExecuteScriptAsync(script).AsTask()
        };
        if (ActiveTabIdProvider?.Invoke() is int activeTabId &&
            _framesByTab.TryGetValue(activeTabId, out var frames))
        {
            // Copie : la liste peut bouger (frame detruite) pendant les awaits.
            // Seules les iframes du MEME site que l'identifiant recoivent le
            // secret (faille corrigee le 2026-09-24, voir
            // CredentialFillTargetPolicy) - jamais une pub ou un widget tiers.
            foreach (var tracked in frames.ToArray())
            {
                if (!CredentialFillTargetPolicy.IsAllowedTarget(tracked.CurrentUrl, credential.Origin, credential.LoginUrl))
                {
                    continue;
                }
                var frame = tracked.Frame;
                targets.Add(script => frame.ExecuteScriptAsync(script).AsTask());
            }
        }

        foreach (var execute in targets)
        {
            // Ne redemander que ce qui manque, pour ne pas remplir deux fois
            // (la frame principale voit deja les iframes same-origin).
            var payload = JsonSerializer.Serialize(new
            {
                username = filledUsername ? string.Empty : credential.Username,
                password = filledPassword ? string.Empty : credential.Password,
                origin = credential.Origin
            });

            try
            {
                var resultJson = await execute(
                    $"window.__novaFillCredential ? window.__novaFillCredential({payload}) : null");
                var outcome = ParseFillOutcome(resultJson);
                if (outcome is null)
                {
                    continue;
                }

                filledUsername |= outcome.FilledUsername;
                filledPassword |= outcome.FilledPassword;
                foundAnyField |= outcome.FoundUsernameField || outcome.FoundPasswordField;
            }
            catch (Exception ex)
            {
                // Frame detruite/naviguee entre-temps : on passe a la suivante.
                scriptError = ex.Message;
            }

            if (filledPassword && (filledUsername || !wantsUsername))
            {
                break;
            }
        }

        var result = ToFillResult(filledUsername, filledPassword, foundAnyField, scriptError);
        Lumora.WinUI.WinUiRuntimeTrace.Write(
            $"FillAsync: origin={credential.Origin} user={filledUsername} pass={filledPassword} champTrouve={foundAnyField} -> {result.Message}");
        return result;
    }

    private static CredentialFillResult ToFillResult(
        bool filledUsername, bool filledPassword, bool foundAnyField, string? scriptError)
    {
        if (filledUsername && filledPassword)
            return new CredentialFillResult(true, "Identifiants remplis.");
        if (filledPassword)
            return new CredentialFillResult(true, "Mot de passe rempli.");
        if (filledUsername)
            return new CredentialFillResult(true, "Identifiant rempli. Mot de passe attendu.");
        if (foundAnyField)
            return new CredentialFillResult(false, "Champ détecté, mais le site a refusé l'écriture.");
        if (scriptError is not null)
            return new CredentialFillResult(false, $"Remplissage impossible: {scriptError}");
        return new CredentialFillResult(false, "Aucun champ de connexion visible.");
    }

    private sealed record FillOutcome(
        bool FilledUsername,
        bool FilledPassword,
        bool FoundUsernameField,
        bool FoundPasswordField);

    private static FillOutcome? ParseFillOutcome(string rawJson)
    {
        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node is JsonValue value && value.TryGetValue<string>(out var nested))
            {
                node = JsonNode.Parse(nested);
            }

            if (node is not JsonObject obj)
            {
                return null;
            }

            return new FillOutcome(
                GetBool(obj, "filledUsername") ?? false,
                GetBool(obj, "filledPassword") ?? false,
                GetBool(obj, "foundUsernameField") ?? false,
                GetBool(obj, "foundPasswordField") ?? false);
        }
        catch
        {
            return null;
        }
    }

    // Remplit le(s) champ(s) "nouveau mot de passe" (inscription/changement de
    // mot de passe) avec une valeur générée côté app — pas de capture Chromium.
    // window.__novaFillNewPassword est défini par CredentialCapturePasswordModule.js,
    // déjà attaché à ce moment (c'est lui qui a détecté le champ en premier lieu).
    public async Task<CredentialFillResult> FillGeneratedPasswordAsync(string generatedPassword)
    {
        var activeCore = ActiveCoreProvider?.Invoke();
        if (activeCore is null)
        {
            return new CredentialFillResult(false, "Moteur web indisponible.");
        }

        var pageUrl = activeCore.Source;
        if (!CredentialFillTargetPolicy.IsSameSiteFrame(pageUrl, pageUrl))
        {
            return new CredentialFillResult(false, "Remplissage impossible sur cette page.");
        }

        var payload = JsonSerializer.Serialize(generatedPassword);
        var script = $"window.__novaFillNewPassword ? window.__novaFillNewPassword({payload}) : null";
        CredentialFillResult? lastFailure = null;

        try
        {
            var result = ParseScriptResult(await activeCore.ExecuteScriptAsync(script));
            if (result is { Success: true }) return result;
            lastFailure = result;
        }
        catch (Exception ex)
        {
            lastFailure = new CredentialFillResult(false, $"Remplissage impossible: {ex.Message}");
        }

        // Frame principale sans champ : tenter les iframes suivies (meme logique
        // multi-frames que FillAsync, un formulaire d'inscription peut y vivre).
        if (ActiveTabIdProvider?.Invoke() is int activeTabId &&
            _framesByTab.TryGetValue(activeTabId, out var frames))
        {
            foreach (var tracked in frames.ToArray())
            {
                // Meme site que la page uniquement : le mot de passe genere sera
                // enregistre pour CE site, il ne doit jamais partir chez un tiers.
                if (!CredentialFillTargetPolicy.IsSameSiteFrame(tracked.CurrentUrl, pageUrl))
                {
                    continue;
                }
                try
                {
                    var result = ParseScriptResult(await tracked.Frame.ExecuteScriptAsync(script));
                    if (result is { Success: true }) return result;
                    lastFailure ??= result;
                }
                catch { }
            }
        }

        return lastFailure ?? new CredentialFillResult(false, "Champ nouveau mot de passe introuvable.");
    }

    private void Core_WebMessageReceived(int ownerTabId, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = ParseMessage(e.WebMessageAsJson);
        if (message is null)
        {
            return;
        }

        var type = GetString(message, "t") ?? GetString(message, "type");

        // Adresse ATTESTEE par WebView2 (document qui a reellement poste le
        // message), jamais celle que la page declare dans son JSON : sinon
        // evil.com pouvait poster {origin:"https://banque.fr", ...} et obtenir
        // une offre "Mettre a jour le mot de passe pour banque.fr" qui, acceptee,
        // ecrasait la vraie entree du Coffre (relecture securite 2026-09-24).
        if (!Uri.TryCreate(e.Source, UriKind.Absolute, out var attestedSource) ||
            (attestedSource.Scheme != Uri.UriSchemeHttps && attestedSource.Scheme != Uri.UriSchemeHttp))
        {
            return;
        }
        message["origin"] = PublicSuffixService.OriginOf(e.Source);
        message["loginUrl"] = e.Source;
        message.Remove("o");
        message.Remove("login_url");

        if (type == "nova.credential.fill-report")
        {
            PublishFillReport(ownerTabId, message);
            return;
        }

        if (type == "nova.credential.username")
        {
            RememberUsername(message);
            return;
        }

        if (type == "nova.credential.page-state")
        {
            // L'état de page ne pilote l'UI (barre de remplissage, statut d'attente)
            // que pour l'onglet visible ; un onglet d'arrière-plan ne doit pas
            // déclencher de proposition pour une page que l'utilisateur ne voit pas.
            // Comparaison par ID d'onglet (entiers), jamais par référence d'objet.
            // Fail-closed (2026-08-19) : cf. PublishFillReport ci-dessus, même raisonnement.
            if (ownerTabId != ActiveTabIdProvider?.Invoke())
            {
                return;
            }

            PublishPageState(message);
            return;
        }

        if (type is not ("nova.credential.candidate" or "cred"))
        {
            return;
        }

        var origin = GetString(message, "origin") ?? GetString(message, "o") ?? string.Empty;
        var username = GetString(message, "username") ?? GetString(message, "u") ?? string.Empty;
        var password = GetString(message, "password") ?? GetString(message, "p") ?? string.Empty;
        var loginUrl = GetString(message, "loginUrl") ?? GetString(message, "login_url") ?? string.Empty;
        var source = GetString(message, "source") ?? "page";
        var confidence = GetInt(message, "confidence") ?? 0;

        if (string.IsNullOrWhiteSpace(password) || password.Length < 3)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            username = RecentUsernameFor(origin);
        }

        // Identifiant introuvable : on transmet quand meme. Un mot de passe
        // soumis suffit a justifier l'offre d'enregistrement (voir
        // PasswordManagerInteractionService.BuildSaveOffer), l'utilisateur
        // complete l'identifiant dans la barre. Le rejet silencieux d'avant
        // privait de toute offre des qu'une heuristique ratait le champ
        // (bug reel 2026-09-24, dailyuploads.io).

        if (string.IsNullOrWhiteSpace(loginUrl))
        {
            loginUrl = origin;
        }

        CredentialCaptured?.Invoke(new CredentialCapture(
            PublicSuffixService.OriginOf(origin),
            username.Trim(),
            password,
            loginUrl,
            source,
            confidence));
    }

    private void PublishPageState(JsonObject message)
    {
        var origin = GetString(message, "origin") ?? GetString(message, "o") ?? string.Empty;
        var loginUrl = GetString(message, "loginUrl") ?? GetString(message, "login_url") ?? string.Empty;
        var username = GetString(message, "username") ?? GetString(message, "u") ?? string.Empty;
        var source = GetString(message, "source") ?? "page";
        var confidence = GetInt(message, "confidence") ?? 0;
        var hasUsernameField = GetBool(message, "hasUsernameField") ?? false;
        var hasPasswordField = GetBool(message, "hasPasswordField") ?? false;
        var hasEmptyNewPasswordField = GetBool(message, "hasEmptyNewPasswordField") ?? false;

        if (string.IsNullOrWhiteSpace(origin))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            username = RecentUsernameFor(origin);
        }

        if (string.IsNullOrWhiteSpace(loginUrl))
        {
            loginUrl = origin;
        }

        Lumora.WinUI.WinUiRuntimeTrace.Write(
            $"PublishPageState -> PageStateChanged: origin={PublicSuffixService.OriginOf(origin)} loginUrl={loginUrl} hasUser={hasUsernameField} hasPass={hasPasswordField} hasNewPass={hasEmptyNewPasswordField}");

        PageStateChanged?.Invoke(new CredentialPageState(
            PublicSuffixService.OriginOf(origin),
            loginUrl,
            hasUsernameField,
            hasPasswordField,
            username.Trim(),
            source,
            confidence,
            hasEmptyNewPasswordField));
    }

    private void RememberUsername(JsonObject message)
    {
        var origin = GetString(message, "origin") ?? GetString(message, "o") ?? string.Empty;
        var username = GetString(message, "username") ?? GetString(message, "u") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var root = PublicSuffixService.RootDomainOf(origin);
        if (!string.IsNullOrWhiteSpace(root))
        {
            _recentUsersByRoot[root] = username.Trim();
        }
    }

    private string RecentUsernameFor(string origin)
    {
        var root = PublicSuffixService.RootDomainOf(origin);
        return !string.IsNullOrWhiteSpace(root) && _recentUsersByRoot.TryGetValue(root, out var user)
            ? user
            : string.Empty;
    }

    private static JsonObject? ParseMessage(string rawJson)
    {
        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node is JsonObject obj)
            {
                return obj;
            }

            if (node is JsonValue value && value.TryGetValue<string>(out var nested))
            {
                return JsonNode.Parse(nested) as JsonObject;
            }
        }
        catch { }

        return null;
    }

    private static CredentialFillResult? ParseScriptResult(string rawJson)
    {
        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node is JsonValue value && value.TryGetValue<string>(out var nested))
            {
                node = JsonNode.Parse(nested);
            }

            if (node is not JsonObject obj)
            {
                return null;
            }

            return new CredentialFillResult(
                GetBool(obj, "success") ?? false,
                GetString(obj, "message") ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonObject obj, string name) =>
        obj.TryGetPropertyValue(name, out var node) ? node?.GetValue<string>() : null;

    private static int? GetInt(JsonObject obj, string name) =>
        obj.TryGetPropertyValue(name, out var node) &&
        node is JsonValue valueNode &&
        valueNode.TryGetValue<int>(out var value)
            ? value
            : null;

    private static bool? GetBool(JsonObject obj, string name) =>
        obj.TryGetPropertyValue(name, out var node) &&
        node is JsonValue valueNode &&
        valueNode.TryGetValue<bool>(out var value)
            ? value
            : null;

    // Le script de capture est decoupe en modules a responsabilite unique
    // (voir Lumora.WinUI/Credentials/CredentialCapture*.js) : Dom (utilitaires
    // partages) -> Site (page/adresse) -> Password (tout ce qui touche au mot
    // de passe) -> Username (tout ce qui touche a l'identifiant, ne peut lire
    // l'etat de Password qu'via son API) -> Orchestrator (cablage des
    // evenements, cablage seulement). WebView2 n'a qu'une seule entree
    // (AddScriptToExecuteOnDocumentCreatedAsync prend UNE chaine) : on charge
    // et concatene ces fichiers dans cet ordre de dependance avant injection.
    private static readonly string[] CredentialCaptureModules =
    {
        "CredentialCaptureDom.js",
        "CredentialCaptureSiteModule.js",
        "CredentialCapturePasswordModule.js",
        "CredentialCaptureUsernameModule.js",
        "CredentialCaptureOrchestrator.js",
    };

    private static async Task<string> LoadCredentialCaptureScriptAsync()
    {
        var parts = new List<string>(CredentialCaptureModules.Length);
        foreach (var module in CredentialCaptureModules)
        {
            parts.Add(await LoadScriptAsync(module));
        }
        return string.Join("\n;\n", parts);
    }

    private static async Task<string> LoadScriptAsync(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Credentials", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(Environment.CurrentDirectory, "Lumora.WinUI", "Credentials", fileName),
            Path.Combine(Environment.CurrentDirectory, "Credentials", fileName),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }
        }

        throw new FileNotFoundException($"Script introuvable: {fileName}");
    }
}
