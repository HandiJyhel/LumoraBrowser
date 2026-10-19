using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Textes de la page Nouvel onglet (salutation, date, phrase d'accueil, page a reprendre)
// et phrases de la Vue d'ensemble des Reglages : fonctions pures.
public class NewTabTextTests
{
    [Theory]
    [InlineData(0, "Bonne nuit")]
    [InlineData(4, "Bonne nuit")]
    [InlineData(5, "Bonjour")]
    [InlineData(11, "Bonjour")]
    [InlineData(12, "Bon après-midi")]
    [InlineData(17, "Bon après-midi")]
    [InlineData(18, "Bonsoir")]
    [InlineData(22, "Bonsoir")]
    [InlineData(23, "Bonne nuit")]
    public void La_salutation_suit_l_heure(int hour, string expected)
    {
        Assert.Equal(expected, NewTabText.GreetingMoment(hour));
    }

    [Fact]
    public void La_salutation_porte_le_prenom_quand_il_existe()
    {
        Assert.Equal("Bonne nuit, Camille.", NewTabText.Greeting(2, "Camille"));
        Assert.Equal("Bonne nuit, Camille.", NewTabText.Greeting(2, "  Camille "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void La_salutation_sans_prenom_n_a_pas_de_virgule(string? name)
    {
        Assert.Equal("Bonsoir.", NewTabText.Greeting(20, name));
    }

    [Fact]
    public void La_date_est_en_toutes_lettres_avec_une_majuscule()
    {
        var line = NewTabText.DateLine(new DateTime(2026, 10, 19, 3, 6, 0));
        Assert.Equal("Lundi 19 octobre, 3 h 06.", line);
    }

    [Fact]
    public void La_date_du_premier_du_mois_n_ajoute_pas_de_zero()
    {
        var line = NewTabText.DateLine(new DateTime(2026, 11, 1, 14, 30, 0));
        Assert.Equal("Dimanche 1 novembre, 14 h 30.", line);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Lumora")]
    [InlineData("lumora")]
    public void Sans_phrase_perso_la_salutation_automatique_reste(string? stored)
    {
        Assert.Null(NewTabText.ResolvePhrase(stored));
    }

    [Fact]
    public void Une_phrase_perso_est_gardee_telle_quelle_sans_espaces_autour()
    {
        Assert.Equal("Rien n'est vrai, tout est permis", NewTabText.ResolvePhrase("  Rien n'est vrai, tout est permis "));
    }

    [Theory]
    [InlineData(20, "à l'instant")]
    [InlineData(60, "il y a 1 minute")]
    [InlineData(12 * 60, "il y a 12 minutes")]
    [InlineData(60 * 60, "il y a 1 heure")]
    [InlineData(3 * 60 * 60, "il y a 3 heures")]
    public void L_age_de_la_page_est_exprime_a_la_francaise(int seconds, string expected)
    {
        var now = new DateTimeOffset(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, NewTabText.ResumeAge(now.AddSeconds(-seconds), now));
    }

    [Fact]
    public void L_age_passe_en_jours_puis_disparait_apres_un_mois()
    {
        var now = DateTimeOffset.Now;
        Assert.Equal("hier", NewTabText.ResumeAge(now.AddDays(-1), now));
        Assert.Equal("il y a 5 jours", NewTabText.ResumeAge(now.AddDays(-5), now));
        Assert.Null(NewTabText.ResumeAge(now.AddDays(-45), now));
    }

    [Fact]
    public void La_page_a_reprendre_echappe_le_titre_et_l_adresse()
    {
        var now = new DateTimeOffset(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
        var html = NewTabText.ResumeHtml(
            "https://exemple.fr/?a=1&b=\"2\"", "<script>alert(1)</script> Mon \"titre\"", now.AddMinutes(-12), now);

        Assert.NotNull(html);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("b=\"2\"", html, StringComparison.Ordinal);
        Assert.StartsWith("Tu étais sur <a href=\"", html, StringComparison.Ordinal);
        Assert.EndsWith("</a>, il y a 12 minutes.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void La_page_a_reprendre_tronque_un_titre_trop_long()
    {
        var now = DateTimeOffset.Now;
        var html = NewTabText.ResumeHtml("https://exemple.fr", new string('x', 200), now.AddMinutes(-3), now);
        Assert.NotNull(html);
        Assert.Contains("…</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 81), html, StringComparison.Ordinal);
    }

    [Fact]
    public void La_page_a_reprendre_utilise_l_adresse_sans_titre_et_se_tait_apres_un_mois()
    {
        var now = DateTimeOffset.Now;
        Assert.Contains(">https://exemple.fr</a>", NewTabText.ResumeHtml("https://exemple.fr", "  ", now.AddHours(-2), now), StringComparison.Ordinal);
        Assert.StartsWith("Tu viens d'ouvrir", NewTabText.ResumeHtml("https://exemple.fr", "X", now.AddSeconds(-10), now), StringComparison.Ordinal);
        Assert.Null(NewTabText.ResumeHtml("https://exemple.fr", "X", now.AddDays(-40), now));
        Assert.Null(NewTabText.ResumeHtml("", "X", now.AddMinutes(-1), now));
    }

    [Fact]
    public void La_couleur_d_un_raccourci_est_stable_et_insensible_a_la_casse()
    {
        Assert.Equal(NewTabText.TileColor("Ameli"), NewTabText.TileColor("ameli"));
        Assert.StartsWith("#", NewTabText.TileColor("Wikipédia"), StringComparison.Ordinal);
        Assert.StartsWith("#", NewTabText.TileColor(null), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("camille", "C")]
    [InlineData("  éric", "É")]
    [InlineData("", "?")]
    [InlineData(null, "?")]
    [InlineData("---", "?")]
    public void L_initiale_ignore_les_signes(string? name, string expected)
    {
        Assert.Equal(expected, NewTabText.Initial(name));
    }
}

public class SettingsOverviewTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Sans_sauvegarde_la_phrase_la_dit_et_le_bouton_est_plein()
    {
        var c = SettingsOverviewText.Compose(false, true, "Braise", 1248, null, true, Now);
        Assert.Equal(
            "Lumora est en thème sombre, couleur Braise. Depuis l'ouverture, le bloqueur a arrêté 1 248 requêtes. Tu n'as jamais exporté de sauvegarde.",
            c.PlainText);
        Assert.Equal(OverviewAction.ExportBackup, c.Action);
        Assert.True(c.ActionIsPrimary);
    }

    [Fact]
    public void Avec_une_sauvegarde_il_n_y_a_plus_de_bouton_plein()
    {
        var c = SettingsOverviewText.Compose(false, false, "d'origine", 12, new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero), true, Now);
        Assert.Contains("thème clair, couleur d'origine", c.PlainText, StringComparison.Ordinal);
        Assert.EndsWith("Ta dernière sauvegarde date du 12 octobre.", c.PlainText, StringComparison.Ordinal);
        Assert.Equal(OverviewAction.ExportBackup, c.Action);
        Assert.False(c.ActionIsPrimary);
    }

    [Fact]
    public void Le_premier_du_mois_s_ecrit_1er()
    {
        var c = SettingsOverviewText.Compose(false, true, "Braise", 3, new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), true, Now);
        Assert.EndsWith("date du 1er octobre.", c.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_sauvegarde_d_une_autre_annee_donne_l_annee()
    {
        var c = SettingsOverviewText.Compose(false, true, "Braise", 3, new DateTimeOffset(2025, 3, 5, 9, 0, 0, TimeSpan.Zero), true, Now);
        Assert.EndsWith("date du 5 mars 2025.", c.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_profil_sans_mot_de_passe_n_est_pas_culpabilise_pour_la_sauvegarde()
    {
        var c = SettingsOverviewText.Compose(false, true, "Braise", 5, null, false, Now);
        Assert.DoesNotContain("sauvegarde", c.PlainText, StringComparison.Ordinal);
        Assert.Equal(OverviewAction.None, c.Action);
    }

    [Fact]
    public void En_invite_on_propose_de_creer_un_profil_sans_parler_de_theme()
    {
        var c = SettingsOverviewText.Compose(true, true, "Braise", 312, null, false, Now);
        Assert.Equal(
            "Tu navigues en invité : rien n'est gardé quand tu fermes Lumora. Depuis l'ouverture, le bloqueur a arrêté 312 requêtes.",
            c.PlainText);
        Assert.Equal(OverviewAction.CreateProfile, c.Action);
        Assert.True(c.ActionIsPrimary);
    }

    [Theory]
    [InlineData(0, "Le bloqueur n'a encore rien arrêté depuis l'ouverture.")]
    [InlineData(1, "Depuis l'ouverture, le bloqueur a arrêté 1 requête.")]
    public void Zero_et_une_seule_requete_ont_leur_propre_phrase(int total, string expected)
    {
        var c = SettingsOverviewText.Compose(false, true, "Braise", total, null, false, Now);
        Assert.Contains(expected, c.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_mots_cles_pointent_vers_la_bonne_section()
    {
        var c = SettingsOverviewText.Compose(false, true, "Braise", 5, null, true, Now);
        var links = c.Segments.Where(s => s.Link != OverviewLink.None).ToDictionary(s => s.Text, s => s.Link);
        Assert.Equal(OverviewLink.Appearance, links["thème sombre"]);
        Assert.Equal(OverviewLink.Appearance, links["Braise"]);
        Assert.Equal(OverviewLink.Privacy, links["5 requêtes"]);
        Assert.Equal(OverviewLink.Storage, links["sauvegarde"]);
    }
}
