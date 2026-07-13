namespace Lumora.WinUI;

internal sealed class SitePermissionRule
{
    public string RootDomain { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string State { get; set; } = SitePermissionPolicy.Ask;
}

internal sealed record SitePermissionDescriptor(string Key, string Label, string Detail);

internal static class SitePermissionPolicy
{
    public const string Ask = "ask";
    public const string Allow = "allow";
    public const string Block = "block";

    public static readonly IReadOnlyList<SitePermissionDescriptor> KnownPermissions =
    [
        new("camera", "Camera", "Acces a la camera"),
        new("microphone", "Microphone", "Acces au micro"),
        new("geolocation", "Localisation", "Position approximative ou precise"),
        new("notifications", "Notifications", "Alertes envoyees par le site"),
        new("clipboard-read", "Presse-papiers", "Lecture du presse-papiers"),
        new("multiple-automatic-downloads", "Telechargements multiples", "Plusieurs telechargements lances par le site"),
        new("file-read-write", "Fichiers locaux", "Acces lecture/ecriture demande par le site")
    ];

    public static string NormalizeRootDomain(string rootDomain) =>
        string.IsNullOrWhiteSpace(rootDomain)
            ? string.Empty
            : rootDomain.Trim().Trim('.').ToLowerInvariant();

    public static string NormalizeKind(string kind)
    {
        var normalized = (kind ?? string.Empty).Trim();
        if (normalized.Length == 0) return string.Empty;

        return normalized switch
        {
            "Camera" => "camera",
            "Microphone" => "microphone",
            "Geolocation" => "geolocation",
            "Notifications" => "notifications",
            "ClipboardRead" => "clipboard-read",
            "MultipleAutomaticDownloads" => "multiple-automatic-downloads",
            "FileReadWrite" => "file-read-write",
            _ => ToKebabCase(normalized)
        };
    }

    public static string NormalizeState(string state) =>
        string.Equals(state, Allow, StringComparison.OrdinalIgnoreCase) ? Allow :
        string.Equals(state, Block, StringComparison.OrdinalIgnoreCase) ? Block :
        Ask;

    public static string StateFor(IEnumerable<SitePermissionRule> rules, string rootDomain, string kind)
    {
        var root = NormalizeRootDomain(rootDomain);
        var key = NormalizeKind(kind);
        return rules.FirstOrDefault(rule =>
            string.Equals(NormalizeRootDomain(rule.RootDomain), root, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeKind(rule.Kind), key, StringComparison.OrdinalIgnoreCase)) is { } match
                ? NormalizeState(match.State)
                : Ask;
    }

    public static void SetState(IList<SitePermissionRule> rules, string rootDomain, string kind, string state)
    {
        var root = NormalizeRootDomain(rootDomain);
        var key = NormalizeKind(kind);
        var normalizedState = NormalizeState(state);
        if (root.Length == 0 || key.Length == 0) return;

        for (var i = rules.Count - 1; i >= 0; i--)
        {
            var rule = rules[i];
            if (string.Equals(NormalizeRootDomain(rule.RootDomain), root, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeKind(rule.Kind), key, StringComparison.OrdinalIgnoreCase))
            {
                rules.RemoveAt(i);
            }
        }

        if (normalizedState == Ask) return;

        rules.Add(new SitePermissionRule
        {
            RootDomain = root,
            Kind = key,
            State = normalizedState
        });
    }

    public static string StateLabel(string state) =>
        NormalizeState(state) switch
        {
            Allow => "Autorise",
            Block => "Bloque",
            _ => "Demander"
        };

    private static string ToKebabCase(string value)
    {
        var chars = new List<char>(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsWhiteSpace(c) || c == '_' || c == '-')
            {
                if (chars.Count > 0 && chars[^1] != '-') chars.Add('-');
                continue;
            }

            if (char.IsUpper(c) && i > 0 && chars.Count > 0 && chars[^1] != '-')
            {
                chars.Add('-');
            }

            chars.Add(char.ToLowerInvariant(c));
        }

        return new string(chars.ToArray()).Trim('-');
    }
}
