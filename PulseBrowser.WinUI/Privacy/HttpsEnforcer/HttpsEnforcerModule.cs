namespace PulseBrowser.Privacy.HttpsEnforcer;

// Upgrade automatique HTTP → HTTPS avant navigation.
// Préserve localhost, 127.x et adresses IP locales.
internal sealed class HttpsEnforcerModule : IPrivacyModule
{
    public string Id => "https-enforcer";
    public string DisplayName => "Forçage HTTPS";
    public bool IsEnabled { get; set; } = true;

    public string? CleanUrl(string uri)
    {
        if (!uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return null;

        var host = ExtractHost(uri);
        if (host is null || IsLocal(host)) return null;

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
        host == "::1" ||
        host == "[::1]" ||
        host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
}
