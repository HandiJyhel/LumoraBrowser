using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Lumora.WinUI.PasswordManager;

// Verification "mot de passe compromis dans une fuite connue", par k-anonymat
// (API publique Have I Been Pwned, pwnedpasswords.com/api/v3#SearchingPwnedPasswordsByRange).
// Seuls les 5 premiers caracteres hexa du SHA-1 du mot de passe partent sur le
// reseau - jamais le mot de passe, jamais son hash complet. Le SHA-1 ici sert
// uniquement de cle de recherche pour ce protocole tiers, pas de mecanisme de
// protection Lumora (le coffre reste chiffre AES-256-GCM/Argon2id independamment).
//
// Explicitement declenchee par l'utilisateur (bouton dans le bilan de sante du
// coffre), jamais automatique - meme regle que tout le reste du projet pour une
// requete sortante (voir FilterListManager).
internal static class BreachChecker
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private const string RangeUrlFormat = "https://api.pwnedpasswords.com/range/{0}";

    // Nombre de fuites connues contenant ce mot de passe exact (0 = non trouve).
    public static async Task<int> CheckAsync(string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(password)) return 0;

        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
        var prefix = hash[..5];
        var suffix = hash[5..];

        var response = await Http.GetStringAsync(string.Format(RangeUrlFormat, prefix), cancellationToken);
        foreach (var rawLine in response.Split('\n'))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf(':');
            if (separator < 0) continue;

            var candidateSuffix = line[..separator];
            if (!string.Equals(candidateSuffix, suffix, StringComparison.OrdinalIgnoreCase)) continue;

            return int.TryParse(line[(separator + 1)..], out var count) ? count : 0;
        }

        return 0;
    }
}
