using Lumora.WinUI.Credentials;
using Xunit;

namespace Lumora.Tests;

// Vecteurs de test verrouillés sur RFC 6238 Annexe B (secret ASCII
// "12345678901234567890", encodé en base32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ").
public sealed class TotpServiceTests
{
    private const string Rfc6238Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59, "94287082")]
    [InlineData(1111111109, "07081804")]
    [InlineData(1111111111, "14050471")]
    [InlineData(1234567890, "89005924")]
    [InlineData(2000000000, "69279037")]
    public void GenerateCode_correspond_aux_vecteurs_RFC_6238_en_8_chiffres(long unixSeconds, string expected)
    {
        var code = TotpService.GenerateCode(Rfc6238Secret, DateTimeOffset.FromUnixTimeSeconds(unixSeconds), digits: 8);
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1234567890, "005924")]
    public void GenerateCode_par_defaut_donne_les_6_derniers_chiffres_du_vecteur(long unixSeconds, string expectedLast6)
    {
        var code = TotpService.GenerateCode(Rfc6238Secret, DateTimeOffset.FromUnixTimeSeconds(unixSeconds));
        Assert.Equal(6, code.Length);
        Assert.Equal(expectedLast6, code);
    }

    // Base alignee sur une frontiere de fenetre de 30s (999999990 % 30 == 0),
    // pour que les decalages en secondes ci-dessous tombent exactement dans ou
    // hors de la meme fenetre.
    private const long WindowAlignedBase = 999999990;

    [Fact]
    public void GenerateCode_reste_stable_dans_la_meme_fenetre()
    {
        var a = TotpService.GenerateCode(Rfc6238Secret, DateTimeOffset.FromUnixTimeSeconds(WindowAlignedBase));
        var b = TotpService.GenerateCode(Rfc6238Secret, DateTimeOffset.FromUnixTimeSeconds(WindowAlignedBase + 29));
        Assert.Equal(a, b);
    }

    [Fact]
    public void GenerateCode_change_a_la_fenetre_suivante()
    {
        var a = TotpService.GenerateCode(Rfc6238Secret, DateTimeOffset.FromUnixTimeSeconds(WindowAlignedBase));
        var b = TotpService.GenerateCode(Rfc6238Secret, DateTimeOffset.FromUnixTimeSeconds(WindowAlignedBase + 30));
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 29)]
    [InlineData(29, 1)]
    [InlineData(30, 30)]
    public void SecondsRemaining_calcule_le_temps_avant_rotation(long secondsIntoWindow, int expectedRemaining)
    {
        var remaining = TotpService.SecondsRemaining(DateTimeOffset.FromUnixTimeSeconds(secondsIntoWindow));
        Assert.Equal(expectedRemaining, remaining);
    }

    [Fact]
    public void IsValidSecret_accepte_un_secret_base32_correct()
    {
        Assert.True(TotpService.IsValidSecret(Rfc6238Secret));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base32-1")]
    public void IsValidSecret_refuse_un_secret_invalide(string secret)
    {
        Assert.False(TotpService.IsValidSecret(secret));
    }

    [Fact]
    public void ParseSecretInput_accepte_un_secret_colle_avec_espaces()
    {
        var account = TotpService.ParseSecretInput("gezd gnbv gy3t qojq gezd gnbv gy3t qojq");
        Assert.NotNull(account);
        Assert.Equal(Rfc6238Secret, account!.Secret);
    }

    [Fact]
    public void ParseSecretInput_refuse_un_texte_qui_nest_pas_un_secret()
    {
        Assert.Null(TotpService.ParseSecretInput("ceci n'est pas un secret totp !!!"));
    }

    [Fact]
    public void ParseSecretInput_extrait_issuer_compte_et_parametres_dune_uri_otpauth()
    {
        var uri = $"otpauth://totp/Exemple:alice%40exemple.fr?secret={Rfc6238Secret}&issuer=Exemple&digits=6&period=30";
        var account = TotpService.ParseSecretInput(uri);

        Assert.NotNull(account);
        Assert.Equal(Rfc6238Secret, account!.Secret);
        Assert.Equal("Exemple", account.Issuer);
        Assert.Equal("alice@exemple.fr", account.AccountName);
        Assert.Equal(6, account.Digits);
        Assert.Equal(30, account.Period);
    }

    [Fact]
    public void ParseSecretInput_uri_otpauth_sans_secret_est_refusee()
    {
        Assert.Null(TotpService.ParseSecretInput("otpauth://totp/Exemple:alice?issuer=Exemple"));
    }

    [Fact]
    public void ParseSecretInput_uri_dun_autre_type_est_refusee()
    {
        Assert.Null(TotpService.ParseSecretInput("otpauth://hotp/Exemple:alice?secret=" + Rfc6238Secret));
    }
}
