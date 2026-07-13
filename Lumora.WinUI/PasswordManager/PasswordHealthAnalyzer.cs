namespace Lumora.WinUI.PasswordManager;

internal enum PasswordStrength
{
    Weak,
    Medium,
    Strong
}

internal sealed record PasswordHealthReport(
    int TotalCount,
    IReadOnlyList<IReadOnlyList<VaultCredential>> ReusedGroups,
    IReadOnlyList<VaultCredential> WeakCredentials,
    IReadOnlyList<VaultCredential> StaleCredentials)
{
    public int ReusedCount => ReusedGroups.Sum(group => group.Count);
    public bool IsHealthy => ReusedGroups.Count == 0 && WeakCredentials.Count == 0 && StaleCredentials.Count == 0;
}

// Bilan de santé du coffre : réutilisés, faibles, anciens. Analyse 100% locale,
// aucune donnée (ni hash) ne quitte la machine — c'est la règle du projet. La
// vérification « compromis dans une fuite » (type Have I Been Pwned) exigerait
// une requête sortante : exclue tant qu'elle n'a pas été explicitement validée.
internal static class PasswordHealthAnalyzer
{
    // Un mot de passe non changé depuis 2 ans est signalé comme ancien : assez
    // long pour ne pas harceler, assez court pour attraper les comptes oubliés.
    public static readonly TimeSpan StaleAge = TimeSpan.FromDays(730);

    // Mots de passe triviaux les plus posés en France (minuscules, comparaison insensible à la casse).
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456", "1234567", "12345678", "123456789", "1234567890",
        "password", "password1", "motdepasse", "azerty", "azerty123",
        "azertyuiop", "qwerty", "qwerty123", "abc123", "admin",
        "welcome", "letmein", "iloveyou", "jetaime", "bonjour",
        "soleil", "chocolat", "doudou", "loulou", "marseille",
        "000000", "111111", "654321",
    };

    public static PasswordHealthReport Analyze(IReadOnlyList<VaultCredential> credentials, DateTimeOffset? now = null)
    {
        var reference = now ?? DateTimeOffset.UtcNow;
        var staleBefore = reference.Subtract(StaleAge).ToUnixTimeSeconds();

        // Réutilisation : même mot de passe exact sur au moins deux sites distincts.
        // Deux comptes du même site partageant un mot de passe ne sont pas signalés
        // (cas volontaire fréquent : compte perso + compte pro).
        var reusedGroups = credentials
            .Where(cred => !string.IsNullOrEmpty(cred.Password))
            .GroupBy(cred => cred.Password, StringComparer.Ordinal)
            .Where(group => group.Select(cred => cred.Origin)
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .Count() >= 2)
            .Select(group => (IReadOnlyList<VaultCredential>)group.ToList())
            .OrderByDescending(group => group.Count)
            .ToList();

        var weak = credentials
            .Where(cred => EvaluateStrength(cred.Password) == PasswordStrength.Weak)
            .ToList();

        // Ancien = jamais mis à jour depuis StaleAge. Les entrées sans date (imports
        // historiques, UpdatedAt = 0) ne sont pas signalées : on ne sait rien d'elles.
        var stale = credentials
            .Where(cred =>
            {
                var updated = cred.UpdatedAt > 0 ? cred.UpdatedAt : cred.CreatedAt;
                return updated > 0 && updated < staleBefore;
            })
            .ToList();

        return new PasswordHealthReport(credentials.Count, reusedGroups, weak, stale);
    }

    public static PasswordStrength EvaluateStrength(string password)
    {
        if (string.IsNullOrEmpty(password)) return PasswordStrength.Weak;
        if (CommonPasswords.Contains(password)) return PasswordStrength.Weak;
        if (password.Length < 8) return PasswordStrength.Weak;
        if (password.Distinct().Count() == 1) return PasswordStrength.Weak;

        var classes = CharacterClassCount(password);
        if (classes <= 1) return PasswordStrength.Weak;

        return password.Length >= 12 && classes >= 3
            ? PasswordStrength.Strong
            : PasswordStrength.Medium;
    }

    private static int CharacterClassCount(string password)
    {
        var lower = false; var upper = false; var digit = false; var other = false;
        foreach (var ch in password)
        {
            if (char.IsLower(ch)) lower = true;
            else if (char.IsUpper(ch)) upper = true;
            else if (char.IsDigit(ch)) digit = true;
            else other = true;
        }

        return (lower ? 1 : 0) + (upper ? 1 : 0) + (digit ? 1 : 0) + (other ? 1 : 0);
    }
}
