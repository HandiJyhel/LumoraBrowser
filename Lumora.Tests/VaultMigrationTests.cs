using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Rétro-compatibilité : un coffre écrit par l'ANCIEN code (AES-256-CBC, sans le champ
// "data_cipher") doit toujours s'ouvrir, puis migrer vers AES-GCM au premier
// enregistrement. On fabrique ici une fixture au format hérité pour le prouver, car
// l'application ne sait plus produire de coffre CBC.
public sealed class VaultMigrationTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), "nova_legacy_" + Guid.NewGuid().ToString("N") + ".nova");

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    [Fact]
    public void Un_coffre_CBC_herite_s_ouvre_puis_migre_en_GCM()
    {
        const string password = "mot-de-passe-legacy";
        WriteLegacyCbcVault(password, ("https://exemple.fr", "alice", "s3cret"));

        // Ouverture : la lecture passe par le chemin CBC (data_cipher absent → "cbc").
        var vault = new VaultStore(_file);
        Assert.True(vault.IsLocked);
        Assert.True(vault.Unlock(password));
        Assert.Equal("s3cret", vault.ListCredentials().Single().Password);

        // Le fichier sur disque est encore en CBC tant qu'on n'a rien réécrit.
        Assert.Equal("cbc", DataCipherOnDisk());

        // Toute écriture (ici un ajout) doit migrer le blob en AES-GCM authentifié.
        vault.Upsert("https://autre.fr", "bob", "hunter2");
        Assert.Equal("gcm", DataCipherOnDisk());

        // Et le coffre migré se relit correctement depuis le disque.
        vault.Lock();
        var reopened = new VaultStore(_file);
        Assert.True(reopened.Unlock(password));
        Assert.Equal(2, reopened.ListCredentials().Count);
    }

    private string DataCipherOnDisk()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(_file, Encoding.UTF8));
        return doc.RootElement.TryGetProperty("data_cipher", out var v)
            ? v.GetString() ?? "cbc"
            : "cbc";
    }

    // Reproduit fidèlement l'ancien format : clé Argon2id, blob AES-256-CBC (IV ‖ chiffré),
    // en-tête JSON SANS champ "data_cipher".
    private void WriteLegacyCbcVault(string password, params (string origin, string user, string pass)[] creds)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var key = DeriveArgon2id(password, salt);

        var list = creds.Select(c => new VaultCredential
        {
            Origin = c.origin, Username = c.user, Password = c.pass,
            CreatedAt = 1, UpdatedAt = 1
        }).ToList();
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(list, new JsonSerializerOptions { WriteIndented = false });

        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;
        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(plaintext, 0, plaintext.Length);
        var blob = new byte[iv.Length + cipher.Length];
        Buffer.BlockCopy(iv, 0, blob, 0, iv.Length);
        Buffer.BlockCopy(cipher, 0, blob, iv.Length, cipher.Length);

        var header = new
        {
            version = 2,
            mode = "aes256",
            kdf = "argon2id",
            salt = Convert.ToBase64String(salt),
            argon2_mem = 65536,
            argon2_iter = 3,
            argon2_par = 4,
            data = Convert.ToBase64String(blob)
            // NB : pas de "data_cipher" → l'ancien format.
        };
        File.WriteAllText(_file, JsonSerializer.Serialize(header), Encoding.UTF8);
    }

    private static byte[] DeriveArgon2id(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = 65536,
            Iterations = 3,
            DegreeOfParallelism = 4
        };
        return argon2.GetBytes(32);
    }
}
