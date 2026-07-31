using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private void CoreWebView2_WebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs args)
    {
        if (sender is not CoreWebView2 core)
        {
            return;
        }

        // Santé du document principal (5xx, redirections permanentes) — voir
        // MainWindow.SiteNotFound.cs. Avant le filtre diagnostic : celui-ci ne
        // concerne que les sites listés, la santé concerne toutes les pages.
        ObserveMainDocumentResponse(args);

        var pageUri = core.Source;
        var root = LoginDiagnosticRootForRequest(args.Request.Uri, pageUri, CoreWebView2WebResourceContext.Other);
        if (root is null)
        {
            return;
        }

        try
        {
            var status = args.Response.StatusCode;
            _loginDiagnostics.Record(root, "response", args.Request.Uri, null, status);
        }
        catch (Exception error)
        {
            _loginDiagnostics.Record(root, "response-error", args.Request.Uri, error.GetType().Name);
        }
    }

    private void RecordLoginDiagnosticForNavigation(string? pageUri, string kind, string? targetUri, string? detail = null)
    {
        var root = LoginDiagnosticRootFor(targetUri) ?? LoginDiagnosticRootFor(pageUri);
        if (root is null)
        {
            return;
        }

        _loginDiagnostics.Record(root, kind, targetUri, detail);
    }

    private void RecordLoginDiagnosticRequest(CoreWebView2WebResourceRequestedEventArgs args, string pageUri, string decision)
    {
        var root = LoginDiagnosticRootForRequest(args.Request.Uri, pageUri, args.ResourceContext);
        if (root is null)
        {
            return;
        }

        _loginDiagnostics.Record(root, "request-" + decision, args.Request.Uri, args.ResourceContext.ToString());
    }

    private string? LoginDiagnosticRootFor(string? uriOrHost)
    {
        if (string.IsNullOrWhiteSpace(uriOrHost) || _uiSettings.LoginDiagnosticSites.Count == 0)
        {
            return null;
        }

        var root = uriOrHost.Contains("://", StringComparison.Ordinal)
            ? Credentials.PublicSuffixService.RootDomainOf(uriOrHost)
            : Credentials.PublicSuffixService.RootDomainOf("https://" + uriOrHost);

        return _uiSettings.LoginDiagnosticSites.FirstOrDefault(domain =>
            domain.Equals(root, StringComparison.OrdinalIgnoreCase));
    }

    private string? LoginDiagnosticRootForRequest(string requestUri, string? pageUri, CoreWebView2WebResourceContext context)
    {
        if (_uiSettings.LoginDiagnosticSites.Count == 0)
        {
            return null;
        }

        var pageRoot = LoginDiagnosticRootFor(pageUri);
        if (pageRoot is not null && IsLoginDiagnosticRelevantRequest(requestUri, pageRoot, context))
        {
            return pageRoot;
        }

        if (IsGoogleIdentityHost(requestUri) &&
            _uiSettings.LoginDiagnosticSites.Count == 1)
        {
            return _uiSettings.LoginDiagnosticSites[0];
        }

        return null;
    }

    private bool IsLoginDiagnosticRelevantRequest(string requestUri, string rootDomain, CoreWebView2WebResourceContext context)
    {
        if (!Uri.TryCreate(requestUri, UriKind.Absolute, out var request))
        {
            return false;
        }

        var requestRoot = Credentials.PublicSuffixService.RootDomainOf(request.Host);
        if (requestRoot.Equals(rootDomain, StringComparison.OrdinalIgnoreCase))
        {
            return context is CoreWebView2WebResourceContext.Document
                or CoreWebView2WebResourceContext.Script
                or CoreWebView2WebResourceContext.XmlHttpRequest
                or CoreWebView2WebResourceContext.Fetch
                or CoreWebView2WebResourceContext.Other
                || IsSessionRelevantUrl(requestUri);
        }

        return IsGoogleIdentityHost(requestUri) || IsSessionRelevantUrl(requestUri);
    }

    private static bool IsGoogleIdentityHost(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        var host = parsed.Host;
        return host.Equals("accounts.google.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("apis.google.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("accounts.gstatic.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("ssl.gstatic.com", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("www.gstatic.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".gstatic.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSessionRelevantUrl(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        var path = parsed.AbsolutePath.ToLowerInvariant();
        return path.Contains("login", StringComparison.Ordinal) ||
               path.Contains("signin", StringComparison.Ordinal) ||
               path.Contains("oauth", StringComparison.Ordinal) ||
               path.Contains("auth", StringComparison.Ordinal) ||
               path.StartsWith("/gsi/", StringComparison.Ordinal);
    }

    private async Task<string> ExportLoginDiagnosticAsync(CurrentSiteInfo site)
    {
        var context = await BuildLoginDiagnosticContextAsync(site);
        var outputDir = Path.Combine(_profile.NavigationDir, "login-diagnostics");
        return _loginDiagnostics.WriteReport(site.RootDomain, outputDir, context);
    }

    private async Task<List<string>> BuildLoginDiagnosticContextAsync(CurrentSiteInfo site)
    {
        var lines = new List<string>
        {
            $"Version Lumora: {Version}",
            $"URL active: {SiteLoginDiagnosticRecorder.SanitizeUrl(site.Address)}",
            $"Compatibilite connexion: {(IsLoginCompatibilitySite(site.RootDomain) ? "active" : "inactive")}",
            $"Diagnostic connexion: {(IsLoginDiagnosticSite(site.RootDomain) ? "actif" : "inactif")}",
            $"Purge de session au demarrage: {(_uiSettings.SessionPurgeEnabled ? "active" : "inactive")}",
            $"Session conservee pour ce site: {(IsTrustedSessionSite(site.RootDomain) ? "oui" : "non")}"
        };

        var core = _browserView?.CoreWebView2;
        if (core is null)
        {
            lines.Add("Cookies: moteur WebView2 indisponible");
            return lines;
        }

        try
        {
            var cookies = await core.CookieManager.GetCookiesAsync(string.Empty);
            var siteCookies = cookies.Count(cookie =>
                RootDomainOf(cookie.Domain).Equals(site.RootDomain, StringComparison.OrdinalIgnoreCase));
            var googleCookies = cookies.Count(cookie =>
                RootDomainOf(cookie.Domain).Equals("google.com", StringComparison.OrdinalIgnoreCase));

            lines.Add($"Cookies {site.RootDomain}: {siteCookies}");
            lines.Add($"Cookies google.com: {googleCookies}");
        }
        catch (Exception error)
        {
            lines.Add($"Cookies: lecture impossible ({error.GetType().Name})");
        }

        return lines;
    }

    // pageUri = e.Source (attesté par WebView2), jamais le champ JSON "root" :
    // sans ça, n'importe quelle page pourrait revendiquer un root différent du
    // sien (par ex. le domaine d'une banque pour laquelle le diagnostic est
    // actif) et polluer le rapport de diagnostic local avec des entrées
    // falsifiées.
    private void HandleLoginDiagnosticMessage(string? pageUri, JsonObject obj)
    {
        var claimedRoot = obj["root"]?.GetValue<string>() ?? string.Empty;
        var actualRoot = LoginDiagnosticRootFor(pageUri);
        if (actualRoot is null || !actualRoot.Equals(claimedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var kind = obj["kind"]?.GetValue<string>() ?? "script";
        var url = obj["url"]?.GetValue<string>();
        var detail = obj["detail"]?.GetValue<string>();
        _loginDiagnostics.Record(actualRoot, kind, url, detail, null, "document-script");
    }
}
