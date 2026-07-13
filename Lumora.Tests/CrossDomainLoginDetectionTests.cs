using Lumora.WinUI;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;
using Xunit;

namespace Lumora.Tests;

// Detection "changement de domaine" : un site qui deplace ses comptes vers un
// nouveau domaine (ex. weareholy.com -> holy.com). A la prochaine connexion
// reussie, on doit reconnaitre le meme compte et proposer de rattacher le
// nouveau domaine — jamais sur une simple ressemblance, uniquement sur un
// identifiant + mot de passe identiques (preuve fournie par le login).
public sealed class CrossDomainLoginDetectionTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), "nova_test_" + Guid.NewGuid().ToString("N") + ".nova");

    private (PasswordManagerService service, PasswordManagerInteractionService interaction) Build(out VaultStore vault)
    {
        vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        var service = new PasswordManagerService(vault);
        return (service, new PasswordManagerInteractionService(service));
    }

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    [Fact]
    public void FindSameLoginOnOtherDomain_reconnait_le_meme_compte_sous_un_autre_domaine()
    {
        var (service, _) = Build(out var vault);
        vault.Upsert("https://fr.weareholy.com", "handijyhel@gmail.com", "MonMotDePasse");

        var match = service.FindSameLoginOnOtherDomain(
            "https://fr.holy.com", "handijyhel@gmail.com", "MonMotDePasse");

        Assert.NotNull(match);
        Assert.Equal("https://fr.weareholy.com", match!.Origin);
    }

    [Fact]
    public void FindSameLoginOnOtherDomain_ignore_un_mot_de_passe_different()
    {
        var (service, _) = Build(out var vault);
        vault.Upsert("https://fr.weareholy.com", "handijyhel@gmail.com", "AncienMotDePasse");

        // Meme identifiant mais mot de passe different : ce n'est PAS une preuve que
        // c'est le meme compte (garde anti-phishing : aucune correspondance laxiste).
        Assert.Null(service.FindSameLoginOnOtherDomain(
            "https://fr.holy.com", "handijyhel@gmail.com", "AutreMotDePasse"));
    }

    [Fact]
    public void FindSameLoginOnOtherDomain_ne_se_declenche_pas_sur_le_meme_domaine()
    {
        var (service, _) = Build(out var vault);
        vault.Upsert("https://fr.holy.com", "alice", "secret");

        Assert.Null(service.FindSameLoginOnOtherDomain(
            "https://fr.holy.com", "alice", "secret"));
    }

    [Fact]
    public void BuildSaveOffer_propose_de_rattacher_le_nouveau_domaine()
    {
        var (_, interaction) = Build(out var vault);
        vault.Upsert("https://fr.weareholy.com", "handijyhel@gmail.com", "MonMotDePasse");
        vault.SetLabelById(vault.ListCredentials()[0].Id, "HOLY");

        var offer = interaction.BuildSaveOffer(new CredentialCapture(
            "https://fr.holy.com",
            "handijyhel@gmail.com",
            "MonMotDePasse",
            "https://fr.holy.com/account/login",
            "submit",
            90));

        Assert.NotNull(offer);
        Assert.Equal("weareholy.com", offer!.LinkedFromDomain);
        // Le nom personnalise du compte existant est repris pour la nouvelle entree.
        Assert.Equal("HOLY", offer.Draft.Label);
    }

    [Fact]
    public void BuildSaveOffer_offre_normale_si_aucun_compte_correspondant()
    {
        var (_, interaction) = Build(out var vault);
        vault.Upsert("https://autre-site.com", "bob", "xyz");

        var offer = interaction.BuildSaveOffer(new CredentialCapture(
            "https://fr.holy.com",
            "handijyhel@gmail.com",
            "MonMotDePasse",
            "https://fr.holy.com/account/login",
            "submit",
            90));

        Assert.NotNull(offer);
        Assert.Null(offer!.LinkedFromDomain);
    }

    [Fact]
    public void Save_du_rattachement_rend_le_remplissage_disponible_sur_le_nouveau_domaine()
    {
        var (service, interaction) = Build(out var vault);
        vault.Upsert("https://fr.weareholy.com", "handijyhel@gmail.com", "MonMotDePasse");

        var offer = interaction.BuildSaveOffer(new CredentialCapture(
            "https://fr.holy.com", "handijyhel@gmail.com", "MonMotDePasse",
            "https://fr.holy.com/account/login", "submit", 90));
        Assert.NotNull(offer);
        service.Save(offer!.Draft);

        // Le nouveau domaine propose desormais le remplissage.
        var matches = service.FindAllForAddress("https://fr.holy.com/account/login");
        Assert.Single(matches);
        Assert.Equal("handijyhel@gmail.com", matches[0].Username);
    }
}
