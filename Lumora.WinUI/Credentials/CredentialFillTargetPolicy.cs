namespace Lumora.WinUI.Credentials;

// ── Où un secret du Coffre a-t-il le droit d'être envoyé ? ─────────────────────
// Classe PURE (aucune dépendance UI), compilée aussi dans Lumora.Tests.
//
// Faille trouvée à l'audit de sécurité du 2026-09-24 : le remplissage envoyait
// le mot de passe à TOUTES les iframes de premier niveau de la page, quel que
// soit leur site (publicité, widget tiers...), dès que la page principale
// n'avait pas tout rempli - cas typique des connexions en deux étapes. Une
// iframe tierce n'avait qu'à définir sa propre fonction de remplissage pour
// recevoir le secret. Règle désormais appliquée avant CHAQUE envoi (page
// principale comprise, elle a pu naviguer entre l'offre et le clic) :
//  - même site (domaine enregistrable, liste publique des suffixes complète)
//    que l'identifiant enregistré (son origine ou son adresse de connexion) ;
//  - jamais d'un identifiant enregistré en HTTPS vers une page HTTP (le mot de
//    passe y circulerait en clair sur le réseau) ;
//  - adresse inconnue ou non web (about:blank, about:srcdoc, data:...) = refus.
internal static class CredentialFillTargetPolicy
{
    public static bool IsAllowedTarget(string? targetUrl, string credentialOrigin, string? credentialLoginUrl)
    {
        if (!TryParseWeb(targetUrl, out var target))
        {
            return false;
        }

        var targetRoot = PublicSuffixService.RootDomainOf(target.AbsoluteUri);
        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            return false;
        }

        foreach (var raw in new[] { credentialOrigin, credentialLoginUrl })
        {
            // Origine importée sans schéma ("exemple.fr") : domaine vérifié
            // quand même, mais schéma inconnu, donc pas de règle HTTPS -> HTTP.
            var reference = !string.IsNullOrWhiteSpace(raw) && !raw.Contains("://", StringComparison.Ordinal)
                ? "http://" + raw.Trim()
                : raw;
            if (!TryParseWeb(reference, out var saved))
            {
                continue;
            }

            if (saved.Scheme == Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttps)
            {
                continue;
            }

            var savedRoot = PublicSuffixService.RootDomainOf(saved.AbsoluteUri);
            if (!string.IsNullOrWhiteSpace(savedRoot) &&
                savedRoot.Equals(targetRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Mot de passe généré (inscription) : pas encore d'identifiant enregistré,
    // la référence est la page principale elle-même. Une iframe n'est
    // éligible que si elle appartient au même site que la page.
    public static bool IsSameSiteFrame(string? frameUrl, string? pageUrl) =>
        TryParseWeb(pageUrl, out var page) &&
        IsAllowedTarget(frameUrl, page.AbsoluteUri, null);

    private static bool TryParseWeb(string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(parsed.Host))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
