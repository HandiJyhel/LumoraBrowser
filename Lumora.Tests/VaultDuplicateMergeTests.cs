using Lumora.WinUI;
using Lumora.WinUI.PasswordManager;
using Xunit;

namespace Lumora.Tests;

// Fusion des doublons du coffre (import depuis un gestionnaire tiers : le meme
// compte enregistre sous www./apex/sous-domaines) et nom d'affichage sans "www.".
public sealed class VaultDuplicateMergeTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), "nova_test_" + Guid.NewGuid().ToString("N") + ".nova");

    private PasswordManagerService NewService(out VaultStore vault)
    {
        vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        return new PasswordManagerService(vault);
    }

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    [Fact]
    public void MergeDuplicates_fusionne_www_et_apex_du_meme_compte()
    {
        var service = NewService(out var vault);
        vault.Upsert("https://amazon.fr", "alice@mail.fr", "s3cret");
        vault.Upsert("https://www.amazon.fr", "alice@mail.fr", "s3cret");
        vault.Upsert("https://auth.amazon.fr", "ALICE@MAIL.FR", "s3cret");

        var merged = service.MergeDuplicates();

        Assert.Equal(2, merged);
        Assert.Single(vault.ListCredentials());
    }

    [Fact]
    public void MergeDuplicates_ne_touche_jamais_deux_mots_de_passe_differents()
    {
        var service = NewService(out var vault);
        vault.Upsert("https://exemple.fr", "alice", "ancien-mdp");
        vault.Upsert("https://www.exemple.fr", "alice", "nouveau-mdp");

        var merged = service.MergeDuplicates();

        Assert.Equal(0, merged);
        Assert.Equal(2, vault.ListCredentials().Count);
    }

    [Fact]
    public void MergeDuplicates_separe_les_comptes_distincts_du_meme_site()
    {
        var service = NewService(out var vault);
        vault.Upsert("https://google.fr", "perso@gmail.com", "mdp");
        vault.Upsert("https://www.google.fr", "boulot@gmail.com", "mdp");

        Assert.Equal(0, service.MergeDuplicates());
        Assert.Equal(2, vault.ListCredentials().Count);
    }

    [Fact]
    public void MergeDuplicates_conserve_l_entree_la_plus_renseignee()
    {
        var service = NewService(out var vault);
        vault.Upsert("https://www.micromania.fr", "alice", "s3cret");
        vault.Upsert("https://micromania.fr", "alice", "s3cret", "https://micromania.fr/login");
        var labeled = vault.ListCredentials().First(c => !string.IsNullOrWhiteSpace(c.LoginUrl));
        vault.SetLabelById(labeled.Id, "Micromania perso");

        Assert.Equal(1, service.MergeDuplicates());

        var remaining = Assert.Single(vault.ListCredentials());
        Assert.Equal("Micromania perso", remaining.Label);
        Assert.Equal("https://micromania.fr/login", remaining.LoginUrl);
    }

    [Fact]
    public void FindDuplicates_est_un_dry_run_sans_suppression()
    {
        var service = NewService(out var vault);
        vault.Upsert("https://exemple.fr", "alice", "s3cret");
        vault.Upsert("https://www.exemple.fr", "alice", "s3cret");

        Assert.Single(service.FindDuplicates());
        Assert.Equal(2, vault.ListCredentials().Count);
    }

    [Fact]
    public void DisplayName_retire_le_prefixe_www()
    {
        var service = NewService(out var vault);
        vault.Upsert("https://www.amazon.fr", "alice", "s3cret");

        var cred = Assert.Single(vault.ListCredentials());
        Assert.Equal("amazon.fr", PasswordManagerService.DisplayName(cred));
    }

    [Fact]
    public void DisplayName_prefere_toujours_le_nom_personnalise()
    {
        var service = NewService(out var vault);
        vault.Upsert("https://www.amazon.fr", "alice", "s3cret");
        var cred = Assert.Single(vault.ListCredentials());
        vault.SetLabelById(cred.Id, "Amazon perso");

        Assert.Equal("Amazon perso", PasswordManagerService.DisplayName(vault.ListCredentials()[0]));
    }
}
