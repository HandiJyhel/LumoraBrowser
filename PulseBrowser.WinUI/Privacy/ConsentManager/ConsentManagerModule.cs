namespace PulseBrowser.Privacy.ConsentManager;

// Gestion automatique des bandeaux cookies (RGPD/CCPA).
// Deux mécanismes complémentaires :
//   1. Stubs IAB TCF v1/v2 — répond "refus total" aux CMPs qui interrogent window.__cmp/__tcfapi
//      avant même qu'ils n'affichent leur bandeau. Couvre ~60-70% des sites.
//   2. Clics automatiques sur "Refuser tout" — sélecteurs CSS pour +20 CMPs majeurs,
//      plus correspondance par texte ("tout refuser", "reject all"…) pour les bandeaux maison.
//      Utilise MutationObserver pour les CMPs qui chargent après le DOM.
//
// Aucune donnée n'est envoyée à un serveur tiers. Tout se passe en JS local dans la page.
internal sealed class ConsentManagerModule : IPrivacyModule
{
    public string Id          => "consent-manager";
    public string DisplayName => "Refus automatique des cookies";
    public bool IsEnabled { get; set; } = true;

    public string BuildInjectionScript() => ConsentManagerScripts.InjectionScript;
    public static string RetryScript     => ConsentManagerScripts.RetryScript;
}
