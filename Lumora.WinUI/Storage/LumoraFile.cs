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

    public static void WriteAllText(string path, string content)
    {
        var plain  = Encoding.UTF8.GetBytes(content);
        var cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
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

    private static byte[] UnprotectWithCompatibility(byte[] cipher)
    {
        try
        {
            return ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
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
