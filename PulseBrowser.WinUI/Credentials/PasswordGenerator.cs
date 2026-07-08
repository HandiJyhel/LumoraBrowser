using System.Security.Cryptography;

namespace PulseBrowser.WinUI.Credentials;

// Génère des mots de passe aléatoires cryptographiquement sûrs (RandomNumberGenerator,
// tirage sans biais). Les jeux de caractères excluent les ambigus (0/O/1/l/I) et le
// résultat contient au moins un caractère de chaque classe active.
internal static class PasswordGenerator
{
    private const string Lower   = "abcdefghijkmnopqrstuvwxyz";  // sans « l »
    private const string Upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // sans « I », « O »
    private const string Digits  = "23456789";                   // sans « 0 », « 1 »
    private const string Symbols = "!@#$%^&*()-_=+[]{}?";

    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static string Generate(int length = 20, bool useSymbols = true)
    {
        length = Math.Clamp(length, MinLength, MaxLength);

        var classes = new List<string> { Lower, Upper, Digits };
        if (useSymbols) classes.Add(Symbols);
        var alphabet = string.Concat(classes);

        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Pick(alphabet);

        // Garantit au moins un caractère de chaque classe active, à des positions
        // distinctes tirées au hasard (length >= 8 >= nombre de classes).
        var positions = Enumerable.Range(0, length).ToArray();
        Shuffle(positions);
        for (var i = 0; i < classes.Count; i++)
            chars[positions[i]] = Pick(classes[i]);

        return new string(chars);
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];

    private static void Shuffle(int[] values)
    {
        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
