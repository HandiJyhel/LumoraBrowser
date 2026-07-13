namespace Lumora.Privacy.ConsentManager;

// Gestion automatique des bandeaux cookies (RGPD/CCPA).
// Refus automatique non destructif : Lumora clique sur un bouton visible "Refuser
// tout" quand le site le propose clairement, mais n'émule plus un consentement TCF
// global avant l'initialisation du site. Certains sites lient leur connexion à leur
// CMP ; un faux refus précoce peut rendre le bouton Connexion inerte.
//
// Aucune donnée n'est envoyée à un serveur tiers. Tout se passe en JS local dans la page.
internal sealed class ConsentManagerModule : IPrivacyModule
{
    public string Id          => "consent-manager";
    public string DisplayName => "Refus automatique des cookies";
    public bool IsEnabled { get; set; } = true;

    private readonly HashSet<string> _loginCompatibilitySites = new(StringComparer.OrdinalIgnoreCase);

    public void SetLoginCompatibilitySites(IEnumerable<string> rootDomains)
    {
        _loginCompatibilitySites.Clear();
        foreach (var domain in rootDomains)
        {
            var normalized = domain.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalized))
                _loginCompatibilitySites.Add(normalized);
        }
    }

    public string BuildInjectionScript() => ConsentManagerScripts.BuildInjectionScript(_loginCompatibilitySites);
    public string BuildRetryScript() => ConsentManagerScripts.BuildRetryScript(_loginCompatibilitySites);
}
