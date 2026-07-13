using Lumora.WinUI.Credentials;

namespace Lumora.WinUI;

// Logique pure : décide si une page chargée dans une fenêtre d'application web
// est toujours dans le périmètre de l'app installée. Confinement « doux » :
// jamais de blocage de navigation (casserait les redirections OAuth type
// Google/Microsoft), seulement un signal visuel avec retour possible.
internal static class WebAppUrlPolicy
{
    public static bool IsWithinAppScope(string appRootDomain, string navigatingUrl)
    {
        if (string.IsNullOrWhiteSpace(appRootDomain))
        {
            return true;
        }

        // Schémas internes (lumora://, about:, data:...) : pas de notion de domaine
        // HTTP, on ne les considère jamais comme une sortie de périmètre.
        if (!Uri.TryCreate(navigatingUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return true;
        }

        var targetRoot = PublicSuffixService.RootDomainOf(navigatingUrl);
        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            return true;
        }

        return string.Equals(targetRoot, appRootDomain, StringComparison.OrdinalIgnoreCase);
    }
}
