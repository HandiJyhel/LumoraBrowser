namespace PulseBrowser.Privacy.CosmeticFilter;

internal record CosmeticRule(
    string? Domain,    // null = règle générique (toutes pages)
    string Selector,
    bool IsException
);

// Parseur des règles cosmétiques du format Adblock Plus / uBlock Origin.
// Format : [domaine(s)]##sélecteur  ou  [domaine(s)]#@#sélecteur (exception)
// Seules les règles génériques (sans domaine) et les règles de domaine standard sont traitées.
// Les filtres procéduraux uBlock (:has-text, :upward, :matches-css…) sont ignorés ici
// — ils appartiennent au ScriptletInjector (module suivant).
internal static class CosmeticFilterParser
{
    // Préfixes procéduraux non standard à exclure (ne sont pas du CSS valide)
    private static readonly string[] ProceduralFilters =
        [":has-text(", ":upward(", ":matches-css", ":-abp-", ":remove", ":watch-attr",
         ":contains(", "+js(", ":xpath(", ":min-text-length"];

    public static IEnumerable<CosmeticRule> ParseLines(IEnumerable<string> lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length < 4 || line[0] == '!' || line[0] == '[') continue;

            // Détecter exception #@# avant ##
            int sepLen;
            int sepIdx = line.IndexOf("#@#", StringComparison.Ordinal);
            bool isException;

            if (sepIdx >= 0)
            {
                isException = true;
                sepLen = 3;
            }
            else
            {
                sepIdx = line.IndexOf("##", StringComparison.Ordinal);
                if (sepIdx < 0) continue;
                isException = false;
                sepLen = 2;
            }

            var domainPart   = line[..sepIdx].Trim();
            var selectorPart = line[(sepIdx + sepLen)..].Trim();

            if (string.IsNullOrEmpty(selectorPart)) continue;

            // Ignorer les filtres procéduraux (pas du CSS valide)
            bool procedural = false;
            foreach (var pf in ProceduralFilters)
            {
                if (selectorPart.Contains(pf, StringComparison.OrdinalIgnoreCase))
                { procedural = true; break; }
            }
            if (procedural) continue;

            // Règle générique (aucun domaine)
            if (string.IsNullOrEmpty(domainPart))
            {
                if (IsValidSelector(selectorPart))
                    yield return new CosmeticRule(null, selectorPart, isException);
                continue;
            }

            // Règle de domaine (peut être multi-domaines séparés par virgule)
            foreach (var rawDomain in domainPart.Split(','))
            {
                var d = rawDomain.Trim();
                if (d.Length == 0) continue;

                // Domaines précédés de ~ = exclusion de domaine → ignorer pour l'instant
                if (d[0] == '~') continue;

                if (IsValidSelector(selectorPart))
                    yield return new CosmeticRule(d.ToLowerInvariant(), selectorPart, isException);
            }
        }
    }

    private static bool IsValidSelector(string selector)
    {
        if (selector.Length == 0 || selector.Length > 512) return false;
        // Rejeter les pseudo-éléments uBlock non standards
        if (selector.StartsWith('+') || selector.StartsWith('/')) return false;
        return true;
    }
}
