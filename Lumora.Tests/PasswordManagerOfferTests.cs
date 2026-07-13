using Lumora.WinUI;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;
using Xunit;

namespace Lumora.Tests;

// Verrouille la logique de proposition d'enregistrement (BuildSaveOffer), dont
// l'Option A : proposer même quand aucun identifiant n'a été capturé.
public sealed class PasswordManagerOfferTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), "nova_offer_" + Guid.NewGuid().ToString("N") + ".nova");
    private readonly PasswordManagerInteractionService _interaction;
    private readonly PasswordManagerService _manager;

    public PasswordManagerOfferTests()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        _manager = new PasswordManagerService(vault);
        _interaction = new PasswordManagerInteractionService(_manager);
    }

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    private static CredentialCapture Capture(string origin, string user, string pass) =>
        new(origin, user, pass, origin, "submit", 90);

    [Fact]
    public void Propose_avec_identifiant()
    {
        var offer = _interaction.BuildSaveOffer(Capture("https://exemple.fr", "alice", "s3cret"));
        Assert.NotNull(offer);
        Assert.Equal("alice", offer!.Username);
        Assert.False(offer.IsUpdate);
    }

    [Fact]
    public void Option_A_propose_meme_sans_identifiant()
    {
        var offer = _interaction.BuildSaveOffer(Capture("https://exemple.fr", "", "s3cret"));
        Assert.NotNull(offer);                 // avant : renvoyait null (aucune offre)
        Assert.Equal(string.Empty, offer!.Username);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]   // < 3 caractères
    public void Ne_propose_pas_pour_un_mot_de_passe_trop_court(string weak)
    {
        Assert.Null(_interaction.BuildSaveOffer(Capture("https://exemple.fr", "alice", weak)));
    }

    [Fact]
    public void Ne_propose_pas_si_origine_vide()
    {
        Assert.Null(_interaction.BuildSaveOffer(Capture("", "alice", "s3cret")));
    }

    [Fact]
    public void Ne_repropose_pas_un_identifiant_identique_deja_enregistre()
    {
        _manager.Save(new PasswordManagerEntryDraft("https://exemple.fr", "alice", "s3cret"));
        var offer = _interaction.BuildSaveOffer(Capture("https://exemple.fr", "alice", "s3cret"));
        Assert.Null(offer);
    }

    [Fact]
    public void Propose_une_mise_a_jour_si_mot_de_passe_change()
    {
        _manager.Save(new PasswordManagerEntryDraft("https://exemple.fr", "alice", "ancien"));
        var offer = _interaction.BuildSaveOffer(Capture("https://exemple.fr", "alice", "nouveau"));
        Assert.NotNull(offer);
        Assert.True(offer!.IsUpdate);
    }
}
