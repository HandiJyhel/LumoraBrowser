using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Lumora.WinUI;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;
using Xunit;

namespace Lumora.Tests;

// Verrouille le support multi-compte par site (ex. deux comptes Google sur
// google.com) : stockage de plusieurs identifiants distincts par origine,
// autofill qui les propose tous plutôt qu'un seul, et Renommer/Supprimer
// qui n'affectent que le compte visé via son Id stable.
public sealed class MultiAccountCredentialTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), "nova_multiaccount_" + Guid.NewGuid().ToString("N") + ".nova");
    private readonly VaultStore _vault;
    private readonly PasswordManagerService _manager;
    private readonly PasswordManagerInteractionService _interaction;

    public MultiAccountCredentialTests()
    {
        _vault = new VaultStore(_file);
        _vault.SetMasterPassword("pw");
        _manager = new PasswordManagerService(_vault);
        _interaction = new PasswordManagerInteractionService(_manager);
    }

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    [Fact]
    public void Deux_comptes_distincts_sur_le_meme_site_sont_stockes_separement()
    {
        _vault.Upsert("https://google.com", "perso@gmail.com", "s3cret1");
        _vault.Upsert("https://google.com", "pro@gmail.com", "s3cret2");

        var creds = _vault.ListCredentials();
        Assert.Equal(2, creds.Count);
        Assert.Contains(creds, c => c.Username == "perso@gmail.com" && c.Password == "s3cret1");
        Assert.Contains(creds, c => c.Username == "pro@gmail.com" && c.Password == "s3cret2");
    }

    [Fact]
    public void Chaque_compte_recoit_un_Id_stable_et_distinct()
    {
        _vault.Upsert("https://google.com", "perso@gmail.com", "s3cret1");
        _vault.Upsert("https://google.com", "pro@gmail.com", "s3cret2");

        var creds = _vault.ListCredentials();
        Assert.All(creds, c => Assert.False(string.IsNullOrEmpty(c.Id)));
        Assert.NotEqual(creds[0].Id, creds[1].Id);
    }

    [Fact]
    public void FindAllForAddress_propose_tous_les_comptes_du_site()
    {
        _vault.Upsert("https://google.com", "perso@gmail.com", "s3cret1");
        _vault.Upsert("https://google.com", "pro@gmail.com", "s3cret2");

        var matches = _manager.FindAllForAddress("https://google.com/login");
        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void EvaluatePage_remonte_les_deux_comptes_pour_choix_utilisateur()
    {
        _vault.Upsert("https://google.com", "perso@gmail.com", "s3cret1");
        _vault.Upsert("https://google.com", "pro@gmail.com", "s3cret2");

        var pageState = new CredentialPageState("https://google.com", "https://google.com/login", true, true, "", "test", 100);
        var decision = _interaction.EvaluatePage("https://google.com/login", pageState);

        Assert.True(decision.CanOfferFill);
        Assert.Equal(2, decision.Credentials.Count);
    }

    [Fact]
    public void Un_seul_compte_reste_lidentifiant_direct_comme_avant()
    {
        _vault.Upsert("https://google.com", "perso@gmail.com", "s3cret1");

        var pageState = new CredentialPageState("https://google.com", "https://google.com/login", true, true, "", "test", 100);
        var decision = _interaction.EvaluatePage("https://google.com/login", pageState);

        Assert.Single(decision.Credentials);
        Assert.Equal("perso@gmail.com", decision.Credentials[0].Username);
    }

    [Fact]
    public void DeleteById_ne_supprime_que_le_compte_vise()
    {
        _vault.Upsert("https://google.com", "perso@gmail.com", "s3cret1");
        _vault.Upsert("https://google.com", "pro@gmail.com", "s3cret2");
        var toDelete = _vault.ListCredentials().Single(c => c.Username == "perso@gmail.com");

        _manager.DeleteById(toDelete.Id);

        var remaining = _vault.ListCredentials();
        Assert.Single(remaining);
        Assert.Equal("pro@gmail.com", remaining[0].Username);
    }

    [Fact]
    public void RenameById_ne_renomme_que_le_compte_vise()
    {
        _vault.Upsert("https://google.com", "perso@gmail.com", "s3cret1");
        _vault.Upsert("https://google.com", "pro@gmail.com", "s3cret2");
        var target = _vault.ListCredentials().Single(c => c.Username == "pro@gmail.com");

        _manager.RenameById(target.Id, "Google pro");

        var creds = _vault.ListCredentials();
        Assert.Equal("Google pro", creds.Single(c => c.Username == "pro@gmail.com").Label);
        Assert.Equal(string.Empty, creds.Single(c => c.Username == "perso@gmail.com").Label);
    }

    [Fact]
    public void Un_coffre_herite_sans_Id_recoit_des_Id_uniques_a_l_ouverture_sans_ecrire_seul()
    {
        var legacyFile = Path.Combine(Path.GetTempPath(), "nova_legacy_noid_" + Guid.NewGuid().ToString("N") + ".nova");
        try
        {
            const string password = "pw-legacy";
            WriteLegacyVaultWithoutIds(legacyFile, password,
                ("https://google.com", "perso@gmail.com", "s3cret1"),
                ("https://google.com", "pro@gmail.com", "s3cret2"));

            var vault = new VaultStore(legacyFile);
            Assert.True(vault.Unlock(password));

            var creds = vault.ListCredentials();
            Assert.Equal(2, creds.Count);
            Assert.All(creds, c => Assert.False(string.IsNullOrEmpty(c.Id)));
            Assert.NotEqual(creds[0].Id, creds[1].Id);
        }
        finally
        {
            try { if (File.Exists(legacyFile)) File.Delete(legacyFile); } catch { }
        }
    }

    // Fabrique un coffre au format actuel (AES-256-GCM) mais avec des identifiants
    // SANS champ "id" (comme tout coffre écrit avant l'introduction du multi-compte),
    // pour prouver que la migration d'Id se fait bien à l'ouverture.
    private static void WriteLegacyVaultWithoutIds(string file, string password, params (string origin, string user, string pass)[] creds)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = 65536,
            Iterations = 3,
            DegreeOfParallelism = 4
        };
        var key = argon2.GetBytes(32);

        var list = creds.Select(c => new { origin = c.origin, username = c.user, password = c.pass, label = "", login_url = "", created_at = 1, updated_at = 1 }).ToList();
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(list);

        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16))
            aes.Encrypt(nonce, plaintext, cipher, tag);
        var wrapped = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, wrapped, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, wrapped, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, wrapped, nonce.Length + tag.Length, cipher.Length);

        var header = new
        {
            version = 2,
            mode = "aes256",
            kdf = "argon2id",
            salt = Convert.ToBase64String(salt),
            argon2_mem = 65536,
            argon2_iter = 3,
            argon2_par = 4,
            data = Convert.ToBase64String(wrapped),
            data_cipher = "gcm"
        };
        File.WriteAllText(file, JsonSerializer.Serialize(header), Encoding.UTF8);
    }
}
