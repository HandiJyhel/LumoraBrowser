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
    private readonly Dictionary<CoreWebView2, int> _tabIdByCore = new();
    private readonly Dictionary<CoreWebView2, List<CoreWebView2Frame>> _framesByCore = new();
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
        if (!_tabIdByCore.TryAdd(core, tabId))
        {
            return;
        }

        core.WebMessageReceived += Core_WebMessageReceived;
        _framesByCore[core] = new List<CoreWebView2Frame>();
        core.FrameCreated += Core_FrameCreated;

        var script = await LoadCredentialCaptureScriptAsync();
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        Lumora.WinUI.WinUiRuntimeTrace.Write($"CredentialService: script de capture attache (onglet {tabId})");
    }

    public void Detach(CoreWebView2 core)
    {
        if (!_tabIdByCore.Remove(core))
        {
            return;
        }

        core.WebMessageReceived -= Core_WebMessageReceived;
        core.FrameCreated -= Core_FrameCreated;
        _framesByCore.Remove(core);
    }

    // Suivi des iframes de premier niveau : le script de capture y est injecte
    // d'office (AddScriptToExecuteOnDocumentCreatedAsync couvre toutes les frames),
    // mais ExecuteScriptAsync sur le CoreWebView2 ne touche QUE la frame
    // principale. Pour remplir un formulaire de connexion loge dans une iframe
    // cross-origin, il faut appeler chaque CoreWebView2Frame individuellement.
    private void Core_FrameCreated(object? sender, CoreWebView2FrameCreatedEventArgs e)
    {
        if (sender is not CoreWebView2 core || !_framesByCore.TryGetValue(core, out var frames))
        {
            return;
        }

        var frame = e.Frame;
        frames.Add(frame);
        frame.Destroyed += (_, _) => frames.Remove(frame);
        // Seul le rapport differe de remplissage est ecoute ici : router les
        // page-states des iframes vers l'UI ferait clignoter/masquer la barre au
        // gre des frames tierces (pubs, widgets) qui publient un etat sans champ.
        frame.WebMessageReceived += (_, args) => HandleFrameMessage(core, args.WebMessageAsJson);
    }

    private void HandleFrameMessage(CoreWebView2 core, string webMessageAsJson)
    {
        var message = ParseMessage(webMessageAsJson);
        if (message is null)
        {
            return;
        }

        var type = GetString(message, "t") ?? GetString(message, "type");
        if (type == "nova.credential.fill-report")
        {
            PublishFillReport(core, message);
        }
    }

    private void PublishFillReport(CoreWebView2 core, JsonObject message)
    {
        // Meme garde que le page-state : seul l'onglet visible pilote l'UI. Sens
        // volontairement "fail-closed" : si ce core n'est pas retrouvé dans
        // _tabIdByCore (identité de wrapper WinRT potentiellement instable, piège déjà
        // documenté dans NavigationHealthTracker.cs), on ne publie PAS plutôt que de
        // publier en silence pour un onglet non identifié (audit du 2026-08-19).
        if (!_tabIdByCore.TryGetValue(core, out var ownerTabId) ||
            ownerTabId != ActiveTabIdProvider?.Invoke())
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

        var wantsUsername = !string.IsNullOrEmpty(credential.Username);
        var filledUsername = false;
        var filledPassword = false;
        var foundAnyField = false;
        string? scriptError = null;

        var targets = new List<Func<string, Task<string>>>
        {
            script => activeCore.ExecuteScriptAsync(script).AsTask()
        };
        if (_framesByCore.TryGetValue(activeCore, out var frames))
        {
            // Copie : la liste peut bouger (frame detruite) pendant les awaits.
            foreach (var frame in frames.ToArray())
            {
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
            return new CredentialFillResult(false, "Champ detecte, mais le site a refuse l'ecriture.");
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
        if (_framesByCore.TryGetValue(activeCore, out var frames))
        {
            foreach (var frame in frames.ToArray())
            {
                try
                {
                    var result = ParseScriptResult(await frame.ExecuteScriptAsync(script));
                    if (result is { Success: true }) return result;
                    lastFailure ??= result;
                }
                catch { }
            }
        }

        return lastFailure ?? new CredentialFillResult(false, "Champ nouveau mot de passe introuvable.");
    }

    private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = ParseMessage(e.WebMessageAsJson);
        if (message is null)
        {
            return;
        }

        var type = GetString(message, "t") ?? GetString(message, "type");

        if (type == "nova.credential.fill-report")
        {
            if (sender is CoreWebView2 reportCore)
            {
                PublishFillReport(reportCore, message);
            }
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
            if (sender is CoreWebView2 core &&
                (!_tabIdByCore.TryGetValue(core, out var ownerTabId) ||
                 ownerTabId != ActiveTabIdProvider?.Invoke()))
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

        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

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
