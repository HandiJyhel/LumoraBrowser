using PulseBrowser.WinUI.Credentials;
using Xunit;

namespace PulseBrowser.Tests;

// Import/export CSV du gestionnaire de mots de passe — logique auparavant
// enfouie (et non testée) dans MainWindow.Vault.cs.
public sealed class CredentialCsvTests
{
    [Fact]
    public void Parse_reconnait_les_en_tetes_chrome()
    {
        var csv = "name,url,username,password\nGoogle,https://accounts.google.com/,jean@example.com,secret1\n";
        var items = CredentialCsv.Parse(csv);

        var item = Assert.Single(items);
        Assert.Equal("https://accounts.google.com", item.Origin);
        Assert.Equal("jean@example.com", item.Username);
        Assert.Equal("secret1", item.Password);
    }

    [Fact]
    public void Parse_reconnait_les_en_tetes_firefox()
    {
        var csv = "url,username,password,httpRealm,formActionOrigin,guid,timeCreated,timeLastUsed,timePasswordChanged\n" +
                  "https://example.com/login,alice,hunter2,,,,,,\n";
        var items = CredentialCsv.Parse(csv);

        var item = Assert.Single(items);
        Assert.Equal("https://example.com", item.Origin);
        Assert.Equal("alice", item.Username);
        Assert.Equal("hunter2", item.Password);
    }

    [Fact]
    public void Parse_ignore_les_lignes_sans_url_ou_mot_de_passe()
    {
        var csv = "url,username,password\n,bob,secret\nhttps://site.test,carol,\n";
        Assert.Empty(CredentialCsv.Parse(csv));
    }

    [Fact]
    public void Parse_retourne_vide_sans_colonnes_url_et_password()
    {
        var csv = "name,notes\nSite,rien d'utile\n";
        Assert.Empty(CredentialCsv.Parse(csv));
    }

    [Fact]
    public void Parse_gere_les_champs_entre_guillemets_avec_virgule_et_guillemet_echappe()
    {
        var csv = "url,username,password\nhttps://site.test,\"doe, jane\",\"p\"\"ass\"\n";
        var item = Assert.Single(CredentialCsv.Parse(csv));
        Assert.Equal("doe, jane", item.Username);
        Assert.Equal("p\"ass", item.Password);
    }

    [Fact]
    public void Escape_entoure_de_guillemets_seulement_si_necessaire()
    {
        Assert.Equal("simple", CredentialCsv.Escape("simple"));
        Assert.Equal("\"a,b\"", CredentialCsv.Escape("a,b"));
        Assert.Equal("\"a\"\"b\"", CredentialCsv.Escape("a\"b"));
        Assert.Equal("\"a\nb\"", CredentialCsv.Escape("a\nb"));
    }

    [Fact]
    public void Escape_puis_SplitLine_est_un_aller_retour_stable()
    {
        var raw = "valeur, avec \"guillemets\" et virgule";
        var escaped = CredentialCsv.Escape(raw);
        var roundTripped = Assert.Single(CredentialCsv.SplitLine(escaped));
        Assert.Equal(raw, roundTripped);
    }

    [Fact]
    public void NormalizeOrigin_retire_chemin_et_query()
    {
        Assert.Equal("https://example.com", CredentialCsv.NormalizeOrigin("https://example.com/login?x=1"));
    }

    [Fact]
    public void NormalizeOrigin_retourne_la_valeur_brute_si_pas_une_uri_valide()
    {
        Assert.Equal("pas-une-url", CredentialCsv.NormalizeOrigin("pas-une-url"));
    }
}
