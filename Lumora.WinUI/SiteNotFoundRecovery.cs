using Lumora.WinUI.Credentials;

namespace Lumora.WinUI;

// Reprise après « site introuvable » (le domaine ne se résout plus : site fermé,
// changement de nom de domaine, faute de frappe) : classe PURE (aucune dépendance
// UI), compilée aussi dans Lumora.Tests. Là où Chrome envoie l'adresse en échec
// aux serveurs de Google pour obtenir une correction, Lumora calcule tout
// localement (favoris, historique, onglets ouverts) et ne laisse partir une
// recherche web QUE sur clic explicite de l'utilisateur.
public static class SiteNotFoundRecovery
{
    // Requête envoyée au moteur de recherche si l'utilisateur clique : le nom du
    // site SANS extension (« zone-telechargement » pour zone-telechargement.win).
    // Un site qui déménage change presque toujours d'extension en gardant son
    // nom : chercher le nom seul retrouve le nouveau domaine bien mieux que
    // l'ancienne adresse complète. Repli sur l'hôte si le nom est trop court
    // pour faire une requête sensée (« t.co »).
    public static string? SearchQueryFor(string? url)
    {
        var host = ComparableHostOf(url);
        if (host is null)
            return null;

        var root = PublicSuffixService.RootDomainOf("https://" + host);
        if (string.IsNullOrEmpty(root))
            return host;

        var dot = root.IndexOf('.');
        var label = dot > 0 ? root[..dot] : root;
        return label.Length >= 3 ? label : host;
    }

    public static string? DisplayHostOf(string? url)
    {
        var host = ComparableHostOf(url);
        return host;
    }

    // Variante avec/sans « www. » de l'URL en échec : certains domaines ne
    // servent qu'une des deux formes. Calcul local, aucune requête réseau.
    public static string? WwwVariantOf(string? url)
    {
        if (!TryParseWebUrl(url, out var uri))
            return null;

        var builder = new UriBuilder(uri)
        {
            Host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? uri.Host[4..]
                : "www." + uri.Host
        };
        return builder.Uri.ToString();
    }

    // Cherche dans les URL déjà connues localement un domaine très proche du
    // domaine en échec : faute de frappe probable (« gogle.fr » → « google.fr »)
    // ou mauvaise extension (« monsite.com » → « monsite.fr »). Retourne
    // l'origine (schéma + hôte) du meilleur candidat, ou null si rien d'assez
    // proche — mieux vaut aucune suggestion qu'une suggestion farfelue.
    public static string? ClosestKnownUrl(string? failedUrl, IEnumerable<string?> knownUrls)
    {
        var failedHost = ComparableHostOf(failedUrl);
        if (failedHost is null)
            return null;

        string? bestUrl = null;
        var bestDistance = int.MaxValue;

        foreach (var knownUrl in knownUrls)
        {
            if (!TryParseWebUrl(knownUrl, out var knownUri))
                continue;

            var knownHost = ComparableHostOf(knownUrl);
            // Un hôte identique échouerait de la même façon : inutile de le proposer.
            if (knownHost is null || knownHost.Equals(failedHost, StringComparison.Ordinal))
                continue;

            var distance = HostDistance(failedHost, knownHost);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestUrl = knownUri.GetLeftPart(UriPartial.Authority);
            }
        }

        return bestDistance <= MaxAcceptableDistance(failedHost) ? bestUrl : null;
    }

    // Distance entre deux hôtes : une simple mauvaise extension (« monsite.com »
    // vs « monsite.fr ») compte comme 1 quel que soit l'écart littéral, sinon
    // distance d'édition classique.
    private static int HostDistance(string failedHost, string knownHost)
    {
        var failedLabel = RegistrableLabelOf(failedHost);
        if (failedLabel is { Length: >= 4 } &&
            failedLabel.Equals(RegistrableLabelOf(knownHost), StringComparison.Ordinal))
        {
            return 1;
        }

        return EditDistance(failedHost, knownHost);
    }

    // Étiquette enregistrable du domaine (« monsite » pour « www.monsite.fr »),
    // via la Public Suffix List pour ne pas confondre extension et sous-domaine.
    private static string? RegistrableLabelOf(string host)
    {
        var root = PublicSuffixService.RootDomainOf("https://" + host);
        if (string.IsNullOrEmpty(root))
            return null;

        var dot = root.IndexOf('.');
        return dot > 0 ? root[..dot] : root;
    }

    // Seuil volontairement strict : 1 faute pour un domaine court, 2 pour un
    // domaine long. Au-delà, ce n'est plus une faute de frappe plausible.
    private static int MaxAcceptableDistance(string failedHost) =>
        failedHost.Length >= 10 ? 2 : 1;

    private static int EditDistance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 2)
            return int.MaxValue;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    // Hôte comparable : minuscules, sans « www. ». Null si l'URL n'est pas une
    // adresse web (les pages internes lumora:// n'ont pas de reprise DNS).
    private static string? ComparableHostOf(string? url)
    {
        if (!TryParseWebUrl(url, out var uri))
            return null;

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];

        return host.Contains('.') ? host : null;
    }

    private static bool TryParseWebUrl(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            parsed.Host.Length == 0 ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
