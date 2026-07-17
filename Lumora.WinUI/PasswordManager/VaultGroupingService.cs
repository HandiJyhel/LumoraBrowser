using Lumora.WinUI.Credentials;

namespace Lumora.WinUI.PasswordManager;

internal enum VaultSortMode
{
    Recent,
    Alphabetical
}

// Un groupe = un site (sous-domaines fusionnés sur le domaine racine, comme les
// doublons d'import dans PasswordManagerService). Les comptes du groupe restent
// triés du plus récent au plus ancien, quel que soit le tri des groupes entre eux.
internal sealed record VaultCredentialGroup(string DisplayName, IReadOnlyList<VaultCredential> Credentials)
{
    public long LatestUpdatedAt => Credentials.Count == 0 ? 0 : Credentials.Max(c => c.UpdatedAt);
}

internal static class VaultGroupingService
{
    public static IReadOnlyList<VaultCredentialGroup> Group(
        IReadOnlyList<VaultCredential> credentials,
        VaultSortMode sortMode)
    {
        var groups = credentials
            .GroupBy(SiteKey, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var ordered = g
                    .OrderByDescending(c => c.UpdatedAt)
                    .ThenBy(c => c.Username, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                return new VaultCredentialGroup(DisplayName(g.Key), ordered);
            })
            .ToList();

        return sortMode switch
        {
            VaultSortMode.Alphabetical => groups
                .OrderBy(g => g.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            _ => groups
                .OrderByDescending(g => g.LatestUpdatedAt)
                .ToList()
        };
    }

    // Même clé que la détection de doublons de PasswordManagerService : domaine
    // racine, ou host complet si aucun domaine racine n'est reconnu.
    private static string SiteKey(VaultCredential credential)
    {
        var root = PublicSuffixService.RootDomainOf(credential.Origin);
        return string.IsNullOrWhiteSpace(root)
            ? PublicSuffixService.HostOf(credential.Origin).ToLowerInvariant()
            : root.ToLowerInvariant();
    }

    private static string DisplayName(string siteKey) =>
        siteKey.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && siteKey.Length > 4
            ? siteKey[4..]
            : siteKey;
}
