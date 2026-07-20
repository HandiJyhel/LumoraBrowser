namespace Lumora.WinUI;

internal sealed class SiteComfortRule
{
    public string RootDomain { get; set; } = string.Empty;
    public int ZoomPercent { get; set; } = SiteComfortPolicy.DefaultZoomPercent;
    public bool LargeText { get; set; }
    public bool ReduceMotion { get; set; }
}

internal static class SiteComfortPolicy
{
    public const int DefaultZoomPercent = 100;
    public const int MinZoomPercent = 80;
    public const int MaxZoomPercent = 200;

    public static readonly IReadOnlyList<int> SuggestedZoomPercents =
    [
        90,
        100,
        110,
        125,
        140,
        160
    ];

    public static string NormalizeRootDomain(string rootDomain) =>
        string.IsNullOrWhiteSpace(rootDomain)
            ? string.Empty
            : rootDomain.Trim().Trim('.').ToLowerInvariant();

    public static int NormalizeZoomPercent(int zoomPercent) =>
        Math.Clamp(zoomPercent, MinZoomPercent, MaxZoomPercent);

    public static SiteComfortRule? FindRule(IEnumerable<SiteComfortRule> rules, string rootDomain)
    {
        var root = NormalizeRootDomain(rootDomain);
        return rules.FirstOrDefault(rule =>
            string.Equals(NormalizeRootDomain(rule.RootDomain), root, StringComparison.OrdinalIgnoreCase));
    }

    public static int ZoomPercentFor(IEnumerable<SiteComfortRule> rules, string rootDomain) =>
        FindRule(rules, rootDomain) is { } rule
            ? NormalizeZoomPercent(rule.ZoomPercent)
            : DefaultZoomPercent;

    public static bool LargeTextFor(IEnumerable<SiteComfortRule> rules, string rootDomain) =>
        FindRule(rules, rootDomain)?.LargeText ?? false;

    public static bool ReduceMotionFor(IEnumerable<SiteComfortRule> rules, string rootDomain) =>
        FindRule(rules, rootDomain)?.ReduceMotion ?? false;

    public static bool HasOverrides(IEnumerable<SiteComfortRule> rules, string rootDomain) =>
        FindRule(rules, rootDomain) is { } rule && !IsDefault(rule);

    public static void SetZoomPercent(IList<SiteComfortRule> rules, string rootDomain, int zoomPercent)
    {
        var rule = GetOrCreateRule(rules, rootDomain);
        if (rule is null) return;

        rule.ZoomPercent = NormalizeZoomPercent(zoomPercent);
        CleanupDefaultRule(rules, rule);
    }

    public static void SetLargeText(IList<SiteComfortRule> rules, string rootDomain, bool enabled)
    {
        var rule = GetOrCreateRule(rules, rootDomain);
        if (rule is null) return;

        rule.LargeText = enabled;
        CleanupDefaultRule(rules, rule);
    }

    public static void SetReduceMotion(IList<SiteComfortRule> rules, string rootDomain, bool enabled)
    {
        var rule = GetOrCreateRule(rules, rootDomain);
        if (rule is null) return;

        rule.ReduceMotion = enabled;
        CleanupDefaultRule(rules, rule);
    }

    public static void Reset(IList<SiteComfortRule> rules, string rootDomain)
    {
        var root = NormalizeRootDomain(rootDomain);
        for (var i = rules.Count - 1; i >= 0; i--)
        {
            if (string.Equals(NormalizeRootDomain(rules[i].RootDomain), root, StringComparison.OrdinalIgnoreCase))
            {
                rules.RemoveAt(i);
            }
        }
    }

    private static SiteComfortRule? GetOrCreateRule(IList<SiteComfortRule> rules, string rootDomain)
    {
        var root = NormalizeRootDomain(rootDomain);
        if (root.Length == 0) return null;

        var existing = FindRule(rules, root);
        if (existing is not null) return existing;

        var created = new SiteComfortRule { RootDomain = root };
        rules.Add(created);
        return created;
    }

    private static void CleanupDefaultRule(IList<SiteComfortRule> rules, SiteComfortRule rule)
    {
        if (!IsDefault(rule)) return;

        for (var i = rules.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(rules[i], rule))
            {
                rules.RemoveAt(i);
                return;
            }
        }
    }

    private static bool IsDefault(SiteComfortRule rule) =>
        NormalizeZoomPercent(rule.ZoomPercent) == DefaultZoomPercent &&
        !rule.LargeText &&
        !rule.ReduceMotion;
}
