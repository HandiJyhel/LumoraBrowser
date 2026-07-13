namespace Lumora.WinUI;

// Logique pure des cartes de paiement (validation, affichage) — aucune dépendance
// UI, testable par dotnet test.
internal static class PaymentCardUtil
{
    // Ne garde que les chiffres (espaces, tirets et points de saisie tolérés).
    public static string NormalizeNumber(string raw) =>
        new((raw ?? string.Empty).Where(char.IsDigit).ToArray());

    // Longueur plausible (12-19 chiffres) + somme de Luhn : détecte les fautes
    // de frappe, pas la validité bancaire réelle.
    public static bool IsValidNumber(string raw)
    {
        var digits = NormalizeNumber(raw);
        if (digits.Length is < 12 or > 19) return false;

        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }

    // Réseau de la carte d'après les préfixes publics (pour l'icône/le titre).
    public static string BrandOf(string raw)
    {
        var d = NormalizeNumber(raw);
        if (d.Length < 2) return "Carte";
        if (d[0] == '4') return "Visa";
        if (d.StartsWith("34") || d.StartsWith("37")) return "American Express";
        if (d[0] == '5' && d[1] is >= '1' and <= '5') return "Mastercard";
        if (d.Length >= 4 && int.TryParse(d[..4], out var p4) && p4 is >= 2221 and <= 2720) return "Mastercard";
        if (d.StartsWith("6011") || d.StartsWith("65")) return "Discover";
        return "Carte";
    }

    public static string Last4(string raw)
    {
        var d = NormalizeNumber(raw);
        return d.Length >= 4 ? d[^4..] : d;
    }

    // Affichage masqué : seuls les 4 derniers chiffres sont visibles.
    public static string MaskedNumber(string raw) => $"•••• •••• •••• {Last4(raw)}";

    // Année sur 2 chiffres acceptée à la saisie (27 → 2027).
    public static int NormalizeYear(int year) => year is >= 0 and <= 99 ? 2000 + year : year;

    public static bool IsValidExpiry(int month, int year)
    {
        var y = NormalizeYear(year);
        return month is >= 1 and <= 12 && y is >= 2000 and <= 2099;
    }

    // Expirée = le mois d'expiration est déjà passé (la carte reste valable
    // jusqu'au dernier jour de son mois).
    public static bool IsExpired(int month, int year, DateTime nowUtc)
    {
        var y = NormalizeYear(year);
        return y < nowUtc.Year || (y == nowUtc.Year && month < nowUtc.Month);
    }

    public static string ExpiryText(int month, int year) =>
        $"{month:00}/{NormalizeYear(year) % 100:00}";
}
