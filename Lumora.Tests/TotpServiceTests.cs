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

    // ── Multi-algorithme (SHA1/SHA256/SHA512) ───────────────────────────────

    [Fact]
    public void ParseSecretInput_sans_parametre_algorithm_vaut_SHA1_par_defaut()
    {
        var account = TotpService.ParseSecretInput($"otpauth://totp/Exemple:alice?secret={Rfc6238Secret}");
        Assert.NotNull(account);
        Assert.Equal(OtpHashAlgorithm.Sha1, account!.Algorithm);
    }

    // Le paramètre reste une chaîne (pas OtpHashAlgorithm, type "internal") :
    // une méthode de test [Theory] doit être publique pour qu'xUnit la
    // découvre, et une méthode publique ne peut pas exposer un type moins
    // accessible dans sa signature (CS0051), même avec InternalsVisibleTo.
    [Theory]
    [InlineData("SHA1", "SHA1")]
    [InlineData("sha256", "SHA256")]
    [InlineData("SHA512", "SHA512")]
    [InlineData("sha-256", "SHA256")]
    [InlineData("md5", "SHA1")] // valeur non reconnue -> repli SHA1
    public void ParseAlgorithmName_reconnait_les_variantes_usuelles(string raw, string expectedName)
    {
        Assert.Equal(expectedName, TotpService.AlgorithmName(TotpService.ParseAlgorithmName(raw)));
    }

    [Theory]
    [InlineData("SHA1")]
    [InlineData("SHA256")]
    [InlineData("SHA512")]
    public void AlgorithmName_puis_ParseAlgorithmName_font_un_aller_retour_stable(string name)
    {
        var algorithm = TotpService.ParseAlgorithmName(name);
        Assert.Equal(name, TotpService.AlgorithmName(algorithm));
        Assert.Equal(algorithm, TotpService.ParseAlgorithmName(TotpService.AlgorithmName(algorithm)));
    }

    [Fact]
    public void ParseSecretInput_extrait_lalgorithme_dune_uri_otpauth()
    {
        var uri = $"otpauth://totp/Exemple:alice?secret={Rfc6238Secret}&algorithm=SHA256";
        var account = TotpService.ParseSecretInput(uri);
        Assert.NotNull(account);
        Assert.Equal(OtpHashAlgorithm.Sha256, account!.Algorithm);
    }

    [Fact]
    public void GenerateCode_avec_des_algorithmes_differents_donne_des_codes_differents()
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(59);
        var sha1 = TotpService.GenerateCode(Rfc6238Secret, at, digits: 8, algorithm: OtpHashAlgorithm.Sha1);
        var sha256 = TotpService.GenerateCode(Rfc6238Secret, at, digits: 8, algorithm: OtpHashAlgorithm.Sha256);
        var sha512 = TotpService.GenerateCode(Rfc6238Secret, at, digits: 8, algorithm: OtpHashAlgorithm.Sha512);

        // Même secret/même instant : le SHA1 doit retomber sur le vecteur RFC
        // 6238 déjà verrouillé plus haut, et les trois algorithmes doivent
        // diverger entre eux (sinon le paramètre serait ignoré silencieusement).
        Assert.Equal("94287082", sha1);
        Assert.NotEqual(sha1, sha256);
        Assert.NotEqual(sha1, sha512);
        Assert.NotEqual(sha256, sha512);
    }

    [Fact]
    public void GenerateCode_par_defaut_sans_algorithme_precise_reste_SHA1()
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(59);
        Assert.Equal(
            TotpService.GenerateCode(Rfc6238Secret, at, digits: 8, algorithm: OtpHashAlgorithm.Sha1),
            TotpService.GenerateCode(Rfc6238Secret, at, digits: 8));
    }

    // ── Garde-fou period<=0 (bug réel trouvé le 2026-08-19) ──────────────────
    // Une URI otpauth:// avec period=0/négatif (malformée, corrompue, ou hostile)
    // provoquait un DivideByZeroException à chaque appel de GenerateCode/
    // SecondsRemaining, donc un plantage en boucle à l'ouverture de la fiche.

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-30)]
    public void GenerateCode_avec_period_invalide_ne_plante_pas(int invalidPeriod)
    {
        var code = TotpService.GenerateCode(Rfc6238Secret, DateTimeOffset.FromUnixTimeSeconds(59), period: invalidPeriod);
        Assert.Equal(6, code.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SecondsRemaining_avec_period_invalide_ne_plante_pas(int invalidPeriod)
    {
        var remaining = TotpService.SecondsRemaining(DateTimeOffset.FromUnixTimeSeconds(59), period: invalidPeriod);
        Assert.InRange(remaining, 1, TotpService.DefaultPeriod);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void ParseSecretInput_uri_otpauth_avec_period_invalide_replie_sur_le_defaut(int invalidPeriod)
    {
        var uri = $"otpauth://totp/Exemple:alice?secret={Rfc6238Secret}&period={invalidPeriod}";
        var account = TotpService.ParseSecretInput(uri);

        Assert.NotNull(account);
        Assert.Equal(TotpService.DefaultPeriod, account!.Period);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(999)]
    public void ParseSecretInput_uri_otpauth_avec_digits_hors_plage_replie_sur_le_defaut(int invalidDigits)
    {
        var uri = $"otpauth://totp/Exemple:alice?secret={Rfc6238Secret}&digits={invalidDigits}";
        var account = TotpService.ParseSecretInput(uri);

        Assert.NotNull(account);
        Assert.Equal(TotpService.DefaultDigits, account!.Digits);
    }
}
