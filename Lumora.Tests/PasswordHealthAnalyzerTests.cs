using Lumora.WinUI;
using Lumora.WinUI.PasswordManager;
using Xunit;

namespace Lumora.Tests;

public sealed class PasswordHealthAnalyzerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    private static VaultCredential Cred(
        string origin,
        string password,
        string username = "user",
        long? updatedAt = null) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Origin = origin,
        Username = username,
        Password = password,
        CreatedAt = updatedAt ?? Now.ToUnixTimeSeconds(),
        UpdatedAt = updatedAt ?? Now.ToUnixTimeSeconds()
    };

    // ── Force du mot de passe ────────────────────────────────────────────────

    [Theory]
    [InlineData("court1A")]          // < 8 caractères
    [InlineData("azerty123")]        // liste des mots de passe courants
    [InlineData("MOTDEPASSE")]       // courant, insensible à la casse
    [InlineData("aaaaaaaaaaaa")]     // un seul caractère répété
    [InlineData("abcdefghij")]       // une seule classe de caractères
    [InlineData("")]                 // vide
    public void EvaluateStrength_FlagsWeakPasswords(string password)
    {
        Assert.Equal(PasswordStrength.Weak, PasswordHealthAnalyzer.EvaluateStrength(password));
    }

    [Fact]
    public void EvaluateStrength_MediumForShortButMixed()
    {
        Assert.Equal(PasswordStrength.Medium, PasswordHealthAnalyzer.EvaluateStrength("abc12345"));
    }

    [Fact]
    public void EvaluateStrength_StrongForLongAndVaried()
    {
        Assert.Equal(PasswordStrength.Strong, PasswordHealthAnalyzer.EvaluateStrength("Tk9!vRq2#mZp"));
    }

    // ── Réutilisation ────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_FlagsSamePasswordOnTwoSites()
    {
        var report = PasswordHealthAnalyzer.Analyze(new[]
        {
            Cred("https://site-a.fr", "Partage!2345"),
            Cred("https://site-b.fr", "Partage!2345"),
            Cred("https://site-c.fr", "Unique!67890")
        }, Now);

        var group = Assert.Single(report.ReusedGroups);
        Assert.Equal(2, group.Count);
        Assert.Equal(2, report.ReusedCount);
    }

    [Fact]
    public void Analyze_IgnoresSamePasswordOnSameSite()
    {
        var report = PasswordHealthAnalyzer.Analyze(new[]
        {
            Cred("https://site-a.fr", "Partage!2345", username: "perso"),
            Cred("https://site-a.fr", "Partage!2345", username: "pro")
        }, Now);

        Assert.Empty(report.ReusedGroups);
    }

    // ── Ancienneté ───────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_FlagsPasswordsOlderThanTwoYears()
    {
        var threeYearsAgo = Now.AddYears(-3).ToUnixTimeSeconds();
        var oneYearAgo = Now.AddYears(-1).ToUnixTimeSeconds();

        var report = PasswordHealthAnalyzer.Analyze(new[]
        {
            Cred("https://vieux.fr", "Solide!Mdp42", updatedAt: threeYearsAgo),
            Cred("https://recent.fr", "Solide!Mdp43", updatedAt: oneYearAgo)
        }, Now);

        var stale = Assert.Single(report.StaleCredentials);
        Assert.Equal("https://vieux.fr", stale.Origin);
    }

    [Fact]
    public void Analyze_IgnoresEntriesWithoutDates()
    {
        var report = PasswordHealthAnalyzer.Analyze(new[]
        {
            Cred("https://import.fr", "Solide!Mdp42", updatedAt: 0)
        }, Now);

        Assert.Empty(report.StaleCredentials);
    }

    // ── Rapport global ───────────────────────────────────────────────────────

    [Fact]
    public void Analyze_HealthyVaultProducesHealthyReport()
    {
        var report = PasswordHealthAnalyzer.Analyze(new[]
        {
            Cred("https://site-a.fr", "Tk9!vRq2#mZp"),
            Cred("https://site-b.fr", "Wx3$hNb7&fLe")
        }, Now);

        Assert.True(report.IsHealthy);
        Assert.Equal(2, report.TotalCount);
    }

    [Fact]
    public void Analyze_WeakAndReusedAreIndependentSignals()
    {
        // "azerty123" partagé sur deux sites : signalé à la fois réutilisé ET faible.
        var report = PasswordHealthAnalyzer.Analyze(new[]
        {
            Cred("https://site-a.fr", "azerty123"),
            Cred("https://site-b.fr", "azerty123")
        }, Now);

        Assert.False(report.IsHealthy);
        Assert.Single(report.ReusedGroups);
        Assert.Equal(2, report.WeakCredentials.Count);
    }
}
