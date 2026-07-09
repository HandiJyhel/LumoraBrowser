namespace PulseBrowser.Privacy.NetworkBlocker;

internal enum RuleAction { Block, Allow }

internal record struct ParsedRule(
    RuleAction Action,
    string Domain,       // non-vide si règle de domaine (||domain^)
    string Pattern,      // non-vide si règle de sous-chaîne
    bool ThirdPartyOnly
);

// Parseur du format Adblock Plus / uBlock Origin (sous-ensemble couvrant 95 % des règles réelles)
internal static class FilterParser
{
    public static IEnumerable<ParsedRule> ParseLines(IEnumerable<string> lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();

            // Commentaires et métadonnées
            if (line.Length == 0 || line[0] == '!' || line[0] == '[') continue;

            // Règles cosmétiques (CSS) — réservées aux prochains modules
            if (line.Contains("##") || line.Contains("#@#") || line.Contains("#?#")) continue;

            // Exception : @@||domain^
            bool isException = line.StartsWith("@@", StringComparison.Ordinal);
            if (isException) line = line[2..];

            // Règle de domaine : ||domain^[options]
            if (line.StartsWith("||", StringComparison.Ordinal))
            {
                var body = line[2..];
                var rule = ParseDomainRule(body, isException ? RuleAction.Allow : RuleAction.Block);
                if (rule.HasValue) yield return rule.Value;
                continue;
            }

            // Règle de sous-chaîne simple (hors domaine)
            // On ignore les règles trop génériques (< 8 chars) qui génèrent trop de faux positifs
            if (!isException && line.Length >= 8 && !line.Contains('*') && !line.StartsWith('/'))
            {
                yield return new ParsedRule(RuleAction.Block, string.Empty, line, false);
            }
        }
    }

    private static ParsedRule? ParseDomainRule(string body, RuleAction action)
    {
        // Extraire les options après $
        bool thirdPartyOnly = false;
        var dollarPos = body.LastIndexOf('$');
        if (dollarPos >= 0)
        {
            var options = body[(dollarPos + 1)..];
            body = body[..dollarPos];
            foreach (var opt in options.Split(','))
            {
                var o = opt.Trim();
                if (o.Equals("third-party", StringComparison.OrdinalIgnoreCase))
                    thirdPartyOnly = true;
                // Règle restreinte à des sites précis ($domain=a.com|b.com) : sans matching
                // par domaine de page, l'appliquer globalement sur-bloquerait des ressources
                // légitimes (ex. ||lh3.googleusercontent.com^$domain=site-pirate → casse les
                // avatars Google partout). On préfère ignorer la règle plutôt que sur-bloquer.
                if (o.StartsWith("domain=", StringComparison.OrdinalIgnoreCase))
                    return null;
                // ~third-party : ignorer (ne bloque que first-party — rare, on skip)
            }
        }

        // Retirer le ^ final
        if (body.EndsWith('^')) body = body[..^1];

        // Si le corps contient un slash, il y a un chemin → traiter comme sous-chaîne
        var slashPos = body.IndexOf('/');
        if (slashPos > 0)
        {
            var domain = body[..slashPos];
            if (!IsValidDomain(domain)) return null;
            // Conserver comme pattern sous-chaîne complet
            return new ParsedRule(action, string.Empty, body, thirdPartyOnly);
        }

        if (!IsValidDomain(body)) return null;
        return new ParsedRule(action, body.ToLowerInvariant(), string.Empty, thirdPartyOnly);
    }

    private static bool IsValidDomain(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length < 3 || s.Length > 253) return false;
        // Rejeter les wildcards et caractères spéciaux
        foreach (var c in s)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '.' && c != '-' && c != '_') return false;
        }
        return s.Contains('.');
    }
}
