namespace Lumora.WinUI.Sessions;

// Logique pure : décide si Lumora doit proposer « Rester connecté ? » après un
// login détecté. Ne fait aucune E/S ; les listes de sites de confiance et de
// refus viennent de UiSettings, la décision de purge en dépend directement.
internal static class SessionKeepAdvisor
{
    public static bool ShouldOfferKeepSession(
        bool sessionPurgeEnabled,
        IEnumerable<string> trustedSites,
        IEnumerable<string> declinedSites,
        string rootDomain)
    {
        if (!sessionPurgeEnabled || string.IsNullOrWhiteSpace(rootDomain))
        {
            return false;
        }

        if (trustedSites.Contains(rootDomain, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return !declinedSites.Contains(rootDomain, StringComparer.OrdinalIgnoreCase);
    }
}
