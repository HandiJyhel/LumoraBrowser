using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class AnnotationStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lumora-annotations-tests-{Guid.NewGuid():N}");

    private AnnotationStore NewStore() => new(Path.Combine(_dir, "annotations.lumora"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void MagasinVide_AucuneAnnotation()
    {
        Assert.Empty(NewStore().AllAnnotations());
        Assert.Empty(NewStore().AnnotatedPages());
    }

    [Fact]
    public void Ajout_PersisteEtRelit()
    {
        var store = NewStore();
        var created = store.Add("https://exemple.fr/article", "Un article",
            "extrait surligne", "texte avant ", " texte apres", "mon commentaire");

        Assert.NotNull(created);
        // Relecture par une NOUVELLE instance : la persistance disque est réelle.
        var reloaded = NewStore().AllAnnotations();
        var annotation = Assert.Single(reloaded);
        Assert.Equal(created!.Id, annotation.Id);
        Assert.Equal("https://exemple.fr/article", annotation.Url);
        Assert.Equal("Un article", annotation.PageTitle);
        Assert.Equal("extrait surligne", annotation.Quote);
        Assert.Equal("texte avant ", annotation.Prefix);
        Assert.Equal(" texte apres", annotation.Suffix);
        Assert.Equal("mon commentaire", annotation.Comment);
    }

    [Fact]
    public void ExtraitAvecTabulationsEtRetours_SurvitAuFormat()
    {
        var store = NewStore();
        var quote = "ligne 1\tavec tab\nligne 2 : caractères accentués é à ü, émoji \U0001F58D";
        store.Add("https://exemple.fr/", "Page", quote, "avant\t", "\napres", "note\nmultiligne");

        var annotation = Assert.Single(NewStore().AllAnnotations());
        Assert.Equal(quote, annotation.Quote);
        Assert.Equal("avant\t", annotation.Prefix);
        Assert.Equal("\napres", annotation.Suffix);
        Assert.Equal("note\nmultiligne", annotation.Comment);
    }

    [Fact]
    public void UrlNonWeb_OuExtraitVide_Refuses()
    {
        var store = NewStore();
        Assert.Null(store.Add("javascript:alert(1)", "Page", "extrait", "", "", ""));
        Assert.Null(store.Add("lumora://accueil", "Page", "extrait", "", "", ""));
        Assert.Null(store.Add("https://exemple.fr/", "Page", "   ", "", "", ""));
        Assert.Empty(store.AllAnnotations());
    }

    [Fact]
    public void NormalisationUrl_IgnoreLeFragment()
    {
        var store = NewStore();
        store.Add("https://exemple.fr/article#section-2", "Page", "extrait un", "", "", "");
        store.Add("https://exemple.fr/article", "Page", "extrait deux", "", "", "");

        // Même page : le fragment désigne un endroit, pas une autre page.
        Assert.Equal(2, store.CountForPage("https://exemple.fr/article#autre"));
        Assert.Single(store.AnnotatedPages());
    }

    [Fact]
    public void PourLaPage_OrdreDeCreation()
    {
        var store = NewStore();
        var first = store.Add("https://exemple.fr/", "Page", "premier extrait", "", "", "")!;
        var second = store.Add("https://exemple.fr/", "Page", "second extrait", "", "", "")!;
        store.Add("https://autre.fr/", "Autre", "ailleurs", "", "", "");
        // Modifier le premier ne change pas l'ordre de lecture de la page.
        store.UpdateComment(first.Id, "commentaire ajoute apres coup");

        var forPage = store.ForPage("https://exemple.fr/");
        Assert.Equal(2, forPage.Count);
        Assert.Equal(first.Id, forPage[0].Id);
        Assert.Equal(second.Id, forPage[1].Id);
    }

    [Fact]
    public void PagesAnnotees_GroupeesEtComptees()
    {
        var store = NewStore();
        store.Add("https://exemple.fr/a", "Ancien titre", "extrait 1", "", "", "");
        store.Add("https://exemple.fr/a", "Titre a jour", "extrait 2", "", "", "");
        store.Add("https://autre.fr/b", "Autre page", "extrait 3", "", "", "");

        var pages = store.AnnotatedPages();
        Assert.Equal(2, pages.Count);
        // La page la plus récemment annotée d'abord, avec le titre le plus récent.
        Assert.Equal("https://autre.fr/b", pages[0].Url);
        var pageA = Assert.Single(pages, page => page.Url == "https://exemple.fr/a");
        Assert.Equal(2, pageA.Count);
        Assert.Equal("Titre a jour", pageA.PageTitle);
    }

    [Fact]
    public void MiseAJourCommentaire_ChangeCommentaireEtDate()
    {
        var store = NewStore();
        var created = store.Add("https://exemple.fr/", "Page", "extrait", "", "", "v1")!;

        var updated = store.UpdateComment(created.Id, "v2");

        Assert.NotNull(updated);
        Assert.Equal("v2", updated!.Comment);
        Assert.Equal(created.Quote, updated.Quote);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);

        // Sans changement réel, pas de fausse « modification ».
        var untouched = store.UpdateComment(created.Id, "v2");
        Assert.Equal(updated.UpdatedAt, untouched!.UpdatedAt);
    }

    [Fact]
    public void MiseAJour_AnnotationInconnue_RendNull()
    {
        Assert.Null(NewStore().UpdateComment("ann-999", "commentaire"));
    }

    [Fact]
    public void Suppression_RetireLAnnotation()
    {
        var store = NewStore();
        var a = store.Add("https://exemple.fr/", "Page", "extrait A", "", "", "")!;
        store.Add("https://exemple.fr/", "Page", "extrait B", "", "", "");

        Assert.True(store.Remove(a.Id));
        Assert.False(store.Remove(a.Id));
        Assert.Equal("extrait B", Assert.Single(NewStore().AllAnnotations()).Quote);
    }

    [Fact]
    public void SuppressionParPage_RetireToutesLesAnnotationsDeLaPage()
    {
        var store = NewStore();
        store.Add("https://exemple.fr/a", "Page A", "extrait 1", "", "", "");
        store.Add("https://exemple.fr/a#note", "Page A", "extrait 2", "", "", "");
        store.Add("https://autre.fr/b", "Page B", "extrait 3", "", "", "");

        Assert.Equal(2, store.RemoveForPage("https://exemple.fr/a"));
        Assert.Equal(0, store.RemoveForPage("https://exemple.fr/a"));
        Assert.Equal("https://autre.fr/b", Assert.Single(NewStore().AllAnnotations()).Url);
    }

    [Fact]
    public void Identifiants_UniquesMemeApresSuppression()
    {
        var store = NewStore();
        var a = store.Add("https://exemple.fr/", "Page", "extrait A", "", "", "")!;
        var b = store.Add("https://exemple.fr/", "Page", "extrait B", "", "", "")!;
        store.Remove(a.Id);
        var c = store.Add("https://exemple.fr/", "Page", "extrait C", "", "", "")!;

        Assert.NotEqual(b.Id, c.Id);
    }

    [Fact]
    public void Recherche_ExtraitCommentaireTitreEtUrl()
    {
        var now = DateTimeOffset.Now;
        var annotation = new PageAnnotation("ann-1", "https://exemple.fr/recette",
            "Tarte aux pommes", "peler les fruits", "", "", "a tester ce week-end", now, now);

        Assert.True(AnnotationStore.Matches(annotation, "FRUITS"));
        Assert.True(AnnotationStore.Matches(annotation, "week-end"));
        Assert.True(AnnotationStore.Matches(annotation, "tarte"));
        Assert.True(AnnotationStore.Matches(annotation, "exemple.fr"));
        Assert.True(AnnotationStore.Matches(annotation, "  "));
        Assert.False(AnnotationStore.Matches(annotation, "voiture"));
    }

    [Fact]
    public void ExtraitAffichable_PremiereLigneTronquee()
    {
        var now = DateTimeOffset.Now;
        PageAnnotation With(string quote) => new("ann-1", "https://exemple.fr/", "Page", quote, "", "", "", now, now);

        Assert.Equal("Extrait court", AnnotationStore.DisplayQuote(With("\n  Extrait court\nsuite")));
        var longQuote = new string('a', 120);
        Assert.Equal(new string('a', 87) + "...", AnnotationStore.DisplayQuote(With(longQuote)));
    }

    [Fact]
    public void ModeInvite_EnMemoireSeulement()
    {
        var store = NewStore();
        store.SetGuestMode(true);
        store.Add("https://exemple.fr/", "Page", "extrait invite", "", "", "");

        Assert.Single(store.AllAnnotations());
        // Rien sur le disque : une nouvelle instance ne voit rien.
        Assert.Empty(NewStore().AllAnnotations());
    }
}
