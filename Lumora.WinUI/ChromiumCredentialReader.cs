using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Lumora.WinUI;

// Pont Chromium → coffre souverain.
//
// Les navigateurs bases sur Chromium (WebView2, mais aussi Chrome, Edge, Brave,
// Vivaldi, Opera... une fois installes a cote de Lumora) enregistrent les mots de
// passe dans une base SQLite "Login Data", chiffres en AES-256-GCM. La cle maitre
// est rangee dans "Local State" (JSON), elle-meme protegee par DPAPI (compte
// Windows courant) — donc lisible par n'importe quel processus tournant sous ce
// meme compte, quel que soit le navigateur qui a chiffre a l'origine.
//
// Ce lecteur dechiffre ces identifiants pour les importer dans vault.lumora, afin
// que le coffre de l'utilisateur reflete reellement ses mots de passe : soit ceux
// que Chromium aurait enregistres avant la bascule 100% maison (migration interne),
// soit ceux d'un autre navigateur installe sur la machine (import volontaire,
// declenche par l'utilisateur depuis le gestionnaire de mots de passe).
internal static class ChromiumCredentialReader
{
    // browserDataFolder = valeur de WEBVIEW2_USER_DATA_FOLDER (_profile.BrowserDataDir).
    // Chromium crée un sous-dossier "EBWebView" dedans.
    public static List<(string origin, string username, string password)> Read(string browserDataFolder)
    {
        var ebDir = Path.Combine(browserDataFolder, "EBWebView");
        return ReadFrom(Path.Combine(ebDir, "Local State"), Path.Combine(ebDir, "Default", "Login Data"));
    }

    // Lecture générique : localStatePath porte la clé maître DPAPI, loginDataPath
    // la base SQLite des identifiants. Utilisé aussi bien pour le magasin interne
    // (Read ci-dessus) que pour un navigateur externe installé (PasswordImportSource).
    public static List<(string origin, string username, string password)> ReadFrom(string localStatePath, string loginDataPath)
    {
        var result = new List<(string, string, string)>();
        try
        {
            if (!File.Exists(localStatePath) || !File.Exists(loginDataPath)) return result;

            var key = GetMasterKey(localStatePath);
            if (key is null) return result;

            // "Login Data" peut être ouvert par le navigateur → on travaille sur une copie temporaire.
            var tmp = Path.Combine(Path.GetTempPath(), "nova_login_" + Guid.NewGuid().ToString("N") + ".db");
            File.Copy(loginDataPath, tmp, true);
            try
            {
                using var conn = new SqliteConnection($"Data Source={tmp};Mode=ReadOnly");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT origin_url, username_value, password_value FROM logins WHERE blacklisted_by_user = 0";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var url = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
                    var user = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (reader[2] is not byte[] blob || blob.Length == 0) continue;
                    var pw = DecryptPassword(blob, key);
                    if (string.IsNullOrEmpty(pw)) continue;
                    result.Add((OriginOf(url), user, pw));
                }
            }
            finally { try { File.Delete(tmp); } catch { } }
        }
        catch { }
        return result;
    }

    // Décompte rapide (sans déchiffrement) pour l'affichage de la liste de sources
    // détectées, avant que l'utilisateur choisisse une source à importer.
    public static int CountLogins(string loginDataPath)
    {
        try
        {
            if (!File.Exists(loginDataPath)) return 0;
            var tmp = Path.Combine(Path.GetTempPath(), "nova_login_count_" + Guid.NewGuid().ToString("N") + ".db");
            File.Copy(loginDataPath, tmp, true);
            try
            {
                using var conn = new SqliteConnection($"Data Source={tmp};Mode=ReadOnly");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM logins WHERE blacklisted_by_user = 0";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            finally { try { File.Delete(tmp); } catch { } }
        }
        catch { return 0; }
    }

    private static byte[]? GetMasterKey(string localStatePath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(localStatePath));
            var b64 = doc.RootElement.GetProperty("os_crypt").GetProperty("encrypted_key").GetString();
            if (b64 is null) return null;
            var raw = Convert.FromBase64String(b64);
            // Préfixe ASCII "DPAPI" (5 octets) avant le blob DPAPI.
            if (raw.Length > 5 && Encoding.ASCII.GetString(raw, 0, 5) == "DPAPI")
                raw = raw[5..];
            return ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser);
        }
        catch { return null; }
    }

    private static string DecryptPassword(byte[] blob, byte[] key)
    {
        try
        {
            // Format moderne "v10"/"v11" : préfixe(3) + nonce(12) + ciphertext + tag(16).
            if (blob.Length > 31 && blob[0] == (byte)'v' && blob[1] == (byte)'1')
            {
                var nonce  = blob[3..15];
                var tag    = blob[^16..];
                var cipher = blob[15..^16];
                var plain  = new byte[cipher.Length];
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            // Ancien format : DPAPI direct sur tout le blob.
            var dec = ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }
        catch { return string.Empty; }
    }

    private static string OriginOf(string url)
    {
        try { var u = new Uri(url); return $"{u.Scheme}://{u.Host}"; }
        catch { return url; }
    }
}
