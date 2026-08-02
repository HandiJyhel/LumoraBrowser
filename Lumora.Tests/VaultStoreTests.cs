using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Vérifie le round-trip AES-GCM du coffre, la persistance sur disque, et les
// chemins de déverrouillage (mot de passe, PIN, clé de récupération).
public sealed class VaultStoreTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), "nova_test_" + Guid.NewGuid().ToString("N") + ".nova");

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    [Fact]
    public void SetMasterPassword_puis_Upsert_expose_l_identifiant()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("motdepasse-fort");
        vault.Upsert("https://exemple.fr", "alice", "s3cret", "https://exemple.fr/login");

        var creds = vault.ListCredentials();
        Assert.Single(creds);
        Assert.Equal("alice", creds[0].Username);
        Assert.Equal("s3cret", creds[0].Password);
    }

    [Fact]
    public void Lock_purge_la_liste_et_Unlock_la_restaure()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.Upsert("https://exemple.fr", "alice", "s3cret");

        vault.Lock();
        Assert.True(vault.IsLocked);
        Assert.Empty(vault.ListCredentials());

        Assert.True(vault.Unlock("pw"));
        Assert.False(vault.IsLocked);
        Assert.Single(vault.ListCredentials());
    }

    [Fact]
    public void Reouverture_depuis_le_disque_dechiffre_en_GCM()
    {
        var first = new VaultStore(_file);
        first.SetMasterPassword("pw");
        first.Upsert("https://exemple.fr", "alice", "s3cret");
        first.Lock();

        // Nouvelle instance = relecture réelle du fichier écrit en AES-GCM.
        var reopened = new VaultStore(_file);
        Assert.True(reopened.IsLocked);
        Assert.True(reopened.Unlock("pw"));
        Assert.Equal("s3cret", reopened.ListCredentials().Single().Password);
    }

    [Fact]
    public void Unlock_refuse_un_mauvais_mot_de_passe()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("bon");
        vault.Upsert("https://exemple.fr", "alice", "s3cret");
        vault.Lock();

        Assert.False(vault.Unlock("mauvais"));
        Assert.True(vault.IsLocked);
        Assert.True(vault.Unlock("bon"));
    }

    [Fact]
    public void SetUsernameById_corrige_l_identifiant_sans_toucher_au_mot_de_passe()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.Upsert("https://accounts.google.com", "544873", "s3cret");
        var id = vault.ListCredentials().Single().Id;

        vault.SetUsernameById(id, "vraie.adresse@gmail.com");

        var cred = vault.ListCredentials().Single();
        Assert.Equal("vraie.adresse@gmail.com", cred.Username);
        Assert.Equal("s3cret", cred.Password);
    }

    [Fact]
    public void Deverrouillage_par_PIN()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.Upsert("https://exemple.fr", "alice", "s3cret");
        vault.EnablePinUnlock("246810");
        vault.Lock();

        var reopened = new VaultStore(_file);
        Assert.False(reopened.UnlockWithPin("000000"));
        Assert.True(reopened.UnlockWithPin("246810"));
        Assert.Equal("s3cret", reopened.ListCredentials().Single().Password);
    }

    [Fact]
    public void Deverrouillage_par_cle_de_recuperation()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.Upsert("https://exemple.fr", "alice", "s3cret");
        Assert.True(vault.SetRecoveryKey("NOVA-AAAA-BBBB-CCCC"));
        vault.Lock();

        var reopened = new VaultStore(_file);
        Assert.False(reopened.UnlockWithRecoveryKey("NOVA-0000-0000-0000"));
        Assert.True(reopened.UnlockWithRecoveryKey("NOVA-AAAA-BBBB-CCCC"));
        Assert.Equal("s3cret", reopened.ListCredentials().Single().Password);
    }

    [Fact]
    public void Delete_pose_un_tombstone_qui_bloque_la_reimportation()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.Upsert("https://exemple.fr", "alice", "s3cret");
        vault.Delete("https://exemple.fr", "alice");
        Assert.Empty(vault.ListCredentials());

        var reimported = vault.ImportClear(new[] { ("https://exemple.fr", "alice", "s3cret") });
        Assert.Equal(0, reimported);
        Assert.Empty(vault.ListCredentials());
    }

    [Fact]
    public void ImportClear_enrichi_conserve_label_et_url_de_connexion()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");

        var imported = vault.ImportClear(new[]
        {
            ("https://account.proton.me", "jyhel@example.com", "secret", "https://account.proton.me/login", "Proton perso")
        });

        Assert.Equal(1, imported);
        var cred = Assert.Single(vault.ListCredentials());
        Assert.Equal("Proton perso", cred.Label);
        Assert.Equal("https://account.proton.me/login", cred.LoginUrl);
        Assert.Equal("secret", cred.Password);
    }
}
