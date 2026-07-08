using System.Globalization;
using System.Net;
using System.Reflection;

namespace PulseBrowser.WinUI.Credentials;

// Normalisation hôte/origine/domaine racine, adossée à la Public Suffix List
// officielle (Mozilla, licence MPL 2.0, https://publicsuffix.org) embarquée en
// ressource. Le rapprochement par domaine racine du gestionnaire de mots de
// passe et le regroupement des sessions reposent sur ce service : en cas de
// problème de chargement de la liste, on retombe sur l'heuristique courte
// historique plutôt que de casser ces fonctions.
internal static class PublicSuffixService
{
    private sealed record SuffixData(
        HashSet<string> Exact,
        HashSet<string> Wildcards,
        HashSet<string> Exceptions);

    private static readonly Lazy<SuffixData?> Rules = new(LoadRules, isThreadSafe: true);

    // Heuristique de secours si la ressource PSL est indisponible.
    private static readonly HashSet<string> FallbackTwoPartSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "co.uk", "org.uk", "gov.uk", "ac.uk", "me.uk",
        "com.au", "net.au", "org.au", "co.nz",
        "co.jp", "ne.jp", "or.jp",
        "com.br", "com.mx", "com.ar", "com.co",
        "co.in", "com.cn", "com.hk", "com.sg", "com.tw", "com.tr",
        "co.za", "co.kr", "com.pl", "com.pt", "com.ua", "com.vn", "com.my", "com.ph",
    };

    public static string HostOf(string url)
    {
        if (Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
        {
            return NormalizeHost(uri.Host);
        }

        return NormalizeHost(url ?? string.Empty);
    }

    public static string OriginOf(string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
        {
            return url ?? string.Empty;
        }

        return $"{uri.Scheme}://{NormalizeHost(uri.Host)}";
    }

    public static string RootDomainOf(string urlOrHost)
    {
        var host = HostOf(urlOrHost).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        // Adresses IP : pas de notion de domaine racine.
        if (IPAddress.TryParse(host, out _))
        {
            return host;
        }

        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length <= 1)
        {
            return host;
        }

        var data = Rules.Value;
        return data is null
            ? FallbackRootDomain(host, labels)
            : RegistrableDomain(host, labels, data);
    }

    // ── Algorithme Public Suffix List ─────────────────────────────────────────
    // https://github.com/publicsuffix/list/wiki/Format#formal-algorithm
    // Règle gagnante : une exception (!) l'emporte, sinon la règle la plus longue.
    // Sans correspondance, la règle implicite "*" fait du dernier label le suffixe.

    private static string RegistrableDomain(string host, string[] labels, SuffixData data)
    {
        var suffixStart = labels.Length - 1;

        for (var i = labels.Length - 1; i >= 0; i--)
        {
            var candidate = string.Join('.', labels[i..]);

            if (data.Exceptions.Contains(candidate))
            {
                // Le suffixe public est l'exception privée de son premier label ;
                // le domaine enregistrable est donc l'exception elle-même.
                return candidate;
            }

            if (data.Exact.Contains(candidate))
            {
                suffixStart = Math.Min(suffixStart, i);
            }

            // Une règle "*.base" rend public tout "<label>.base".
            if (i + 1 < labels.Length &&
                data.Wildcards.Contains(string.Join('.', labels[(i + 1)..])))
            {
                suffixStart = Math.Min(suffixStart, i);
            }
        }

        // L'hôte entier est un suffixe public (ex. "co.uk") : rien à raccourcir.
        if (suffixStart == 0)
        {
            return host;
        }

        return string.Join('.', labels[(suffixStart - 1)..]);
    }

    private static string FallbackRootDomain(string host, string[] labels)
    {
        if (labels.Length <= 2)
        {
            return host;
        }

        var lastTwo = $"{labels[^2]}.{labels[^1]}";
        return FallbackTwoPartSuffixes.Contains(lastTwo)
            ? $"{labels[^3]}.{lastTwo}"
            : lastTwo;
    }

    // ── Chargement de la ressource embarquée ──────────────────────────────────

    private static SuffixData? LoadRules()
    {
        try
        {
            var assembly = typeof(PublicSuffixService).Assembly;
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("public_suffix_list.dat", StringComparison.OrdinalIgnoreCase));
            if (resourceName is null)
            {
                return null;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return null;
            }

            var exact = new HashSet<string>(StringComparer.Ordinal);
            var wildcards = new HashSet<string>(StringComparer.Ordinal);
            var exceptions = new HashSet<string>(StringComparer.Ordinal);
            var idn = new IdnMapping();

            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } rawLine)
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var isException = line.StartsWith('!');
                if (isException)
                {
                    line = line[1..];
                }

                var isWildcard = line.StartsWith("*.", StringComparison.Ordinal);
                if (isWildcard)
                {
                    line = line[2..];
                }

                // La liste contient des entrées Unicode ; les hôtes WebView2 sont
                // en punycode → on aligne les règles sur la forme ASCII.
                string rule;
                try
                {
                    rule = idn.GetAscii(line.ToLowerInvariant());
                }
                catch
                {
                    continue;
                }

                if (isException)
                {
                    exceptions.Add(rule);
                }
                else if (isWildcard)
                {
                    wildcards.Add(rule);
                }
                else
                {
                    exact.Add(rule);
                }
            }

            return exact.Count == 0 ? null : new SuffixData(exact, wildcards, exceptions);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeHost(string host)
    {
        var value = (host ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        return value.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? value[4..] : value;
    }
}
