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

    // Chaîne JS entre apostrophes. Retours à la ligne et séparateurs Unicode
    // échappés (sinon erreur de syntaxe), "<" aussi : dans un bloc <script>,
    // "</script>" terminerait le bloc prématurément.
    public static string JsString(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'")
            .Replace("\n", "\\n").Replace("\r", "\\r")
            .Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029")
            .Replace("<", "\\x3C");

    // Chaîne JS placée DANS un attribut HTML (onclick="f('...')") : le
    // navigateur décode l'attribut avant d'exécuter le JS, il faut donc
    // l'échappement JS PUIS l'échappement HTML. JsString seul laissait un
    // guillemet fermer l'attribut (audit sécurité 2026-09-24).
    public static string JsAttribute(string value) =>
        HtmlAttribute(JsString(value));
}
