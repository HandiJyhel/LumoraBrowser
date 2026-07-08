using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace PulseBrowser.WinUI.Credentials;

internal sealed class CredentialService
{
    // Un WebView2 par onglet : le service observe tous les moteurs attachés, mais
    // seul le moteur ACTIF (onglet visible) pilote l'interface (page-state, autofill).
    private readonly HashSet<CoreWebView2> _attachedCores = new();
    private CoreWebView2? _activeCore;
    private readonly Dictionary<string, string> _recentUsersByRoot = new(StringComparer.OrdinalIgnoreCase);

    public event Action<CredentialCapture>? CredentialCaptured;
    public event Action<CredentialPageState>? PageStateChanged;

    public async Task AttachAsync(CoreWebView2 core)
    {
        if (!_attachedCores.Add(core))
        {
            return;
        }

        core.WebMessageReceived += Core_WebMessageReceived;
        _activeCore ??= core;

        var script = await LoadScriptAsync("CredentialCaptureScript.js");
        await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    public void Detach(CoreWebView2 core)
    {
        if (!_attachedCores.Remove(core))
        {
            return;
        }

        core.WebMessageReceived -= Core_WebMessageReceived;
        if (ReferenceEquals(_activeCore, core))
        {
            _activeCore = null;
        }
    }

    public void SetActiveCore(CoreWebView2? core) => _activeCore = core;

    public async Task<CredentialFillResult> FillAsync(VaultCredential credential)
    {
        if (_activeCore is null)
        {
            return new CredentialFillResult(false, "Moteur web indisponible.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            username = credential.Username,
            password = credential.Password,
            origin = credential.Origin
        });

        try
        {
            var script = await LoadScriptAsync("CredentialAutofillScript.js");
            var executable = script.Replace("__PULSE_CREDENTIAL_PAYLOAD__", payload);
            var resultJson = await _activeCore.ExecuteScriptAsync(executable);
            var result = ParseScriptResult(resultJson);
            return result ?? new CredentialFillResult(true, "Identifiants remplis.");
        }
        catch (Exception ex)
        {
            return new CredentialFillResult(false, $"Remplissage impossible: {ex.Message}");
        }
    }

    private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = ParseMessage(e.WebMessageAsJson);
        if (message is null)
        {
            return;
        }

        var type = GetString(message, "t") ?? GetString(message, "type");

        if (type == "pulse.credential.username")
        {
            RememberUsername(message);
            return;
        }

        if (type == "pulse.credential.page-state")
        {
            // L'état de page ne pilote l'UI (barre de remplissage, statut d'attente)
            // que pour l'onglet visible ; un onglet d'arrière-plan ne doit pas
            // déclencher de proposition pour une page que l'utilisateur ne voit pas.
            if (sender is CoreWebView2 core && !ReferenceEquals(core, _activeCore))
            {
                return;
            }

            PublishPageState(message);
            return;
        }

        if (type is not ("pulse.credential.candidate" or "cred"))
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

        PageStateChanged?.Invoke(new CredentialPageState(
            PublicSuffixService.OriginOf(origin),
            loginUrl,
            hasUsernameField,
            hasPasswordField,
            username.Trim(),
            source,
            confidence));
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

    private static async Task<string> LoadScriptAsync(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Credentials", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(Environment.CurrentDirectory, "PulseBrowser.WinUI", "Credentials", fileName),
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
