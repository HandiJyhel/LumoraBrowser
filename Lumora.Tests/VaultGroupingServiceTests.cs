using Lumora.WinUI;
using Lumora.WinUI.PasswordManager;
using Xunit;

namespace Lumora.Tests;

public sealed class VaultGroupingServiceTests
{
    private static VaultCredential Cred(string origin, string username, long updatedAt) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Origin = origin,
        Username = username,
        Password = "pw",
        UpdatedAt = updatedAt,
        CreatedAt = updatedAt
    };

    [Fact]
    public void Fusionne_les_sous_domaines_du_meme_site()
    {
        var credentials = new[]
        {
            Cred("https://accounts.google.com", "alice@gmail.com", 100),
            Cred("https://www.google.com", "bob@gmail.com", 90)
        };

        var groups = VaultGroupingService.Group(credentials, VaultSortMode.Recent);

        var group = Assert.Single(groups);
        Assert.Equal("google.com", group.DisplayName);
        Assert.Equal(2, group.Credentials.Count);
    }

    [Fact]
    public void Garde_des_sites_differents_dans_des_groupes_separes()
    {
        var credentials = new[]
        {
            Cred("https://amazon.fr", "alice", 100),
            Cred("https://ebay.fr", "alice", 90)
        };

        var groups = VaultGroupingService.Group(credentials, VaultSortMode.Recent);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void Trie_les_comptes_dans_un_groupe_du_plus_recent_au_plus_ancien()
    {
        var credentials = new[]
        {
            Cred("https://google.com", "old@gmail.com", 50),
            Cred("https://google.com", "new@gmail.com", 200)
        };

        var groups = VaultGroupingService.Group(credentials, VaultSortMode.Recent);

        var group = Assert.Single(groups);
        Assert.Equal("new@gmail.com", group.Credentials[0].Username);
        Assert.Equal("old@gmail.com", group.Credentials[1].Username);
    }

    [Fact]
    public void Tri_recent_ordonne_les_groupes_par_derniere_mise_a_jour()
    {
        var credentials = new[]
        {
            Cred("https://ancien-site.fr", "alice", 10),
            Cred("https://site-recent.fr", "alice", 500)
        };

        var groups = VaultGroupingService.Group(credentials, VaultSortMode.Recent);

        Assert.Equal("site-recent.fr", groups[0].DisplayName);
        Assert.Equal("ancien-site.fr", groups[1].DisplayName);
    }

    [Fact]
    public void Tri_alphabetique_ordonne_les_groupes_par_nom()
    {
        var credentials = new[]
        {
            Cred("https://zorro.fr", "alice", 10),
            Cred("https://amazon.fr", "alice", 500)
        };

        var groups = VaultGroupingService.Group(credentials, VaultSortMode.Alphabetical);

        Assert.Equal("amazon.fr", groups[0].DisplayName);
        Assert.Equal("zorro.fr", groups[1].DisplayName);
    }

    [Fact]
    public void Liste_vide_donne_aucun_groupe()
    {
        var groups = VaultGroupingService.Group(Array.Empty<VaultCredential>(), VaultSortMode.Recent);

        Assert.Empty(groups);
    }
}
