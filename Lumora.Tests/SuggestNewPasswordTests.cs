using Lumora.WinUI;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;
using Xunit;

namespace Lumora.Tests;

// Verrouille la priorite de la suggestion de mot de passe fort (champ "nouveau
// mot de passe" detecte cote page) sur toute proposition de remplissage d'un
// identifiant deja enregistre, y compris quand un compte existe pour ce site.
public sealed class SuggestNewPasswordTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), "nova_suggest_" + Guid.NewGuid().ToString("N") + ".nova");
    private readonly VaultStore _vault;
    private readonly PasswordManagerInteractionService _interaction;

    public SuggestNewPasswordTests()
    {
        _vault = new VaultStore(_file);
        _vault.SetMasterPassword("pw");
        var manager = new PasswordManagerService(_vault);
        _interaction = new PasswordManagerInteractionService(manager);
    }

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    private static CredentialPageState NewPasswordState(string origin) =>
        new(origin, origin + "/inscription", false, true, "", "test", 100, HasEmptyNewPasswordField: true);

    [Fact]
    public void Champ_nouveau_mot_de_passe_declenche_la_suggestion()
    {
        var decision = _interaction.EvaluatePage("https://exemple.fr/inscription", NewPasswordState("https://exemple.fr"));

        Assert.Equal(PasswordManagerPromptKind.SuggestNewPassword, decision.Kind);
        Assert.Empty(decision.Credentials);
    }

    [Fact]
    public void La_suggestion_prime_meme_si_un_compte_existe_deja_pour_ce_site()
    {
        _vault.Upsert("https://exemple.fr", "alice", "ancien-mdp");

        var decision = _interaction.EvaluatePage("https://exemple.fr/parametres/mot-de-passe", NewPasswordState("https://exemple.fr"));

        Assert.Equal(PasswordManagerPromptKind.SuggestNewPassword, decision.Kind);
    }

    [Fact]
    public void Pas_de_champ_nouveau_mot_de_passe_pas_de_suggestion()
    {
        _vault.Upsert("https://exemple.fr", "alice", "s3cret");
        var loginState = new CredentialPageState("https://exemple.fr", "https://exemple.fr/login", true, true, "", "test", 100);

        var decision = _interaction.EvaluatePage("https://exemple.fr/login", loginState);

        Assert.NotEqual(PasswordManagerPromptKind.SuggestNewPassword, decision.Kind);
        Assert.Equal(PasswordManagerPromptKind.FillAvailable, decision.Kind);
    }
}
