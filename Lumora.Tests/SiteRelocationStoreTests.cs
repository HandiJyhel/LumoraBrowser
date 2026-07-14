using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class SiteRelocationStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    // Faux disque : la persistance chiffrée du produit est injectée par délégués,
    // les tests la remplacent par un dictionnaire en mémoire.
    private static (SiteRelocationStore Store, Dictionary<string, string> Disk) NewStore(
        Dictionary<string, string>? disk = null)
    {
        disk ??= new Dictionary<string, string>();
        var store = new SiteRelocationStore(
            "site-relocations.lumora",
            path => disk.TryGetValue(path, out var value) ? value : null,
            (path, value) => disk[path] = value);
        return (store, disk);
    }

    // ── Apprentissage des redirections permanentes ──────────────────────────

    [Fact]
    public void RedirectionDePageDaccueil_Enregistree()
    {
        var (store, _) = NewStore();

        var relocation = store.RecordPermanentRedirect(
            "https://zone-annuaire.irish/", "https://www.zone-annuaire.poker/", Now);

        Assert.NotNull(relocation);
        Assert.Equal("zone-annuaire.irish", relocation!.FromRootDomain);
        Assert.Equal("https://www.zone-annuaire.poker", relocation.ToOrigin);
        Assert.Equal("https://www.zone-annuaire.poker/", store.TargetFor("https://zone-annuaire.irish/"));
    }

    [Fact]
    public void MemeDomaineRacine_Ignore()
    {
        // http→https ou www→apex : une normalisation, pas un déménagement.
        var (store, _) = NewStore();

        Assert.Null(store.RecordPermanentRedirect("http://monsite.fr/", "https://www.monsite.fr/", Now));
        Assert.Empty(store.All);
    }

    [Fact]
    public void RaccourcisseurDurl_Ignore()
    {
        // bit.ly/abc → article profond : chemin non conservé et pas une page
        // d'accueil, ce n'est pas un site qui déménage.
        var (store, _) = NewStore();

        Assert.Null(store.RecordPermanentRedirect(
            "https://bit.ly/abc123", "https://blog.example.com/article/42", Now));
    }

    [Fact]
    public void CheminConserve_Enregistre()
    {
        var (store, _) = NewStore();

        var relocation = store.RecordPermanentRedirect(
            "https://old.win/film/x", "https://new.poker/film/x", Now);

        Assert.NotNull(relocation);
        Assert.Equal("old.win", relocation!.FromRootDomain);
    }

    [Fact]
    public void CheminDifferent_HorsAccueil_Ignore()
    {
        var (store, _) = NewStore();

        Assert.Null(store.RecordPermanentRedirect(
            "https://old.win/film/x", "https://new.poker/autre-chose", Now));
    }

    // ── Chaînes de déménagements ────────────────────────────────────────────

    [Fact]
    public void ChaineDeDemenagements_ResolueDirectement()
    {
        // A→B puis B→C : demander A doit mener à C sans passer par B (mort).
        var (store, _) = NewStore();
        store.RecordPermanentRedirect("https://site.win/", "https://site.irish/", Now);
        store.RecordPermanentRedirect("https://site.irish/", "https://site.poker/", Now.AddDays(1));

        Assert.Equal("https://site.poker/", store.TargetFor("https://site.win/"));
        Assert.Equal("https://site.poker", store.Find("site.win")!.ToOrigin);
    }

    [Fact]
    public void RetourALAncienDomaine_PasDentreeCirculaire()
    {
        var (store, _) = NewStore();
        store.RecordPermanentRedirect("https://site.win/", "https://site.poker/", Now);
        store.RecordPermanentRedirect("https://site.poker/", "https://site.win/", Now.AddDays(1));

        // site.win est redevenu la bonne adresse : aucune entrée ne doit le
        // déclarer déménagé (entrée circulaire purgée).
        Assert.Null(store.Find("site.win"));
        Assert.Null(store.TargetFor("https://site.win/"));
        Assert.Equal("https://site.win/", store.TargetFor("https://site.poker/"));
    }

    [Fact]
    public void TargetFor_ConserveCheminEtRequete()
    {
        var (store, _) = NewStore();
        store.RecordPermanentRedirect("https://old.win/", "https://new.poker/", Now);

        Assert.Equal(
            "https://new.poker/film/x?y=1",
            store.TargetFor("https://old.win/film/x?y=1"));
    }

    [Fact]
    public void TargetFor_DomaineInconnu_Null()
    {
        var (store, _) = NewStore();

        Assert.Null(store.TargetFor("https://jamais-vu.fr/"));
        Assert.Null(store.TargetFor("lumora://accueil"));
        Assert.Null(store.TargetFor(null));
    }

    // ── Persistance et mode invité ──────────────────────────────────────────

    [Fact]
    public void RoundTripDisque()
    {
        var (store1, disk) = NewStore();
        store1.RecordPermanentRedirect("https://old.win/", "https://new.poker/", Now);

        var (store2, _) = NewStore(disk);

        Assert.Equal("https://new.poker", store2.Find("old.win")!.ToOrigin);
    }

    [Fact]
    public void MarkUpdateOffered_Persiste()
    {
        var (store1, disk) = NewStore();
        store1.RecordPermanentRedirect("https://old.win/", "https://new.poker/", Now);
        store1.MarkUpdateOffered("old.win");

        var (store2, _) = NewStore(disk);

        Assert.True(store2.Find("old.win")!.UpdateOffered);
    }

    [Fact]
    public void ModeInvite_AucuneEcritureDisque()
    {
        var (store, disk) = NewStore();
        store.SetGuestMode(true);

        store.RecordPermanentRedirect("https://old.win/", "https://new.poker/", Now);

        Assert.Empty(disk);
    }

    // ── Réécriture d'URL ────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://old.win/film/x?y=1", "https://new.poker", "https://new.poker/film/x?y=1")]
    [InlineData("https://www.old.win/", "https://new.poker", "https://new.poker/")]
    public void RewriteToOrigin_ConserveCheminEtRequete(string url, string origin, string attendu) =>
        Assert.Equal(attendu, SiteRelocationStore.RewriteToOrigin(url, origin));

    [Fact]
    public void RewriteToOrigin_UrlInvalide_Null() =>
        Assert.Null(SiteRelocationStore.RewriteToOrigin("lumora://accueil", "https://new.poker"));

    // ── Plan de mise à jour favoris/raccourcis ──────────────────────────────

    [Fact]
    public void Plan_NeToucheQueLesEntreesDeLAncienDomaine()
    {
        var relocation = new SiteRelocation("old.win", "https://new.poker", Now);
        var bookmarks = new[]
        {
            ("b1", "https://www.old.win/page"),
            ("b2", "https://autre.fr/")
        };
        var shortcuts = new[]
        {
            ("ZA", "https://old.win/"),
            ("Autre", "https://autre.fr/")
        };

        var plan = SiteRelocationUpdatePlanner.Compute(relocation, bookmarks, shortcuts);

        Assert.Equal(2, plan.TotalChanges);
        Assert.Equal("https://new.poker/page", plan.BookmarkUrlsById["b1"]);
        Assert.False(plan.BookmarkUrlsById.ContainsKey("b2"));
        Assert.Equal(1, plan.ShortcutChanges);
        Assert.Equal(("ZA", "https://new.poker/"), plan.Shortcuts[0]);
        Assert.Equal(("Autre", "https://autre.fr/"), plan.Shortcuts[1]);
    }

    [Fact]
    public void Plan_RienAMettreAJour()
    {
        var relocation = new SiteRelocation("old.win", "https://new.poker", Now);

        var plan = SiteRelocationUpdatePlanner.Compute(
            relocation,
            new[] { ("b1", "https://autre.fr/") },
            Array.Empty<(string, string)>());

        Assert.Equal(0, plan.TotalChanges);
    }
}
