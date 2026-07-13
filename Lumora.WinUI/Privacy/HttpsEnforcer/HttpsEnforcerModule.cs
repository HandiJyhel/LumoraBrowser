namespace Lumora.Privacy.HttpsEnforcer;

// Upgrade automatique HTTP → HTTPS avant navigation.
// Préserve localhost, 127.x et adresses IP locales.
internal sealed class HttpsEnforcerModule : IPrivacyModule
{
    public string Id => "https-enforcer";
    public string DisplayName => "Forçage HTTPS";
    public bool IsEnabled { get; set; } = true;

    // Hôtes pour lesquels l'utilisateur a explicitement accepté de continuer en HTTP
    // (le site ne supporte pas HTTPS). Valable le temps de la session uniquement.
    private readonly HashSet<string> _allowedHttpHosts = new(StringComparer.OrdinalIgnoreCase);

    public void AllowHttp(string host)
    {
        if (!string.IsNullOrWhiteSpace(host)) _allowedHttpHosts.Add(host);
    }

    public string? CleanUrl(string uri)
    {
        if (!uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return null;

        var host = ExtractHost(uri);
        if (host is null || IsLocal(host) || _allowedHttpHosts.Contains(host)) return null;

        return "https://" + uri[7..];
    }

    private static string? ExtractHost(string uri)
    {
        try { return new Uri(uri).Host; }
        catch { return null; }
    }

    private static bool IsLocal(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.StartsWith("127.") ||
        host.StartsWith("192.168.") ||
        host.StartsWith("10.") ||
        IsPrivate172(host) ||
        host == "::1" ||
        host == "[::1]" ||
        host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);

    // Plage privée RFC1918 172.16.0.0/12 (172.16.x.x à 172.31.x.x).
    private static bool IsPrivate172(string host)
    {
        if (!host.StartsWith("172.")) return false;
        var rest = host[4..];
        var dot = rest.IndexOf('.');
        return dot > 0 &&
               int.TryParse(rest[..dot], out var second) &&
               second >= 16 && second <= 31;
    }
}
