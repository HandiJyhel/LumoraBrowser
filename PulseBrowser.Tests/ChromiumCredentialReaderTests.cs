using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PulseBrowser.WinUI;
using Xunit;

namespace PulseBrowser.Tests;

// Fabrique un couple ("Local State", "Login Data") au format Chromium reel
// (cle maitre DPAPI dans Local State, mots de passe AES-256-GCM "v10" dans la
// base SQLite) pour prouver que ChromiumCredentialReader dechiffre correctement
// un profil externe (Chrome, Edge, Brave...), pas seulement le magasin interne.
public sealed class ChromiumCredentialReaderTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "pulse_chromium_fixture_" + Guid.NewGuid().ToString("N"));

    public ChromiumCredentialReaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void ReadFrom_dechiffre_les_identifiants_dun_profil_externe()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var localState = WriteLocalState(key);
        var loginData = WriteLoginData(
            key,
            ("https://exemple.fr/login", "alice", "s3cret"),
            ("https://autre.com/", "bob", "hunter2"));

        var creds = ChromiumCredentialReader.ReadFrom(localState, loginData);

        Assert.Equal(2, creds.Count);
        Assert.Contains(creds, c => c.origin == "https://exemple.fr" && c.username == "alice" && c.password == "s3cret");
        Assert.Contains(creds, c => c.origin == "https://autre.com" && c.username == "bob" && c.password == "hunter2");
    }

    [Fact]
    public void ReadFrom_ignore_les_lignes_blacklistees_par_lutilisateur()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var localState = WriteLocalState(key);
        var loginData = WriteLoginData(key, blacklisted: true, ("https://exemple.fr/", "alice", "s3cret"));

        var creds = ChromiumCredentialReader.ReadFrom(localState, loginData);

        Assert.Empty(creds);
    }

    [Fact]
    public void CountLogins_compte_sans_dechiffrer()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var loginData = WriteLoginData(
            key,
            ("https://exemple.fr/", "alice", "s3cret"),
            ("https://autre.com/", "bob", "hunter2"));

        Assert.Equal(2, ChromiumCredentialReader.CountLogins(loginData));
    }

    [Fact]
    public void ReadFrom_renvoie_vide_si_les_fichiers_sont_absents()
    {
        var creds = ChromiumCredentialReader.ReadFrom(
            Path.Combine(_dir, "manquant", "Local State"),
            Path.Combine(_dir, "manquant", "Login Data"));

        Assert.Empty(creds);
    }

    private string WriteLocalState(byte[] key)
    {
        var protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
        var prefixed = new byte[5 + protectedKey.Length];
        Encoding.ASCII.GetBytes("DPAPI").CopyTo(prefixed, 0);
        protectedKey.CopyTo(prefixed, 5);
        var b64 = Convert.ToBase64String(prefixed);

        var path = Path.Combine(_dir, "Local State");
        File.WriteAllText(path, "{\"os_crypt\":{\"encrypted_key\":\"" + b64 + "\"}}");
        return path;
    }

    private string WriteLoginData(byte[] key, params (string url, string user, string pass)[] rows) =>
        WriteLoginData(key, blacklisted: false, rows);

    private string WriteLoginData(byte[] key, bool blacklisted, params (string url, string user, string pass)[] rows)
    {
        var path = Path.Combine(_dir, "Login Data");
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using (var create = conn.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE logins (
                    origin_url TEXT,
                    username_value TEXT,
                    password_value BLOB,
                    blacklisted_by_user INTEGER DEFAULT 0
                )
                """;
            create.ExecuteNonQuery();
        }

        foreach (var (url, user, pass) in rows)
        {
            using var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO logins (origin_url, username_value, password_value, blacklisted_by_user) VALUES ($u, $n, $p, $b)";
            insert.Parameters.AddWithValue("$u", url);
            insert.Parameters.AddWithValue("$n", user);
            insert.Parameters.AddWithValue("$p", EncryptPasswordV10(key, pass));
            insert.Parameters.AddWithValue("$b", blacklisted ? 1 : 0);
            insert.ExecuteNonQuery();
        }

        return path;
    }

    // Reproduit le format Chromium "v10" : "v10"(3) + nonce(12) + cipher + tag(16).
    private static byte[] EncryptPasswordV10(byte[] key, string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var blob = new byte[3 + nonce.Length + cipher.Length + tag.Length];
        Encoding.ASCII.GetBytes("v10").CopyTo(blob, 0);
        nonce.CopyTo(blob, 3);
        cipher.CopyTo(blob, 3 + nonce.Length);
        tag.CopyTo(blob, 3 + nonce.Length + cipher.Length);
        return blob;
    }
}
