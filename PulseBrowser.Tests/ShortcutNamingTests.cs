using PulseBrowser.WinUI;
using Xunit;

namespace PulseBrowser.Tests;

// Nom de fichier .lnk des applications web — doit rester un nom de fichier
// Windows valide quel que soit le titre du site.
public sealed class ShortcutNamingTests
{
    [Fact]
    public void SanitizeFileName_retire_les_caracteres_interdits_et_ajoute_l_id_court()
    {
        var name = ShortcutNaming.SanitizeFileName("Site : édition / test?", "abcdef1234567890");
        Assert.Equal("Site  édition  test - abcdef12", name);
    }

    [Fact]
    public void SanitizeFileName_retombe_sur_un_nom_par_defaut_si_le_titre_est_vide_apres_nettoyage()
    {
        var name = ShortcutNaming.SanitizeFileName("///???", "abc123");
        Assert.Equal("Application Pulse - abc123", name);
    }

    [Fact]
    public void SanitizeFileName_tronque_les_titres_trop_longs()
    {
        var longTitle = new string('a', 80);
        var name = ShortcutNaming.SanitizeFileName(longTitle, "id123456");
        Assert.Equal(new string('a', 50) + " - id123456", name);
    }

    [Fact]
    public void SanitizeFileName_gere_un_id_plus_court_que_huit_caracteres()
    {
        var name = ShortcutNaming.SanitizeFileName("App", "xy");
        Assert.Equal("App - xy", name);
    }
}
