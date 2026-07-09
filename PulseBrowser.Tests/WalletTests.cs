using PulseBrowser.WinUI;
using Xunit;

namespace PulseBrowser.Tests;

// Portefeuille : validation pure des cartes (Luhn, réseau, masquage, expiration)
// et round-trip chiffré des cartes dans vault.pulse (même clé que les identifiants,
// verrouillage, persistance disque, rétro-compatibilité des coffres sans cartes).
public sealed class WalletTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), "pulse_test_" + Guid.NewGuid().ToString("N") + ".pulse");

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    private static VaultPaymentCard Card(string number = "4111111111111111",
        string label = "Carte perso", int month = 12, int year = 2030) => new()
    {
        Label = label, Holder = "DUPONT Jean", Number = number,
        ExpMonth = month, ExpYear = year
    };

    // ── PaymentCardUtil ───────────────────────────────────────────────────────

    [Fact]
    public void Luhn_accepte_les_numeros_valides_meme_avec_espaces()
    {
        Assert.True(PaymentCardUtil.IsValidNumber("4111111111111111"));
        Assert.True(PaymentCardUtil.IsValidNumber("4111 1111 1111 1111"));
        Assert.True(PaymentCardUtil.IsValidNumber("5555-5555-5555-4444"));
        Assert.True(PaymentCardUtil.IsValidNumber("371449635398431")); // Amex 15 chiffres
    }

    [Fact]
    public void Luhn_refuse_faute_de_frappe_et_longueur_invalide()
    {
        Assert.False(PaymentCardUtil.IsValidNumber("4111111111111112")); // dernier chiffre faux
        Assert.False(PaymentCardUtil.IsValidNumber("4111"));             // trop court
        Assert.False(PaymentCardUtil.IsValidNumber(""));
        Assert.False(PaymentCardUtil.IsValidNumber("pas un numero"));
    }

    [Fact]
    public void Reseau_detecte_par_prefixe()
    {
        Assert.Equal("Visa", PaymentCardUtil.BrandOf("4111111111111111"));
        Assert.Equal("Mastercard", PaymentCardUtil.BrandOf("5555555555554444"));
        Assert.Equal("Mastercard", PaymentCardUtil.BrandOf("2221000000000009"));
        Assert.Equal("American Express", PaymentCardUtil.BrandOf("371449635398431"));
        Assert.Equal("Discover", PaymentCardUtil.BrandOf("6011111111111117"));
        Assert.Equal("Carte", PaymentCardUtil.BrandOf("9999999999999999"));
    }

    [Fact]
    public void Masquage_ne_montre_que_les_quatre_derniers_chiffres()
    {
        var masked = PaymentCardUtil.MaskedNumber("4111111111111111");
        Assert.Equal("•••• •••• •••• 1111", masked);
        Assert.DoesNotContain("4111111111111111", masked);
    }

    [Fact]
    public void Expiration_annee_courte_normalisee_et_bornes_du_mois()
    {
        Assert.Equal(2027, PaymentCardUtil.NormalizeYear(27));
        Assert.Equal(2027, PaymentCardUtil.NormalizeYear(2027));
        Assert.True(PaymentCardUtil.IsValidExpiry(1, 27));
        Assert.True(PaymentCardUtil.IsValidExpiry(12, 2030));
        Assert.False(PaymentCardUtil.IsValidExpiry(0, 2030));
        Assert.False(PaymentCardUtil.IsValidExpiry(13, 2030));
        Assert.Equal("03/27", PaymentCardUtil.ExpiryText(3, 27));
    }

    [Fact]
    public void Carte_valable_jusqu_a_la_fin_de_son_mois_d_expiration()
    {
        var now = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(PaymentCardUtil.IsExpired(7, 2026, now)); // mois courant : encore valable
        Assert.True(PaymentCardUtil.IsExpired(6, 2026, now));  // mois précédent : expirée
        Assert.False(PaymentCardUtil.IsExpired(1, 2027, now));
    }

    // ── VaultStore : cartes ───────────────────────────────────────────────────

    [Fact]
    public void Upsert_et_lecture_d_une_carte()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.UpsertCard(Card());

        var cards = vault.ListCards();
        var stored = Assert.Single(cards);
        Assert.Equal("4111111111111111", stored.Number);
        Assert.Equal("Carte perso", stored.Label);
        Assert.False(string.IsNullOrWhiteSpace(stored.Id)); // Id attribué
    }

    [Fact]
    public void Coffre_verrouille_ne_montre_ni_n_accepte_aucune_carte()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.UpsertCard(Card());
        vault.Lock();

        Assert.Empty(vault.ListCards());
        vault.UpsertCard(Card(label: "Ignoree"));
        Assert.True(vault.Unlock("pw"));
        Assert.Single(vault.ListCards()); // l'upsert verrouillé a bien été refusé
    }

    [Fact]
    public void Reouverture_depuis_le_disque_restaure_les_cartes_en_GCM()
    {
        var first = new VaultStore(_file);
        first.SetMasterPassword("pw");
        first.UpsertCard(Card());
        first.Upsert("https://exemple.fr", "alice", "s3cret"); // identifiants intacts à côté
        first.Lock();

        var reopened = new VaultStore(_file);
        Assert.True(reopened.Unlock("pw"));
        Assert.Equal("4111111111111111", reopened.ListCards().Single().Number);
        Assert.Equal("s3cret", reopened.ListCredentials().Single().Password);
    }

    [Fact]
    public void Le_numero_n_apparait_jamais_en_clair_dans_le_fichier()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.UpsertCard(Card());

        var raw = File.ReadAllText(_file);
        Assert.DoesNotContain("4111111111111111", raw);
        Assert.DoesNotContain("DUPONT", raw);
    }

    [Fact]
    public void Upsert_par_Id_met_a_jour_sans_dupliquer()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.UpsertCard(Card());
        var id = vault.ListCards().Single().Id;

        vault.UpsertCard(new VaultPaymentCard
        {
            Id = id, Label = "Renommee", Holder = "DUPONT Jean",
            Number = "4111111111111111", ExpMonth = 1, ExpYear = 2031
        });

        var stored = Assert.Single(vault.ListCards());
        Assert.Equal("Renommee", stored.Label);
        Assert.Equal(2031, stored.ExpYear);
    }

    [Fact]
    public void DeleteCard_supprime_de_facon_persistante()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.UpsertCard(Card());
        vault.DeleteCard(vault.ListCards().Single().Id);
        Assert.Empty(vault.ListCards());

        var reopened = new VaultStore(_file);
        Assert.True(reopened.Unlock("pw"));
        Assert.Empty(reopened.ListCards());
    }

    [Fact]
    public void Ancien_coffre_sans_champ_cards_donne_un_portefeuille_vide()
    {
        // Coffre créé avec identifiants, dont on retire le champ "cards" pour
        // simuler un fichier antérieur à 0.45 (rétro-compatibilité).
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        vault.Upsert("https://exemple.fr", "alice", "s3cret");
        vault.Lock();

        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_file))!.AsObject();
        json.Remove("cards");
        json.Remove("recovery_cards");
        File.WriteAllText(_file, json.ToJsonString());

        var reopened = new VaultStore(_file);
        Assert.True(reopened.Unlock("pw"));
        Assert.Empty(reopened.ListCards());
        Assert.Equal("s3cret", reopened.ListCredentials().Single().Password);
    }

    [Fact]
    public void Recuperation_par_cle_de_secours_restaure_aussi_les_cartes()
    {
        var vault = new VaultStore(_file);
        vault.SetMasterPassword("pw");
        Assert.True(vault.SetRecoveryKey("PULSE-ABCD-EFGH-IJKL"));
        vault.UpsertCard(Card());
        vault.Upsert("https://exemple.fr", "alice", "s3cret");
        vault.Lock();

        var reopened = new VaultStore(_file);
        Assert.True(reopened.UnlockWithRecoveryKey("PULSE-ABCD-EFGH-IJKL"));
        Assert.Equal("4111111111111111", reopened.ListCards().Single().Number);
    }
}
