using System.Text;

namespace Lumora.WinUI;

internal sealed class SiteLoginDiagnosticRecorder
{
    private const int MaxEvents = 1800;
    private readonly object _gate = new();
    private readonly List<LoginDiagnosticEvent> _events = new();

    public void Record(string rootDomain, string kind, string? url = null, string? detail = null, int? statusCode = null, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(rootDomain))
        {
            rootDomain = "unknown";
        }

        var entry = new LoginDiagnosticEvent(
            DateTimeOffset.Now,
            rootDomain.Trim().ToLowerInvariant(),
            Clean(kind, 80),
            SanitizeUrl(url),
            Clean(detail, 700),
            statusCode,
            Clean(source, 180));

        lock (_gate)
        {
            _events.Add(entry);
            if (_events.Count > MaxEvents)
            {
                _events.RemoveRange(0, _events.Count - MaxEvents);
            }
        }
    }

    public string WriteReport(string rootDomain, string outputDirectory, IEnumerable<string> contextLines)
    {
        Directory.CreateDirectory(outputDirectory);
        var safeRoot = new string(rootDomain
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '.')
            .ToArray());
        if (string.IsNullOrWhiteSpace(safeRoot))
        {
            safeRoot = "site";
        }

        var path = Path.Combine(outputDirectory, $"login-diagnostic-{safeRoot}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, BuildReport(rootDomain, contextLines), Encoding.UTF8);
        return path;
    }

    private string BuildReport(string rootDomain, IEnumerable<string> contextLines)
    {
        var root = rootDomain.Trim().ToLowerInvariant();
        List<LoginDiagnosticEvent> snapshot;
        lock (_gate)
        {
            snapshot = _events
                .Where(entry => entry.RootDomain.Equals(root, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var builder = new StringBuilder();
        builder.AppendLine("Lumora - diagnostic connexion");
        builder.AppendLine();
        builder.AppendLine($"Site: {root}");
        builder.AppendLine($"Date: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine("Donnees sensibles: valeurs de cookies, mots de passe, tokens et parametres d'URL masques.");
        builder.AppendLine();
        builder.AppendLine("Contexte");
        foreach (var line in contextLines)
        {
            builder.AppendLine("- " + line);
        }

        builder.AppendLine();
        builder.AppendLine($"Evenements ({snapshot.Count})");
        if (snapshot.Count == 0)
        {
            builder.AppendLine("- Aucun evenement capture pour ce domaine.");
        }
        else
        {
            foreach (var entry in snapshot)
            {
                builder.Append(entry.Time.ToString("HH:mm:ss.fff"));
                builder.Append(" | ");
                builder.Append(entry.Kind);
                if (entry.StatusCode.HasValue)
                {
                    builder.Append(" | status=");
                    builder.Append(entry.StatusCode.Value);
                }
                if (!string.IsNullOrWhiteSpace(entry.Url))
                {
                    builder.Append(" | ");
                    builder.Append(entry.Url);
                }
                if (!string.IsNullOrWhiteSpace(entry.Source))
                {
                    builder.Append(" | source=");
                    builder.Append(entry.Source);
                }
                if (!string.IsNullOrWhiteSpace(entry.Detail))
                {
                    builder.Append(" | ");
                    builder.Append(entry.Detail);
                }
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    public static string BuildInjectionScript(IEnumerable<string> rootDomains)
    {
        var domains = rootDomains
            .Select(domain => domain.Trim().ToLowerInvariant())
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(domain => "'" + domain.Replace("\\", "\\\\").Replace("'", "\\'") + "'");
        var compatibilityList = "[" + string.Join(",", domains) + "]";

        return $$$"""
        (function(){
            'use strict';

            var DOMAINS = {{{compatibilityList}}};
            var lastUserActionAt = 0;

            function hostMatchesDomain(host, domain) {
                return host === domain || host.endsWith('.' + domain);
            }

            function rootForPage() {
                try {
                    var host = (location.hostname || '').toLowerCase();
                    for (var i = 0; i < DOMAINS.length; i++) {
                        if (hostMatchesDomain(host, DOMAINS[i])) return DOMAINS[i];
                    }
                } catch(e) {}
                return '';
            }

            var root = rootForPage();
            if (!root) return;

            function post(kind, detail) {
                try {
                    chrome.webview.postMessage({
                        t: 'nova.loginDiagnostic',
                        root: root,
                        kind: kind,
                        url: location.href,
                        detail: String(detail || '').slice(0, 700)
                    });
                } catch(e) {}
            }

            function markUserAction(evt) {
                lastUserActionAt = Date.now();
                var label = '';
                try {
                    var el = evt && evt.target;
                    label = ((el && (el.getAttribute('aria-label') || el.innerText || el.textContent || el.id || el.className)) || '')
                        .toString()
                        .replace(/\s+/g, ' ')
                        .trim()
                        .slice(0, 120);
                } catch(e) {}
                post('user-action', (evt ? evt.type : 'unknown') + (label ? ': ' + label : ''));
            }

            function wrapGoogleIdentity() {
                try {
                    var id = window.google && window.google.accounts && window.google.accounts.id;
                    if (!id || id.__novaDiagnosticWrapped) return false;

                    var originalInitialize = id.initialize;
                    var originalPrompt = id.prompt;
                    var originalRenderButton = id.renderButton;

                    if (typeof originalInitialize === 'function') {
                        id.initialize = function(options) {
                            post('google.initialize', options && typeof options === 'object'
                                ? 'keys=' + Object.keys(options).join(',')
                                : 'no-options');
                            return originalInitialize.apply(this, arguments);
                        };
                    }

                    if (typeof originalPrompt === 'function') {
                        id.prompt = function(momentCallback) {
                            post('google.prompt', (Date.now() - lastUserActionAt <= 6000) ? 'after-user-action' : 'automatic');
                            return originalPrompt.apply(this, arguments);
                        };
                    }

                    if (typeof originalRenderButton === 'function') {
                        id.renderButton = function() {
                            post('google.renderButton', 'called');
                            return originalRenderButton.apply(this, arguments);
                        };
                    }

                    try {
                        Object.defineProperty(id, '__novaDiagnosticWrapped', {
                            value: true,
                            configurable: true
                        });
                    } catch(e) {
                        id.__novaDiagnosticWrapped = true;
                    }

                    post('google.wrapper', 'installed');
                    return true;
                } catch(e) {
                    post('google.wrapper-error', e && (e.message || e.toString()));
                    return false;
                }
            }

            function scheduleWrap() {
                var attempts = 0;
                var timer = setInterval(function(){
                    attempts++;
                    if (wrapGoogleIdentity() || attempts > 160) {
                        clearInterval(timer);
                    }
                }, 125);
            }

            window.addEventListener('pointerdown', markUserAction, true);
            window.addEventListener('keydown', markUserAction, true);
            window.addEventListener('submit', markUserAction, true);
            window.addEventListener('error', function(evt){
                post('window.error', (evt.message || 'error') + ' @ ' + (evt.filename || '') + ':' + (evt.lineno || 0));
            }, true);
            window.addEventListener('unhandledrejection', function(evt){
                var reason = evt && evt.reason;
                post('window.unhandledrejection', reason && (reason.message || reason.toString ? reason.toString() : reason));
            }, true);

            post('diagnostic.script', 'installed');
            scheduleWrap();
        })();
        """;
    }

    public static string SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Clean(url, 220);
        }

        var builder = new StringBuilder();
        builder.Append(uri.Scheme);
        builder.Append("://");
        builder.Append(uri.Host);
        if (!uri.IsDefaultPort)
        {
            builder.Append(':');
            builder.Append(uri.Port);
        }
        builder.Append(uri.AbsolutePath);

        var query = uri.Query.TrimStart('?');
        if (!string.IsNullOrWhiteSpace(query))
        {
            var keys = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2)[0])
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12);
            builder.Append("?[");
            builder.Append(string.Join(",", keys));
            builder.Append("]");
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            builder.Append("#[fragment]");
        }

        return builder.ToString();
    }

    private static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var clean = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= maxLength ? clean : clean[..maxLength] + "...";
    }

    private sealed record LoginDiagnosticEvent(
        DateTimeOffset Time,
        string RootDomain,
        string Kind,
        string Url,
        string Detail,
        int? StatusCode,
        string Source);
}
