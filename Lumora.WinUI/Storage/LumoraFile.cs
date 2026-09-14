using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Lumora.WinUI;

// ── Chiffrement DPAPI des fichiers .lumora ───────────────────────────────────

internal static class LumoraFile
{
    // Entropie spécifique au projet pour distinguer nos fichiers d'autres données DPAPI
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Lumora.WinUI.v1");
    private static readonly byte[] LegacyNovaEntropy = Encoding.UTF8.GetBytes("NovaBrowser.WinUI.v1");
    private static readonly byte[] LegacyPulseEntropy = Encoding.UTF8.GetBytes("PulseBrowser.WinUI.v1");

    // Entropie additionnelle propre au profil courant (2026-09-10, comptes
    // sans mot de passe - voir ProfileEntropyStore.cs). Ambiante plutot que
    // parametre thread a travers chaque appelant (Bookmarks, History, Notes,
    // UiSettings, VaultStore... une quinzaine de fichiers) : posee UNE fois
    // au demarrage (MainWindow.xaml.cs, avant tout chargement de fichier
    // .lumora) via SetProfileEntropy, lue implicitement ici. Reste vide par
    // defaut = comportement EXACTEMENT identique a avant cette fonctionnalite
    // - aucun risque pour les profils AVEC mot de passe, pour lesquels rien
    // n'appelle jamais SetProfileEntropy.
    private static byte[] _profileEntropy = Array.Empty<byte>();

    public static void SetProfileEntropy(byte[] entropy) => _profileEntropy = entropy ?? Array.Empty<byte>();

    public static void ClearProfileEntropy() => _profileEntropy = Array.Empty<byte>();

    // UserProfile.Save/Load (Models/UserProfile.cs) chiffrent profile.lumora
    // eux-memes, en dehors de WriteAllText/ReadAllText ci-dessus (entropie
    // DPAPI null de tout temps, decouvert en verifiant en direct le
    // 2026-09-10 - la combinaison Entropy+_profileEntropy ci-dessus ne
    // s'appliquait donc jamais a ce fichier precis). Expose la meme entropie
    // ambiante pour que ce fichier en beneficie aussi, sans dupliquer l'etat :
    // vide (null) tant que rien ne l'a posee = comportement EXACTEMENT
    // identique a avant cette fonctionnalite pour un profil AVEC mot de
    // passe (jamais pose pour eux).
    public static byte[]? CurrentProfileEntropyOrNull() =>
        _profileEntropy.Length == 0 ? null : _profileEntropy;

    public static void WriteAllText(string path, string content)
    {
        var plain  = Encoding.UTF8.GetBytes(content);
        var cipher = ProtectedData.Protect(plain, CurrentEntropy(), DataProtectionScope.CurrentUser);
        var dir    = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, cipher);
    }

    public static string ReadAllText(string path)
    {
        var cipher = File.ReadAllBytes(path);
        var plain  = UnprotectWithCompatibility(cipher);
        return Encoding.UTF8.GetString(plain);
    }

    public static string? TryReadAllText(string path)
    {
        try   { return File.Exists(path) ? ReadAllText(path) : null; }
        catch { return null; }
    }

    private static byte[] CurrentEntropy() =>
        _profileEntropy.Length == 0 ? Entropy : Entropy.Concat(_profileEntropy).ToArray();

    private static byte[] UnprotectWithCompatibility(byte[] cipher)
    {
        try
        {
            return ProtectedData.Unprotect(cipher, CurrentEntropy(), DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            // Migration transparente (2026-09-10) : un fichier ecrit avant
            // l'ajout de l'entropie de profil (ou pendant que ce profil avait
            // encore un mot de passe) ne l'a pas dans sa combinaison - on
            // retombe sur l'entropie fixe seule avant les entropies "legacy"
            // de renommage. Prochaine ecriture re-chiffre avec la nouvelle
            // combinaison, aucune migration explicite necessaire.
            if (_profileEntropy.Length > 0)
            {
                try
                {
                    return ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
                }
                catch (CryptographicException) { }
            }

            try
            {
                return ProtectedData.Unprotect(cipher, LegacyNovaEntropy, DataProtectionScope.CurrentUser);
            }
            catch (CryptographicException)
            {
                return ProtectedData.Unprotect(cipher, LegacyPulseEntropy, DataProtectionScope.CurrentUser);
            }
        }
    }
}
