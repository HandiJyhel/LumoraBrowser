using PulseBrowser.WinUI.Settings;
using Xunit;

namespace PulseBrowser.Tests;

// Sérialisation texte des raccourcis nouvel onglet (panneau Paramètres) —
// logique auparavant enfouie et non testée dans MainWindow.Settings.cs.
public sealed class NewTabShortcutTextTests
{
    [Fact]
    public void ToText_puis_Parse_est_un_aller_retour_stable()
    {
        var shortcuts = new List<(string Title, string Url)>
        {
            ("Gmail", "https://mail.google.com"),
            ("Actu", "https://news.example.com")
        };

        var text = NewTabShortcutText.ToText(shortcuts);
        var roundTripped = NewTabShortcutText.Parse(text);

        Assert.Equal(shortcuts, roundTripped);
    }

    [Fact]
    public void ToText_ignore_les_entrees_incompletes()
    {
        var shortcuts = new List<(string Title, string Url)>
        {
            ("Sans URL", ""),
            ("", "https://sans-titre.test"),
            ("OK", "https://ok.test")
        };

        Assert.Equal("OK | https://ok.test", NewTabShortcutText.ToText(shortcuts));
    }

    [Fact]
    public void Parse_ignore_les_lignes_sans_separateur_ou_incompletes()
    {
        var text = "Ligne sans separateur\nTitre |\n| https://sans-titre.test\nOK | https://ok.test";
        var shortcuts = NewTabShortcutText.Parse(text);

        var shortcut = Assert.Single(shortcuts);
        Assert.Equal(("OK", "https://ok.test"), shortcut);
    }

    [Fact]
    public void Parse_tronque_a_douze_raccourcis()
    {
        var text = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Site{i} | https://site{i}.test"));
        Assert.Equal(12, NewTabShortcutText.Parse(text).Count);
    }

    [Fact]
    public void Parse_ne_coupe_que_sur_le_premier_separateur()
    {
        // Un "|" supplémentaire dans l'URL (rare mais possible) reste dans le champ URL
        // plutôt que de faire échouer le parsing en 3 colonnes.
        var shortcuts = NewTabShortcutText.Parse("Titre | https://site.test/a|b");
        var shortcut = Assert.Single(shortcuts);
        Assert.Equal("Titre", shortcut.Title);
        Assert.Equal("https://site.test/a|b", shortcut.Url);
    }
}
