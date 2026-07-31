namespace Lumora.Privacy.NetworkBlocker;

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
                if (o.Length == 0) continue;

                if (o.Equals("third-party", StringComparison.OrdinalIgnoreCase))
                {
                    thirdPartyOnly = true;
                    continue;
                }

                // Toute autre option ($domain=, ~third-party, ou une restriction de
                // type de ressource comme $subdocument/$script/$image...) ne peut pas
                // être respectée par ce moteur simplifié : ShouldBlock n'a ni le
                // domaine de la page cible au moment du parsing, ni le type de
                // ressource pour les règles de domaine. L'appliquer quand meme
                // reviendrait a bloquer bien plus large que prevu par la regle
                // d'origine. Trouve en conditions reelles le 2026-07-22 :
                // ||eporner.com^$subdocument,~third-party (censee bloquer
                // l'incrustation du site en iframe chez un tiers) etait appliquee
                // comme un blocage total et permanent du domaine, rendant la page
                // blanche pour ses propres ressources (CSS, JS, images) des qu'on
                // visitait le site directement. Ignorer la regle plutot que la
                // sur-appliquer, meme principe deja retenu pour domain=.
                return null;
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
