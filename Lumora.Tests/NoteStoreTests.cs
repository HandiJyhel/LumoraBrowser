using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class NoteStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lumora-notes-tests-{Guid.NewGuid():N}");

    private NoteStore NewStore() => new(Path.Combine(_dir, "notes.lumora"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void MagasinVide_AucuneNote()
    {
        Assert.Empty(NewStore().AllNotes());
    }

    [Fact]
    public void Ajout_PersisteEtRelit()
    {
        var store = NewStore();
        var created = store.Add("Courses", "Pain\nLait", "https://exemple.fr/liste");

        // Relecture par une NOUVELLE instance : la persistance disque est réelle.
        var reloaded = NewStore().AllNotes();
        var note = Assert.Single(reloaded);
        Assert.Equal(created.Id, note.Id);
        Assert.Equal("Courses", note.Title);
        Assert.Equal("Pain\nLait", note.Content);
        Assert.Equal("https://exemple.fr/liste", note.Url);
    }

    [Fact]
    public void ContenuAvecTabulationsEtRetours_SurvitAuFormat()
    {
        var store = NewStore();
        var content = "ligne 1\tavec tab\nligne 2 : caractères accentués é à ü, émoji \U0001F4DD";
        store.Add("Test", content);

        Assert.Equal(content, Assert.Single(NewStore().AllNotes()).Content);
    }

    [Fact]
    public void UrlNonWeb_Ignoree()
    {
        var store = NewStore();
        store.Add("Note", "contenu", "javascript:alert(1)");
        store.Add("Note2", "contenu", "lumora://accueil");

        Assert.All(store.AllNotes(), note => Assert.Equal(string.Empty, note.Url));
    }

    [Fact]
    public void MiseAJour_ChangeContenuEtDate()
    {
        var store = NewStore();
        var created = store.Add("Brouillon", "v1");

        var updated = store.Update(created.Id, "Brouillon", "v2");

        Assert.NotNull(updated);
        Assert.Equal("v2", updated!.Content);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
    }

    [Fact]
    public void MiseAJourSansChangement_NeReecritPas()
    {
        var store = NewStore();
        var created = store.Add("Stable", "contenu");

        var untouched = store.Update(created.Id, "Stable", "contenu");

        // UpdatedAt intact : pas de fausse « modification » qui remonterait la
        // note en tête de liste à chaque ouverture de l'éditeur.
        Assert.Equal(created.UpdatedAt, untouched!.UpdatedAt);
    }

    [Fact]
    public void MiseAJour_NoteInconnue_RendNull()
    {
        Assert.Null(NewStore().Update("note-999", "titre", "contenu"));
    }

    [Fact]
    public void Suppression_RetireLaNote()
    {
        var store = NewStore();
        var a = store.Add("A", "");
        store.Add("B", "");

        Assert.True(store.Remove(a.Id));
        Assert.False(store.Remove(a.Id));
        Assert.Equal("B", Assert.Single(NewStore().AllNotes()).Title);
    }

    [Fact]
    public void Tri_DerniereModifieeEnPremier()
    {
        var store = NewStore();
        var first = store.Add("Ancienne", "");
        store.Add("Recente", "");
        store.Update(first.Id, "Ancienne", "modifiee — repasse en tete");

        Assert.Equal("Ancienne", store.AllNotes()[0].Title);
    }

    [Fact]
    public void Identifiants_UniquesMemeApresSuppression()
    {
        var store = NewStore();
        var a = store.Add("A", "");
        var b = store.Add("B", "");
        store.Remove(a.Id);
        var c = store.Add("C", "");

        Assert.NotEqual(b.Id, c.Id);
    }

    [Fact]
    public void TitreAffichable_RepliSurContenuPuisLibelle()
    {
        var now = DateTimeOffset.Now;
        Assert.Equal("Mon titre", NoteStore.DisplayTitle(new Note("n1", "Mon titre", "contenu", "", now, now)));
        Assert.Equal("Premiere ligne", NoteStore.DisplayTitle(new Note("n2", "", "\n  Premiere ligne\nsuite", "", now, now)));
        Assert.Equal("Note sans titre", NoteStore.DisplayTitle(new Note("n3", "", "   ", "", now, now)));
    }

    [Fact]
    public void Recherche_TitreContenuEtUrl()
    {
        var now = DateTimeOffset.Now;
        var note = new Note("n1", "Courses de Noel", "acheter du pain", "https://exemple.fr/liste", now, now);

        Assert.True(NoteStore.Matches(note, "noel"));
        Assert.True(NoteStore.Matches(note, "PAIN"));
        Assert.True(NoteStore.Matches(note, "exemple.fr"));
        Assert.True(NoteStore.Matches(note, "  "));
        Assert.False(NoteStore.Matches(note, "voiture"));
    }

    [Fact]
    public void ModeInvite_EnMemoireSeulement()
    {
        var store = NewStore();
        store.SetGuestMode(true);
        store.Add("Note invitee", "temporaire");

        Assert.Single(store.AllNotes());
        // Rien sur le disque : une nouvelle instance ne voit rien.
        Assert.Empty(NewStore().AllNotes());
    }
}
