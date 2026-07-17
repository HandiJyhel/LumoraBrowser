using System.Security.Cryptography;

namespace Lumora.WinUI.Credentials;

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

    public const int MinPassphraseWords = 4;
    public const int MaxPassphraseWords = 10;

    // Phrase de passe : plus facile à taper/retenir qu'une suite aléatoire, tout
    // en gardant une entropie raisonnable (~270 mots, tirage crypto-sûr sans biais
    // -> ~8,1 bits/mot, ex. 5 mots ~= 40 bits). Liste locale embarquée : aucune
    // ressource externe, aucun réseau.
    public static string GeneratePassphrase(int wordCount = 5, string separator = "-")
    {
        wordCount = Math.Clamp(wordCount, MinPassphraseWords, MaxPassphraseWords);
        var words = new string[wordCount];
        for (var i = 0; i < wordCount; i++)
            words[i] = Wordlist[RandomNumberGenerator.GetInt32(Wordlist.Length)];
        return string.Join(separator, words);
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

    // ~270 mots anglais courants (3-8 lettres), sans ambiguite visuelle ni terme
    // sensible : liste locale fixe, aucune dependance externe.
    private static readonly string[] Wordlist =
    {
        "acid", "actor", "amber", "anchor", "angle", "apple", "arena", "arrow",
        "aspen", "atlas", "autumn", "badge", "baker", "banjo", "barley", "basil",
        "basin", "beach", "beacon", "bear", "beaver", "beetle", "bench", "berry",
        "birch", "bison", "blade", "blaze", "bloom", "blue", "boat", "bonus",
        "boot", "brave", "breeze", "brick", "bridge", "brook", "bronze", "brush",
        "cabin", "cable", "cactus", "camel", "camp", "canal", "candle", "canoe",
        "canyon", "cargo", "castle", "cedar", "cellar", "chalk", "charm", "cheese",
        "cherry", "chess", "chief", "cider", "circle", "cliff", "cloak", "clover",
        "coast", "cobalt", "coffee", "comet", "compass", "coral", "corn", "cotton",
        "coyote", "crane", "creek", "crest", "cricket", "crown", "cube", "cyan",
        "dawn", "deer", "delta", "desert", "diamond", "dolphin", "dome", "dove",
        "drift", "eagle", "earth", "east", "echo", "eel", "ember", "engine",
        "falcon", "fawn", "fern", "field", "finch", "fire", "flame", "flint",
        "flower", "forest", "forge", "fossil", "fox", "frost", "garden", "gecko",
        "gem", "giant", "ginger", "glacier", "glass", "gold", "grain", "granite",
        "grape", "grass", "gravel", "grove", "gull", "harbor", "hare", "harp",
        "harvest", "hawk", "hazel", "heron", "hill", "honey", "hornet", "horse",
        "hunter", "ibis", "iris", "island", "ivory", "jade", "jasmine", "jay",
        "jelly", "jungle", "juniper", "kestrel", "kiln", "kite", "koala", "lagoon",
        "lake", "lantern", "laurel", "leaf", "lemon", "lilac", "lily", "lime",
        "lion", "lotus", "lumber", "lunar", "lynx", "magnet", "maple", "marble",
        "marsh", "meadow", "meteor", "mint", "mirror", "mist", "moss", "moth",
        "mountain", "mulberry", "myrtle", "nectar", "nest", "nettle", "north",
        "nova", "oak", "oasis", "ocean", "olive", "onyx", "opal", "orange",
        "orbit", "orchid", "osprey", "otter", "owl", "oxide", "palm", "panda",
        "panther", "pearl", "pebble", "pepper", "petal", "pigeon", "pine", "planet",
        "plaza", "plum", "poplar", "poppy", "prairie", "puma", "quartz", "quiver",
        "rabbit", "raven", "reed", "reef", "ridge", "river", "robin", "rocket",
        "rose", "ruby", "sage", "salmon", "sand", "sapling", "sequoia", "shell",
        "shore", "silver", "sky", "slate", "sloth", "smoke", "snail", "sparrow",
        "spring", "spruce", "star", "stone", "storm", "stream", "summit", "swan",
        "tempest", "thistle", "thunder", "tiger", "timber", "topaz", "torch", "trail",
        "trout", "tulip", "tundra", "turtle", "valley", "velvet", "violet", "walnut",
        "walrus", "warbler", "willow", "winter", "wolf", "zephyr"
    };
}
