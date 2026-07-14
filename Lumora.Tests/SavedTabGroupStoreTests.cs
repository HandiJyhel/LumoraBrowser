using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class SavedTabGroupStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    // Store en mémoire : simule la persistance chiffrée par un dictionnaire.
    private static SavedTabGroupStore NewStore(Dictionary<string, string> disk) =>
        new("groups",
            path => disk.TryGetValue(path, out var v) ? v : null,
            (path, content) => disk[path] = content);

    private static SavedTabGroupTab Tab(string title, string url) => new(title, url);

    [Fact]
    public void Save_EnregistreEtRelitDepuisLeDisque()
    {
        var disk = new Dictionary<string, string>();
        var store = NewStore(disk);

        var saved = store.Save("Boulot", 2, new[]
        {
            Tab("GitHub", "https://github.com"),
            Tab("Docs", "https://docs.example.com")
        }, Now);

        Assert.NotNull(saved);
        Assert.Equal(2, saved!.TabCount);

        // Un nouveau store sur le meme disque retrouve le groupe.
        var reloaded = NewStore(disk);
        Assert.Equal(1, reloaded.Count);
        Assert.Equal("Boulot", reloaded.Groups[0].Name);
        Assert.Equal(2, reloaded.Groups[0].ColorIndex);
    }

    [Fact]
    public void Save_IgnoreLesPagesInternesEtSansUrlWeb()
    {
        var store = NewStore(new());

        var saved = store.Save("Mixte", 0, new[]
        {
            Tab("Accueil", "lumora://accueil"),
            Tab("Site", "https://exemple.fr"),
            Tab("Vide", "")
        }, Now);

        Assert.NotNull(saved);
        Assert.Single(saved!.Tabs);
        Assert.Equal("https://exemple.fr", saved.Tabs[0].Url);
    }

    [Fact]
    public void Save_GroupeSansPageEnregistrable_RetourneNull()
    {
        var store = NewStore(new());

        var saved = store.Save("Vide", 0, new[]
        {
            Tab("Accueil", "lumora://accueil")
        }, Now);

        Assert.Null(saved);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Save_TitreVide_RemplaceParUrl()
    {
        var store = NewStore(new());

        var saved = store.Save("G", 0, new[] { Tab("", "https://exemple.fr/page") }, Now);

        Assert.Equal("https://exemple.fr/page", saved!.Tabs[0].Title);
    }

    [Fact]
    public void Groups_LesPlusRecentsDAbord()
    {
        var store = NewStore(new());
        store.Save("Ancien", 0, new[] { Tab("A", "https://a.fr") }, Now.AddDays(-2));
        store.Save("Recent", 0, new[] { Tab("B", "https://b.fr") }, Now);

        Assert.Equal("Recent", store.Groups[0].Name);
        Assert.Equal("Ancien", store.Groups[1].Name);
    }

    [Fact]
    public void Remove_SupprimeEtPersiste()
    {
        var disk = new Dictionary<string, string>();
        var store = NewStore(disk);
        var saved = store.Save("Boulot", 0, new[] { Tab("A", "https://a.fr") }, Now);

        Assert.True(store.Remove(saved!.Id));
        Assert.Equal(0, store.Count);
        Assert.Equal(0, NewStore(disk).Count);
    }

    [Fact]
    public void Remove_IdInconnu_RetourneFalse()
    {
        var store = NewStore(new());
        Assert.False(store.Remove("inconnu"));
    }

    [Fact]
    public void Find_RetrouveParId()
    {
        var store = NewStore(new());
        var saved = store.Save("G", 0, new[] { Tab("A", "https://a.fr") }, Now);

        Assert.Equal(saved!.Id, store.Find(saved.Id)!.Id);
        Assert.Null(store.Find("autre"));
    }

    [Fact]
    public void ModeInvite_NEcritRienSurLeDisqueEtVideLaMemoire()
    {
        var disk = new Dictionary<string, string>();
        var store = NewStore(disk);
        store.Save("Avant", 0, new[] { Tab("A", "https://a.fr") }, Now);
        var diskBefore = new Dictionary<string, string>(disk);

        store.SetGuestMode(true);
        Assert.Equal(0, store.Count); // mémoire vidée

        store.Save("Invite", 0, new[] { Tab("B", "https://b.fr") }, Now);
        // Le disque n'a pas bouge depuis l'entree en mode invite.
        Assert.Equal(diskBefore["groups"], disk["groups"]);
    }

    [Theory]
    [InlineData("https://exemple.fr", true)]
    [InlineData("http://exemple.fr", true)]
    [InlineData("file:///c:/page.html", true)]
    [InlineData("lumora://accueil", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsSavableUrl(string url, bool expected)
    {
        Assert.Equal(expected, SavedTabGroupStore.IsSavableUrl(url));
    }
}
