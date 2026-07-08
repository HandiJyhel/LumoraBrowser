using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace PulseBrowser.WinUI;

// Pont Chromium → coffre souverain.
//
// Le moteur Chromium (WebView2) enregistre les mots de passe de façon fiable dans
// une base SQLite "Login Data", chiffrés en AES-256-GCM. La clé maître est rangée
// dans "Local State" (JSON), elle-même protégée par DPAPI (compte Windows courant).
//
// Ce lecteur déchiffre ces identifiants pour les importer dans vault.pulse, afin que
// le coffre de l'utilisateur reflète réellement ses mots de passe.
internal static class ChromiumCredentialReader
{
    // browserDataFolder = valeur de WEBVIEW2_USER_DATA_FOLDER (_profile.BrowserDataDir).
    // Chromium crée un sous-dossier "EBWebView" dedans.
    public static List<(string origin, string username, string password)> Read(string browserDataFolder)
    {
        var result = new List<(string, string, string)>();
        try
        {
            var ebDir      = Path.Combine(browserDataFolder, "EBWebView");
            var localState = Path.Combine(ebDir, "Local State");
            var loginData  = Path.Combine(ebDir, "Default", "Login Data");
            if (!File.Exists(localState) || !File.Exists(loginData)) return result;

            var key = GetMasterKey(localState);
            if (key is null) return result;

            // "Login Data" est ouvert par WebView2 → on travaille sur une copie temporaire.
            var tmp = Path.Combine(Path.GetTempPath(), "pulse_login_" + Guid.NewGuid().ToString("N") + ".db");
            File.Copy(loginData, tmp, true);
            try
            {
                using var conn = new SqliteConnection($"Data Source={tmp};Mode=ReadOnly");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT origin_url, username_value, password_value FROM logins";
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
