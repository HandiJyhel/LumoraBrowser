namespace Lumora.WinUI;

internal static class BrandingText
{
    public const string ProductName = "Lumora";

    public static string NormalizeLegacyProductTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ProductName;

        var title = value.Trim();
        return IsLegacyProductTitle(title) ? ProductName : title;
    }

    private static bool IsLegacyProductTitle(string title) =>
        string.Equals(title, "Pulse", StringComparison.OrdinalIgnoreCase)
        || string.Equals(title, "Pulse Browser", StringComparison.OrdinalIgnoreCase)
        || string.Equals(title, "Nova", StringComparison.OrdinalIgnoreCase)
        || string.Equals(title, "Nova Browser", StringComparison.OrdinalIgnoreCase);
}
