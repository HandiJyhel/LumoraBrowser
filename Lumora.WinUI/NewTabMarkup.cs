using System.Net;

namespace Lumora.WinUI;

// Échappement HTML/JS pour la page nouvel onglet (titres et URL de raccourcis
// viennent de l'utilisateur et sont injectés dans du HTML/JS généré). Aucune
// dépendance UI — testable par dotnet test.
internal static class NewTabMarkup
{
    public static string NormalizeShortcutUrl(string url)
    {
        var value = url.Trim();
        if (value.StartsWith("lumora://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return value;

        return "https://" + value;
    }

    public static string ShortcutInitial(string title)
    {
        var trimmed = title.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "•" : trimmed[..1].ToUpperInvariant();
    }

    public static string HtmlText(string? value) =>
        WebUtility.HtmlEncode(BrandingText.NormalizeLegacyProductTitle(value));

    public static string HtmlAttribute(string value) =>
        WebUtility.HtmlEncode(value);

    public static string JsString(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");
}
