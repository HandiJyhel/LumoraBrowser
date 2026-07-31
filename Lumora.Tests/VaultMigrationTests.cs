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

    // Retro-compatibilite : un coffre GCM ecrit AVANT l'introduction de l'AAD
    // (donnee authentifiee additionnelle) doit rester lisible, puis etre
    // reecrit AVEC l'AAD des le prochain enregistrement. On fabrique ici un
    // blob GCM sans AAD (equivalent a une AAD vide), reproduisant fidelement
    // ce qu'ecrivait VaultStore avant ce correctif.
    [Fact]
    public void Un_coffre_GCM_sans_AAD_s_ouvre_puis_est_reecrit_avec_AAD()
    {
        const string password = "mot-de-passe-pre-aad";
        WriteLegacyGcmVaultWithoutAad(password, ("https://exemple.fr", "alice", "s3cret"));

        var vault = new VaultStore(_file);
        Assert.True(vault.IsLocked);
        Assert.True(vault.Unlock(password));
        Assert.Equal("s3cret", vault.ListCredentials().Single().Password);

        // Le blob sur disque n'a pas encore d'AAD tant qu'on n'a rien reecrit :
        // le dechiffrer avec l'AAD attendue par le code actuel doit encore echouer.
        Assert.False(TryDecryptDataBlobWithAad(GetKeyForCurrentHeader(password)));

        // Toute ecriture doit reecrire le blob avec l'AAD.
        vault.Upsert("https://autre.fr", "bob", "hunter2");
        Assert.True(TryDecryptDataBlobWithAad(GetKeyForCurrentHeader(password)));

        // Et le coffre migre se relit correctement depuis le disque.
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

    private byte[] GetKeyForCurrentHeader(string password)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(_file, Encoding.UTF8));
        var salt = Convert.FromBase64String(doc.RootElement.GetProperty("salt").GetString()!);
        return DeriveArgon2id(password, salt);
    }

    // Reproduit precisement TryDecrypt("data", ..., aad: "Lumora.Vault.Data.v1")
    // pour prouver, depuis l'exterieur, que le blob sur disque exige desormais
    // cette AAD precise (donc qu'il a bien ete reecrit apres le correctif).
    private bool TryDecryptDataBlobWithAad(byte[] key)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(_file, Encoding.UTF8));
        var dataBase64 = doc.RootElement.GetProperty("data").GetString()!;
        var bytes = Convert.FromBase64String(dataBase64);
        var aad = Encoding.UTF8.GetBytes("Lumora.Vault.Data.v1");
        const int nonceSize = 12, tagSize = 16;
        var nonce = bytes[..nonceSize];
        var tag = bytes[nonceSize..(nonceSize + tagSize)];
        var cipher = bytes[(nonceSize + tagSize)..];
        var plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key, tagSize);
            aes.Decrypt(nonce, cipher, tag, plain, aad);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    // Reproduit fidelement ce que VaultStore ecrivait AVANT l'introduction de
    // l'AAD : blob AES-256-GCM (nonce(12) . tag(16) . cipher), chiffre sans
    // AAD (equivalent a une AAD vide).
    private void WriteLegacyGcmVaultWithoutAad(string password, params (string origin, string user, string pass)[] creds)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var key = DeriveArgon2id(password, salt);

        var list = creds.Select(c => new VaultCredential
        {
            Origin = c.origin, Username = c.user, Password = c.pass,
            CreatedAt = 1, UpdatedAt = 1
        }).ToList();
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(list, new JsonSerializerOptions { WriteIndented = false });

        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16))
            aes.Encrypt(nonce, plaintext, cipher, tag); // pas d'AAD, comme avant le correctif

        var blob = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, blob, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, blob, nonce.Length + tag.Length, cipher.Length);

        var header = new
        {
            version = 2,
            mode = "aes256",
            kdf = "argon2id",
            salt = Convert.ToBase64String(salt),
            argon2_mem = 65536,
            argon2_iter = 3,
            argon2_par = 4,
            data = Convert.ToBase64String(blob),
            data_cipher = "gcm"
        };
        File.WriteAllText(_file, JsonSerializer.Serialize(header), Encoding.UTF8);
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
