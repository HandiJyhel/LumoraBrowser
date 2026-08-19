using Xunit;

namespace Lumora.Tests;

// Tableau de bord "Mon compte" (0.93.45.0-dev) : logique pure de formatage,
// isolee de MainWindow.AccountDashboard.cs (depend de WinUI, non testable
// directement - meme contrainte que les autres fichiers WinUI du projet).
public sealed class AccountDashboardFormatterTests
{
    [Theory]
    [InlineData(0, "0 o")]
    [InlineData(512, "512 o")]
    [InlineData(1024, "1 Ko")]
    [InlineData(49459, "48,3 Ko")]
    [InlineData(327155712, "312 Mo")]
    [InlineData(1288490188, "1,2 Go")]
    public void FormatDataSize_choisit_la_bonne_unite(long bytes, string expected)
    {
        Assert.Equal(expected, Lumora.WinUI.AccountDashboardFormatter.FormatDataSize(bytes));
    }

    [Fact]
    public void HasBackup_faux_quand_jamais_sauvegarde()
    {
        Assert.False(Lumora.WinUI.AccountDashboardFormatter.HasBackup(0));
        Assert.True(Lumora.WinUI.AccountDashboardFormatter.HasBackup(1755000000));
    }

    [Theory]
    [InlineData(0, "aujourd'hui")]
    [InlineData(1, "hier")]
    [InlineData(3, "il y a 3 jours")]
    [InlineData(7, "il y a 1 semaine")]
    [InlineData(20, "il y a 2 semaines")]
    [InlineData(45, "il y a 1 mois")]
    [InlineData(90, "il y a 3 mois")]
    [InlineData(400, "il y a 1 an")]
    [InlineData(800, "il y a 2 ans")]
    public void FormatRelativeAge_choisit_la_bonne_granularite(int daysAgo, string expected)
    {
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var at = now.AddDays(-daysAgo);
        Assert.Equal(expected, Lumora.WinUI.AccountDashboardFormatter.FormatRelativeAge(at, now));
    }
}
