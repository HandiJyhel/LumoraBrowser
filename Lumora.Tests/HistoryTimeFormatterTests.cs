using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Formatage relatif des dates dans le panneau Historique — les bornes de jours
// (aujourd'hui/hier/cette semaine) sont une source classique d'erreurs par un.
// DateTimeKind.Local (pas UTC) : Format() lit time.LocalDateTime, donc les
// dates de test doivent porter le fuseau local pour rester stables sur toute
// machine, quel que soit son fuseau.
public sealed class HistoryTimeFormatterTests
{
    private static DateTimeOffset Local(int y, int m, int d, int h, int mi) =>
        new(new DateTime(y, m, d, h, mi, 0, DateTimeKind.Local));

    private static readonly DateTimeOffset Now = Local(2026, 7, 9, 18, 0);

    [Fact]
    public void Format_meme_jour_dit_aujourd_hui()
    {
        var visitedAt = Local(2026, 7, 9, 8, 30);
        Assert.Equal("Aujourd'hui 08:30", HistoryTimeFormatter.Format(visitedAt, Now));
    }

    [Fact]
    public void Format_veille_dit_hier()
    {
        var visitedAt = Local(2026, 7, 8, 23, 59);
        Assert.Equal("Hier 23:59", HistoryTimeFormatter.Format(visitedAt, Now));
    }

    [Fact]
    public void Format_avant_hier_utilise_le_jour_abrege_en_francais()
    {
        var visitedAt = Local(2026, 7, 6, 10, 0); // lundi
        var formatted = HistoryTimeFormatter.Format(visitedAt, Now);
        Assert.DoesNotContain("Aujourd'hui", formatted);
        Assert.DoesNotContain("Hier", formatted);
        Assert.Contains("10:00", formatted);
    }

    [Fact]
    public void Format_plus_d_une_semaine_utilise_la_date_complete()
    {
        var visitedAt = Local(2026, 6, 1, 10, 0);
        Assert.Equal("01/06/2026 10:00", HistoryTimeFormatter.Format(visitedAt, Now));
    }
}
