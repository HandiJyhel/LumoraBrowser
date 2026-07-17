using System.Security.Cryptography;

namespace Lumora.WinUI.Credentials;

// Compte TOTP prêt à l'emploi : secret normalisé (base32, majuscules, sans
// espaces) + métadonnées optionnelles récupérées d'une URI otpauth://.
internal sealed record TotpAccount(
    string Secret,
    string Issuer = "",
    string AccountName = "",
    int Digits = TotpService.DefaultDigits,
    int Period = TotpService.DefaultPeriod);

// TOTP local (RFC 6238 / HOTP RFC 4226), compatible Google Authenticator et
// équivalents : calcul purement local à partir d'un secret et de l'heure,
// aucune donnée envoyée nulle part. Seul l'algorithme SHA-1 est implémenté
// (c'est celui utilisé par la quasi-totalité des services, y compris ceux
// qui annoncent SHA-256/512 dans leur URI otpauth mais l'ignorent en
// pratique côté validation).
internal static class TotpService
{
    public const int DefaultDigits = 6;
    public const int DefaultPeriod = 30;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string GenerateCode(string base32Secret, DateTimeOffset at, int digits = DefaultDigits, int period = DefaultPeriod)
    {
        var key = Base32Decode(base32Secret);
        var counter = at.ToUnixTimeSeconds() / period;

        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);

        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff);

        var divisor = (int)Math.Pow(10, digits);
        var code = binary % divisor;
        return code.ToString().PadLeft(digits, '0');
    }

    // Secondes restantes avant la rotation du code courant (pour l'anneau de
    // compte à rebours affiché à côté du code).
    public static int SecondsRemaining(DateTimeOffset at, int period = DefaultPeriod)
    {
        var elapsed = (int)(at.ToUnixTimeSeconds() % period);
        return period - elapsed;
    }

    public static bool IsValidSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        try
        {
            return Base32Decode(secret).Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Point d'entrée unique pour la saisie utilisateur : accepte soit un
    // secret base32 collé tel quel, soit une URI otpauth:// complète (export
    // de la plupart des applications d'authentification).
    public static TotpAccount? ParseSecretInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        if (trimmed.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseOtpAuthUri(trimmed);
        }

        var normalized = NormalizeSecret(trimmed);
        return IsValidSecret(normalized) ? new TotpAccount(normalized) : null;
    }

    private static TotpAccount? TryParseOtpAuthUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!uri.Scheme.Equals("otpauth", StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals("totp", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parameters = ParseQuery(uri.Query);
        if (!parameters.TryGetValue("secret", out var rawSecret) || string.IsNullOrWhiteSpace(rawSecret))
        {
            return null;
        }

        var secret = NormalizeSecret(rawSecret);
        if (!IsValidSecret(secret))
        {
            return null;
        }

        var label = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var issuer = parameters.TryGetValue("issuer", out var issuerParam) ? issuerParam : string.Empty;
        var accountName = label;
        var separator = label.IndexOf(':');
        if (separator >= 0)
        {
            if (string.IsNullOrWhiteSpace(issuer))
            {
                issuer = label[..separator];
            }
            accountName = label[(separator + 1)..];
        }

        var digits = parameters.TryGetValue("digits", out var digitsRaw) && int.TryParse(digitsRaw, out var d) ? d : DefaultDigits;
        var period = parameters.TryGetValue("period", out var periodRaw) && int.TryParse(periodRaw, out var p) ? p : DefaultPeriod;

        return new TotpAccount(secret, issuer.Trim(), accountName.Trim(), digits, period);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..eq]);
            var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            result[key] = value;
        }
        return result;
    }

    private static string NormalizeSecret(string raw) =>
        raw.Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();

    private static byte[] Base32Decode(string input)
    {
        var cleaned = input.TrimEnd('=');
        if (cleaned.Length == 0)
        {
            throw new FormatException("Secret TOTP vide.");
        }

        var bits = 0;
        var value = 0;
        var output = new List<byte>(cleaned.Length * 5 / 8);
        foreach (var c in cleaned)
        {
            var index = Base32Alphabet.IndexOf(c);
            if (index < 0)
            {
                throw new FormatException($"Caractere invalide dans le secret TOTP : '{c}'.");
            }

            value = (value << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return output.ToArray();
    }
}
