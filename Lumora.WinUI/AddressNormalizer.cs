namespace Lumora.WinUI;

// Transforme la saisie de la barre d'adresse en URL navigable : protocole connu
// conservé, domaine complété en https, localhost en http, le reste part en
// recherche. Logique pure partagée entre la fenêtre principale et la fenêtre
// de navigation privée.
internal static class AddressNormalizer
{
    public static string Normalize(string raw, string searchEngine, string emptyFallback = "lumora://accueil")
    {
        var value = raw.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return emptyFallback;

        // Protocoles connus → navigation directe
        if (value.StartsWith("lumora://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return value;

        // Contient des espaces → forcément une recherche
        if (value.Contains(' '))
            return SearchUrl(value, searchEngine);

        // localhost / localhost:port → navigation locale sans TLS
        if (value.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase))
            return $"http://{value}";

        // Ressemble à un domaine (contient un point, pas d'espace) → https://
        if (value.Contains('.'))
            return $"https://{value}";

        // Mot seul sans point → recherche
        return SearchUrl(value, searchEngine);
    }

    public static string SearchUrl(string query, string searchEngine)
    {
        var q = Uri.EscapeDataString(query);
        // Langue de l'interface système (ex. "fr") pour éviter les résultats en anglais.
        var lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return searchEngine switch
        {
            "duckduckgo" => $"https://duckduckgo.com/?q={q}&kl={lang}-{lang}",
            "brave"      => $"https://search.brave.com/search?q={q}",
            "bing"       => $"https://www.bing.com/search?q={q}&setlang={lang}",
            _            => $"https://www.google.com/search?q={q}&hl={lang}",
        };
    }
}
